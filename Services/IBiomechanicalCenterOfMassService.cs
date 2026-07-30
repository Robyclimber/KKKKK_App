using RouteLab.Models;

namespace RouteLab.Services;

public interface IBiomechanicalCenterOfMassService
{
    BiomechanicalCenterOfMassResult Estimate(BiomechanicalPoseRequest request);
}
