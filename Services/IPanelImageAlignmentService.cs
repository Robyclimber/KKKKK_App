using RouteLab.Models;

namespace RouteLab.Services;

public interface IPanelImageAlignmentService
{
    Task<PanelImageAlignmentSuggestion?> SuggestAlignmentAsync(PanelDefinition panel, CancellationToken cancellationToken = default);
}

