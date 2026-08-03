# AMS2 `.bff` Pak Format & Livery Slot Modding — Reference Notes

Reverse-engineered and empirically validated against a real Automobilista 2 install
(Steam, `Automobilista 2\Pakfiles\...`) while adding a 7th livery slot to the
`formula_hitech_g1m3` car. Everything below was confirmed by actually unpacking,
patching, repacking, and successfully loading the game with the change in effect.

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
| 0x00 | 4 | magic `"PAK "` |
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
   - `Pakfiles\Vehicles\<Car>.bff` → `vehicles\<car>\<car>.rcf` (and any `_hr`
     variant) — the per-car pak.
   - `Pakfiles\Vehicles\vehiclespersistent.bff` (note: plural
     "vehicle**s**persistent") — a **global, boot-time-loaded pak** containing
     one `.rcf`/`.rcf_hr` pair per vehicle in the entire game (791 entries
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

## Open questions / risks for future work

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
