using Microsoft.Maui.Controls.Shapes;
using RuoteLab.Models;
using RuoteLab.Services;
using RuoteLab.ViewModels;

namespace RuoteLab;

public partial class HoldAnalysisPage : ContentPage
{
    private const int HoleBatchSize = 24;
    private readonly IHoldAnalysisSuggestionService suggestionService;
    private readonly IWallConfigurationStorageService storageService;
    private readonly WallDefinition wall;
    private IReadOnlyList<WallHoleDefinition> orderedHoles = Array.Empty<WallHoleDefinition>();
    private int renderedHoleCount;
    private bool isApplyingSuggestions;

    public HoldAnalysisPage(GymSetupViewModel viewModel, IWallConfigurationStorageService storageService, WallDefinition wall)
    {
        InitializeComponent();
        suggestionService = ((App)Application.Current!).HoldAnalysisSuggestionService;
        this.storageService = storageService;
        this.wall = wall;
        orderedHoles = wall.GetOrderedHoles();

        PageTitleLabel.Text = $"Analisi prese - {wall.Name}";
        PageSubtitleLabel.Text = $"Fori totali: {orderedHoles.Count} - Sala {wall.RoomName}";
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
        var holdSwitch = new Switch
        {
            IsToggled = hole.HasHold,
            OnColor = Color.FromArgb("#F2C94C"),
            ThumbColor = hole.HasHold ? Color.FromArgb("#14110B") : Color.FromArgb("#B9AA79")
        };

        var sizePicker = new Picker
        {
            Title = "Taglia",
            ItemsSource = Enum.GetValues<HoldSize>()
                .Select(value => WallHoleDefinition.GetHoldSizeLabel(value))
                .ToList(),
            SelectedIndex = (int)hole.HoldSize,
            IsEnabled = hole.HasHold
        };

        var typePicker = new Picker
        {
            Title = "Tipo presa",
            ItemsSource = Enum.GetValues<HoldType>()
                .Select(value => WallHoleDefinition.GetHoldTypeLabel(value))
                .ToList(),
            SelectedIndex = (int)hole.HoldType,
            IsEnabled = hole.HasHold
        };

        var suggestion = suggestionService.Suggest(wall, hole);
        var suggestionLabel = new Label
        {
            Text = $"Suggerito: {(suggestion.HasHold ? $"{WallHoleDefinition.GetHoldSizeLabel(suggestion.HoldSize)} - {WallHoleDefinition.GetHoldTypeLabel(suggestion.HoldType)}" : "Foro vuoto")} | {suggestion.Reason}",
            FontSize = 11,
            TextColor = Color.FromArgb("#B9AA79")
        };

        var metadataLabel = new Label
        {
            Text = hole.HasHold
                ? $"Stato presa: {hole.HoldSummary}"
                : "Stato presa: Foro vuoto",
            FontSize = 11,
            TextColor = hole.HasEstimatedHoldMetadata ? Color.FromArgb("#F2C94C") : Color.FromArgb("#7ED6A1")
        };

        var applySuggestionButton = new Button
        {
            Text = "Applica suggerimento",
            FontSize = 12
        };

        applySuggestionButton.Clicked += (_, _) =>
        {
            ApplySuggestion(hole.Number, suggestion, holdSwitch, sizePicker, typePicker);
        };

        holdSwitch.Toggled += (_, args) =>
        {
            if (args.Value)
            {
                wall.SetHoleHold(hole.Number, (HoldSize)Math.Max(0, sizePicker.SelectedIndex), (HoldType)Math.Max(0, typePicker.SelectedIndex));
            }
            else
            {
                wall.ClearHoleHold(hole.Number);
            }

            sizePicker.IsEnabled = args.Value;
            typePicker.IsEnabled = args.Value;
        };

        sizePicker.SelectedIndexChanged += (_, _) =>
        {
            if (!holdSwitch.IsToggled || sizePicker.SelectedIndex < 0)
            {
                return;
            }

            wall.SetHoleHold(hole.Number, (HoldSize)sizePicker.SelectedIndex, (HoldType)Math.Max(0, typePicker.SelectedIndex));
        };

        typePicker.SelectedIndexChanged += (_, _) =>
        {
            if (!holdSwitch.IsToggled || typePicker.SelectedIndex < 0)
            {
                return;
            }

            wall.SetHoleHold(hole.Number, (HoldSize)Math.Max(0, sizePicker.SelectedIndex), (HoldType)typePicker.SelectedIndex);
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
                applySuggestionButton,
                sizePicker,
                typePicker
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

    private async void OnSaveClicked(object? sender, EventArgs e)
    {
        try
        {
            var result = await storageService.SaveAsync(wall);
            await DisplayAlertAsync("Analisi prese", $"Prese salvate.\n{result}", "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Analisi prese", $"Errore salvataggio prese: {ex.Message}", "OK");
        }
    }

    private async void OnApplySuggestionsClicked(object? sender, EventArgs e)
    {
        if (isApplyingSuggestions)
        {
            return;
        }

        isApplyingSuggestions = true;
        if (sender is Button button)
        {
            button.IsEnabled = false;
        }

        try
        {
            var processed = 0;
            foreach (var hole in orderedHoles.Where(hole => !hole.HasHold))
            {
                var suggestion = suggestionService.Suggest(wall, hole);
                if (suggestion.HasHold)
                {
                    wall.SetHoleHold(hole.Number, suggestion.HoldSize, suggestion.HoldType);
                }

                processed++;
                if (processed % 8 == 0)
                {
                    RenderProgressLabel.Text = $"Analisi in corso: {processed}/{orderedHoles.Count}";
                    await Task.Yield();
                }
            }

            orderedHoles = wall.GetOrderedHoles();
            RebuildHoleCards(reset: true);
        }
        finally
        {
            isApplyingSuggestions = false;
            if (sender is Button buttonToEnable)
            {
                buttonToEnable.IsEnabled = true;
            }
        }
    }

    private void ApplySuggestion(int holeNumber, HoldSuggestion suggestion, Switch holdSwitch, Picker sizePicker, Picker typePicker)
    {
        if (!suggestion.HasHold)
        {
            wall.ClearHoleHold(holeNumber);
            holdSwitch.IsToggled = false;
            return;
        }

        wall.SetHoleHold(holeNumber, suggestion.HoldSize, suggestion.HoldType);
        holdSwitch.IsToggled = true;
        sizePicker.SelectedIndex = (int)suggestion.HoldSize;
        typePicker.SelectedIndex = (int)suggestion.HoldType;
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
        LoadMoreButton.IsEnabled = !isApplyingSuggestions;
    }
}
