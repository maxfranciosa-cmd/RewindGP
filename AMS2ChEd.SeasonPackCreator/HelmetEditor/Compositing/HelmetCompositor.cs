using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using AMS2ChEd.SeasonPackEditor.HelmetEditor.Models;

namespace AMS2ChEd.SeasonPackEditor.HelmetEditor.Compositing
{
    // ════════════════════════════════════════════════════════════════════════
    // HelmetCompositor
    //
    // Two-pass pipeline:
    //
    //  Pass 1 — Color replacement
    //    Load {Era}_colors.png (Pbgra32).
    //    For every pixel whose RGB matches one of the 6 key colors (within
    //    tolerance), replace it with the user's chosen color.
    //    Result: WriteableBitmap "colorLayer".
    //
    //  Pass 2 — Sticker warping
    //    Load {Era}_stickers.png (Pbgra32).
    //    For each StickerSlotDefinition that has an assigned sticker path:
    //      a) Locate all pixels of that slot's key color → build a pixel mask.
    //      b) Find the tight bounding rect of the mask.
    //      c) Detect the 4 corner vertices of the warped quad inside that rect.
    //      d) Crop + transform the user's sticker per the slot definition.
    //      e) Perspective-warp (homography) the sticker into the quad shape.
    //      f) Apply the pixel mask so only the colored area receives paint.
    //      g) Alpha-composite onto colorLayer.
    //
    // No external OpenCV dependency required — homography is implemented here
    // using a classic 8-point DLT (Direct Linear Transform) solved with a
    // simple Gaussian elimination. Fast enough for 2048×2048 in < 1 second.
    // ════════════════════════════════════════════════════════════════════════

    public class HelmetCompositor
    {
        // ─── Public API ───────────────────────────────────────────────────────

        /// <summary>
        /// Composite and return a WPF BitmapSource for live preview.
        /// </summary>
        public BitmapSource Composite(
            HelmetEraManifest manifest,
            HelmetCompositeInput definition,
            string manifestFolder)
        {
            // ── Load color texture ────────────────────────────────────────────
            string colorsPath = ResolvePath(manifestFolder, manifest.ColorsTexturePath);
            byte[] canvas = LoadPbgra(colorsPath,
                manifest.TextureWidth, manifest.TextureHeight,
                out int texW, out int texH);

            // ── Pass 1: color replacement ─────────────────────────────────────
            ReplaceKeyColors(canvas, texW, texH,
                definition.ZoneColors, manifest.KeyColorTolerance);

            // ── Pass 2: sticker warping ───────────────────────────────────────
            string stickersPath = ResolvePath(manifestFolder, manifest.StickersTemplatePath);
            if (File.Exists(stickersPath) && manifest.StickerSlots.Count > 0)
            {
                byte[] stickerTemplate = LoadPbgra(stickersPath, texW, texH,
                    out _, out _);

                foreach (var slot in manifest.StickerSlots)
                {
                    string key = slot.Color.ToString();
                    if (!definition.StickerPaths.TryGetValue(key, out var stickerPath))
                        continue;
                    if (!File.Exists(stickerPath))
                        continue;

                    CompositeSticker(canvas, stickerTemplate, slot, stickerPath,
                        texW, texH, manifest.KeyColorTolerance);
                }
            }

            // ── Wrap result as BitmapSource ───────────────────────────────────
            return ToBitmapSource(canvas, texW, texH);
        }

        /// <summary>
        /// Composite and write to a PNG file.
        /// DDS conversion should be applied by the caller afterwards.
        /// </summary>
        public void SaveAsPng(
            HelmetEraManifest manifest,
            HelmetCompositeInput definition,
            string manifestFolder,
            string outputPath)
        {
            var bmp = Composite(manifest, definition, manifestFolder);
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
            using var stream = File.Create(outputPath);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bmp));
            encoder.Save(stream);
        }

        // ─── Pass 1: Key Color Replacement ───────────────────────────────────

        private static void ReplaceKeyColors(
            byte[] canvas, int w, int h,
            Dictionary<string, string> zoneColors,
            byte tolerance)
        {
            // Pre-resolve user colors to avoid repeated dictionary lookups per pixel
            var replacements = new Dictionary<KeyColor, (byte R, byte G, byte B)>();
            foreach (var (keyStr, hexColor) in zoneColors)
            {
                if (!Enum.TryParse<KeyColor>(keyStr, out var key)) continue;
                try
                {
                    var c = (Color)ColorConverter.ConvertFromString(hexColor);
                    replacements[key] = (c.R, c.G, c.B);
                }
                catch { }
            }

            if (replacements.Count == 0) return;

            int stride = w * 4; // Pbgra32: 4 bytes per pixel (B G R A)

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int i = y * stride + x * 4;
                    byte b = canvas[i + 0];
                    byte g = canvas[i + 1];
                    byte r = canvas[i + 2];
                    byte a = canvas[i + 3];

                    if (a == 0) continue; // fully transparent — skip

                    var match = KeyColorValues.Identify(r, g, b, tolerance);
                    if (match == null) continue;
                    if (!replacements.TryGetValue(match.Value, out var newRgb)) continue;

                    canvas[i + 0] = newRgb.B;
                    canvas[i + 1] = newRgb.G;
                    canvas[i + 2] = newRgb.R;
                    // Preserve original alpha
                }
            }
        }

        // ─── Pass 2: Sticker Warping ──────────────────────────────────────────

        private static void CompositeSticker(
            byte[] canvas,
            byte[] stickerTemplate,
            StickerSlotDefinition slot,
            string stickerPath,
            int texW, int texH,
            byte tolerance)
        {
            // ── Step A: Build pixel mask from sticker template ────────────────
            var (targetR, targetG, targetB) = KeyColorValues.Rgb[slot.Color];
            int stride = texW * 4;

            // Find bounding rect of the colored region
            int minX = texW, minY = texH, maxX = 0, maxY = 0;
            bool any = false;

            for (int y = 0; y < texH; y++)
            {
                for (int x = 0; x < texW; x++)
                {
                    int i = y * stride + x * 4;
                    byte b = stickerTemplate[i + 0];
                    byte g = stickerTemplate[i + 1];
                    byte r = stickerTemplate[i + 2];
                    byte a = stickerTemplate[i + 3];
                    if (a == 0) continue;

                    if (Math.Abs(r - targetR) <= tolerance &&
                        Math.Abs(g - targetG) <= tolerance &&
                        Math.Abs(b - targetB) <= tolerance)
                    {
                        if (x < minX) minX = x;
                        if (y < minY) minY = y;
                        if (x > maxX) maxX = x;
                        if (y > maxY) maxY = y;
                        any = true;
                    }
                }
            }

            if (!any) return;

            int bboxW = maxX - minX + 1;
            int bboxH = maxY - minY + 1;

            // ── Step B: Detect the 4 quad corners ────────────────────────────
            // Strategy: for each of the 4 quadrants of the bounding rect,
            // find the pixel of the target color that is furthest from the
            // bounding rect center. This robustly picks the 4 actual corners
            // of a convex quad even with aliasing on the edges.

            double cx = minX + bboxW / 2.0;
            double cy = minY + bboxH / 2.0;

            // [0]=TopLeft [1]=TopRight [2]=BottomRight [3]=BottomLeft
            var corners = new (double X, double Y)[4];
            double[] bestDist = new double[4];

            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    int i = y * stride + x * 4;
                    byte pb = stickerTemplate[i + 0];
                    byte pg = stickerTemplate[i + 1];
                    byte pr = stickerTemplate[i + 2];
                    byte pa = stickerTemplate[i + 3];
                    if (pa == 0) continue;

                    if (Math.Abs(pr - targetR) > tolerance ||
                        Math.Abs(pg - targetG) > tolerance ||
                        Math.Abs(pb - targetB) > tolerance) continue;

                    double dx = x - cx;
                    double dy = y - cy;
                    double dist = dx * dx + dy * dy;

                    // Quadrant classification
                    int q = (dx >= 0 ? 1 : 0) | (dy >= 0 ? 2 : 0);
                    // q: 0=TL, 1=TR, 2=BL, 3=BR
                    // Remap to [0]=TL [1]=TR [2]=BR [3]=BL
                    int idx = q == 0 ? 0 : q == 1 ? 1 : q == 3 ? 2 : 3;

                    if (dist > bestDist[idx])
                    {
                        bestDist[idx] = dist;
                        corners[idx] = (x, y);
                    }
                }
            }

            // dst corners: TL, TR, BR, BL in destination (texture) space
            // src corners: four corners of the prepared sticker image

            // ── Step C: Load + prepare the sticker image ──────────────────────
            byte[] stickerPixels = LoadAndPrepareStickerImage(
                stickerPath, slot,
                out int stickerW, out int stickerH);

            // Source quad = the four corners of the prepared sticker image
            var srcQuad = new (double X, double Y)[4]
            {
                (0,             0           ),  // TL
                (stickerW - 1,  0           ),  // TR
                (stickerW - 1,  stickerH - 1),  // BR
                (0,             stickerH - 1),  // BL
            };

            // ── Step D: Compute homography src → dst ──────────────────────────
            double[]? H = ComputeHomography(srcQuad, corners);
            if (H == null) return; // degenerate quad — skip

            // ── Step E: Warp and composite into canvas ────────────────────────
            // Iterate over the destination bounding rect, map each pixel back
            // to source space (inverse warp), sample, mask, alpha-composite.

            double[]? Hinv = ComputeHomography(corners, srcQuad);
            if (Hinv == null) return;

            for (int dy2 = minY; dy2 <= maxY; dy2++)
            {
                for (int dx2 = minX; dx2 <= maxX; dx2++)
                {
                    // Check mask: this pixel must be part of the colored quad
                    int mi = dy2 * stride + dx2 * 4;
                    byte mb = stickerTemplate[mi + 0];
                    byte mg = stickerTemplate[mi + 1];
                    byte mr = stickerTemplate[mi + 2];
                    byte ma = stickerTemplate[mi + 3];
                    if (ma == 0) continue;
                    if (Math.Abs(mr - targetR) > tolerance ||
                        Math.Abs(mg - targetG) > tolerance ||
                        Math.Abs(mb - targetB) > tolerance) continue;

                    // Map destination pixel back to sticker source space
                    MapPoint(Hinv, dx2, dy2, out double sx, out double sy);

                    int six = (int)Math.Round(sx);
                    int siy = (int)Math.Round(sy);
                    if (six < 0 || six >= stickerW || siy < 0 || siy >= stickerH) continue;

                    int srcIdx = siy * stickerW * 4 + six * 4;
                    byte sb = stickerPixels[srcIdx + 0];
                    byte sg = stickerPixels[srcIdx + 1];
                    byte sr = stickerPixels[srcIdx + 2];
                    byte sa = stickerPixels[srcIdx + 3];

                    if (sa == 0) continue; // sticker pixel is transparent

                    // Alpha-composite sticker over canvas (straight alpha blend)
                    int ci = dy2 * stride + dx2 * 4;
                    float alpha = sa / 255f;
                    float inv = 1f - alpha;

                    canvas[ci + 0] = Clamp(sb * alpha + canvas[ci + 0] * inv);
                    canvas[ci + 1] = Clamp(sg * alpha + canvas[ci + 1] * inv);
                    canvas[ci + 2] = Clamp(sr * alpha + canvas[ci + 2] * inv);
                    canvas[ci + 3] = Math.Max(sa, canvas[ci + 3]);
                }
            }
        }

        // ─── Sticker Image Preparation ────────────────────────────────────────

        private static byte[] LoadAndPrepareStickerImage(
            string path,
            StickerSlotDefinition slot,
            out int outW,
            out int outH)
        {
            // ── Load as Pbgra32 ───────────────────────────────────────────────
            var src = new BitmapImage();
            src.BeginInit();
            src.UriSource = new Uri(path, UriKind.Absolute);
            src.CacheOption = BitmapCacheOption.OnLoad;
            src.EndInit();
            src.Freeze();

            var converted = new FormatConvertedBitmap(src, PixelFormats.Pbgra32, null, 0);
            int fullW = converted.PixelWidth;
            int fullH = converted.PixelHeight;

            // ── Step 1: Downscale to fit within ImageBase dimensions ───────────
            // If the user supplies an image larger than the base dimensions in
            // either axis, scale it down uniformly (preserving aspect ratio) so
            // that it fits within ImageBaseWidth × ImageBaseHeight.
            // Images that are already equal to or smaller than the base are left
            // as-is — we never upscale here.
            //
            // The crop coordinates in the slot definition are always expressed
            // relative to the base dimensions, so after this step the pixel
            // buffer is guaranteed to be at most ImageBaseWidth × ImageBaseHeight,
            // and the crop can be applied directly without any coordinate remapping.

            int baseW = slot.ImageBaseWidth > 0 ? slot.ImageBaseWidth : fullW;
            int baseH = slot.ImageBaseHeight > 0 ? slot.ImageBaseHeight : fullH;

            int workW = fullW;
            int workH = fullH;
            byte[] work;

            bool needsDownscale = fullW > baseW || fullH > baseH;
            if (needsDownscale)
            {
                // Uniform scale: pick the factor that brings both axes within bounds
                double scaleX = (double)baseW / fullW;
                double scaleY = (double)baseH / fullH;
                double scale = Math.Min(scaleX, scaleY); // always ≤ 1.0

                workW = Math.Max(1, (int)Math.Round(fullW * scale));
                workH = Math.Max(1, (int)Math.Round(fullH * scale));

                // Use WPF's TransformedBitmap for a clean high-quality downscale
                var scaled = new TransformedBitmap(
                    converted,
                    new ScaleTransform(scale, scale));

                int scaledStride = workW * 4;
                work = new byte[workH * scaledStride];
                scaled.CopyPixels(work, scaledStride, 0);
            }
            else
            {
                // Already within bounds — copy pixels directly
                int stride = fullW * 4;
                work = new byte[fullH * stride];
                converted.CopyPixels(work, stride, 0);
            }

            // ── Step 2: Crop ──────────────────────────────────────────────────
            // Crop coordinates are in base-dimension space. Because workW/workH
            // are now ≤ baseW/baseH, we clamp to the actual work buffer size.

            int cropX = slot.SourceX;
            int cropY = slot.SourceY;
            int cropW = slot.SourceWidth > 0 ? slot.SourceWidth : workW - cropX;
            int cropH = slot.SourceHeight > 0 ? slot.SourceHeight : workH - cropY;

            cropX = Math.Max(0, Math.Min(cropX, workW - 1));
            cropY = Math.Max(0, Math.Min(cropY, workH - 1));
            cropW = Math.Max(1, Math.Min(cropW, workW - cropX));
            cropH = Math.Max(1, Math.Min(cropH, workH - cropY));

            int workStride = workW * 4;
            byte[] cropped = new byte[cropH * cropW * 4];
            for (int row = 0; row < cropH; row++)
            {
                int srcOff = (cropY + row) * workStride + cropX * 4;
                int dstOff = row * cropW * 4;
                Array.Copy(work, srcOff, cropped, dstOff, cropW * 4);
            }

            // ── Step 3: Rotation ──────────────────────────────────────────────
            int w = cropW, h = cropH;
            byte[] buf = cropped;

            int rotSteps = ((slot.Rotation / 90) % 4 + 4) % 4;
            for (int step = 0; step < rotSteps; step++)
                buf = Rotate90CW(buf, ref w, ref h);

            // ── Step 4: Flip ──────────────────────────────────────────────────
            if (slot.FlipHorizontal) buf = FlipH(buf, w, h);
            if (slot.FlipVertical) buf = FlipV(buf, w, h);

            outW = w;
            outH = h;
            return buf;
        }

        private static byte[] Rotate90CW(byte[] src, ref int w, ref int h)
        {
            byte[] dst = new byte[src.Length];
            int newW = h, newH = w;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int si = (y * w + x) * 4;
                    int di = (x * newW + (newW - 1 - y)) * 4;
                    dst[di + 0] = src[si + 0];
                    dst[di + 1] = src[si + 1];
                    dst[di + 2] = src[si + 2];
                    dst[di + 3] = src[si + 3];
                }
            w = newW; h = newH;
            return dst;
        }

        private static byte[] FlipH(byte[] src, int w, int h)
        {
            byte[] dst = new byte[src.Length];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int si = (y * w + x) * 4;
                    int di = (y * w + (w - 1 - x)) * 4;
                    dst[di + 0] = src[si + 0]; dst[di + 1] = src[si + 1];
                    dst[di + 2] = src[si + 2]; dst[di + 3] = src[si + 3];
                }
            return dst;
        }

        private static byte[] FlipV(byte[] src, int w, int h)
        {
            byte[] dst = new byte[src.Length];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int si = (y * w + x) * 4;
                    int di = ((h - 1 - y) * w + x) * 4;
                    dst[di + 0] = src[si + 0]; dst[di + 1] = src[si + 1];
                    dst[di + 2] = src[si + 2]; dst[di + 3] = src[si + 3];
                }
            return dst;
        }

        // ─── Homography (DLT — no external dependencies) ──────────────────────
        //
        // Computes a 3×3 projective transform matrix H such that for each
        // corresponding point pair (src[i], dst[i]):
        //   [dst.x, dst.y, 1]^T  ∝  H · [src.x, src.y, 1]^T
        //
        // Solved via the 8-point DLT formulation with Gaussian elimination.
        // Works for any non-degenerate set of 4 point correspondences.

        private static double[]? ComputeHomography(
            (double X, double Y)[] src,
            (double X, double Y)[] dst)
        {
            // Build 8×9 matrix A (two equations per point pair)
            double[,] A = new double[8, 9];
            for (int i = 0; i < 4; i++)
            {
                double sx = src[i].X, sy = src[i].Y;
                double dx = dst[i].X, dy = dst[i].Y;

                A[2 * i, 0] = sx; A[2 * i, 1] = sy; A[2 * i, 2] = 1;
                A[2 * i, 3] = 0; A[2 * i, 4] = 0; A[2 * i, 5] = 0;
                A[2 * i, 6] = -dx * sx; A[2 * i, 7] = -dx * sy; A[2 * i, 8] = -dx;

                A[2 * i + 1, 0] = 0; A[2 * i + 1, 1] = 0; A[2 * i + 1, 2] = 0;
                A[2 * i + 1, 3] = sx; A[2 * i + 1, 4] = sy; A[2 * i + 1, 5] = 1;
                A[2 * i + 1, 6] = -dy * sx; A[2 * i + 1, 7] = -dy * sy; A[2 * i + 1, 8] = -dy;
            }

            // Solve A·h = 0 by finding the null-space via SVD approximation:
            // For 4-point DLT we can fix h[8]=1 and solve the 8×8 system.
            // This avoids a full SVD — sufficient for well-conditioned quads.
            double[,] M = new double[8, 8];
            double[] b = new double[8];
            for (int r = 0; r < 8; r++)
            {
                for (int c = 0; c < 8; c++) M[r, c] = A[r, c];
                b[r] = -A[r, 8];
            }

            double[]? h = GaussianElimination(M, b);
            if (h == null) return null;

            // H = [h0..h7, 1] reshaped row-major 3×3
            return new double[]
            {
                h[0], h[1], h[2],
                h[3], h[4], h[5],
                h[6], h[7], 1.0
            };
        }

        private static void MapPoint(double[] H, double x, double y,
            out double ox, out double oy)
        {
            double w2 = H[6] * x + H[7] * y + H[8];
            if (Math.Abs(w2) < 1e-10) { ox = oy = 0; return; }
            ox = (H[0] * x + H[1] * y + H[2]) / w2;
            oy = (H[3] * x + H[4] * y + H[5]) / w2;
        }

        /// <summary>Solves Ax = b via Gaussian elimination with partial pivoting.</summary>
        private static double[]? GaussianElimination(double[,] A, double[] b)
        {
            int n = b.Length;
            double[,] M = (double[,])A.Clone();
            double[] v = (double[])b.Clone();

            for (int col = 0; col < n; col++)
            {
                // Partial pivot
                int pivot = col;
                double best = Math.Abs(M[col, col]);
                for (int row = col + 1; row < n; row++)
                {
                    double abs = Math.Abs(M[row, col]);
                    if (abs > best) { best = abs; pivot = row; }
                }
                if (best < 1e-12) return null; // singular

                if (pivot != col)
                {
                    for (int c = 0; c < n; c++)
                        (M[col, c], M[pivot, c]) = (M[pivot, c], M[col, c]);
                    (v[col], v[pivot]) = (v[pivot], v[col]);
                }

                double diag = M[col, col];
                for (int row = col + 1; row < n; row++)
                {
                    double factor = M[row, col] / diag;
                    for (int c = col; c < n; c++)
                        M[row, c] -= factor * M[col, c];
                    v[row] -= factor * v[col];
                }
            }

            // Back-substitution
            double[] x = new double[n];
            for (int row = n - 1; row >= 0; row--)
            {
                double sum = v[row];
                for (int c = row + 1; c < n; c++) sum -= M[row, c] * x[c];
                x[row] = sum / M[row, row];
            }
            return x;
        }

        // ─── Pixel Helpers ────────────────────────────────────────────────────

        private static byte[] LoadPbgra(string path, int expectedW, int expectedH,
            out int actualW, out int actualH)
        {
            if (!File.Exists(path))
            {
                actualW = expectedW; actualH = expectedH;
                return new byte[expectedW * expectedH * 4]; // transparent blank
            }

            var src = new BitmapImage();
            src.BeginInit();
            src.UriSource = new Uri(path, UriKind.Absolute);
            src.CacheOption = BitmapCacheOption.OnLoad;
            src.EndInit();
            src.Freeze();

            var conv = new FormatConvertedBitmap(src, PixelFormats.Pbgra32, null, 0);
            actualW = conv.PixelWidth;
            actualH = conv.PixelHeight;
            int stride = actualW * 4;
            byte[] pixels = new byte[actualH * stride];
            conv.CopyPixels(pixels, stride, 0);
            return pixels;
        }

        private static BitmapSource ToBitmapSource(byte[] pixels, int w, int h)
        {
            var wb = new WriteableBitmap(w, h, 96, 96, PixelFormats.Pbgra32, null);
            wb.WritePixels(new Int32Rect(0, 0, w, h), pixels, w * 4, 0);
            wb.Freeze();
            return wb;
        }

        private static string ResolvePath(string folder, string relative)
            => Path.IsPathRooted(relative)
                ? relative
                : Path.Combine(folder, relative);

        private static byte Clamp(float v) =>
            v < 0 ? (byte)0 : v > 255 ? (byte)255 : (byte)v;
    }
}