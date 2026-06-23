using HelixToolkit.SharpDX.Core;
using HelixToolkit.SharpDX.Core.Assimp;
using HelixToolkit.SharpDX.Core.Model;
using HelixToolkit.SharpDX.Core.Model.Scene;
using HelixToolkit.Wpf.SharpDX;
using Microsoft.Win32;
using SharpDX;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Text.Json;
using System.Text.Json.Serialization;
using Media3D = System.Windows.Media.Media3D;
using WpfColor = System.Windows.Media.Color;

namespace AMS2ChEd.SeasonPackEditor.HelmetEditor.Controls
{
    public partial class HelmetSticker3DControl : UserControl
    {
        // ── Scene ─────────────────────────────────────────────────────────────
        private Camera _camera;
        private EffectsManager _effectsManager;
        private HelixToolkitScene _scene;
        private SceneNodeGroupModel3D _sceneModel;

        private Media3D.Point3D _helmetCenter = new Media3D.Point3D(0, 1650, 0);
        private double _helmetSize = 400;

        // ── Helmet template
        private HelmetTemplate _currentTemplate;

        // ── Zone textures (stored as paths, reloaded fresh each bake) ──────────
        private string _zoneLeftPath;
        private string _zoneRightPath;
        private string _zoneFrontPath;
        private string _zoneBackPath;
        private string _zoneTopPath;

        // ── Output ────────────────────────────────────────────────────────────
        private WriteableBitmap _outputTexture;
        private MemoryStream _textureStream;

        public HelmetSticker3DControl()
        {
            InitializeComponent();
            InitializeScene();
            Loaded += (s, e) => LoadHelmetTemplates();
        }

        private void InitializeScene()
        {
            _effectsManager = new DefaultEffectsManager();
            _camera = new PerspectiveCamera
            {
                Position = new Media3D.Point3D(-400, 1650, 0),
                LookDirection = new Media3D.Vector3D(1, 0, 0),
                UpDirection = new Media3D.Vector3D(0, 1, 0),
                FieldOfView = 45,
                NearPlaneDistance = 0.1,
                FarPlaneDistance = 5000
            };
            DataContext = new { Camera = _camera, EffectsManager = _effectsManager };
        }

        // ── Helmet templates ──────────────────────────────────────────────────

        private void LoadHelmetTemplates()
        {
            var appDir = AppDomain.CurrentDomain.BaseDirectory;
            var templatesDir = Path.Combine(appDir, "HelmetTemplates");
            HelmetCombo.Items.Clear();

            if (!Directory.Exists(templatesDir)) return;

            foreach (var jsonFile in Directory.GetFiles(templatesDir, "*.json").OrderBy(f => f))
            {
                try
                {
                    var json = File.ReadAllText(jsonFile);
                    var template = JsonSerializer.Deserialize<HelmetTemplate>(json,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    if (template == null) continue;
                    template.SourceFile = jsonFile;
                    HelmetCombo.Items.Add(template);
                }
                catch { /* skip malformed JSON */ }
            }

            if (HelmetCombo.Items.Count > 0)
                HelmetCombo.SelectedIndex = 0;
        }

        private void HelmetCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (HelmetCombo.SelectedItem is not HelmetTemplate template) return;
            _currentTemplate = template;

            var appDir = AppDomain.CurrentDomain.BaseDirectory;
            var objPath = Path.IsPathRooted(template.ObjectFilePath)
                        ? template.ObjectFilePath
                        : Path.Combine(appDir, template.ObjectFilePath);

            if (!File.Exists(objPath))
            {
                MessageBox.Show($"OBJ file not found:\n{objPath}", "Load Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                var importer = new Importer();
                _scene = importer.Load(objPath);
                if (_scene?.Root == null) throw new Exception("No scene loaded from OBJ");

                if (_sceneModel != null) Viewport.Items.Remove(_sceneModel);
                _sceneModel = new SceneNodeGroupModel3D();
                _sceneModel.AddNode(_scene.Root);
                Viewport.Items.Add(_sceneModel);

                ReframeCamera();
                RefreshBakeButton();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load helmet:\n{ex.Message}", "Load Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ── Camera reframe ────────────────────────────────────────────────────

        private void ReframeCamera()
        {
            if (_scene?.Root == null) return;

            // Compute mesh AABB from all mesh nodes
            float minX = float.MaxValue, minY = float.MaxValue, minZ = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue, maxZ = float.MinValue;

            foreach (var meshNode in _scene.Root.Traverse().OfType<MeshNode>())
            {
                var geom = meshNode.Geometry as HelixToolkit.SharpDX.Core.MeshGeometry3D;
                if (geom?.Positions == null) continue;
                foreach (var p in geom.Positions)
                {
                    if (p.X < minX) minX = p.X; if (p.X > maxX) maxX = p.X;
                    if (p.Y < minY) minY = p.Y; if (p.Y > maxY) maxY = p.Y;
                    if (p.Z < minZ) minZ = p.Z; if (p.Z > maxZ) maxZ = p.Z;
                }
            }

            if (minX == float.MaxValue) return; // no geometry found

            // Update helmet center and size for projection
            _helmetCenter = new Media3D.Point3D(
                (minX + maxX) / 2.0,
                (minY + maxY) / 2.0,
                (minZ + maxZ) / 2.0);

            _helmetSize = Math.Max(maxX - minX, Math.Max(maxY - minY, maxZ - minZ));

            // Position camera to the left side at a comfortable distance
            double distance = _helmetSize * 2.5;
            if (_camera is PerspectiveCamera cam)
            {
                cam.Position = new Media3D.Point3D(
                    _helmetCenter.X - distance,
                    _helmetCenter.Y,
                    _helmetCenter.Z);
                cam.LookDirection = new Media3D.Vector3D(distance, 0, 0);
                cam.UpDirection = new Media3D.Vector3D(0, 1, 0);
                cam.NearPlaneDistance = _helmetSize * 0.01;
                cam.FarPlaneDistance = _helmetSize * 20;
            }
        }

        // ── Load zone textures ────────────────────────────────────────────────

        private void ClearLeft_Click(object sender, RoutedEventArgs e)
        {
            _zoneLeftPath = null;
            LeftFileLabel.Text = "—";
            LeftFileLabel.Foreground = new SolidColorBrush(WpfColor.FromRgb(0x55, 0x55, 0x55));
            RefreshBakeButton();
        }
        private void ClearRight_Click(object sender, RoutedEventArgs e)
        {
            _zoneRightPath = null;
            RightFileLabel.Text = "—";
            RightFileLabel.Foreground = new SolidColorBrush(WpfColor.FromRgb(0x55, 0x55, 0x55));
            RefreshBakeButton();
        }
        private void ClearFront_Click(object sender, RoutedEventArgs e)
        {
            _zoneFrontPath = null;
            FrontFileLabel.Text = "—";
            FrontFileLabel.Foreground = new SolidColorBrush(WpfColor.FromRgb(0x55, 0x55, 0x55));
            RefreshBakeButton();
        }
        private void ClearBack_Click(object sender, RoutedEventArgs e)
        {
            _zoneBackPath = null;
            BackFileLabel.Text = "—";
            BackFileLabel.Foreground = new SolidColorBrush(WpfColor.FromRgb(0x55, 0x55, 0x55));
            RefreshBakeButton();
        }
        private void ClearTop_Click(object sender, RoutedEventArgs e)
        {
            _zoneTopPath = null;
            TopFileLabel.Text = "—";
            TopFileLabel.Foreground = new SolidColorBrush(WpfColor.FromRgb(0x55, 0x55, 0x55));
            RefreshBakeButton();
        }

        private void LoadLeft_Click(object sender, RoutedEventArgs e)
        {
            _zoneLeftPath = LoadZonePng("Left");
            if (_zoneLeftPath != null) SetLabel(LeftFileLabel, "(loaded)");
            RefreshBakeButton();
        }
        private void LoadRight_Click(object sender, RoutedEventArgs e)
        {
            _zoneRightPath = LoadZonePng("Right");
            if (_zoneRightPath != null) SetLabel(RightFileLabel, "(loaded)");
            RefreshBakeButton();
        }
        private void LoadFront_Click(object sender, RoutedEventArgs e)
        {
            _zoneFrontPath = LoadZonePng("Front");
            if (_zoneFrontPath != null) SetLabel(FrontFileLabel, "(loaded)");
            RefreshBakeButton();
        }
        private void LoadBack_Click(object sender, RoutedEventArgs e)
        {
            _zoneBackPath = LoadZonePng("Back");
            if (_zoneBackPath != null) SetLabel(BackFileLabel, "(loaded)");
            RefreshBakeButton();
        }
        private void LoadTop_Click(object sender, RoutedEventArgs e)
        {
            _zoneTopPath = LoadZonePng("Top");
            if (_zoneTopPath != null) SetLabel(TopFileLabel, "(loaded)");
            RefreshBakeButton();
        }

        private void SetLabel(System.Windows.Controls.TextBlock label, string text)
        {
            label.Text = text;
            label.Foreground = new SolidColorBrush(WpfColor.FromRgb(0xAA, 0xAA, 0xAA));
        }

        private string LoadZonePng(string zoneName)
        {
            var dlg = new OpenFileDialog { Title = $"Load {zoneName} Zone PNG", Filter = "PNG Images|*.png" };
            dlg.InitialDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SampleHelmetTextures");
            if (dlg.ShowDialog() != true) return null;
            if (!File.Exists(dlg.FileName)) return null;
            return dlg.FileName;
        }

        private static BitmapSource LoadBitmapFromPath(string path)
        {
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.UriSource = new Uri(path, UriKind.Absolute);
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
            bmp.EndInit();
            bmp.Freeze();
            return bmp;
        }

        // ── Bake ──────────────────────────────────────────────────────────────

        private void Bake_Click(object sender, RoutedEventArgs e)
        {
            if (_scene?.Root == null) return;

            int texWidth = _currentTemplate?.ExportWidth ?? 2048;
            int texHeight = _currentTemplate?.ExportHeight ?? 2048;

            BakeStatusLabel.Text = "Baking...";
            BakeButton.IsEnabled = false;

            try
            {
                _outputTexture = new WriteableBitmap(texWidth, texHeight, 96, 96, PixelFormats.Bgra32, null);

                var meshData = GatherMeshData();
                if (meshData.Count == 0)
                {
                    BakeStatusLabel.Text = "No mesh data found.";
                    return;
                }

                // Reload all zone textures fresh from disk so edits are picked up
                var zones = new (BitmapSource tex, ProjectionView view)[]
                {
                    (_zoneLeftPath  != null ? LoadBitmapFromPath(_zoneLeftPath)  : null, ProjectionView.Left),
                    (_zoneRightPath != null ? LoadBitmapFromPath(_zoneRightPath) : null, ProjectionView.Right),
                    (_zoneFrontPath != null ? LoadBitmapFromPath(_zoneFrontPath) : null, ProjectionView.Front),
                    (_zoneBackPath  != null ? LoadBitmapFromPath(_zoneBackPath)  : null, ProjectionView.Back),
                    (_zoneTopPath   != null ? LoadBitmapFromPath(_zoneTopPath)   : null, ProjectionView.Top),
                };

                foreach (var (tex, view) in zones)
                {
                    if (tex == null) continue;
                    bool flipU = (view == ProjectionView.Left && LeftFlipU.IsChecked == true)
                              || (view == ProjectionView.Right && RightFlipU.IsChecked == true);
                    bool flipV = (view == ProjectionView.Left && LeftFlipV.IsChecked == true)
                              || (view == ProjectionView.Right && RightFlipV.IsChecked == true);
                    ProjectZoneOntoTexture(tex, view, meshData, flipU, flipV);
                }

                DilateBakedTexture();
                ApplyTemplateOverlay();
                ApplyOutputTextureTo3D();
                ExportButton.IsEnabled = true;
                BakeStatusLabel.Text = "Bake complete.";
            }
            catch (Exception ex)
            {
                BakeStatusLabel.Text = $"Bake failed: {ex.Message}";
                MessageBox.Show($"Bake failed:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                BakeButton.IsEnabled = true;
            }
        }

        // ── Projection core ───────────────────────────────────────────────────

        private enum ProjectionView { Left, Right, Front, Back, Top }

        /// <summary>
        /// Orthographic slab projection.
        ///
        /// Iterates in OUTPUT TEXTURE space (mesh UVs) for each triangle.
        /// For each covered texel, computes where the 3D point sits on the
        /// zone projection plane, then samples the zone texture there.
        /// The zone texture auto-scales to fill the full visible face — no gaps.
        /// </summary>
        private void ProjectZoneOntoTexture(BitmapSource zoneBitmap, ProjectionView view, List<TriangleData> meshData, bool flipU = false, bool flipV = false)
        {
            var formatted = new FormatConvertedBitmap(zoneBitmap, PixelFormats.Bgra32, null, 0);
            int zw = formatted.PixelWidth;
            int zh = formatted.PixelHeight;
            int zStride = ((zw * 32 + 31) / 32) * 4;
            byte[] zpx = new byte[zStride * zh];
            formatted.CopyPixels(zpx, zStride, 0);

            // World Y bounds for top zone height culling
            float worldYMin = meshData.Min(t => Math.Min(t.P0.Y, Math.Min(t.P1.Y, t.P2.Y)));
            float worldYMax = meshData.Max(t => Math.Max(t.P0.Y, Math.Max(t.P1.Y, t.P2.Y)));
            float worldYRange = worldYMax - worldYMin;

            var projDir = GetProjectionDirection(view);
            GetSlabAxes(view, out Vector3 axisU, out Vector3 axisV);

            // Apply per-helmet rotation correction to compensate for baked geometry tilt.
            //
            // Left/Right: helmet has no left-right lean, RotationCorrection is typically ~0.
            // Front/Back: helmet leans forward (bottom-Z > top-Z), so stripes slope downward
            //             toward the back. Fix by rotating axisV toward the projection direction
            //             (i.e. tilting the "vertical" to follow the helmet's actual lean).
            //             This is a rotation around axisU rather than in the UV plane.

            bool hasSideCorrection = _currentTemplate != null && _currentTemplate.RotationCorrection != 0f;
            bool hasFrontCorrection = _currentTemplate != null && _currentTemplate.FrontBackRotationCorrection != 0f;
            bool isSideZone = view == ProjectionView.Left || view == ProjectionView.Right;
            bool isFrontZone = view == ProjectionView.Front || view == ProjectionView.Back || view == ProjectionView.Top;

            if (isSideZone && hasSideCorrection)
            {
                // Left/Right: rotate axisU/axisV in the UV plane (same as before)
                float deg = view == ProjectionView.Left
                    ? -_currentTemplate.RotationCorrection
                    : _currentTemplate.RotationCorrection;
                float rad = deg * (float)Math.PI / 180f;
                float cos = (float)Math.Cos(rad);
                float sin = (float)Math.Sin(rad);
                Vector3 newU = new Vector3(
                    axisU.X * cos - axisV.X * sin,
                    axisU.Y * cos - axisV.Y * sin,
                    axisU.Z * cos - axisV.Z * sin);
                Vector3 newV = new Vector3(
                    axisU.X * sin + axisV.X * cos,
                    axisU.Y * sin + axisV.Y * cos,
                    axisU.Z * sin + axisV.Z * cos);
                axisU = Vector3.Normalize(newU);
                axisV = Vector3.Normalize(newV);
            }
            else if (isFrontZone && hasFrontCorrection)
            {
                // Front/Back/Top: the helmet leans forward so stripes tilt toward the back.
                // Fix by rotating axisV around axisU — this tilts "vertical" forward/backward
                // to follow the helmet's actual lean rather than pure world Y.
                float deg = view switch
                {
                    ProjectionView.Top => 0f,  // top looks straight down, no correction needed
                    ProjectionView.Back => -_currentTemplate.FrontBackRotationCorrection,
                    _ => _currentTemplate.FrontBackRotationCorrection
                };
                float rad = deg * (float)Math.PI / 180f;
                float cos = (float)Math.Cos(rad);
                float sin = (float)Math.Sin(rad);
                // Rotate axisV around axisU
                Vector3 newV = new Vector3(
                    axisV.X * cos + projDir.X * sin,
                    axisV.Y * cos + projDir.Y * sin,
                    axisV.Z * cos + projDir.Z * sin);
                axisV = Vector3.Normalize(newV);
                // Re-orthogonalise axisU against the new axisV
                axisU = Vector3.Normalize(Vector3.Cross(axisV, projDir));
            }

            float slabUMin = ProjectAll(meshData, axisU, out float slabUMax);
            float slabVMin = ProjectAll(meshData, axisV, out float slabVMax);
            float slabURange = slabUMax - slabUMin;
            float slabVRange = slabVMax - slabVMin;
            if (slabURange < 1e-6f || slabVRange < 1e-6f) return;



            int tw = _outputTexture.PixelWidth;
            int th = _outputTexture.PixelHeight;
            int tStride = _outputTexture.BackBufferStride;

            _outputTexture.Lock();
            byte[] tpx = new byte[tStride * th];
            System.Runtime.InteropServices.Marshal.Copy(_outputTexture.BackBuffer, tpx, 0, tpx.Length);

            foreach (var tri in meshData)
            {
                if (Vector3.Dot(tri.Normal, projDir) >= 0f) continue;

                // Top zone: only paint the upper crown of the helmet.
                // Compute world Y fraction and skip triangles below 70% height.
                if (view == ProjectionView.Top)
                {
                    float cy = (tri.P0.Y + tri.P1.Y + tri.P2.Y) / 3f;
                    float heightFraction = (cy - worldYMin) / worldYRange;
                    if (heightFraction < 0.70f) continue;
                }

                // Output texture pixel coords for each vertex
                float p0x = tri.UV0.X * (tw - 1), p0y = tri.UV0.Y * (th - 1);
                float p1x = tri.UV1.X * (tw - 1), p1y = tri.UV1.Y * (th - 1);
                float p2x = tri.UV2.X * (tw - 1), p2y = tri.UV2.Y * (th - 1);

                // Zone texture coords for each vertex via slab projection
                float z0u = (Vector3.Dot(tri.P0, axisU) - slabUMin) / slabURange;
                float z0v = (Vector3.Dot(tri.P0, axisV) - slabVMin) / slabVRange;
                float z1u = (Vector3.Dot(tri.P1, axisU) - slabUMin) / slabURange;
                float z1v = (Vector3.Dot(tri.P1, axisV) - slabVMin) / slabVRange;
                float z2u = (Vector3.Dot(tri.P2, axisU) - slabUMin) / slabURange;
                float z2v = (Vector3.Dot(tri.P2, axisV) - slabVMin) / slabVRange;

                int minX = Math.Max(0, (int)Math.Floor(Math.Min(p0x, Math.Min(p1x, p2x))));
                int maxX = Math.Min(tw - 1, (int)Math.Ceiling(Math.Max(p0x, Math.Max(p1x, p2x))));
                int minY = Math.Max(0, (int)Math.Floor(Math.Min(p0y, Math.Min(p1y, p2y))));
                int maxY = Math.Min(th - 1, (int)Math.Ceiling(Math.Max(p0y, Math.Max(p1y, p2y))));

                if (minX > maxX || minY > maxY) continue;

                float denom = (p1y - p2y) * (p0x - p2x) + (p2x - p1x) * (p0y - p2y);
                if (Math.Abs(denom) < 0.5f) continue;
                float invDenom = 1f / denom;

                for (int ty = minY; ty <= maxY; ty++)
                {
                    for (int tx = minX; tx <= maxX; tx++)
                    {
                        float px = tx + 0.5f;
                        float py = ty + 0.5f;

                        float w0 = ((p1y - p2y) * (px - p2x) + (p2x - p1x) * (py - p2y)) * invDenom;
                        float w1 = ((p2y - p0y) * (px - p2x) + (p0x - p2x) * (py - p2y)) * invDenom;
                        float w2 = 1f - w0 - w1;

                        if (w0 < -0.001f || w1 < -0.001f || w2 < -0.001f) continue;

                        float zu = z0u * w0 + z1u * w1 + z2u * w2;
                        float zv = z0v * w0 + z1v * w1 + z2v * w2;
                        zu = Math.Max(0f, Math.Min(0.9999f, zu));
                        zv = Math.Max(0f, Math.Min(0.9999f, zv));

                        if (flipU) zu = 0.9999f - zu;
                        if (flipV) zv = 0.9999f - zv;

                        // Circular mask for top zone: discard corners outside inscribed circle
                        if (view == ProjectionView.Top)
                        {
                            float du = zu - 0.5f;
                            float dv = zv - 0.5f;
                            if (du * du + dv * dv > 0.25f) continue; // outside radius 0.5
                        }

                        int zx = (int)(zu * zw);
                        int zy = (int)(zv * zh);
                        int zIdx = zy * zStride + zx * 4;

                        byte zB = zpx[zIdx];
                        byte zG = zpx[zIdx + 1];
                        byte zR = zpx[zIdx + 2];
                        byte zA = zpx[zIdx + 3];
                        if (zA == 0) continue;

                        float alpha = zA / 255f;
                        float invAlpha = 1f - alpha;
                        int tIdx = ty * tStride + tx * 4;

                        tpx[tIdx] = (byte)(zB * alpha + tpx[tIdx] * invAlpha);
                        tpx[tIdx + 1] = (byte)(zG * alpha + tpx[tIdx + 1] * invAlpha);
                        tpx[tIdx + 2] = (byte)(zR * alpha + tpx[tIdx + 2] * invAlpha);
                        tpx[tIdx + 3] = (byte)Math.Min(255, tpx[tIdx + 3] + zA);
                    }
                }
            }

            System.Runtime.InteropServices.Marshal.Copy(tpx, 0, _outputTexture.BackBuffer, tpx.Length);
            _outputTexture.AddDirtyRect(new Int32Rect(0, 0, tw, th));
            _outputTexture.Unlock();
        }

        // ── Slab helpers ──────────────────────────────────────────────────────

        private static Vector3 GetProjectionDirection(ProjectionView view) => view switch
        {
            ProjectionView.Left => new Vector3(1, 0, 0),
            ProjectionView.Right => new Vector3(-1, 0, 0),
            ProjectionView.Front => new Vector3(0, 0, -1),
            ProjectionView.Back => new Vector3(0, 0, 1),
            ProjectionView.Top => new Vector3(0, -1, 0),
            _ => throw new ArgumentOutOfRangeException()
        };

        private static void GetSlabAxes(ProjectionView view, out Vector3 axisU, out Vector3 axisV)
        {
            switch (view)
            {
                case ProjectionView.Left:
                    axisU = new Vector3(0, 0, -1);
                    axisV = new Vector3(0, -1, 0);
                    break;
                case ProjectionView.Right:
                    axisU = new Vector3(0, 0, 1);
                    axisV = new Vector3(0, -1, 0);
                    break;
                case ProjectionView.Front:
                    axisU = new Vector3(1, 0, 0);
                    axisV = new Vector3(0, -1, 0);
                    break;
                case ProjectionView.Back:
                    axisU = new Vector3(-1, 0, 0);
                    axisV = new Vector3(0, -1, 0);
                    break;
                case ProjectionView.Top:
                    axisU = new Vector3(1, 0, 0);
                    axisV = new Vector3(0, 0, 1);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private static float ProjectAll(List<TriangleData> tris, Vector3 axis, out float max)
        {
            float min = float.MaxValue;
            max = float.MinValue;
            foreach (var t in tris)
            {
                float d0 = Vector3.Dot(t.P0, axis);
                float d1 = Vector3.Dot(t.P1, axis);
                float d2 = Vector3.Dot(t.P2, axis);
                if (d0 < min) min = d0; if (d0 > max) max = d0;
                if (d1 < min) min = d1; if (d1 > max) max = d1;
                if (d2 < min) min = d2; if (d2 > max) max = d2;
            }
            return min;
        }

        // ── Mesh data ─────────────────────────────────────────────────────────

        private struct TriangleData
        {
            public Vector3 P0, P1, P2;
            public Vector2 UV0, UV1, UV2;
            public Vector3 Normal;
        }

        private List<TriangleData> GatherMeshData()
        {
            var triangles = new List<TriangleData>();
            if (_scene?.Root == null) return triangles;

            foreach (var meshNode in _scene.Root.Traverse().OfType<MeshNode>())
            {
                var geom = meshNode.Geometry as HelixToolkit.SharpDX.Core.MeshGeometry3D;
                if (geom?.Positions == null || geom.TextureCoordinates == null || geom.Indices == null)
                    continue;

                for (int i = 0; i + 2 < geom.Indices.Count; i += 3)
                {
                    int i0 = geom.Indices[i], i1 = geom.Indices[i + 1], i2 = geom.Indices[i + 2];
                    if (i0 >= geom.Positions.Count || i1 >= geom.Positions.Count || i2 >= geom.Positions.Count)
                        continue;

                    var p0 = geom.Positions[i0];
                    var p1 = geom.Positions[i1];
                    var p2 = geom.Positions[i2];
                    var normal = Vector3.Cross(p1 - p0, p2 - p0);
                    if (normal.LengthSquared() < 1e-10f) continue;
                    normal.Normalize();

                    triangles.Add(new TriangleData
                    {
                        P0 = p0,
                        P1 = p1,
                        P2 = p2,
                        UV0 = geom.TextureCoordinates[i0],
                        UV1 = geom.TextureCoordinates[i1],
                        UV2 = geom.TextureCoordinates[i2],
                        Normal = normal
                    });
                }
            }
            return triangles;
        }

        // ── Dilation ─────────────────────────────────────────────────────────

        /// <summary>
        /// Fills 1-pixel gaps at UV island edges by copying the nearest filled
        /// neighbour into any unfilled (alpha=0) pixel that touches a filled one.
        /// Run once after all zones are baked.
        /// </summary>
        private void DilateBakedTexture()
        {
            int tw = _outputTexture.PixelWidth;
            int th = _outputTexture.PixelHeight;
            int tStride = _outputTexture.BackBufferStride;

            _outputTexture.Lock();
            byte[] tpx = new byte[tStride * th];
            System.Runtime.InteropServices.Marshal.Copy(_outputTexture.BackBuffer, tpx, 0, tpx.Length);
            byte[] result = (byte[])tpx.Clone();

            int[] dx = { -1, 1, 0, 0, -1, -1, 1, 1 };
            int[] dy = { 0, 0, -1, 1, -1, 1, -1, 1 };

            // Run multiple passes to fill gaps wider than 1 pixel
            const int passes = 4;
            for (int pass = 0; pass < passes; pass++)
            {
                bool anyFilled = false;
                for (int ty = 0; ty < th; ty++)
                {
                    for (int tx = 0; tx < tw; tx++)
                    {
                        int idx = ty * tStride + tx * 4;
                        if (tpx[idx + 3] > 0) continue; // already filled

                        for (int d = 0; d < dx.Length; d++)
                        {
                            int nx = tx + dx[d];
                            int ny = ty + dy[d];
                            if (nx < 0 || nx >= tw || ny < 0 || ny >= th) continue;
                            int nIdx = ny * tStride + nx * 4;
                            if (tpx[nIdx + 3] == 0) continue;

                            result[idx] = tpx[nIdx];
                            result[idx + 1] = tpx[nIdx + 1];
                            result[idx + 2] = tpx[nIdx + 2];
                            result[idx + 3] = tpx[nIdx + 3];
                            anyFilled = true;
                            break;
                        }
                    }
                }
                if (!anyFilled) break; // no more gaps to fill
                // Feed result back into tpx for next pass
                Array.Copy(result, tpx, result.Length);
            }

            System.Runtime.InteropServices.Marshal.Copy(result, 0, _outputTexture.BackBuffer, result.Length);
            _outputTexture.AddDirtyRect(new Int32Rect(0, 0, tw, th));
            _outputTexture.Unlock();
        }

        // ── Template overlay ─────────────────────────────────────────────────

        private void ApplyTemplateOverlay()
        {
            if (string.IsNullOrEmpty(_currentTemplate?.TemplateOverlay)) return;

            var appDir = AppDomain.CurrentDomain.BaseDirectory;
            var overlayPath = Path.IsPathRooted(_currentTemplate.TemplateOverlay)
                            ? _currentTemplate.TemplateOverlay
                            : Path.Combine(appDir, _currentTemplate.TemplateOverlay);
            if (!File.Exists(overlayPath)) return;

            try
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.UriSource = new Uri(overlayPath, UriKind.Absolute);
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.EndInit();
                bmp.Freeze();

                var formatted = new FormatConvertedBitmap(bmp, PixelFormats.Bgra32, null, 0);
                int ow = formatted.PixelWidth;
                int oh = formatted.PixelHeight;
                int oStride = ((ow * 32 + 31) / 32) * 4;
                byte[] opx = new byte[oStride * oh];
                formatted.CopyPixels(opx, oStride, 0);

                int tw = _outputTexture.PixelWidth;
                int th = _outputTexture.PixelHeight;
                int tStride = _outputTexture.BackBufferStride;

                _outputTexture.Lock();
                byte[] tpx = new byte[tStride * th];
                System.Runtime.InteropServices.Marshal.Copy(_outputTexture.BackBuffer, tpx, 0, tpx.Length);

                for (int ty = 0; ty < th; ty++)
                {
                    int oy = Math.Max(0, Math.Min(oh - 1, (int)((float)ty / th * oh)));
                    for (int tx = 0; tx < tw; tx++)
                    {
                        int ox = Math.Max(0, Math.Min(ow - 1, (int)((float)tx / tw * ow)));
                        int oIdx = oy * oStride + ox * 4;
                        byte oA = opx[oIdx + 3];
                        if (oA == 0) continue;

                        float alpha = oA / 255f;
                        float invAlpha = 1f - alpha;
                        int tIdx = ty * tStride + tx * 4;

                        tpx[tIdx] = (byte)(opx[oIdx] * alpha + tpx[tIdx] * invAlpha);
                        tpx[tIdx + 1] = (byte)(opx[oIdx + 1] * alpha + tpx[tIdx + 1] * invAlpha);
                        tpx[tIdx + 2] = (byte)(opx[oIdx + 2] * alpha + tpx[tIdx + 2] * invAlpha);
                        tpx[tIdx + 3] = 255;
                    }
                }

                System.Runtime.InteropServices.Marshal.Copy(tpx, 0, _outputTexture.BackBuffer, tpx.Length);
                _outputTexture.AddDirtyRect(new Int32Rect(0, 0, tw, th));
                _outputTexture.Unlock();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to apply template overlay:\n{ex.Message}", "Warning",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        // ── Texture to 3D ─────────────────────────────────────────────────────

        private void ApplyOutputTextureTo3D()
        {
            if (_outputTexture == null || _scene?.Root == null) return;
            _textureStream ??= new MemoryStream();
            _textureStream.SetLength(0);
            _textureStream.Position = 0;
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(_outputTexture));
            encoder.Save(_textureStream);
            _textureStream.Position = 0;
            ApplyTextureToAllMeshes(new TextureModel(_textureStream, false));
        }

        private void ApplyTextureToAllMeshes(TextureModel tex)
        {
            if (_scene?.Root == null) return;
            foreach (var mesh in _scene.Root.Traverse().OfType<MeshNode>())
            {
                switch (mesh.Material)
                {
                    case DiffuseMaterialCore d: d.DiffuseMap = tex; d.DiffuseColor = new Color4(1, 1, 1, 1); break;
                    case PBRMaterialCore p: p.AlbedoMap = tex; p.AlbedoColor = new Color4(1, 1, 1, 1); break;
                    case PhongMaterialCore p: p.DiffuseMap = tex; p.DiffuseColor = new Color4(1, 1, 1, 1); break;
                }
                mesh.IsTransparent = false;
            }
        }

        // ── Export ────────────────────────────────────────────────────────────

        private void Export_Click(object sender, RoutedEventArgs e)
        {
            if (_outputTexture == null)
            {
                MessageBox.Show("Bake the helmet first.", "Export", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            var dlg = new SaveFileDialog { Title = "Export Helmet PNG", Filter = "PNG Image|*.png", FileName = "helmet.png" };
            if (dlg.ShowDialog() != true) return;
            try
            {
                using var stream = File.Create(dlg.FileName);
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(_outputTexture));
                encoder.Save(stream);
                MessageBox.Show($"Exported:\n{dlg.FileName}", "Export Complete", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Export failed:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ── UI helpers ────────────────────────────────────────────────────────

        private void RefreshBakeButton()
        {
            bool hasHelmet = _scene?.Root != null;
            bool hasTexture = _zoneLeftPath != null || _zoneRightPath != null
                           || _zoneFrontPath != null || _zoneBackPath != null || _zoneTopPath != null;
            BakeButton.IsEnabled = hasHelmet && hasTexture;
            BakeStatusLabel.Text = hasHelmet && hasTexture ? "Ready to bake."
                                 : hasHelmet ? "Load at least one zone texture."
                                                           : "Load a helmet OBJ first.";
        }
    }

    public class HelmetTemplate
    {
        [JsonPropertyName("Name")]
        public string Name { get; set; }

        [JsonPropertyName("3DObjectFilePath")]
        public string ObjectFilePath { get; set; }

        [JsonPropertyName("ExportWidth")]
        public int ExportWidth { get; set; } = 2048;

        [JsonPropertyName("ExportHeight")]
        public int ExportHeight { get; set; } = 2048;

        [JsonPropertyName("TemplateOverlay")]
        public string TemplateOverlay { get; set; }

        /// <summary>
        /// Degrees to rotate the projection V axis (vertical) around the Z axis.
        /// Use this to compensate for helmets whose geometry has a baked X rotation.
        /// Positive values tilt stripes clockwise, negative counter-clockwise.
        /// </summary>
        [JsonPropertyName("RotationCorrection")]
        public float RotationCorrection { get; set; } = 0f;

        /// <summary>
        /// Degrees to rotate the projection axes for Front/Back/Top zones.
        /// Usually needs a different value than RotationCorrection.
        /// </summary>
        [JsonPropertyName("FrontBackRotationCorrection")]
        public float FrontBackRotationCorrection { get; set; } = 0f;

        [JsonIgnore]
        public string SourceFile { get; set; }

        public override string ToString() => Name ?? Path.GetFileNameWithoutExtension(SourceFile ?? "");
    }
}