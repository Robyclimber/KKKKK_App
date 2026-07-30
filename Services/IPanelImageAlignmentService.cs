using WallPanelPlanner.Models;

namespace WallPanelPlanner.Services;

public interface IPanelImageAlignmentService
{
    Task<PanelImageAlignmentSuggestion?> SuggestAlignmentAsync(PanelDefinition panel, CancellationToken cancellationToken = default);
}
