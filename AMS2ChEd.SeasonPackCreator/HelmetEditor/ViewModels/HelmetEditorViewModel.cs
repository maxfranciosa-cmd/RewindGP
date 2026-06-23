using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using AMS2ChEd.SeasonPackEditor.HelmetEditor.Compositing;
using AMS2ChEd.SeasonPackEditor.HelmetEditor.Models;

namespace AMS2ChEd.SeasonPackEditor.HelmetEditor.ViewModels
{
    public class HelmetEditorViewModel : INotifyPropertyChanged
    {
        // ─── Private state ────────────────────────────────────────────────────

        private readonly HelmetCompositor _compositor = new();

        // The three files the user picks manually
        private string? _colorsTexturePath;
        private string? _stickersTemplatePath;
        private string? _manifestPath;

        // Runtime data derived from loaded files
        private HelmetEraManifest? _manifest;
        private ColorZoneState? _selectedZone;
        private StickerSlotState? _selectedSlot;
        private BitmapSource? _wireframeBitmap;
        private BitmapSource? _previewBitmap;
        private bool _showPreview;
        private Color _pickerColor = Colors.White;

        // Current color assignments (KeyColor.ToString() → hex)
        private readonly Dictionary<string, string> _zoneColors = new();
        // Current sticker assignments (KeyColor.ToString() → absolute path)
        private readonly Dictionary<string, string> _stickerPaths = new();

        // ─── Constructor ─────────────────────────────────────────────────────

        public HelmetEditorViewModel()
        {
            PickColorsTextureCommand = new RelayCommand(PickColorsTexture);
            PickStickersTemplateCommand = new RelayCommand(PickStickersTemplate);
            PickManifestCommand = new RelayCommand(PickManifest);
            ApplyColorCommand = new RelayCommand(ApplyColor,
                                             () => _selectedZone != null);
            ResetZoneCommand = new RelayCommand(ResetZone,
                                             () => _selectedZone != null);
            ResetAllColorsCommand = new RelayCommand(ResetAllColors);
            AssignStickerCommand = new RelayCommand(AssignSticker,
                                             () => _selectedSlot != null);
            ClearStickerCommand = new RelayCommand(ClearSticker,
                                             () => _selectedSlot?.HasSticker == true);
            TogglePreviewCommand = new RelayCommand(TogglePreview,
                                             () => IsReady);
            ExportPngCommand = new RelayCommand(ExportPng,
                                             () => IsReady);
        }

        // ─── File path properties (shown in the UI) ───────────────────────────

        public string ColorsTexturePath
        {
            get => _colorsTexturePath ?? "(not selected)";
            private set { _colorsTexturePath = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsReady)); }
        }

        public string StickersTemplatePath
        {
            get => _stickersTemplatePath ?? "(not selected)";
            private set { _stickersTemplatePath = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsReady)); }
        }

        public string ManifestPath
        {
            get => _manifestPath ?? "(not selected)";
            private set { _manifestPath = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsReady)); }
        }

        /// <summary>True once the colors texture and manifest are both loaded.</summary>
        public bool IsReady => _manifest != null && _colorsTexturePath != null;

        // ─── Observable Collections ───────────────────────────────────────────

        public ObservableCollection<ColorZoneState> ColorZones { get; } = new();
        public ObservableCollection<StickerSlotState> StickerSlots { get; } = new();

        // ─── Selection ────────────────────────────────────────────────────────

        public ColorZoneState? SelectedZone
        {
            get => _selectedZone;
            set
            {
                if (_selectedZone != null) _selectedZone.IsSelected = false;
                _selectedZone = value;
                if (_selectedZone != null)
                {
                    _selectedZone.IsSelected = true;
                    PickerColor = _selectedZone.CurrentColor;
                }
                _selectedSlot = null;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SelectedLabel));
                OnPropertyChanged(nameof(HasZoneSelected));
                OnPropertyChanged(nameof(HasSlotSelected));
                RaiseCanExecuteChanged();
            }
        }

        public StickerSlotState? SelectedSlot
        {
            get => _selectedSlot;
            set
            {
                if (_selectedSlot != null) _selectedSlot.IsSelected = false;
                _selectedSlot = value;
                if (_selectedSlot != null) _selectedSlot.IsSelected = true;
                _selectedZone = null;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SelectedLabel));
                OnPropertyChanged(nameof(HasZoneSelected));
                OnPropertyChanged(nameof(HasSlotSelected));
                OnPropertyChanged(nameof(SelectedStickerPath));
                RaiseCanExecuteChanged();
            }
        }

        public string SelectedLabel =>
            _selectedZone != null ? $"Color zone: {_selectedZone.DisplayName}" :
            _selectedSlot != null ? $"Sticker slot: {_selectedSlot.Definition.DisplayName}" :
            "Select a color zone or sticker slot on the left";

        public bool HasZoneSelected => _selectedZone != null;
        public bool HasSlotSelected => _selectedSlot != null;

        public string? SelectedStickerPath => _selectedSlot?.AssignedImagePath;

        // ─── Canvas bitmaps ───────────────────────────────────────────────────

        public BitmapSource? WireframeBitmap
        {
            get => _wireframeBitmap;
            private set { _wireframeBitmap = value; OnPropertyChanged(); }
        }

        public BitmapSource? PreviewBitmap
        {
            get => _previewBitmap;
            private set { _previewBitmap = value; OnPropertyChanged(); }
        }

        public bool ShowPreview
        {
            get => _showPreview;
            private set { _showPreview = value; OnPropertyChanged(); }
        }

        // ─── Color picker ─────────────────────────────────────────────────────

        public Color PickerColor
        {
            get => _pickerColor;
            set
            {
                _pickerColor = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(PickerColorHex));
            }
        }

        public string PickerColorHex
        {
            get => $"#{_pickerColor.A:X2}{_pickerColor.R:X2}{_pickerColor.G:X2}{_pickerColor.B:X2}";
            set
            {
                try
                {
                    var c = (Color)ColorConverter.ConvertFromString(value);
                    _pickerColor = c;
                    OnPropertyChanged(nameof(PickerColor));
                    OnPropertyChanged();
                }
                catch { }
            }
        }

        // ─── Commands ─────────────────────────────────────────────────────────

        public ICommand PickColorsTextureCommand;
        public ICommand PickStickersTemplateCommand;
        public ICommand PickManifestCommand;
        public ICommand ApplyColorCommand;
        public ICommand ResetZoneCommand;
        public ICommand ResetAllColorsCommand;
        public ICommand AssignStickerCommand;
        public ICommand ClearStickerCommand;
        public ICommand TogglePreviewCommand;
        public ICommand ExportPngCommand;

        // ─── File pickers ─────────────────────────────────────────────────────

        private void PickColorsTexture()
        {
            var dlg = new OpenFileDialog
            {
                Title = "Select Colors Texture",
                Filter = "PNG Images|*.png|All Images|*.png;*.bmp"
            };
            if (dlg.ShowDialog() != true) return;
            _colorsTexturePath = dlg.FileName;
            ColorsTexturePath = dlg.FileName;
            TryPopulateColorZones();
            RefreshPreviewIfVisible();
        }

        private void PickStickersTemplate()
        {
            var dlg = new OpenFileDialog
            {
                Title = "Select Stickers Template",
                Filter = "PNG Images|*.png|All Images|*.png;*.bmp"
            };
            if (dlg.ShowDialog() != true) return;
            _stickersTemplatePath = dlg.FileName;
            StickersTemplatePath = dlg.FileName;
            RefreshPreviewIfVisible();
        }

        private void PickManifest()
        {
            var dlg = new OpenFileDialog
            {
                Title = "Select Manifest JSON",
                Filter = "JSON files|*.json"
            };
            if (dlg.ShowDialog() != true) return;

            try
            {
                var opts = new JsonSerializerOptions
                {
                    Converters = { new JsonStringEnumConverter() }
                };
                _manifest = JsonSerializer.Deserialize<HelmetEraManifest>(
                    File.ReadAllText(dlg.FileName), opts);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load manifest:\n{ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            _manifestPath = dlg.FileName;
            ManifestPath = dlg.FileName;

            PopulateStickerSlotsFromManifest();
            TryPopulateColorZones();
            RaiseCanExecuteChanged();
            RefreshPreviewIfVisible();
        }

        // ─── Color zone commands ──────────────────────────────────────────────

        private void ApplyColor()
        {
            if (_selectedZone == null) return;
            _selectedZone.CurrentColor = PickerColor;
            _zoneColors[_selectedZone.Key.ToString()] =
                $"#{PickerColor.A:X2}{PickerColor.R:X2}{PickerColor.G:X2}{PickerColor.B:X2}";
            RefreshPreviewIfVisible();
            OnPropertyChanged(nameof(ColorZones));
        }

        private void ResetZone()
        {
            if (_selectedZone == null) return;
            var defaultColor = KeyColorValues.ToWpfColor(_selectedZone.Key);
            _selectedZone.CurrentColor = defaultColor;
            PickerColor = defaultColor;
            _zoneColors.Remove(_selectedZone.Key.ToString());
            RefreshPreviewIfVisible();
            OnPropertyChanged(nameof(ColorZones));
        }

        private void ResetAllColors()
        {
            foreach (var z in ColorZones)
                z.CurrentColor = KeyColorValues.ToWpfColor(z.Key);
            _zoneColors.Clear();
            if (_selectedZone != null) PickerColor = _selectedZone.CurrentColor;
            RefreshPreviewIfVisible();
            OnPropertyChanged(nameof(ColorZones));
        }

        // ─── Sticker commands ─────────────────────────────────────────────────

        private void AssignSticker()
        {
            if (_selectedSlot == null) return;
            var title = $"Choose sticker for {_selectedSlot?.Definition?.DisplayName}";
            var dlg = new OpenFileDialog
            {
                Title = title,
                Filter = "PNG Images|*.png|All Images|*.png;*.jpg;*.bmp"
            };
            if (dlg.ShowDialog() != true) return;
            _selectedSlot.AssignedImagePath = dlg.FileName;
            _stickerPaths[_selectedSlot.Definition.Color.ToString()] = dlg.FileName;
            OnPropertyChanged(nameof(SelectedStickerPath));
            OnPropertyChanged(nameof(StickerSlots));
            RaiseCanExecuteChanged();
            RefreshPreviewIfVisible();
        }

        private void ClearSticker()
        {
            if (_selectedSlot == null) return;
            _selectedSlot.AssignedImagePath = null;
            _stickerPaths.Remove(_selectedSlot.Definition.Color.ToString());
            OnPropertyChanged(nameof(SelectedStickerPath));
            OnPropertyChanged(nameof(StickerSlots));
            RaiseCanExecuteChanged();
            RefreshPreviewIfVisible();
        }

        // ─── Preview & Export ─────────────────────────────────────────────────

        private void TogglePreview()
        {
            if (!ShowPreview) { RegeneratePreview(); ShowPreview = true; }
            else { PreviewBitmap = null; ShowPreview = false; }
        }

        private void ExportPng()
        {
            if (_manifest == null || _colorsTexturePath == null) return;

            var dlg = new SaveFileDialog
            {
                Title = "Export Helmet PNG",
                Filter = "PNG Image|*.png",
                FileName = "helmet.png"
            };
            if (dlg.ShowDialog() != true) return;

            try
            {
                var definition = BuildTransientDefinition();
                // The compositor resolves texture paths from the manifest folder.
                // Since the user picked files individually, we override them directly.
                var runtimeManifest = BuildRuntimeManifest();
                string folder = Path.GetDirectoryName(_colorsTexturePath)!;
                _compositor.SaveAsPng(runtimeManifest, definition, folder, dlg.FileName);
                MessageBox.Show($"Exported:\n{dlg.FileName}",
                    "Export Complete", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Export failed:\n{ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ─── Population helpers ───────────────────────────────────────────────

        private void TryPopulateColorZones()
        {
            ColorZones.Clear();
            SelectedZone = null;

            if (_colorsTexturePath == null || !File.Exists(_colorsTexturePath)) return;

            byte tol = _manifest?.KeyColorTolerance ?? 12;

            var src = new BitmapImage();
            src.BeginInit();
            src.UriSource = new Uri(_colorsTexturePath, UriKind.Absolute);
            src.CacheOption = BitmapCacheOption.OnLoad;
            src.EndInit();
            src.Freeze();

            var conv = new FormatConvertedBitmap(src, PixelFormats.Pbgra32, null, 0);
            int w = conv.PixelWidth, h = conv.PixelHeight;
            byte[] pixels = new byte[h * w * 4];
            conv.CopyPixels(pixels, w * 4, 0);

            // Sample every 8th pixel to quickly find which key colors are present
            var found = new HashSet<KeyColor>();
            for (int i = 0; i < pixels.Length; i += 4 * 8)
            {
                byte b = pixels[i], g = pixels[i + 1], r = pixels[i + 2], a = pixels[i + 3];
                if (a == 0) continue;
                var kc = KeyColorValues.Identify(r, g, b, tol);
                if (kc != null) found.Add(kc.Value);
            }

            foreach (var key in found.OrderBy(k => (int)k))
            {
                var initColor = KeyColorValues.ToWpfColor(key);
                if (_zoneColors.TryGetValue(key.ToString(), out var saved))
                    try { initColor = (Color)ColorConverter.ConvertFromString(saved); } catch { }

                ColorZones.Add(new ColorZoneState(key, key.ToString(), initColor));
            }
        }

        private void PopulateStickerSlotsFromManifest()
        {
            StickerSlots.Clear();
            SelectedSlot = null;
            if (_manifest == null) return;
            foreach (var slotDef in _manifest.StickerSlots)
            {
                var state = new StickerSlotState(slotDef);
                if (_stickerPaths.TryGetValue(slotDef.Color.ToString(), out var path))
                    state.AssignedImagePath = path;
                StickerSlots.Add(state);
            }
        }

        // ─── Runtime manifest + definition ────────────────────────────────────
        // The compositor expects a manifest with relative paths and a folder.
        // Since the user picked absolute paths individually, we build a transient
        // manifest pointing at absolute paths and use "" as the folder prefix.

        private HelmetEraManifest BuildRuntimeManifest()
        {
            var m = _manifest != null
                ? new HelmetEraManifest
                {
                    ColorsTexturePath = _colorsTexturePath!,
                    StickersTemplatePath = _stickersTemplatePath ?? string.Empty,
                    WireframePath = string.Empty,
                    TextureWidth = _manifest.TextureWidth,
                    TextureHeight = _manifest.TextureHeight,
                    KeyColorTolerance = _manifest.KeyColorTolerance,
                    StickerSlots = _manifest.StickerSlots
                }
                : new HelmetEraManifest
                {
                    ColorsTexturePath = _colorsTexturePath!,
                    StickersTemplatePath = _stickersTemplatePath ?? string.Empty,
                    WireframePath = string.Empty,
                };
            return m;
        }

        private HelmetCompositeInput BuildTransientDefinition() =>
            new HelmetCompositeInput(_zoneColors, _stickerPaths);

        // ─── Preview regeneration ─────────────────────────────────────────────

        private void RegeneratePreview()
        {
            if (_manifest == null || _colorsTexturePath == null) return;
            try
            {
                var runtimeManifest = BuildRuntimeManifest();
                var definition = BuildTransientDefinition();
                string folder = Path.GetDirectoryName(_colorsTexturePath)!;
                PreviewBitmap = _compositor.Composite(runtimeManifest, definition, folder);
            }
            catch { PreviewBitmap = null; }
        }

        private void RefreshPreviewIfVisible()
        {
            if (ShowPreview) RegeneratePreview();
            RaiseCanExecuteChanged();
        }

        // ─── Helpers ──────────────────────────────────────────────────────────

        private void RaiseCanExecuteChanged()
        {
            (ApplyColorCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (ResetZoneCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (AssignStickerCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (ClearStickerCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (TogglePreviewCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (ExportPngCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }

        // ─── INotifyPropertyChanged ───────────────────────────────────────────

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    // ─── Commands ─────────────────────────────────────────────────────────────

    public class RelayCommand : ICommand
    {
        private readonly Action _execute;
        private readonly Func<bool>? _canExecute;
        public RelayCommand(Action execute, Func<bool>? canExecute = null)
        { _execute = execute; _canExecute = canExecute; }
        public bool CanExecute(object? p) => _canExecute?.Invoke() ?? true;
        public void Execute(object? p) => _execute();
        public event EventHandler? CanExecuteChanged;
        public void RaiseCanExecuteChanged() =>
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }

    public class RelayCommand<T> : ICommand
    {
        private readonly Action<T> _execute;
        private readonly Func<T, bool>? _canExecute;
        public RelayCommand(Action<T> execute, Func<T, bool>? canExecute = null)
        { _execute = execute; _canExecute = canExecute; }
        public bool CanExecute(object? p) =>
            p is T t ? (_canExecute?.Invoke(t) ?? true) : true;
        public void Execute(object? p) { if (p is T t) _execute(t); }
        public event EventHandler? CanExecuteChanged;
        public void RaiseCanExecuteChanged() =>
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}