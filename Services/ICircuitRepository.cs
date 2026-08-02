using RouteLab.Models;

namespace RouteLab.Services;

public interface ICircuitRepository
{
    Task<IReadOnlyList<CircuitDefinition>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<int> SaveAsync(CircuitDefinition circuit, CancellationToken cancellationToken = default);

    Task DeleteAsync(int circuitId, CancellationToken cancellationToken = default);
}

