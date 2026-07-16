using System.Globalization;
using RuoteLab.Models;
using RuoteLab.ViewModels;

namespace RuoteLab.Services;

public sealed class GymSetupEditorStateService : IGymSetupEditorStateService
{
    public WallEditorState BuildWallEditor(GymSetupViewModel viewModel, bool useSelectedWallValues)
    {
        ArgumentNullException.ThrowIfNull(viewModel);

        if (useSelectedWallValues && viewModel.SelectedWall is not null)
        {
            var wall = viewModel.SelectedWall;
            return new WallEditorState
            {
                ModeText = $"Stai modificando la parete: {wall.Name}",
                RoomNameText = viewModel.SelectedRoom?.Name ?? viewModel.SuggestedNextRoomName,
                WallNameText = wall.Name,
                WallWidthText = ToEditorText(wall.Width),
                WallHeightText = ToEditorText(wall.Height)
            };
        }

        return new WallEditorState
        {
            ModeText = "Stai creando una nuova parete",
            RoomNameText = viewModel.SuggestedNextRoomName,
            WallNameText = viewModel.SuggestedNextWallName
        };
    }

    public PanelEditorState BuildPanelEditor(GymSetupViewModel viewModel, bool useSelectedPanelValues)
    {
        ArgumentNullException.ThrowIfNull(viewModel);

        if (useSelectedPanelValues && viewModel.SelectedPanel is not null)
        {
            var panel = viewModel.SelectedPanel;
            return new PanelEditorState
            {
                ModeText = $"Stai modificando il pannello: {panel.Name}",
                PanelNameText = panel.Name,
                PanelXText = ToEditorText(panel.X),
                PanelYText = ToEditorText(panel.Y),
                PanelWidthText = ToEditorText(panel.Width),
                PanelHeightText = ToEditorText(panel.Height),
                HoleOffsetText = ToEditorText(panel.EdgeOffsetX),
                HoleOffsetYText = ToEditorText(panel.EdgeOffsetY),
                HoleHorizontalText = ToEditorText(panel.HorizontalSpacing),
                HoleVerticalText = ToEditorText(panel.VerticalSpacing),
                LedRoutingAxis = panel.LedRoutingAxis,
                LedStartDirection = panel.LedStartDirection
            };
        }

        return new PanelEditorState
        {
            ModeText = "Stai creando un nuovo pannello",
            PanelNameText = viewModel.SuggestedNextPanelName,
            LedRoutingAxis = LedRoutingAxis.Vertical,
            LedStartDirection = LedStartDirection.BottomToTop
        };
    }

    private static string ToEditorText(double value)
    {
        return value.ToString("0.##", CultureInfo.InvariantCulture);
    }
}
