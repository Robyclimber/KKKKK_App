using Microsoft.Maui.Controls.Shapes;
using RouteLab.Models;
using RouteLab.Services;

namespace RouteLab;

public partial class HoldAnalysisPage : ContentPage
{
    private sealed class HoldEditor
    {
        public required int HoleNumber { get; init; }

        public required Switch HoldSwitch { get; init; }

        public required Picker SizePicker { get; init; }

        public required Picker TypePicker { get; init; }

        public required Label MetadataLabel { get; init; }

        public required Label SuggestionLabel { get; init; }

        public required Button SaveButton { get; init; }

        public required Label SaveStatusLabel { get; init; }
    }

    private const int HoleBatchSize = 24;
    private readonly IHoldAnalysisSuggestionService suggestionService;
    private readonly IWallConfigurationStorageService storageService;
    private readonly WallDefinition wall;
    private readonly PanelDefinition panel;
    private IReadOnlyList<WallHoleDefinition> orderedHoles = Array.Empty<WallHoleDefinition>();
    private int renderedHoleCount;

    public HoldAnalysisPage(
        IWallConfigurationStorageService storageService,
        WallDefinition wall,
        PanelDefinition panel)
    {
        InitializeComponent();
        suggestionService = ((App)Application.Current!).HoldAnalysisSuggestionService;
        this.storageService = storageService;
        this.wall = wall;
        this.panel = panel;
        orderedHoles = GetPanelHoles();

        PageTitleLabel.Text = $"Analisi prese - {panel.Name}";
        PageSubtitleLabel.Text = $"{wall.RoomName} / {wall.Name} - Fori del pannello: {orderedHoles.Count}";
        RebuildHoleCards(reset: true);
    }

    private void RebuildHoleCards(bool reset)
    {
        if (reset)
        {
            renderedHoleCount = 0;
            HolesHost.Children.Clear();
        }

        var holesToRender = orderedHoles
            .Skip(renderedHoleCount)
            .Take(HoleBatchSize)
            .ToList();

        foreach (var hole in holesToRender)
        {
            HolesHost.Children.Add(CreateHoleCard(hole));
        }

        renderedHoleCount += holesToRender.Count;
        UpdateRenderProgress();
    }

    private View CreateHoleCard(WallHoleDefinition hole)
    {
        var suggestion = hole.HasEstimatedHoldMetadata
            ? suggestionService.Suggest(wall, hole)
            : null;
        var displayedHasHold = suggestion?.HasHold ?? hole.HasHold;
        var displayedHoldSize = suggestion?.HoldSize ?? hole.HoldSize;
        var displayedHoldType = suggestion?.HoldType ?? hole.HoldType;

        var holdSwitch = new Switch
        {
            IsToggled = displayedHasHold,
            OnColor = Color.FromArgb("#F2C94C"),
            ThumbColor = displayedHasHold ? Color.FromArgb("#14110B") : Color.FromArgb("#B9AA79")
        };

        var sizePicker = new Picker
        {
            Title = "Taglia",
            ItemsSource = Enum.GetValues<HoldSize>()
                .Select(value => WallHoleDefinition.GetHoldSizeLabel(value))
                .ToList(),
            SelectedIndex = (int)displayedHoldSize,
            IsEnabled = displayedHasHold
        };

        var typePicker = new Picker
        {
            Title = "Tipo presa",
            ItemsSource = Enum.GetValues<HoldType>()
                .Select(value => WallHoleDefinition.GetHoldTypeLabel(value))
                .ToList(),
            SelectedIndex = (int)displayedHoldType,
            IsEnabled = displayedHasHold
        };

        var suggestionLabel = new Label
        {
            Text = suggestion is null
                ? string.Empty
                : $"Suggerimento iniziale: {(suggestion.HasHold ? $"{WallHoleDefinition.GetHoldSizeLabel(suggestion.HoldSize)} - {WallHoleDefinition.GetHoldTypeLabel(suggestion.HoldType)}" : "Foro vuoto")} | {suggestion.Reason}",
            FontSize = 11,
            TextColor = Color.FromArgb("#B9AA79"),
            IsVisible = suggestion is not null
        };

        var metadataLabel = new Label
        {
            Text = displayedHasHold
                ? $"Stato presa: {WallHoleDefinition.GetHoldSizeLabel(displayedHoldSize)} - {WallHoleDefinition.GetHoldTypeLabel(displayedHoldType)}"
                : "Stato presa: Foro vuoto",
            FontSize = 11,
            TextColor = hole.HasEstimatedHoldMetadata ? Color.FromArgb("#F2C94C") : Color.FromArgb("#7ED6A1")
        };

        var saveHoldButton = new Button
        {
            Text = "Salva presa",
            FontSize = 12
        };
        var saveStatusLabel = new Label
        {
            Text = "Nessuna modifica da salvare.",
            FontSize = 11,
            TextColor = Color.FromArgb("#B9AA79")
        };

        var editor = new HoldEditor
        {
            HoleNumber = hole.Number,
            HoldSwitch = holdSwitch,
            SizePicker = sizePicker,
            TypePicker = typePicker,
            MetadataLabel = metadataLabel,
            SuggestionLabel = suggestionLabel,
            SaveButton = saveHoldButton,
            SaveStatusLabel = saveStatusLabel
        };

        saveHoldButton.Clicked += async (_, _) =>
        {
            await SaveHoldEditorAsync(editor);
        };

        holdSwitch.Toggled += (_, _) =>
        {
            ApplyHoldEditor(editor);
        };

        sizePicker.SelectedIndexChanged += (_, _) =>
        {
            if (!holdSwitch.IsToggled || sizePicker.SelectedIndex < 0)
            {
                return;
            }

            ApplyHoldEditor(editor);
        };

        typePicker.SelectedIndexChanged += (_, _) =>
        {
            if (!holdSwitch.IsToggled || typePicker.SelectedIndex < 0)
            {
                return;
            }

            ApplyHoldEditor(editor);
        };

        var content = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = 96 },
                new ColumnDefinition { Width = GridLength.Star }
            },
            ColumnSpacing = 14
        };

        var preview = CreateHolePreview(hole);
        if (preview is not null)
        {
            content.Add(preview);
        }

        content.Add(new VerticalStackLayout
        {
            Spacing = 6,
            Children =
            {
                new Label
                {
                    Text = $"Foro {hole.Number}",
                    FontSize = 16,
                    TextColor = Color.FromArgb("#F8E7A8")
                },
                new Label
                {
                    Text = $"{hole.PanelName} - X {hole.AbsoluteX:0.#} - Y {hole.AbsoluteY:0.#}",
                    FontSize = 12,
                    TextColor = Color.FromArgb("#D8A72D")
                },
                metadataLabel,
                suggestionLabel,
                new HorizontalStackLayout
                {
                    Spacing = 10,
                    Children =
                    {
                        new Label
                        {
                            Text = "Ha presa",
                            VerticalOptions = LayoutOptions.Center,
                            TextColor = Color.FromArgb("#B9AA79")
                        },
                        holdSwitch
                    }
                },
                sizePicker,
                typePicker,
                saveHoldButton,
                saveStatusLabel
            }
        }, 1);

        return new Border
        {
            Background = Color.FromArgb("#191611"),
            Stroke = Color.FromArgb("#B9922F"),
            StrokeShape = new RoundRectangle { CornerRadius = 12 },
            Padding = 12,
            Content = content
        };
    }

    private View? CreateHolePreview(WallHoleDefinition hole)
    {
        var panel = wall.FindPanel(hole);
        if (panel is null || string.IsNullOrWhiteSpace(panel.ImagePath) || !File.Exists(panel.ImagePath))
        {
            return new Border
            {
                WidthRequest = 92,
                HeightRequest = 92,
                Stroke = Color.FromArgb("#B9922F"),
                StrokeShape = new RoundRectangle { CornerRadius = 8 },
                Content = new Grid
                {
                    Children =
                    {
                        new Label
                        {
                            Text = "No img",
                            HorizontalOptions = LayoutOptions.Center,
                            VerticalOptions = LayoutOptions.Center,
                            TextColor = Color.FromArgb("#B9AA79")
                        }
                    }
                }
            };
        }

        var source = TryCreateHolePreviewSource(hole, 92d) ?? ImageSource.FromFile(panel.ImagePath);
        return new Border
        {
            WidthRequest = 92,
            HeightRequest = 92,
            Stroke = Color.FromArgb("#B9922F"),
            StrokeShape = new RoundRectangle { CornerRadius = 8 },
            Padding = 0,
            Content = new Grid
            {
                Clip = new RectangleGeometry(new Rect(0, 0, 92, 92)),
                Children =
                {
                    new Image
                    {
                        Source = source,
                        Aspect = Aspect.AspectFill,
                        WidthRequest = 92,
                        HeightRequest = 92
                    },
                    new BoxView
                    {
                        WidthRequest = 10,
                        HeightRequest = 10,
                        CornerRadius = 5,
                        Color = Color.FromArgb("#F2C94C"),
                        HorizontalOptions = LayoutOptions.Center,
                        VerticalOptions = LayoutOptions.Center
                    }
                }
            }
        };
    }

    private ImageSource? TryCreateHolePreviewSource(WallHoleDefinition hole, double previewSize)
    {
        var panel = wall.FindPanel(hole);
        if (panel is null || string.IsNullOrWhiteSpace(panel.ImagePath))
        {
            return null;
        }

        var pixelSize = TryGetImagePixelSize(panel.ImagePath);
        if (pixelSize is null)
        {
            return ImageSource.FromFile(panel.ImagePath);
        }

        var sourceWidth = Math.Max(1d, pixelSize.Value.Width);
        var sourceHeight = Math.Max(1d, pixelSize.Value.Height);
        var imageScale = Math.Max(0.2d, panel.ImageScale);
        var overlayWidth = Math.Max(1d, panel.Width * imageScale);
        var overlayHeight = Math.Max(1d, panel.Height * imageScale);
        var cropWidthPx = sourceWidth * panel.EffectiveImageCropWidthFactor;
        var cropHeightPx = sourceHeight * panel.EffectiveImageCropHeightFactor;
        var holeOverlayX = hole.RelativeX - panel.ImageOffsetX;
        var holeOverlayY = hole.RelativeY - panel.ImageOffsetY;
        var sourcePoint = panel.MapPanelPointToImageSource(holeOverlayX / overlayWidth, holeOverlayY / overlayHeight, sourceWidth, sourceHeight);
        var sourceHoleX = sourcePoint.X;
        var sourceHoleY = sourcePoint.Y;

#if ANDROID
        try
        {
            using var bitmap = Android.Graphics.BitmapFactory.DecodeFile(panel.ImagePath);
            if (bitmap is null)
            {
                return ImageSource.FromFile(panel.ImagePath);
            }

            const double cropWindowMillimeters = 220d;
            var cropScaleX = cropWidthPx / overlayWidth;
            var cropScaleY = cropHeightPx / overlayHeight;
            var cropSizePx = (int)Math.Round(cropWindowMillimeters * ((cropScaleX + cropScaleY) / 2d));
            cropSizePx = Math.Max(96, cropSizePx);
            cropSizePx = Math.Min(cropSizePx, Math.Min(bitmap.Width, bitmap.Height));

            var cropLeft = (int)Math.Round(sourceHoleX - (cropSizePx / 2d));
            var cropTop = (int)Math.Round(sourceHoleY - (cropSizePx / 2d));
            cropLeft = Math.Clamp(cropLeft, 0, Math.Max(0, bitmap.Width - cropSizePx));
            cropTop = Math.Clamp(cropTop, 0, Math.Max(0, bitmap.Height - cropSizePx));

            using var croppedBitmap = Android.Graphics.Bitmap.CreateBitmap(bitmap, cropLeft, cropTop, cropSizePx, cropSizePx);
            using var scaledBitmap = Android.Graphics.Bitmap.CreateScaledBitmap(croppedBitmap, (int)previewSize * 2, (int)previewSize * 2, true);
            using var stream = new MemoryStream();
            scaledBitmap.Compress(Android.Graphics.Bitmap.CompressFormat.Png!, 100, stream);
            var imageBytes = stream.ToArray();
            return ImageSource.FromStream(() => new MemoryStream(imageBytes));
        }
        catch
        {
            return ImageSource.FromFile(panel.ImagePath);
        }
#else
        return ImageSource.FromFile(panel.ImagePath);
#endif
    }

    private static Size? TryGetImagePixelSize(string imagePath)
    {
#if ANDROID
        try
        {
            var options = new Android.Graphics.BitmapFactory.Options
            {
                InJustDecodeBounds = true
            };

            Android.Graphics.BitmapFactory.DecodeFile(imagePath, options);
            if (options.OutWidth > 0 && options.OutHeight > 0)
            {
                return new Size(options.OutWidth, options.OutHeight);
            }
        }
        catch
        {
        }
#endif

        return null;
    }

    private void ApplyHoldEditor(HoldEditor editor)
    {
        editor.SuggestionLabel.IsVisible = false;
        editor.SizePicker.IsEnabled = editor.HoldSwitch.IsToggled;
        editor.TypePicker.IsEnabled = editor.HoldSwitch.IsToggled;
        editor.HoldSwitch.ThumbColor = editor.HoldSwitch.IsToggled
            ? Color.FromArgb("#14110B")
            : Color.FromArgb("#B9AA79");

        if (!editor.HoldSwitch.IsToggled)
        {
            wall.ClearHoleHold(editor.HoleNumber);
            editor.MetadataLabel.Text = "Stato presa: Foro vuoto";
            editor.MetadataLabel.TextColor = Color.FromArgb("#7ED6A1");
            MarkEditorAsModified(editor);
            return;
        }

        var holdSize = (HoldSize)Math.Clamp(
            editor.SizePicker.SelectedIndex,
            0,
            Enum.GetValues<HoldSize>().Length - 1);
        var holdType = (HoldType)Math.Clamp(
            editor.TypePicker.SelectedIndex,
            0,
            Enum.GetValues<HoldType>().Length - 1);

        wall.SetHoleHold(editor.HoleNumber, holdSize, holdType);
        editor.MetadataLabel.Text =
            $"Stato presa: {WallHoleDefinition.GetHoldSizeLabel(holdSize)} - {WallHoleDefinition.GetHoldTypeLabel(holdType)}";
        editor.MetadataLabel.TextColor = Color.FromArgb("#7ED6A1");
        MarkEditorAsModified(editor);
    }

    private async Task SaveHoldEditorAsync(HoldEditor editor)
    {
        using var busy = AppBusy.Show("Salvataggio presa...");
        editor.SaveButton.IsEnabled = false;
        try
        {
            ApplyHoldEditor(editor);
            var hole = wall.GetOrderedHoles()
                .FirstOrDefault(current => current.Number == editor.HoleNumber);
            if (hole.Number == 0)
            {
                throw new InvalidOperationException("Foro non trovato.");
            }

            await storageService.SaveHoleAsync(wall, hole);
            editor.SaveStatusLabel.Text = "Presa salvata.";
            editor.SaveStatusLabel.TextColor = Color.FromArgb("#7ED6A1");
            orderedHoles = GetPanelHoles();
        }
        catch (Exception ex)
        {
            editor.SaveStatusLabel.Text = "Salvataggio non riuscito.";
            editor.SaveStatusLabel.TextColor = Color.FromArgb("#F08A7E");
            await DisplayAlertAsync("Analisi prese", $"Errore salvataggio presa: {ex.Message}", "OK");
        }
        finally
        {
            editor.SaveButton.IsEnabled = true;
        }
    }

    private static void MarkEditorAsModified(HoldEditor editor)
    {
        editor.SaveStatusLabel.Text = "Modifiche da salvare.";
        editor.SaveStatusLabel.TextColor = Color.FromArgb("#F2C94C");
    }

    private void OnLoadMoreClicked(object? sender, EventArgs e)
    {
        if (renderedHoleCount >= orderedHoles.Count)
        {
            return;
        }

        RebuildHoleCards(reset: false);
    }

    private void UpdateRenderProgress()
    {
        RenderProgressLabel.Text = $"Fori caricati: {renderedHoleCount}/{orderedHoles.Count}";
        LoadMoreButton.IsVisible = renderedHoleCount < orderedHoles.Count;
        LoadMoreButton.IsEnabled = true;
    }

    private IReadOnlyList<WallHoleDefinition> GetPanelHoles()
    {
        return wall.GetOrderedHolesForPanel(panel.Name);
    }
}
