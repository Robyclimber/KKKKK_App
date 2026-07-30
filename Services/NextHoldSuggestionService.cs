using RuoteLab.Models;

namespace RuoteLab.Services;

public sealed class NextHoldSuggestionService : INextHoldSuggestionService
{
    public NextHoldSuggestionResult SuggestNextHold(NextHoldSuggestionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Wall);

        var orderedHoles = request.Wall.GetOrderedHoles()
            .Where(hole => hole.HasHold && hole.IsEnabled)
            .ToList();

        if (orderedHoles.Count == 0)
        {
            return new NextHoldSuggestionResult();
        }

        var leftHandHole = FindHoleOrThrow(orderedHoles, request.CurrentLeftHandHoleNumber, nameof(request.CurrentLeftHandHoleNumber));
        var rightHandHole = FindHoleOrThrow(orderedHoles, request.CurrentRightHandHoleNumber, nameof(request.CurrentRightHandHoleNumber));
        var movingHole = request.MovingHand == HandSide.Left ? leftHandHole : rightHandHole;
        var supportHandHole = request.MovingHand == HandSide.Left ? rightHandHole : leftHandHole;
        var leftFootHole = FindOptionalHole(orderedHoles, request.CurrentLeftFootHoleNumber);
        var rightFootHole = FindOptionalHole(orderedHoles, request.CurrentRightFootHoleNumber);

        var centerConfidence = GetCenterConfidence(request, leftFootHole, rightFootHole);
        var currentCenter = ResolveCurrentCenter(request, movingHole, supportHandHole, leftFootHole, rightFootHole);
        var wallAngleDegrees = request.WallAngleDegreesOverride ?? ParseInclinationDegrees(request.Circuit?.Inclination);
        var difficultyFactor = ClimbingGradeScale.ParseDifficultyFactor(request.Circuit?.Difficulty);
        var supportHandQuality = GetHoldQuality(supportHandHole.HoldType);
        var footSupportScore = GetFootSupportScore(leftFootHole, rightFootHole);

        var candidates = orderedHoles
            .Where(candidate => IsCandidateAllowed(candidate, request, movingHole, supportHandHole))
            .Select(candidate => ScoreCandidate(
                candidate,
                request,
                movingHole,
                supportHandHole,
                leftFootHole,
                rightFootHole,
                currentCenter,
                centerConfidence,
                wallAngleDegrees,
                difficultyFactor,
                supportHandQuality,
                footSupportScore))
            .OrderByDescending(candidate => candidate.Score)
            .ThenBy(candidate => candidate.DistanceFromMovingHand)
            .Take(Math.Max(1, request.MaxSuggestions))
            .ToList();

        var bestCandidate = candidates.FirstOrDefault();
        if (bestCandidate is null)
        {
            return new NextHoldSuggestionResult();
        }

        return new NextHoldSuggestionResult
        {
            SuggestedHoleNumber = bestCandidate.HoleNumber,
            SuggestedDirection = bestCandidate.MovementDirection,
            PrimaryReason = bestCandidate.PrimaryReason,
            SecondaryReason = bestCandidate.SecondaryReason,
            CenterConfidence = bestCandidate.CenterConfidence,
            CenterConfidenceLabel = bestCandidate.CenterConfidenceLabel,
            Candidates = candidates
        };
    }

    private static bool IsCandidateAllowed(
        WallHoleDefinition candidate,
        NextHoldSuggestionRequest request,
        WallHoleDefinition movingHole,
        WallHoleDefinition supportHandHole)
    {
        if (request.ExcludeCurrentHandHoles &&
            (candidate.Number == movingHole.Number || candidate.Number == supportHandHole.Number))
        {
            return false;
        }

        if (request.ExcludeFootOnlyHoldsForHands && candidate.HoldType == HoldType.Foothold)
        {
            return false;
        }

        return true;
    }

    private static NextHoldSuggestionCandidate ScoreCandidate(
        WallHoleDefinition candidate,
        NextHoldSuggestionRequest request,
        WallHoleDefinition movingHole,
        WallHoleDefinition supportHandHole,
        WallHoleDefinition? leftFootHole,
        WallHoleDefinition? rightFootHole,
        (double X, double Y) currentCenter,
        double centerConfidence,
        double wallAngleDegrees,
        double difficultyFactor,
        double supportHandQuality,
        double footSupportScore)
    {
        var movingDistance = Distance(movingHole.AbsoluteX, movingHole.AbsoluteY, candidate.AbsoluteX, candidate.AbsoluteY);
        var centerDistance = Distance(currentCenter.X, currentCenter.Y, candidate.AbsoluteX, candidate.AbsoluteY);
        var estimatedCenter = EstimateCenterAfterMove(candidate, supportHandHole, leftFootHole, rightFootHole);
        var centerShift = Distance(currentCenter.X, currentCenter.Y, estimatedCenter.X, estimatedCenter.Y);
        var movementDirection = BuildMovementDirection(movingHole, candidate);
        var extensionRatio = ComputeExtensionRatio(movingDistance, request.Wall);
        var holdMetadataConfidence = GetHoldMetadataConfidence(candidate, movingHole, supportHandHole, leftFootHole, rightFootHole);
        var candidateQuality = GetHoldQuality(candidate.HoldType) * holdMetadataConfidence;
        var transitionScore = GetTransitionScore(movingHole.HoldType, candidate.HoldType) * holdMetadataConfidence;
        var directionScore = GetDirectionScore(movingHole, candidate);
        var wallDifficulty = GetWallDifficulty(wallAngleDegrees, extensionRatio, candidate.AbsoluteY - movingHole.AbsoluteY);
        var difficultyPenalty = GetDifficultyPenalty(difficultyFactor, extensionRatio, candidateQuality, centerShift * centerConfidence);
        var crossPenalty = GetCrossPenalty(request.MovingHand, supportHandHole, candidate);
        var sequenceContinuity = GetSequenceContinuity(candidate, supportHandHole);
        var centerShiftPenalty = centerShift * (0.07d * centerConfidence);

        var score =
            100d
            - movingDistance * 0.08d
            - centerDistance * 0.03d
            - centerShiftPenalty
            - wallDifficulty * 18d
            - difficultyPenalty * 14d
            - crossPenalty * 12d
            + candidateQuality * 22d
            + supportHandQuality * 12d
            + footSupportScore * 10d
            + transitionScore * 12d
            + directionScore * 10d
            + sequenceContinuity * 8d
            - extensionRatio * 15d;

        return new NextHoldSuggestionCandidate
        {
            HoleNumber = candidate.Number,
            HoldType = candidate.HoldType,
            MovementDirection = movementDirection,
            Score = score,
            DistanceFromMovingHand = movingDistance,
            DistanceFromCenter = centerDistance,
            CenterShiftRequired = centerShift,
            CenterConfidence = centerConfidence,
            CenterConfidenceLabel = GetCenterConfidenceLabel(centerConfidence),
            ExtensionRatio = extensionRatio,
            WallDifficulty = wallDifficulty,
            PrimaryReason = BuildPrimaryReason(candidateQuality, centerShift, wallDifficulty, directionScore, difficultyPenalty, holdMetadataConfidence),
            SecondaryReason = BuildSecondaryReason(supportHandQuality, footSupportScore, transitionScore, movementDirection, difficultyFactor, holdMetadataConfidence, centerConfidence)
        };
    }

    private static WallHoleDefinition FindHoleOrThrow(IEnumerable<WallHoleDefinition> holes, int holeNumber, string paramName)
    {
        var hole = holes.FirstOrDefault(item => item.Number == holeNumber);
        if (hole.Number == 0)
        {
            throw new InvalidOperationException($"Foro non trovato per {paramName}: {holeNumber}.");
        }

        return hole;
    }

    private static WallHoleDefinition? FindOptionalHole(IEnumerable<WallHoleDefinition> holes, int? holeNumber)
    {
        if (!holeNumber.HasValue || holeNumber.Value <= 0)
        {
            return null;
        }

        var hole = holes.FirstOrDefault(item => item.Number == holeNumber.Value);
        return hole.Number == 0 ? null : hole;
    }

    private static (double X, double Y) ResolveCurrentCenter(
        NextHoldSuggestionRequest request,
        WallHoleDefinition movingHole,
        WallHoleDefinition supportHandHole,
        WallHoleDefinition? leftFootHole,
        WallHoleDefinition? rightFootHole)
    {
        if (request.CenterX.HasValue && request.CenterY.HasValue)
        {
            return (request.CenterX.Value, request.CenterY.Value);
        }

        var supportPoints = new List<WallHoleDefinition> { movingHole, supportHandHole };
        if (leftFootHole.HasValue)
        {
            supportPoints.Add(leftFootHole.Value);
        }

        if (rightFootHole.HasValue)
        {
            supportPoints.Add(rightFootHole.Value);
        }

        return (
            supportPoints.Average(point => point.AbsoluteX),
            supportPoints.Average(point => point.AbsoluteY));
    }

    private static (double X, double Y) EstimateCenterAfterMove(
        WallHoleDefinition candidate,
        WallHoleDefinition supportHandHole,
        WallHoleDefinition? leftFootHole,
        WallHoleDefinition? rightFootHole)
    {
        var supportPoints = new List<WallHoleDefinition> { candidate, supportHandHole };
        if (leftFootHole.HasValue)
        {
            supportPoints.Add(leftFootHole.Value);
        }

        if (rightFootHole.HasValue)
        {
            supportPoints.Add(rightFootHole.Value);
        }

        return (
            supportPoints.Average(point => point.AbsoluteX),
            supportPoints.Average(point => point.AbsoluteY));
    }

    private static double ParseInclinationDegrees(string? inclination)
    {
        if (string.IsNullOrWhiteSpace(inclination))
        {
            return 0d;
        }

        var normalized = new string(inclination
            .Where(character => char.IsDigit(character) || character is '-' or '+' or '.' or ',')
            .ToArray())
            .Replace(',', '.');

        return double.TryParse(normalized, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var value)
            ? value
            : 0d;
    }

    private static double GetCenterConfidence(NextHoldSuggestionRequest request, WallHoleDefinition? leftFootHole, WallHoleDefinition? rightFootHole)
    {
        if (request.CenterX.HasValue && request.CenterY.HasValue)
        {
            return 1.00d;
        }

        var footCount = 0;
        if (leftFootHole.HasValue)
        {
            footCount++;
        }

        if (rightFootHole.HasValue)
        {
            footCount++;
        }

        return footCount switch
        {
            2 => 0.90d,
            1 => 0.65d,
            _ => 0.35d
        };
    }

    private static string GetCenterConfidenceLabel(double centerConfidence)
    {
        if (centerConfidence >= 0.9d)
        {
            return "alta";
        }

        if (centerConfidence >= 0.6d)
        {
            return "media";
        }

        return "bassa";
    }

    private static double GetHoldQuality(HoldType holdType)
    {
        return holdType switch
        {
            HoldType.Jug => 1.0d,
            HoldType.Pinch => 0.80d,
            HoldType.Edge => 0.72d,
            HoldType.Pocket => 0.68d,
            HoldType.Volume => 0.63d,
            HoldType.Sloper => 0.58d,
            HoldType.Foothold => 0.30d,
            _ => 0.50d
        };
    }

    private static double GetHoldMetadataConfidence(
        WallHoleDefinition candidate,
        WallHoleDefinition movingHole,
        WallHoleDefinition supportHandHole,
        WallHoleDefinition? leftFootHole,
        WallHoleDefinition? rightFootHole)
    {
        var confidences = new List<double>
        {
            candidate.HasEstimatedHoldMetadata ? 0.45d : 1.00d,
            movingHole.HasEstimatedHoldMetadata ? 0.65d : 1.00d,
            supportHandHole.HasEstimatedHoldMetadata ? 0.65d : 1.00d
        };

        if (leftFootHole.HasValue)
        {
            confidences.Add(leftFootHole.Value.HasEstimatedHoldMetadata ? 0.75d : 1.00d);
        }

        if (rightFootHole.HasValue)
        {
            confidences.Add(rightFootHole.Value.HasEstimatedHoldMetadata ? 0.75d : 1.00d);
        }

        return confidences.Average();
    }

    private static double GetTransitionScore(HoldType startType, HoldType candidateType)
    {
        if (startType == candidateType)
        {
            return 0.90d;
        }

        if (candidateType == HoldType.Jug)
        {
            return 1.00d;
        }

        if (startType == HoldType.Sloper && candidateType == HoldType.Edge)
        {
            return 0.55d;
        }

        if (candidateType == HoldType.Foothold)
        {
            return 0.10d;
        }

        return 0.70d;
    }

    private static double GetDirectionScore(WallHoleDefinition start, WallHoleDefinition candidate)
    {
        var deltaY = candidate.AbsoluteY - start.AbsoluteY;
        var deltaX = Math.Abs(candidate.AbsoluteX - start.AbsoluteX);

        if (deltaY > 0 && deltaX < 220d)
        {
            return 1.00d;
        }

        if (deltaY > -40d)
        {
            return 0.70d;
        }

        return 0.35d;
    }

    private static double GetWallDifficulty(double wallAngleDegrees, double extensionRatio, double heightDelta)
    {
        var absoluteAnglePenalty = Math.Abs(wallAngleDegrees) / 45d;
        var overheadPenalty = wallAngleDegrees < 0 ? absoluteAnglePenalty * 1.2d : absoluteAnglePenalty * 0.8d;
        var reachPenalty = extensionRatio > 0.85d ? 0.35d : 0d;
        var upwardPenalty = heightDelta > 0 ? Math.Min(0.25d, heightDelta / 1200d) : 0d;

        return overheadPenalty + reachPenalty + upwardPenalty;
    }

    private static double GetDifficultyPenalty(double difficultyFactor, double extensionRatio, double candidateQuality, double centerShift)
    {
        var reachPenalty = extensionRatio * difficultyFactor;
        var holdPenalty = (1d - candidateQuality) * difficultyFactor;
        var balancePenalty = Math.Min(1d, centerShift / 250d) * difficultyFactor;
        return reachPenalty + holdPenalty + balancePenalty;
    }

    private static double GetCrossPenalty(HandSide movingHand, WallHoleDefinition supportHandHole, WallHoleDefinition candidate)
    {
        var candidateIsAcrossSupport = movingHand == HandSide.Left
            ? candidate.AbsoluteX > supportHandHole.AbsoluteX
            : candidate.AbsoluteX < supportHandHole.AbsoluteX;

        return candidateIsAcrossSupport ? 1.0d : 0d;
    }

    private static double GetSequenceContinuity(WallHoleDefinition candidate, WallHoleDefinition supportHandHole)
    {
        var horizontalGap = Math.Abs(candidate.AbsoluteX - supportHandHole.AbsoluteX);
        var verticalGap = Math.Abs(candidate.AbsoluteY - supportHandHole.AbsoluteY);
        var compactness = horizontalGap <= 350d ? 0.65d : 0.35d;
        var verticalBonus = verticalGap <= 420d ? 0.35d : 0.10d;

        return compactness + verticalBonus;
    }

    private static double GetFootSupportScore(WallHoleDefinition? leftFootHole, WallHoleDefinition? rightFootHole)
    {
        var supports = new[] { leftFootHole, rightFootHole }
            .Where(hole => hole.HasValue)
            .Select(hole => GetHoldQuality(hole!.Value.HoldType))
            .ToList();

        if (supports.Count == 0)
        {
            return 0.30d;
        }

        return supports.Average();
    }

    private static double ComputeExtensionRatio(double movingDistance, WallDefinition wall)
    {
        var referenceReach = Math.Max(600d, Math.Min(wall.Width, wall.Height) * 0.45d);
        return Math.Min(1.50d, movingDistance / referenceReach);
    }

    private static string BuildMovementDirection(WallHoleDefinition start, WallHoleDefinition candidate)
    {
        var deltaX = candidate.AbsoluteX - start.AbsoluteX;
        var deltaY = candidate.AbsoluteY - start.AbsoluteY;
        var horizontal = Math.Abs(deltaX) < 30d ? string.Empty : deltaX > 0 ? "Right" : "Left";
        var vertical = Math.Abs(deltaY) < 30d ? string.Empty : deltaY > 0 ? "Up" : "Down";

        return string.IsNullOrWhiteSpace(vertical + horizontal)
            ? "Static"
            : $"{vertical}{horizontal}";
    }

    private static string BuildPrimaryReason(double candidateQuality, double centerShift, double wallDifficulty, double directionScore, double difficultyPenalty, double holdMetadataConfidence)
    {
        if (holdMetadataConfidence < 0.60d && centerShift <= 120d)
        {
            return "Scelta guidata piu dalla geometria che dal tipo presa";
        }

        if (candidateQuality >= 0.9d)
        {
            return "Presa arrivo molto favorevole";
        }

        if (centerShift <= 120d)
        {
            return "Spostamento del baricentro contenuto";
        }

        if (wallDifficulty <= 0.35d)
        {
            return "Costo della parete contenuto";
        }

        if (difficultyPenalty <= 0.45d)
        {
            return "Compatibile con il grado del circuito";
        }

        if (directionScore >= 0.95d)
        {
            return "Direzione di movimento naturale";
        }

        return "Compromesso tecnico equilibrato";
    }

    private static string BuildSecondaryReason(double supportHandQuality, double footSupportScore, double transitionScore, string movementDirection, double difficultyFactor, double holdMetadataConfidence, double centerConfidence)
    {
        if (holdMetadataConfidence < 0.60d)
        {
            return "Tipo e dimensione presa stimati";
        }

        if (centerConfidence < 0.5d)
        {
            return "Baricentro stimato con affidabilita bassa";
        }

        if (supportHandQuality >= 0.9d)
        {
            return "Altra mano molto stabile";
        }

        if (footSupportScore >= 0.75d)
        {
            return "Buon supporto dei piedi";
        }

        if (transitionScore >= 0.9d)
        {
            return "Transizione di presa favorevole";
        }

        if (difficultyFactor >= 0.8d)
        {
            return "Filtro piu severo per grado alto";
        }

        return $"Movimento previsto: {movementDirection}";
    }

    private static double Distance(double x1, double y1, double x2, double y2)
    {
        var deltaX = x2 - x1;
        var deltaY = y2 - y1;
        return Math.Sqrt((deltaX * deltaX) + (deltaY * deltaY));
    }
}
