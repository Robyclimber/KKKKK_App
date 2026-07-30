using RuoteLab.Models;

namespace RuoteLab.Services;

public interface IPanelImageAlignmentService
{
    Task<PanelImageAlignmentSuggestion?> SuggestAlignmentAsync(PanelDefinition panel, CancellationToken cancellationToken = default);
}
