# AMS2 `.bff` Pak Format & Livery Slot Modding — Reference Notes

Reverse-engineered and empirically validated against a real Automobilista 2 install
(Steam, `Automobilista 2\Pakfiles\...`) while adding a 7th livery slot to the
`formula_hitech_g1m3` car. Everything below was confirmed by actually unpacking,
patching, repacking, and successfully loading the game with the change in effect.

**Read this first**: the actual root cause of "new slot registers as valid but
renders empty" was found and confirmed working in-game - see "ACTUAL root cause
confirmed" near the end of this doc, right before "Open questions". Everything
between here and there (ext-info corruption, `UnknownFlag`, `mSectionInfoPos`,
texture-folder convention, content-uniqueness) was each a real, legitimate bug
in `BffPakEntryInserter` - all fixed and still worth knowing about if that code
path is ever used again - but **none of them were the actual blocker**. Don't
re-walk that whole path before reading the real root cause section first.

## Tooling

- **PCarsTools** (NuGet package, also on GitHub at `OpenSimTools/PCarsTools`, forked
  from `Nenkai/PCarsTools`) is a read-only unpack/decrypt library for Project
  CARS-lineage engine files (PC1/PC2/PC3/AMS2). It has **no packer/encoder** — packing
  had to be reimplemented from scratch based on its format understanding.
- A built copy of `PCarsTools.dll` + `PCarsTools.XCompression.dll` exists at
  `C:\Program Files\AMS2CM\` (AMS2CM — a separate AMS2 mod-installer repo — depends on
  it for its own bootfiles-unpacking step). Useful as a quick way to unpack/verify
  `.bff` files via its `BPakFile.FromFile(path, withExtraInfo, textWriter)` +
  `.UnpackAll(dir)` API, and `.Entries`/`.ExtEntries` for per-file TOC metadata
  (`Offset`, `PakSize`, `FileSize`, `Compression`, `CRC`, path, etc.).
- AMS2CM's repo (`C:\AMS2CM`) is itself a useful reference for the `.bff` format
  because its installer already uses PCarsTools to unpack boot pakfiles.

## `.bff` pak file format

304-byte (`0x130`) header, then a TOC, then packed entry data.

**Header** (all little-endian):
| Offset | Size | Field |
|---|---|---|
| 0x00 | 4 | magic - on-disk bytes are `20 4B 41 50` (`' ', 'K', 'A', 'P'`), i.e. `"PAK "` stored byte-reversed, not `50 41 4B 20` (`'P','A','K',' '`) as a naive reading of "magic PAK " would suggest. Confirmed against real `Formula_Hitech_G1M3.bff`/`vehiclespersistent.bff` files - an earlier from-scratch reimplementation of the reader got this backwards and silently failed closed (`SkippedUnrecognizedFormat`) on every real pak, with no real install available at the time to catch it. |
| 0x04 | 4 | version |
| 0x08 | 4 | file count |
| 0x0C | 8 | data offset |
| 0x14 | 4 | sector size |
| 0x18 | 256 | pak internal name (null-padded) |
| 0x118 | 4 | **TOC size** (bytes) |
| 0x11C | 4 | CRC-table size (0 in files checked) |
| 0x120 | 4 | ext-info size |
| 0x124 | 4 | section-info pos |
| 0x128 | 4 | section-info size |
| 0x12C | 1 | flags |
| 0x12D | 1 | encryption type (`2` = RC4 in every file checked) |
| 0x12E | 2 | pad |

**TOC**: immediately follows the header, `fileCount` × 42-byte (`0x2A`) entries.
The **entire TOC buffer is RC4-decrypted as one contiguous stream** (not per-entry).

Per-entry layout (offsets relative to entry start):
| Offset | Size | Field |
|---|---|---|
| 0x00 | 8 | UID (hash of the file's relative path) |
| 0x08 | 8 | **data offset** (absolute file offset) |
| 0x10 | 4 | **on-disk size** ("PakSize") |
| 0x14 | 4 | **uncompressed size** ("FileSize"/"OrigSize") |
| 0x18 | 8 | timestamp |
| 0x20 | 1 | compression type (`0`=None, `1`=ZLib, `2`=LZX, `3`=Mermaid, `4`=Kraken) |
| 0x21 | 1 | unknown flag |
| 0x22 | 4 | **per-entry CRC** (see below — the game validates this even though PCarsTools' own reader ignores it) |
| 0x26 | 4 | extension (4 ASCII chars) |

After the TOC: an ext-header (`0x308` bytes) + ext-info table (name offsets +
timestamps) + a filename string table — this is what maps TOC entries to their
relative paths. Unaffected by content patches as long as no file is renamed/added.

**Entry data**: `Compression=ZLib` entries are `RC4-encrypted THEN zlib-compressed`
on read (i.e. to build one: zlib-compress first, then RC4-encrypt). Zlib streams
observed all start with the standard `0x78 0x9C` header (.NET's
`System.IO.Compression.ZLibStream` with `CompressionLevel.Optimal` reproduces this
exactly). Entries are 16-byte aligned in at least the small per-car paks; the large
`vehiclespersistent.bff` pak turned out to have looser/larger original alignment
gaps (see "Repacking" below).

### RC4 encryption

Pure XOR stream cipher (KSA+PRGA), so **decrypt and encrypt are the identical
operation** — reuse the same routine both ways.

The key comes from PCarsTools' hardcoded `PC2AndAbove` keyset table (32 possible
27-byte raw keys, keyed by an integer "KeyIndex" that PCarsTools determines
per-pak via filename/path pattern matching against `BConfig`). **KeyIndex differs
per pak file** — confirmed empirically:
- `Formula_Hitech_G1M3.bff` (per-car pak) → KeyIndex **4**
- `vehiclespersistent.bff` (global pak) → KeyIndex **3**

Don't assume a fixed key index across paks — get it via `pak.KeyIndex` from
PCarsTools' `BPakFile.FromFile(...)` before hand-rolling a patcher for a new pak.

Key derivation: take the raw key bytes up to the first `0x00`, then apply this
descrambling pass (all keys need this before use in RC4):
```
xor = [0xAC, 0xC7, 0x91]; tIndex = 0
for i in 0, 2, 4, ... (pairs):
    tmp1 = xor[tIndex++] ^ key[i];   tIndex %= 3
    tmp2 = xor[tIndex++] ^ key[i+1]; tIndex %= 3
    key[i] = tmp2; key[i+1] = tmp1   // note: swapped
for any trailing odd byte:
    key[i] ^= xor[tIndex++]; tIndex %= 3
```

### Per-entry CRC — the gotcha that causes "CRC error loading file"

PCarsTools' own unpacker **never validates** this field, but **the game does** —
leaving it stale after patching an entry causes a crash on selecting the car
("CRC error loading file"), even though the file is otherwise perfectly valid and
unpacks fine via PCarsTools.

Empirically determined (by testing candidates against a known-good original CRC
value pulled from a pristine backup): the algorithm is **CRC-32/JAMCRC** —
standard reflected CRC-32 (poly `0xEDB88320`), init `0xFFFFFFFF`, **no final XOR**
— computed over the **exact on-disk bytes as stored** (i.e. RC4-encrypted +
zlib-compressed, the literal bytes written to the file for that entry — not the
plaintext, not the pre-encryption compressed buffer).

Always recompute and write this for any entry whose content changes.

## Repacking algorithm

Because entries are back-to-back with alignment padding, changing one entry's
size shifts every subsequent entry's data offset. Two situations came up:

1. **Single entry, happens to be second-to-last physically**: only had to shift
   one neighbor's offset. Cheap special case, not generally safe to assume.
2. **General case** (`vehiclespersistent.bff`, 791 entries, patching 2 of them):
   - Parse header + decrypt TOC → all entries' `(index, dataOffset, pakSize,
     origSize)`.
   - Sort entries by **physical data offset** (not TOC index — they don't
     necessarily match).
   - Walk in that order, maintaining a running offset starting from the first
     entry's original offset: for each entry, assign `newOffset = running`, then
     `running += align16(newPakSize)` (new compressed+encrypted size for changed
     entries, original size for untouched ones).
   - Write updated offset (and, for changed entries, size + CRC) back into every
     TOC entry, then re-encrypt the whole TOC buffer.
   - Reassemble the file: header, TOC, ext-header/ext-info (byte-identical, copied
     verbatim), then entry data in the new sorted-offset order — original bytes
     copied for untouched entries, freshly compressed+encrypted bytes for changed
     ones, each padded to 16-byte alignment.
   - **Don't assume the resulting file size will match the original.** The
     `vehiclespersistent.bff` case shrank by ~990KB purely because the original
     had looser padding/alignment than a tight 16-byte repack produces. This is
     harmless as long as the TOC and the actual written data agree — verified by
     round-tripping through PCarsTools' own reader afterward.

**Always validate** a repack by (a) unpacking the result with PCarsTools and
confirming 0 failures / no UID-hash warnings, (b) diffing every *untouched* entry
against the original (SHA-256 per file) to confirm zero collateral corruption,
and (c) confirming the *changed* entries' content matches what was intended.
This caught both the CRC bug and gave confidence despite the unexplained size
delta.

## AMS2 livery system (per car)

Two livery mechanisms exist in AMS2, and it's not obvious upfront which one a
given car uses:

1. **"Fixed entry" style** (e.g. real-team liveries like IndyCar 2023): every
   livery is its own separate `.crd` vehicle file, registered as an independent
   selectable "car" in `vehiclelist.lst`. Adding one of these = duplicating a
   `.crd`, not what this doc is about.
2. **"Generic"/`CustomLiveries` override style** (most single-model cars,
   including `formula_hitech_g1m3`): one car, multiple numbered livery IDs.

For the generic style, the **authoritative slot definition lives in a per-car
`.rcf` file** (`REPLACEMENT_SYSTEM` XML, found at
`vehicles\<car>\<car>.rcf` inside the car's own pak), not in the `.crd`. Schema:

```xml
<REPLACEMENT_SYSTEM>
  <CONFIG>
    <ALLOWUSEROVERRIDES VALUE="1" />
    <USEROVERRIDESFILE VALUE="Vehicles\Textures\CustomLiveries\Overrides\<car>\<car>.xml" />
  </CONFIG>
  <INPUTS>
    <INPUT NAME="LIVERY" OPTIONS="6" />   <!-- total slot COUNT, hard cap -->
  </INPUTS>
  <NAMES INPUT="LIVERY">
    <NAME LIVERY="51" NAME="United Racing #9" />   <!-- one per slot -->
    ...
  </NAMES>
  <CONDITION LIVERY="51">                 <!-- what texture/material this slot swaps in -->
    <REPLACE TEXTURE="..." NEWTEXTURE="..." />
  </CONDITION>
  ...
</REPLACEMENT_SYSTEM>
```

The **loose, user-moddable** file at
`Vehicles\Textures\CustomLiveries\Overrides\<car>\<car>.xml` (referenced via
`USEROVERRIDESFILE`) is the sanctioned "Custom Liveries" system — it can
**replace** the texture/material for any slot ID the `.rcf` already declares, via
`<LIVERY_OVERRIDE LIVERY="NN" NAME="..." BASELIVERY="...">` blocks. **It cannot
add a new slot ID** — a Reiza developer confirmed on their forum: *"It does not
provide a mechanism to add extra liveries... you can only replace those
liveries."* This matches what was found empirically: raising an ID beyond the
`.rcf`'s declared `OPTIONS` count in the loose override XML alone did nothing, and
`-showLiveryIDs` (a documented AMS2 launch option showing valid IDs for the
currently viewed car) never listed it.

### Adding a genuinely new slot (validated end-to-end)

1. Edit the car's `.rcf`: bump `<INPUT NAME="LIVERY" OPTIONS="N">` by one, add a
   `<NAME LIVERY="newID" NAME="...">`, add a `<CONDITION LIVERY="newID">` block
   (can safely bootstrap by duplicating an existing slot's `CONDITION` — nothing
   in the schema requires uniqueness of the underlying texture reference, and no
   helmet/outfit/driver linkage exists at this layer).
2. **Critical**: this `.rcf` is duplicated in (at least) two places and **both
   copies must be patched identically**, or the change silently has no effect:
   - `Pakfiles\Vehicles\<Car>.bff` → `vehicles\<car>\<car>.rcf` (and any
     high-res variant, named `<car>_hr.rcf` — suffix *before* the extension,
     **not** `<car>.rcf_hr` as a distinct extension. Confirmed against a real
     install by decoding the pak's own ext-info filename table directly: a
     candidate path guess using the wrong pattern silently never matches
     anything, and this exact mistake shipped in this codebase for a long
     time - the `_hr` variant went unpatched in every single livery-slot
     patch attempt while the main `.rcf` was patched correctly, which is
     exactly what produced "new slot registers as valid via `-showLiveryIDs`
     but still renders empty") — the per-car pak.
   - `Pakfiles\Vehicles\vehiclespersistent.bff` (note: plural
     "vehicle**s**persistent") — a **global, boot-time-loaded pak** containing
     one `.rcf`/`_hr.rcf` pair per vehicle in the entire game (791 entries
     total in the copy checked). **This is the copy the game actually reads for
     `-showLiveryIDs`/slot validity at boot** — patching only the per-car pak
     was not enough; the crash-free-but-still-not-appearing symptom was caused by
     this still-unpatched duplicate.
   - Don't assume there are only two — if a car has more LOD/mirror `.rcf`
     variants, check whether they're duplicated in `vehiclespersistent.bff` too.
3. Add the actual custom livery via the loose `Overrides` XML as normal (
   `LIVERY_OVERRIDE LIVERY="newID" ...` pointing at your own texture) — this part
   needs no pak editing since it was always the moddable layer.
4. Repack every patched `.bff` (see "Repacking algorithm" above — remember the
   per-entry CRC and the correct KeyIndex for *that specific pak file*).

### Files that turned out NOT to matter (ruled out during the investigation)

- The `.crd` vehicle definition file: no livery/paint-count field exists in its
  schema at all (checked across 605 vehicle `.crd` files; 601 share the exact same
  placeholder `Paint Shop Data="N/A"` value, 4 are blank, zero have anything else).
- `TOCFiles\VehicleLiveries.toc` (game-root level, not under `Pakfiles`): a
  startup-optimization cache — literally embeds byte-for-byte copies of every
  `*_livery.bff` texture pak's own internal TOC, so the engine doesn't have to
  open ~445 separate texture paks individually at boot. Contains filenames, not
  livery-ID validity data. Not relevant to slot count/registration.
- `<Car>_Livery.bff` (e.g. `Formula_Hitech_G1M3_Livery.bff`): just the actual
  livery diffuse textures/materials for the shipped slots. No metadata/count
  file inside it.
- External caches (`%LOCALAPPDATA%`/`%APPDATA%`/`Documents\Automobilista 2\`):
  none exist for AMS2 relevant to vehicle/livery metadata — ruled out as a
  culprit for stale data.
- A parallel **loose file tree** exists at the AMS2 install root (`Vehicles\`,
  `GUI\`, etc., mirroring the packed structure) — generated by AMS2CM (a
  separate mod-installer tool) as part of its own mod-management scheme. Loose
  files at matching paths can shadow packed content in this engine, so it's
  worth checking for a loose override of any file being patched, but in this
  investigation no loose `.rcf` existed there for the car in question.

## Automated slot-injection debugging log (`liveryinjection` branch)

Real-world debugging session against an actual AMS2 install
(`D:\SteamLibrary\steamapps\common\Automobilista 2`), adding a 7th livery slot
to `formula_hitech_g1m3` (declared capacity 6, in `car_model_capacities.json`)
via `RcfLiverySlotPatcher` + `Ams2VehicleLiverySlotPatcher`. Kept here because
this is the first time this code path was exercised against a real install,
and the bugs found are exactly the kind the "Open questions" section above
anticipated. Read this before touching `RcfLiverySlotPatcher.cs`,
`Ams2VehicleLiverySlotPatcher.cs`, or `BffPakReader.cs` again.

### Bugs found and fixed (all confirmed against the real install, read-only diagnostics before each fix, all committed)

1. **`.bff` magic bytes read in the wrong order** (`BffPakReader.Read`). Code
   checked for the literal ASCII order `'P','A','K',' '`. Real files store it
   as `20 4B 41 50` (`' ','K','A','P'`) - confirmed on both
   `Formula_Hitech_G1M3.bff` and `vehiclespersistent.bff`. Every pak read was
   throwing `"Not a recognized .bff pak file (bad magic)"`, so `EnsureSlots`
   silently failed closed (`SkippedUnrecognizedFormat`) for every car, always -
   this is why the old cross-model-redirect-with-solid-colour fallback kept
   firing regardless of the new patcher code. Fixed the byte comparison; fixed
   `BffPakRoundTripTests`' synthetic fixture to match (it had encoded the same
   wrong assumption, since it was written with no real install to check
   against).
2. **UTF-8 BOM not stripped before `XDocument.Parse`**
   (`Ams2VehicleLiverySlotPatcher.EnsureSlotsCore`). Real `.rcf` entries are
   UTF-8-with-BOM. `Encoding.UTF8.GetString` keeps the BOM character in the
   resulting string, and `XDocument.Parse(string)` (unlike `Load(Stream)`)
   treats a leading BOM as invalid content -> `"Data at the root level is
   invalid. Line 1, position 1."`, caught and turned into another silent
   `SkippedUnrecognizedFormat`. Fixed by detecting/stripping the BOM on decode
   and re-prepending it on write-back (byte-faithful round-trip).
3. **Clone template picked by highest LIVERY id, not by CONDITION shape**
   (`RcfLiverySlotPatcher.TryEnsureSlotCount`). Real cars can mix a plain
   `<REPLACE TEXTURE=... NEWTEXTURE=...>` pattern with a `MATERIAL`-based one
   on their last few slots - confirmed on `formula_hitech_g1m3`: slots 51-54
   are plain TEXTURE, slots 55-56 route paint through a shared
   `Vehicles\_Generic_Materials\livery_lm11_10.mtx` MATERIAL and only
   TEXTURE-replace an unrelated legacy asset
   (`Vehicles\Textures\Liveries\AUDI_R18_LIVERY10.dds` - clearly a leftover
   reference to a different car). The app's loose-Overrides generation only
   ever repoints a `TEXTURE`, never a `MATERIAL`, so cloning slot 56 (the
   highest id) produced a new slot whose paint could never be reached by an
   override. Fixed to prefer the highest-id plain-TEXTURE condition as the
   clone template, falling back to highest-id overall only if every slot uses
   MATERIAL.
4. **New `<CONDITION>` appended at the end of the whole document, not grouped
   with sibling LIVERY conditions** (`RcfLiverySlotPatcher.TryEnsureSlotCount`,
   `root.Add(newCondition)`). Real `.rcf` files have `CONDITION` elements for
   multiple `INPUT`s as flat root-level siblings in blocks: all `LIVERY`
   conditions together, then all `TIRE` conditions, then all `DIRTTYPE`
   conditions. `root.Add()` put the new slot after literally everything,
   including `TIRE`/`DIRTTYPE`, physically separated from every other LIVERY
   condition. Fixed to insert via `AddAfterSelf` chained off the clone
   template, keeping new slots grouped with the rest of the LIVERY block.
   Verified against a clean pre-patch backup pak: slots 57/58 now land
   directly after slot 54.

All four were verified with small throwaway read-only console projects
referencing `Ams2ChEd.Business.AMS2` directly (never checked in - built in
the scratchpad temp dir, deleted after use), decoding the real pak/`.rcf`
bytes without ever writing back, so the real game files were only ever
modified by the app itself (with its own automatic pre-patch backups) during
actual user testing, never by ad-hoc diagnostic scripts.

### Still broken after all four fixes: new slot renders as an empty/blank livery

Isolated step by step, in order, all confirmed by the user with real in-game
testing:

- Not the `PREVIEWIMAGE` path (separately confirmed broken/empty for the
  `fondmetal` team specifically - `season pack`'s own
  `liveries_xml/fondmetal.xml` template ships `<PREVIEWIMAGE PATH="" />`, a
  content gap unrelated to slot injection - but fixing it did not fix the
  empty-livery symptom).
- Not the generated BODY texture DDS content - user swapped in a known-good
  DDS from a working slot and the new slot was still empty.
- Not the loose Overrides XML structure - `LIVERY_OVERRIDE`/`HELMET_OVERRIDE`/
  `OUTFIT_OVERRIDE` blocks for the new slot are structurally identical to the
  working ones.
- Not the `CustomAIDrivers\*.xml` <-> Overrides XML name matching - AI livery
  assignment is by a `livery_name` **string** match
  (`"#14 Fondmetal Ford - Mazzimo Frascuono"`), not by numeric LIVERY id;
  confirmed byte-identical between the two files.
- Not `TOCFiles\VehicleLiveries.toc` (the boot-time cache of every
  `*_Livery.bff` texture pak's own TOC) - the new slot's `NEWTEXTURE`
  reference points at a file inside a `_Livery.bff` pak that was **never
  patched**, so any cache describing that pak's contents can't be stale
  relative to it, and the reference is proven already-cached-and-working
  since the clone template slot uses the identical file successfully.
- **Not the `.rcf` slot registration itself** - user confirmed via AMS2's
  `-showLiveryIDs` launch option that `57` **does** appear as a valid livery
  id for the car after patching, ruling out "engine doesn't know slot 57
  exists at all."
- User also did a clean isolation test: removed the custom Overrides XML
  entirely so only the `.rcf`'s own default `CONDITION` textures would show.
  The cloned slot (57) *still* rendered empty, with the exact same
  already-proven-valid `NEWTEXTURE` reference as its (working) template slot.

### Fixed: duplicate `NEWTEXTURE`/`NEWMATERIAL` reference across two LIVERY ids

Confirmed and fixed (`RcfLiverySlotPatcher`/`Ams2VehicleLiverySlotPatcher`):
every originally-shipped `CONDITION` has a distinct `NEWTEXTURE` value, but
the old clone step duplicated the template's `REPLACE` element(s) verbatim,
so a new slot pointed at the *same* `NEWTEXTURE` string as its template. The
`.rcf`'s own default value only needs to be *some* distinct, valid, existing
reference (the loose Overrides XML overrides the actual visible texture at
race time regardless) - `Ams2VehicleLiverySlotPatcher` now provisions a
genuinely new, distinct texture entry per new slot (reusing a sibling
model's known-working texture bytes, injected via `BffPakEntryInserter`) and
`RcfLiverySlotPatcher` repoints each clone at it. This directly contradicted
this doc's own earlier (unvalidated, speculative) claim under "Adding a
genuinely new slot" that "nothing in the schema requires the underlying
texture reference to be unique" - that claim was never actually tested
against a real install; it's wrong.

**This fix alone was not sufficient** - see below.

### `BffPakEntryInserter` also corrupted the pak's ext-info (filename) table (necessary fix, not sufficient alone)

With the duplicate-texture bug fixed, slots still rendered empty. A
manually-patched *working* copy pointed new slots at
`f_hitech_g1m3_livery07.dds`/`livery08.dds` and was confirmed to render
correctly in-game. **Initially misdiagnosed as "reusing pre-existing shipped
spare textures"** - this turned out to be wrong (see the UnknownFlag section
below): a true pristine, never-patched copy of this pak (recovered from the
app's own pre-first-patch backups) has only 32 entries and does **not**
contain `livery07`/`livery08` at all. The `working/` copy's 07/08 entries
were themselves inserted by whatever produced that copy - the same kind of
operation `BffPakEntryInserter` performs, just done correctly. So the real
difference was never "reuse vs. insert" - it was two independently-broken
things `BffPakEntryInserter` was doing on every insert, discovered as
successive layers:

1. **Missing ext-info entry.** A `.bff` pak has a second, separately-
   encrypted table (see below) mapping every TOC entry to its filename,
   distinct from the RC4-encrypted TOC and from `TOCFiles\VehicleLiveries.toc`
   (a boot-time cache, ruled out separately - renaming it changed nothing).
   `BffPakEntryInserter` copied this block byte-for-byte verbatim when
   appending a new TOC entry, so the newly-inserted file never got a
   name-table entry at all.
2. **Stale NameOffsets on every existing entry.** Appending a TOC record
   grows the TOC, shifting the ext-info block to a later absolute file
   offset - but every existing entry's name pointer (`NameOffset`, an
   *absolute file offset*) was never rebased, so every pak this method ever
   touched had **all of its pre-existing files'** name records silently
   broken too, not just the new one.

Fixed and confirmed structurally correct end-to-end (round-tripped against
real, never-before-patched pak bytes: Scribe decrypt→encrypt reproduces the
original bytes exactly; every entry's decoded path hashes back to its own
TOC UID with zero mismatches, on real sibling-model texture data, on all
three pak variants) - see `AMS2ChEd.Tests/BffExtInfoCodecTests.cs`. **This
fix alone was not sufficient** - user confirmed in-game the new slots still
rendered empty even after this fix, a clean backup restore, and a rebuild.
See below for what was still missing.

### Also found: `UnknownFlag` TOC byte hardcoded to 0 instead of matching every other entry

Surveyed every TOC entry (offset `0x21`, 1 byte, meaning never decoded) across
several real paks - the pristine pre-first-patch backup, a sibling model's
pak, and (critically) the *working* copy's own newly-added 07/08 entries -
and every single one, of every file type, in every pak, is `4`. No exceptions
found. `BffPakEntryInserter.AddEntry` hardcoded this to `0` for every entry
it ever inserted. This is invisible to every check performed so far (CRC,
ext-info consistency, decompressed content, UID hash resolution all pass
regardless of this byte's value), which is exactly why it survived the
ext-info fix above undetected. Fixed to copy the value from an existing
entry in the same pak rather than hardcode it - see
`BffPakEntryInserter.cs`'s `unknownFlag` local and its doc comment.

**Not yet confirmed by an in-game test** - this is the current leading
candidate (all prior insertion-path bugs found were confirmed structurally
but not sufficient in-game; this is the first difference found that
distinguishes "the app's insertion" from "every known-working entry,
including manually-inserted ones" on a byte the engine could plausibly gate
loading on). If a slot still renders empty after this fix, re-open this
section and treat `UnknownFlag`'s actual meaning as still unknown rather
than assuming it's "the" fix.

#### `.bff` ext-info block format (new)

Immediately follows the TOC (`HeaderSize + TocSize`), reverse-engineered
from PCarsTools' `BPakFile.FromStream`/`PakFileExtHeader`/`PakFileExtEntry`
(github.com/Nenkai/PCarsTools, MIT licensed - PCarsTools reads it but never
writes it, so packing had to be reimplemented same as the rest of this doc):

- **Ext-header**: 0x308 bytes, **plaintext** (not RC4, not Scribe): `mID`
  (4), `mInfoSize` (4, = the *pre-alignment* size of everything below, see
  next point), `mConfigName`/`mTargetRoot`/`mPlatformName` (0x100 each,
  null-padded ASCII - e.g. `"Reiza.xml"` / `""` / `"PC"` on real files).
- **Entries + string table**: `Align16(mInfoSize)` bytes, encrypted with a
  *different* cipher than the TOC - PCarsTools calls it "Scribe" (an
  RC6-shaped 32-bit-word block cipher, fixed non-per-pak key, 30 rounds;
  PCarsTools only implements decryption - `ScribeCipher.Encrypt` in this
  codebase is this project's own derivation of the inverse, validated by
  round-tripping real bytes before use). Once decrypted: `FileCount` ×
  16-byte records (`NameOffset`: u64, **absolute file offset** of this
  entry's filename string; `ModifiedTime`: u64), index-aligned 1:1 with the
  TOC, followed immediately by a flat string table: each string is
  `[1-byte length][that many ASCII chars]`, referenced by `NameOffset`
  (verified: `NameOffset - (blockStart + 0x308)` = the string's position
  within this decrypted region).
- The **outer pak header's** `mExtInfoSize` field (offset `0x120` in the
  0x130-byte pak header - see the main header table above) = `0x308 +
  mInfoSize` (i.e. ext-header size + the *pre-alignment* entries/strings
  size - confirmed the inner and outer fields always agree on this after
  subtracting 0x308). The on-disk region is `Align16` of that, same
  16-byte-alignment convention as everything else in this format.

Implementation: `BffExtInfoCodec` (`Decode`/`Encode`) + `ScribeCipher`, used
by `BffPakEntryInserter` - decodes the existing table, rebases every
existing `NameOffset` by however far the block moved, appends a new 16-byte
record + string for the new file, re-encrypts, and updates the outer
header's `mExtInfoSize`. `BffPakRepacker` (which never adds/removes files,
so this block never moves) still copies it verbatim - only entry-insertion
needed this.

### Changed texture source/naming: duplicate the model's own texture into its own real folder (untested in-game)

`UnknownFlag` alone was also not sufficient - user re-tested in-game after a
clean backup restore + rebuild and still saw an empty slot. User's own
hypothesis, not yet tried before this: every prior insertion attempt sourced
the new texture's bytes from a **sibling model** and wrote it into a
**brand-new folder** the engine has never indexed
(`vehicles\textures\{carModel}\{carModel}_livery{N}.dds`, all-lowercase,
distinct from every shipped directory). Changed `Ams2VehicleLiverySlotPatcher`
to instead duplicate the SAME model's own existing texture (the same one
`FindSpareTextures`/`TryGetReusableTexturePath` already identify, e.g.
`f_hitech_g1m3_livery09.dds`) into a new file inside that model's own real,
already-shipped folder, keeping its exact prefix/casing and just appending
`_N` before the extension (e.g. `f_hitech_g1m3_livery09_1.dds`) - see
`TryParseLiveryTexturePattern`/`TryGetOwnModelTexture`. Sibling-model reuse
is now only a defensive fallback if the model's own texture is somehow
unreadable.

Confirmed structurally correct end-to-end the same way as the prior
attempts (ext-info self-check 0 mismatches, `UnknownFlag` correct, all three
pak variants patched, real pristine backups + real content) - see
`AMS2ChEd.Tests`. **Not yet confirmed in-game.**

### Root cause found (high confidence): `mSectionInfoPos` header field never repointed

Found by getting a real ground-truth comparison: the user's `working/` copy turned out to be
produced by a real, mature third-party tool (**Rocky's BFF Repacker**, `C:\Program Files\BFF
Repacker`), not by anything in this repo or by hand. Diffing its actual output against a pristine
backup, field-by-field, surfaced a header field at offset **0x124** (`mSectionInfoPos`, alongside
`mSectionInfoSize` at 0x128) that nothing in this codebase had ever read or wondered about - it
was always 0 in the synthetic test fixtures, which masked it entirely.

In a real pak, this points at a 32-byte block (tag bytes `44 48 53 41` on disk, meaning/purpose not
decoded) sitting *after* the ext-info region and *before* the first entry's actual data. Real,
Reiza-shipped paks reserve a **fixed-size padding budget** for this: `extInfoRegionSize + gap-
before-sectionInfo` is a constant (4872 bytes, confirmed identical between a pristine
`formula_hitech_g1m3_Livery.bff` and Rocky's repacked copy of it) - i.e. there's deliberate slack
so a tool can grow the ext-info table without needing to relocate anything after it, as long as
growth stays within budget.

`BffExtInfoCodec`/`BffPakEntryInserter` never knew this reserved region existed - it tight-packs
the new ext-info content and treats everything after it as one opaque "gap" blob, copied verbatim
but relocated (shifted later, since the tight-packed ext-info is smaller than the original
reserved budget was probably meant to accommodate, but the shift still happens because the new
ext-info size differs from the old one). The actual section-info *bytes* end up fine (copied
unchanged, just moved), but **the header's `mSectionInfoPos` was never updated to point at the new
location** - it kept the stale pre-patch value. Every previous "confirmed structurally correct,
still empty in-game" cycle (ext-info, `UnknownFlag`, texture-folder convention, content
uniqueness) never caught this because none of them touch or read this field. Fixed by applying the
same `offsetShift` already used for entry data offsets - proven algebraically equivalent for this
field too (see `BffPakEntryInserter.cs`'s comment on the fix). Verified against real pristine pak
bytes: after the fix, `mSectionInfoPos` correctly lands on the real 32-byte block, unmodified.

**Not yet confirmed in-game** (same caveat as every fix before it - written down explicitly this
time so it isn't skipped again). One further known discrepancy, not yet acted on: Rocky's tool's
own repack additionally *changes the value* of a u32 at offset +0x18 *within* that 32-byte
section-info block (520124 -> 513552 for this pak) - this codebase's fix repoints to the block but
leaves its 32 bytes of content completely unchanged, matching pristine content, not Rocky's
adjusted content. If the in-game test still fails after this fix, decode what that field means
next (it shrinks as ext-info grows - possibly a "remaining reserved capacity" counter) before
looking elsewhere.

## ACTUAL root cause confirmed (in-game): wrong `_hr.rcf` filename pattern

None of the `_Livery.bff`-side fixes above were the blocker. The real bug was
much simpler and entirely on the `.rcf`-bearing-pak side:
`Ams2VehicleLiverySlotPatcher.GetCandidateRcfPaths` probed for the high-res
`.rcf` variant using the pattern `{carModel}.rcf_hr` (a fake `"rcf_hr"`
extension). **The real file is named `{carModel}_hr.rcf`** (an `_hr` suffix
before the genuine `.rcf` extension) - confirmed by decoding a real pak's
ext-info filename table directly (`vehicles\formula_hitech_g1m3\
formula_hitech_g1m3_hr.rcf`), not by guessing. The wrong candidate never
matched anything (`BffPakReader.TryFindEntryByPath` returned null every
time), so `_hr.rcf` was silently skipped in *every single patch attempt*
across this entire investigation, while the main `.rcf` was always patched
correctly. This exactly explains the symptom that survived every other fix:
`-showLiveryIDs` reads the main `.rcf` (always correct, so the slot showed as
"valid"), but the engine's actual per-race texture resolution apparently also
needs `_hr.rcf` in sync, and it silently stayed at the original 6 slots the
whole time. Fixed in `Ams2VehicleLiverySlotPatcher.cs` and in the separate
`AMS2ChEd.PakEditor` tool (same bug, independently present there too).
**Confirmed working in-game** by the user after this fix - first fix in the
whole investigation to actually resolve the symptom.

Once this was fixed, a follow-up test confirmed something important: the
*original*, much simpler "reuse an existing already-referenced texture
directly" approach (`Ams2VehicleLiverySlotPatcher.
TestReuseExistingLiveryTextureDirectly`, currently `true`) **also renders
correctly in-game** now that `_hr.rcf` is in sync. That approach needs zero
`_Livery.bff` insertion - no `BffPakEntryInserter`, no ext-info rebasing, no
`UnknownFlag`/`mSectionInfoPos` handling, none of it. This strongly suggests
every `_Livery.bff`-insertion bug found and fixed above was chasing a problem
that never needed solving in the first place: the "duplicate NEWTEXTURE
across slots doesn't render" finding from the very first debugging session
(see "Automated slot-injection debugging log" above) was tested *before*
`_hr.rcf` was known to be broken, and was likely never actually about
duplicate textures at all.

**Status as of this test**: user is running further tests before deciding
whether to simplify the code down to the reuse-only approach and delete the
insertion machinery, or keep insertion as a fallback for a car that runs out
of distinct existing textures to reuse (a real, if narrower, scenario - see
`FindSpareTextures`'s doc comment). Don't delete `BffPakEntryInserter`/
`BffExtInfoCodec`/`ScribeCipher` or the `_Livery.bff`-side fixes above until
that's settled - they're still correct fixes for the bugs they targeted, just
possibly unnecessary for the common case.

## Open questions / risks for future work

- Whether `BffPakEntryInserter`'s insertion path (ext-info, `UnknownFlag`,
  `mSectionInfoPos` fixes) is still needed for cars that run out of spare/
  reusable existing textures - not yet tested in-game on its own merits now
  that `_hr.rcf` is fixed (every previous test of it was confounded by the
  `_hr.rcf` bug). Worth a clean re-test before assuming it still doesn't work,
  now that the actual blocker is gone.
- **Not yet confirmed by an actual in-game test** that any of the insertion
  fixes above (ext-info, `UnknownFlag`, same-model-folder texture naming)
  resolve the empty-livery symptom - each previous one passed every offline
  structural check and still rendered empty in-game when tested. If this one
  fails too, the pattern strongly suggests *inserting any new pak entry at
  all* may be unreliable for this format in a way not yet identified (rather
  than "one more field to find") - worth stepping back and considering
  whether a genuinely different strategy (e.g. requiring the season pack to
  ship pre-made livery texture files that get installed as loose files
  rather than injected into a `.bff`, if that's even how AMS2 resolves
  textures) is more promising than continuing to iterate on binary pak
  insertion.
- Whether `vehiclespersistent.bff` has its own "TOC-of-TOCs" companion file
  (analogous to `VehicleLiveries.toc` for `*_livery.bff` paks) that could hold a
  stale cached copy of ITS internal TOC — not found/ruled out, just never
  triggered a problem in this case. If a future patch to this pak doesn't take
  effect despite validating correctly via PCarsTools, check `TOCFiles\` for
  anything referencing `vehiclespersistent`.
- The per-entry CRC algorithm (JAMCRC over on-disk bytes) was derived
  empirically from one file/pak; not yet cross-validated against a second,
  independent pak/entry. Worth a quick sanity check if it ever produces another
  "CRC error" on a new target file.
- No packer/encoder exists in PCarsTools itself — everything above required a
  hand-rolled, from-scratch reimplementation of the write path. Any future tool
  built on this knowledge should probably graduate this into a proper, reusable
  library rather than one-off scripts, if this becomes a repeated workflow.
- `ScribeCipher.Encrypt` is this codebase's own derivation (PCarsTools has no
  encoder for this cipher either) - validated by byte-exact round-trip
  against real ext-info bytes and by the full `BffExtInfoCodecTests` suite,
  but worth another sanity check if it ever produces a pak the game itself
  rejects.
