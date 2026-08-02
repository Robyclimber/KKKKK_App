using RouteLab.Models;

namespace RouteLab.Services;

public sealed class NextHoldSuggestionService : INextHoldSuggestionService
{
    private readonly IBiomechanicalCenterOfMassService biomechanicalCenterOfMassService;

    public NextHoldSuggestionService()
        : this(new BiomechanicalCenterOfMassService())
    {
    }

    public NextHoldSuggestionService(IBiomechanicalCenterOfMassService biomechanicalCenterOfMassService)
    {
        this.biomechanicalCenterOfMassService = biomechanicalCenterOfMassService;
    }

    public NextHoldSuggestionResult SuggestNextHold(NextHoldSuggestionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Wall);

        var enabledHoles = request.Wall.GetOrderedHoles()
            .Where(hole => hole.IsEnabled)
            .ToList();

        if (enabledHoles.Count == 0)
        {
            return new NextHoldSuggestionResult();
        }

        var leftHandHole = WithEstimatedDefaultHold(
            FindHoleOrThrow(enabledHoles, request.CurrentLeftHandHoleNumber, nameof(request.CurrentLeftHandHoleNumber)));
        var rightHandHole = WithEstimatedDefaultHold(
            FindHoleOrThrow(enabledHoles, request.CurrentRightHandHoleNumber, nameof(request.CurrentRightHandHoleNumber)));
        var movingHole = request.MovingHand == HandSide.Left ? leftHandHole : rightHandHole;
        var supportHandHole = request.MovingHand == HandSide.Left ? rightHandHole : leftHandHole;
        var availableFootHoles = ResolveFootHoles(enabledHoles, request.CurrentFootHoleNumbers)
            .Select(WithEstimatedDefaultHold)
            .ToList();
        var candidateHoles = enabledHoles
            .Where(hole => hole.HasHold || hole.HasEstimatedHoldMetadata)
            .Select(WithEstimatedDefaultHold)
            .ToList();

        var wallAngleDegrees = request.WallAngleDegreesOverride ?? ParseInclinationDegrees(request.Circuit?.Inclination);
        var currentFootSupport = SelectFootSupports(request, movingHole, supportHandHole, availableFootHoles, wallAngleDegrees);
        var currentCenter = ResolveSelectedCenter(request, movingHole, supportHandHole, currentFootSupport);
        var difficultyFactor = ClimbingGradeScale.ParseDifficultyFactor(request.Circuit?.Difficulty);
        var supportHandQuality = GetHoldQuality(supportHandHole.HoldType);

        var candidates = candidateHoles
            .Where(candidate => IsCandidateAllowed(candidate, request, movingHole, supportHandHole))
            .Select(candidate => ScoreCandidate(
                candidate,
                request,
                movingHole,
                supportHandHole,
                availableFootHoles,
                currentFootSupport,
                currentCenter,
                wallAngleDegrees,
                difficultyFactor,
                supportHandQuality))
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
            MovementPlan = bestCandidate.MovementPlan,
            PrimaryReason = bestCandidate.PrimaryReason,
            SecondaryReason = bestCandidate.SecondaryReason,
            CenterConfidence = bestCandidate.CenterConfidence,
            CenterConfidenceLabel = bestCandidate.CenterConfidenceLabel,
            SupportFootHoleNumbers = bestCandidate.SupportFootHoleNumbers,
            CurrentSupportFootHoleNumbers = bestCandidate.CurrentSupportFootHoleNumbers,
            FootMoveHoleNumbers = bestCandidate.FootMoveHoleNumbers,
            FootRepositionDistance = bestCandidate.FootRepositionDistance,
            PreparationCenterInsideSupportTriangle = bestCandidate.PreparationCenterInsideSupportTriangle,
            PreparationDistanceFromSupportTriangle = bestCandidate.PreparationDistanceFromSupportTriangle,
            CenterInsideSupportTriangle = bestCandidate.CenterInsideSupportTriangle,
            DistanceFromSupportTriangle = bestCandidate.DistanceFromSupportTriangle,
            BiomechanicalCenterX = bestCandidate.BiomechanicalCenterX,
            BiomechanicalCenterY = bestCandidate.BiomechanicalCenterY,
            GravityTorqueNewtonMeter = bestCandidate.GravityTorqueNewtonMeter,
            NormalGravityForceNewton = bestCandidate.NormalGravityForceNewton,
            ReachPenalty = bestCandidate.ReachPenalty,
            IsReachFeasible = bestCandidate.IsReachFeasible,
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

        if (request.Circuit?.Movements.Any(movement =>
                movement.IsFootHold &&
                string.Equals(movement.WallName, request.Wall.Name, StringComparison.Ordinal) &&
                movement.HoleNumber == candidate.Number) == true)
        {
            return false;
        }

        return true;
    }

    private NextHoldSuggestionCandidate ScoreCandidate(
        WallHoleDefinition candidate,
        NextHoldSuggestionRequest request,
        WallHoleDefinition movingHole,
        WallHoleDefinition supportHandHole,
        IReadOnlyList<WallHoleDefinition> availableFootHoles,
        FootSupportSelection currentFootSupport,
        (double X, double Y) currentCenter,
        double wallAngleDegrees,
        double difficultyFactor,
        double supportHandQuality)
    {
        var movingDistance = Distance(movingHole.AbsoluteX, movingHole.AbsoluteY, candidate.AbsoluteX, candidate.AbsoluteY);
        var footSupport = SelectFootSupports(
            request,
            candidate,
            supportHandHole,
            availableFootHoles,
            wallAngleDegrees,
            movingHole,
            currentFootSupport);
        var targetCenter = ResolveSelectedCenter(request, candidate, supportHandHole, footSupport);
        var centerDistance = Distance(targetCenter.X, targetCenter.Y, candidate.AbsoluteX, candidate.AbsoluteY);
        var centerShift = Distance(currentCenter.X, currentCenter.Y, targetCenter.X, targetCenter.Y);
        var footTransitions = BuildFootTransitions(currentFootSupport, footSupport);
        var movementPlan = BuildMovementPlan(
            request,
            movingHole,
            supportHandHole,
            candidate,
            currentFootSupport,
            footSupport,
            footTransitions,
            currentCenter,
            targetCenter);
        var centerConfidence = GetCenterConfidence(request, footSupport);
        var footSupportScore = GetFootSupportScore(footSupport);
        var supportTriangleScore = GetSupportTriangleScore(footSupport);
        var movementDirection = BuildMovementDirection(movingHole, candidate);
        var extensionRatio = ComputeExtensionRatio(movingDistance, request.Wall);
        var holdMetadataConfidence = GetHoldMetadataConfidence(candidate, movingHole, supportHandHole, footSupport.FirstFoot, footSupport.SecondFoot);
        var candidateQuality = GetHoldQuality(candidate.HoldType) * holdMetadataConfidence;
        var transitionScore = GetTransitionScore(movingHole.HoldType, candidate.HoldType) * holdMetadataConfidence;
        var directionScore = GetDirectionScore(movingHole, candidate);
        var wallDifficulty = GetWallDifficulty(wallAngleDegrees, extensionRatio, candidate.AbsoluteY - movingHole.AbsoluteY);
        var difficultyPenalty = GetDifficultyPenalty(difficultyFactor, extensionRatio, candidateQuality, centerShift * centerConfidence);
        var crossPenalty = GetCrossPenalty(request.MovingHand, supportHandHole, candidate);
        var sequenceContinuity = GetSequenceContinuity(candidate, supportHandHole);
        var centerShiftPenalty = centerShift * (0.07d * centerConfidence);
        var evaluatedPoses = footSupport.HasPreparationEvaluation ? 2d : 1d;
        var reachPenalty = footSupport.TotalReachPenalty / evaluatedPoses;
        var preparationPenalty = footSupport.HasPreparationEvaluation &&
                                 !footSupport.PreparationContainsCenter
            ? Math.Min(1d, footSupport.PreparationDistanceToTriangle / 500d)
            : 0d;

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
            + footSupportScore * 8d
            + supportTriangleScore * 20d
            + transitionScore * 12d
            + directionScore * 10d
            + sequenceContinuity * 8d
            - extensionRatio * 15d
            - reachPenalty * 35d
            - preparationPenalty * 18d
            - footSupport.FootMoveCount * 5d
            - footSupport.FootRepositionDistance * 0.015d
            - GetStaticTechniquePenalty(movementPlan, difficultyFactor);

        return new NextHoldSuggestionCandidate
        {
            HoleNumber = candidate.Number,
            HoldType = candidate.HoldType,
            MovementDirection = movementDirection,
            MovementPlan = movementPlan,
            Score = score,
            DistanceFromMovingHand = movingDistance,
            DistanceFromCenter = centerDistance,
            CenterShiftRequired = centerShift,
            CenterConfidence = centerConfidence,
            CenterConfidenceLabel = GetCenterConfidenceLabel(centerConfidence),
            SupportFootHoleNumbers = footSupport.GetHoleNumbers(),
            CurrentSupportFootHoleNumbers = currentFootSupport.GetHoleNumbers(),
            FootMoveHoleNumbers = footTransitions
                .Select(transition => transition.To.Number)
                .ToList(),
            FootRepositionDistance = footSupport.FootRepositionDistance,
            PreparationCenterInsideSupportTriangle = footSupport.PreparationContainsCenter,
            PreparationDistanceFromSupportTriangle = footSupport.PreparationDistanceToTriangle,
            CenterInsideSupportTriangle = footSupport.ContainsCenter,
            DistanceFromSupportTriangle = footSupport.DistanceToTriangle,
            BiomechanicalCenterX = targetCenter.X,
            BiomechanicalCenterY = targetCenter.Y,
            GravityTorqueNewtonMeter = footSupport.Biomechanics?.GravityTorqueNewtonMeter ?? 0d,
            NormalGravityForceNewton = footSupport.Biomechanics?.NormalGravityForceNewton ?? 0d,
            ReachPenalty = reachPenalty,
            IsReachFeasible = footSupport.IsTransitionReachable,
            ExtensionRatio = extensionRatio,
            WallDifficulty = wallDifficulty,
            PrimaryReason = BuildPrimaryReason(candidateQuality, centerShift, wallDifficulty, directionScore, difficultyPenalty, holdMetadataConfidence),
            SecondaryReason = BuildSecondaryReason(supportHandQuality, footSupportScore, transitionScore, movementDirection, difficultyFactor, holdMetadataConfidence, centerConfidence, footSupport)
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

    private static WallHoleDefinition WithEstimatedDefaultHold(WallHoleDefinition hole)
    {
        return hole.HasHold
            ? hole
            : hole with
            {
                HasHold = true,
                HoldSize = HoldSize.M,
                HoldType = HoldType.Jug,
                HasEstimatedHoldMetadata = true
            };
    }

    private static IReadOnlyList<WallHoleDefinition> ResolveFootHoles(
        IReadOnlyList<WallHoleDefinition> holes,
        IReadOnlyList<int> footHoleNumbers)
    {
        if (footHoleNumbers.Count == 0)
        {
            return Array.Empty<WallHoleDefinition>();
        }

        var requestedNumbers = footHoleNumbers
            .Where(number => number > 0)
            .ToHashSet();
        return holes
            .Where(hole => requestedNumbers.Contains(hole.Number))
            .OrderBy(hole => hole.Number)
            .ToList();
    }

    private static (double X, double Y) ResolveBodyCenter(
        NextHoldSuggestionRequest request,
        WallHoleDefinition movingHole,
        WallHoleDefinition supportHandHole)
    {
        if (request.CenterX.HasValue && request.CenterY.HasValue)
        {
            return (request.CenterX.Value, request.CenterY.Value);
        }

        var handCenterX = (movingHole.AbsoluteX + supportHandHole.AbsoluteX) / 2d;
        var handCenterY = (movingHole.AbsoluteY + supportHandHole.AbsoluteY) / 2d;
        var referenceReach = Math.Max(600d, Math.Min(request.Wall.Width, request.Wall.Height) * 0.45d);
        var torsoOffset = Math.Clamp(referenceReach * 0.42d, 250d, 650d);
        var centerX = request.Wall.Width > 0d
            ? Math.Clamp(handCenterX, 0d, request.Wall.Width)
            : handCenterX;
        var centerY = request.Wall.Height > 0d
            ? Math.Clamp(handCenterY + torsoOffset, 0d, request.Wall.Height)
            : handCenterY + torsoOffset;
        return (centerX, centerY);
    }

    private static (double X, double Y) ResolveSelectedCenter(
        NextHoldSuggestionRequest request,
        WallHoleDefinition movingHole,
        WallHoleDefinition supportHandHole,
        FootSupportSelection footSupport)
    {
        if (request.CenterX.HasValue && request.CenterY.HasValue)
        {
            return (request.CenterX.Value, request.CenterY.Value);
        }

        return footSupport.Biomechanics is not null
            ? (footSupport.Biomechanics.EffectiveCenterX, footSupport.Biomechanics.EffectiveCenterY)
            : ResolveBodyCenter(request, movingHole, supportHandHole);
    }

    private FootSupportSelection SelectFootSupports(
        NextHoldSuggestionRequest request,
        WallHoleDefinition movingHole,
        WallHoleDefinition supportHandHole,
        IReadOnlyList<WallHoleDefinition> availableFootHoles,
        double wallAngleDegrees,
        WallHoleDefinition? preparationMovingHole = null,
        FootSupportSelection? currentFootSupport = null)
    {
        var fallbackCenter = ResolveBodyCenter(request, movingHole, supportHandHole);
        if (availableFootHoles.Count == 0)
        {
            return FootSupportSelection.Empty;
        }

        if (availableFootHoles.Count == 1)
        {
            var foot = availableFootHoles[0];
            var selection = new FootSupportSelection(
                foot,
                null,
                false,
                DistanceToSegment(fallbackCenter, supportHandHole, foot),
                0d,
                0d,
                null);
            return AddFootTransitionMetrics(selection, currentFootSupport);
        }

        FootSupportSelection? best = null;
        for (var firstIndex = 0; firstIndex < availableFootHoles.Count - 1; firstIndex++)
        {
            for (var secondIndex = firstIndex + 1; secondIndex < availableFootHoles.Count; secondIndex++)
            {
                var firstFoot = availableFootHoles[firstIndex];
                var secondFoot = availableFootHoles[secondIndex];
                var target = EvaluateFootSupportPair(
                    request,
                    movingHole,
                    supportHandHole,
                    firstFoot,
                    secondFoot,
                    wallAngleDegrees);
                var selection = new FootSupportSelection(
                    firstFoot,
                    secondFoot,
                    target.ContainsCenter,
                    target.DistanceToTriangle,
                    target.StabilityMargin,
                    target.TriangleArea,
                    target.Biomechanics);

                if (preparationMovingHole.HasValue)
                {
                    var preparation = EvaluateFootSupportPair(
                        request,
                        preparationMovingHole.Value,
                        supportHandHole,
                        firstFoot,
                        secondFoot,
                        wallAngleDegrees);
                    selection = selection with
                    {
                        HasPreparationEvaluation = true,
                        PreparationContainsCenter = preparation.ContainsCenter,
                        PreparationDistanceToTriangle = preparation.DistanceToTriangle,
                        PreparationStabilityMargin = preparation.StabilityMargin,
                        PreparationBiomechanics = preparation.Biomechanics
                    };
                }

                selection = AddFootTransitionMetrics(selection, currentFootSupport);

                if (best is null || IsBetterFootSupport(selection, best))
                {
                    best = selection;
                }
            }
        }

        return best ?? FootSupportSelection.Empty;
    }

    private FootSupportEvaluation EvaluateFootSupportPair(
        NextHoldSuggestionRequest request,
        WallHoleDefinition movingHole,
        WallHoleDefinition supportHandHole,
        WallHoleDefinition firstFoot,
        WallHoleDefinition secondFoot,
        double wallAngleDegrees)
    {
        var leftHand = request.MovingHand == HandSide.Left ? movingHole : supportHandHole;
        var rightHand = request.MovingHand == HandSide.Right ? movingHole : supportHandHole;
        var biomechanics = biomechanicalCenterOfMassService.Estimate(new BiomechanicalPoseRequest
        {
            Wall = request.Wall,
            Climber = request.ClimberProfile,
            LeftHand = leftHand,
            RightHand = rightHand,
            FirstFoot = firstFoot,
            SecondFoot = secondFoot,
            WallInclinationDegrees = wallAngleDegrees
        });
        var center = request.CenterX.HasValue && request.CenterY.HasValue
            ? (request.CenterX.Value, request.CenterY.Value)
            : (biomechanics.EffectiveCenterX, biomechanics.EffectiveCenterY);
        var area = GetTriangleArea(supportHandHole, firstFoot, secondFoot);
        var containsCenter = area > 0.001d &&
                             IsPointInsideTriangle(center, supportHandHole, firstFoot, secondFoot);
        var distanceToTriangle = containsCenter
            ? 0d
            : DistanceToTriangle(center, supportHandHole, firstFoot, secondFoot);
        var stabilityMargin = containsCenter
            ? DistanceToTriangleEdges(center, supportHandHole, firstFoot, secondFoot)
            : 0d;
        return new FootSupportEvaluation(
            containsCenter,
            distanceToTriangle,
            stabilityMargin,
            area,
            biomechanics);
    }

    private static FootSupportSelection AddFootTransitionMetrics(
        FootSupportSelection selection,
        FootSupportSelection? currentFootSupport)
    {
        if (currentFootSupport is null || currentFootSupport.FootCount == 0)
        {
            return selection;
        }

        var currentFeet = new[] { currentFootSupport.FirstFoot, currentFootSupport.SecondFoot }
            .Where(foot => foot.HasValue)
            .Select(foot => foot!.Value)
            .ToList();
        var targetFeet = new[] { selection.FirstFoot, selection.SecondFoot }
            .Where(foot => foot.HasValue)
            .Select(foot => foot!.Value)
            .ToList();
        var movedFeet = targetFeet
            .Where(target => currentFeet.All(current => current.Number != target.Number))
            .ToList();
        var repositionDistance = movedFeet.Sum(target =>
            currentFeet.Min(current =>
                Distance(current.AbsoluteX, current.AbsoluteY, target.AbsoluteX, target.AbsoluteY)));

        return selection with
        {
            FootMoveCount = movedFeet.Count,
            FootRepositionDistance = repositionDistance
        };
    }

    private static IReadOnlyList<FootTransition> BuildFootTransitions(
        FootSupportSelection currentFootSupport,
        FootSupportSelection targetFootSupport)
    {
        var currentFeet = currentFootSupport.GetFeet();
        var targetFeet = targetFootSupport.GetFeet();
        var sources = currentFeet
            .Where(current => targetFeet.All(target => target.Number != current.Number))
            .ToList();
        var destinations = targetFeet
            .Where(target => currentFeet.All(current => current.Number != target.Number))
            .ToList();

        if (sources.Count == 0 || destinations.Count == 0)
        {
            return Array.Empty<FootTransition>();
        }

        List<FootTransition> transitions;
        if (sources.Count == 2 && destinations.Count == 2)
        {
            var directDistance =
                DistanceBetweenHoles(sources[0], destinations[0]) +
                DistanceBetweenHoles(sources[1], destinations[1]);
            var crossedDistance =
                DistanceBetweenHoles(sources[0], destinations[1]) +
                DistanceBetweenHoles(sources[1], destinations[0]);
            transitions = directDistance <= crossedDistance
                ? new List<FootTransition>
                {
                    new(sources[0], destinations[0]),
                    new(sources[1], destinations[1])
                }
                : new List<FootTransition>
                {
                    new(sources[0], destinations[1]),
                    new(sources[1], destinations[0])
                };
        }
        else
        {
            transitions = destinations
                .Select(destination =>
                {
                    var source = sources.MinBy(candidate => DistanceBetweenHoles(candidate, destination));
                    return new FootTransition(source, destination);
                })
                .ToList();
        }

        return transitions
            .OrderBy(transition => DistanceBetweenHoles(transition.From, transition.To))
            .ThenBy(transition => transition.To.Number)
            .ToList();
    }

    private static ClimbingMovementPlan BuildMovementPlan(
        NextHoldSuggestionRequest request,
        WallHoleDefinition movingHole,
        WallHoleDefinition supportHandHole,
        WallHoleDefinition candidate,
        FootSupportSelection currentFootSupport,
        FootSupportSelection targetFootSupport,
        IReadOnlyList<FootTransition> footTransitions,
        (double X, double Y) currentCenter,
        (double X, double Y) targetCenter)
    {
        var height = Math.Clamp(request.ClimberProfile.HeightMm, 1200d, 2300d);
        var deltaX = candidate.AbsoluteX - movingHole.AbsoluteX;
        var deltaY = candidate.AbsoluteY - movingHole.AbsoluteY;
        var lateralMovement = Math.Abs(deltaX) >= Math.Max(height * 0.08d, Math.Abs(deltaY) * 0.85d);
        var crossesSupportHand = request.MovingHand == HandSide.Left
            ? candidate.AbsoluteX > supportHandHole.AbsoluteX
            : candidate.AbsoluteX < supportHandHole.AbsoluteX;
        var requiresHipRotation =
            crossesSupportHand ||
            (lateralMovement && Math.Abs(deltaX) >= height * 0.18d);
        var centerShift = Distance(currentCenter.X, currentCenter.Y, targetCenter.X, targetCenter.Y);
        var requiresWeightTransfer =
            footTransitions.Count > 0 ||
            centerShift >= height * 0.05d;
        var isRockOver = IsRockOver(
            height,
            currentCenter,
            targetCenter,
            targetFootSupport,
            footTransitions);

        var movementTypes = new List<ClimbingMovementType>
        {
            lateralMovement
                ? ClimbingMovementType.Lateral
                : ClimbingMovementType.Frontal
        };
        if (requiresHipRotation)
        {
            movementTypes.Add(ClimbingMovementType.HipRotation);
        }

        if (requiresWeightTransfer)
        {
            movementTypes.Add(ClimbingMovementType.WeightTransfer);
        }

        if (isRockOver)
        {
            movementTypes.Add(ClimbingMovementType.RockOver);
        }

        var balanceTechniques = new List<ClimbingBalanceTechnique>();
        if (targetFootSupport.ContainsCenter &&
            (!targetFootSupport.HasPreparationEvaluation || targetFootSupport.PreparationContainsCenter))
        {
            balanceTechniques.Add(ClimbingBalanceTechnique.StableSupportTriangle);
        }

        if (requiresWeightTransfer)
        {
            balanceTechniques.Add(ClimbingBalanceTechnique.ProgressiveWeightTransfer);
        }

        if (isRockOver)
        {
            balanceTechniques.Add(ClimbingBalanceTechnique.FootLoading);
        }

        if (!targetFootSupport.ContainsCenter &&
            targetFootSupport.DistanceToTriangle <= height * 0.15d)
        {
            balanceTechniques.Add(ClimbingBalanceTechnique.Counterbalance);
        }

        if (targetFootSupport.FootCount < 2)
        {
            balanceTechniques.Add(ClimbingBalanceTechnique.Flagging);
        }

        var steps = BuildMovementSteps(
            request.MovingHand,
            movingHole,
            supportHandHole,
            targetFootSupport,
            footTransitions,
            candidate,
            requiresWeightTransfer,
            isRockOver,
            targetCenter);

        var primaryMovement = isRockOver
            ? ClimbingMovementType.RockOver
            : requiresHipRotation
                ? ClimbingMovementType.HipRotation
                : requiresWeightTransfer
                    ? ClimbingMovementType.WeightTransfer
                    : movementTypes[0];

        return new ClimbingMovementPlan
        {
            ExecutionMode = ClimbingExecutionMode.Static,
            PrimaryMovement = primaryMovement,
            MovementTypes = movementTypes.Distinct().ToList(),
            BalanceTechniques = balanceTechniques.Distinct().ToList(),
            Steps = steps
        };
    }

    private static IReadOnlyList<ClimbingMovementStep> BuildMovementSteps(
        HandSide movingHand,
        WallHoleDefinition movingHole,
        WallHoleDefinition supportHandHole,
        FootSupportSelection targetFootSupport,
        IReadOnlyList<FootTransition> footTransitions,
        WallHoleDefinition candidate,
        bool requiresWeightTransfer,
        bool isRockOver,
        (double X, double Y) targetCenter)
    {
        var steps = new List<ClimbingMovementStep>();
        var sequence = 1;
        var supportBodyPart = movingHand == HandSide.Left
            ? ClimbingBodyPart.RightHand
            : ClimbingBodyPart.LeftHand;
        var movingBodyPart = movingHand == HandSide.Left
            ? ClimbingBodyPart.LeftHand
            : ClimbingBodyPart.RightHand;

        steps.Add(new ClimbingMovementStep
        {
            Sequence = sequence++,
            Phase = ClimbingMovementPhase.EstablishSupport,
            BodyPart = supportBodyPart,
            Action = ClimbingMovementAction.MaintainContact,
            FromHoleNumber = supportHandHole.Number,
            ToHoleNumber = supportHandHole.Number
        });

        var movingFootTargets = footTransitions
            .Select(transition => transition.To.Number)
            .ToHashSet();
        foreach (var maintainedFoot in targetFootSupport.GetFeet()
                     .Where(foot => !movingFootTargets.Contains(foot.Number))
                     .OrderBy(foot => foot.Number))
        {
            steps.Add(new ClimbingMovementStep
            {
                Sequence = sequence++,
                Phase = ClimbingMovementPhase.EstablishSupport,
                BodyPart = ClimbingBodyPart.Feet,
                Action = ClimbingMovementAction.MaintainContact,
                FromHoleNumber = maintainedFoot.Number,
                ToHoleNumber = maintainedFoot.Number
            });
        }

        foreach (var transition in footTransitions)
        {
            steps.Add(new ClimbingMovementStep
            {
                Sequence = sequence++,
                Phase = ClimbingMovementPhase.FootPreparation,
                BodyPart = ClimbingBodyPart.Feet,
                Action = ClimbingMovementAction.Move,
                FromHoleNumber = transition.From.Number,
                ToHoleNumber = transition.To.Number
            });
        }

        if (isRockOver)
        {
            var loadedFoot = targetFootSupport.GetFeet()
                .MinBy(foot => Math.Abs(foot.AbsoluteX - targetCenter.X));
            steps.Add(new ClimbingMovementStep
            {
                Sequence = sequence++,
                Phase = ClimbingMovementPhase.WeightTransfer,
                BodyPart = ClimbingBodyPart.Feet,
                Action = ClimbingMovementAction.Load,
                ToHoleNumber = loadedFoot.Number
            });
        }

        if (requiresWeightTransfer)
        {
            steps.Add(new ClimbingMovementStep
            {
                Sequence = sequence++,
                Phase = ClimbingMovementPhase.WeightTransfer,
                BodyPart = ClimbingBodyPart.Body,
                Action = ClimbingMovementAction.TransferWeight
            });
        }

        steps.Add(new ClimbingMovementStep
        {
            Sequence = sequence++,
            Phase = ClimbingMovementPhase.HandMovement,
            BodyPart = movingBodyPart,
            Action = ClimbingMovementAction.Move,
            FromHoleNumber = movingHole.Number,
            ToHoleNumber = candidate.Number
        });
        steps.Add(new ClimbingMovementStep
        {
            Sequence = sequence,
            Phase = ClimbingMovementPhase.Stabilization,
            BodyPart = ClimbingBodyPart.Body,
            Action = ClimbingMovementAction.Stabilize
        });

        return steps;
    }

    private static bool IsRockOver(
        double climberHeight,
        (double X, double Y) currentCenter,
        (double X, double Y) targetCenter,
        FootSupportSelection targetFootSupport,
        IReadOnlyList<FootTransition> footTransitions)
    {
        var targetFeet = targetFootSupport.GetFeet();
        if (targetFeet.Count == 0)
        {
            return false;
        }

        var loadedFoot = targetFeet.MinBy(foot => Math.Abs(foot.AbsoluteX - targetCenter.X));
        var verticalThreshold = climberHeight * 0.055d;
        var isHighSupport = targetFeet.Any(other =>
            other.Number != loadedFoot.Number &&
            loadedFoot.AbsoluteY < other.AbsoluteY - verticalThreshold);
        var isStepUp = footTransitions.Any(transition =>
            transition.To.Number == loadedFoot.Number &&
            transition.To.AbsoluteY < transition.From.AbsoluteY - verticalThreshold);
        var currentHorizontalDistance = Math.Abs(currentCenter.X - loadedFoot.AbsoluteX);
        var targetHorizontalDistance = Math.Abs(targetCenter.X - loadedFoot.AbsoluteX);
        var movedOverFoot =
            targetHorizontalDistance <= climberHeight * 0.12d &&
            targetHorizontalDistance + (climberHeight * 0.025d) < currentHorizontalDistance;

        return (isHighSupport || isStepUp) &&
               movedOverFoot &&
               targetCenter.Y < loadedFoot.AbsoluteY;
    }

    private static double GetStaticTechniquePenalty(
        ClimbingMovementPlan movementPlan,
        double difficultyFactor)
    {
        var complexity = movementPlan.MovementTypes
            .Distinct()
            .Sum(type => type switch
            {
                ClimbingMovementType.Lateral => 1.0d,
                ClimbingMovementType.HipRotation => 3.0d,
                ClimbingMovementType.WeightTransfer => 2.0d,
                ClimbingMovementType.RockOver => 4.0d,
                _ => 0d
            });
        var gradeAdjustment = Math.Clamp(1.15d - (difficultyFactor * 0.35d), 0.75d, 1.15d);
        return complexity * gradeAdjustment;
    }

    private static double DistanceBetweenHoles(
        WallHoleDefinition first,
        WallHoleDefinition second)
    {
        return Distance(first.AbsoluteX, first.AbsoluteY, second.AbsoluteX, second.AbsoluteY);
    }

    private static bool IsBetterFootSupport(FootSupportSelection candidate, FootSupportSelection current)
    {
        var candidateReachable = candidate.IsTransitionReachable;
        var currentReachable = current.IsTransitionReachable;
        if (candidateReachable != currentReachable)
        {
            return candidateReachable;
        }

        if (!candidateReachable &&
            Math.Abs(candidate.TotalReachPenalty - current.TotalReachPenalty) > 0.001d)
        {
            return candidate.TotalReachPenalty < current.TotalReachPenalty;
        }

        if (candidate.ContainsCenter != current.ContainsCenter)
        {
            return candidate.ContainsCenter;
        }

        if (candidate.HasPreparationEvaluation &&
            current.HasPreparationEvaluation &&
            candidate.PreparationContainsCenter != current.PreparationContainsCenter)
        {
            return candidate.PreparationContainsCenter;
        }

        if (candidate.ContainsCenter &&
            Math.Abs(candidate.StabilityMargin - current.StabilityMargin) > 0.001d)
        {
            return candidate.StabilityMargin > current.StabilityMargin;
        }

        if (candidate.PreparationContainsCenter &&
            Math.Abs(candidate.PreparationStabilityMargin - current.PreparationStabilityMargin) > 0.001d)
        {
            return candidate.PreparationStabilityMargin > current.PreparationStabilityMargin;
        }

        if (!candidate.ContainsCenter &&
            Math.Abs(candidate.DistanceToTriangle - current.DistanceToTriangle) > 0.001d)
        {
            return candidate.DistanceToTriangle < current.DistanceToTriangle;
        }

        if (candidate.HasPreparationEvaluation &&
            !candidate.PreparationContainsCenter &&
            Math.Abs(candidate.PreparationDistanceToTriangle - current.PreparationDistanceToTriangle) > 0.001d)
        {
            return candidate.PreparationDistanceToTriangle < current.PreparationDistanceToTriangle;
        }

        if (candidate.FootMoveCount != current.FootMoveCount)
        {
            return candidate.FootMoveCount < current.FootMoveCount;
        }

        if (Math.Abs(candidate.FootRepositionDistance - current.FootRepositionDistance) > 0.001d)
        {
            return candidate.FootRepositionDistance < current.FootRepositionDistance;
        }

        if (Math.Abs(candidate.TriangleArea - current.TriangleArea) > 0.001d)
        {
            return candidate.TriangleArea > current.TriangleArea;
        }

        var candidateQuality = GetFootSupportScore(candidate);
        var currentQuality = GetFootSupportScore(current);
        if (Math.Abs(candidateQuality - currentQuality) > 0.001d)
        {
            return candidateQuality > currentQuality;
        }

        return candidate.GetHoleNumbers().Sum() < current.GetHoleNumbers().Sum();
    }

    private static bool IsPointInsideTriangle(
        (double X, double Y) point,
        WallHoleDefinition first,
        WallHoleDefinition second,
        WallHoleDefinition third)
    {
        var firstSign = Cross(point, first, second);
        var secondSign = Cross(point, second, third);
        var thirdSign = Cross(point, third, first);
        var hasNegative = firstSign < -0.001d || secondSign < -0.001d || thirdSign < -0.001d;
        var hasPositive = firstSign > 0.001d || secondSign > 0.001d || thirdSign > 0.001d;
        return !(hasNegative && hasPositive);
    }

    private static double Cross(
        (double X, double Y) point,
        WallHoleDefinition segmentStart,
        WallHoleDefinition segmentEnd)
    {
        return ((point.X - segmentEnd.AbsoluteX) * (segmentStart.AbsoluteY - segmentEnd.AbsoluteY)) -
               ((segmentStart.AbsoluteX - segmentEnd.AbsoluteX) * (point.Y - segmentEnd.AbsoluteY));
    }

    private static double GetTriangleArea(
        WallHoleDefinition first,
        WallHoleDefinition second,
        WallHoleDefinition third)
    {
        return Math.Abs(
            ((second.AbsoluteX - first.AbsoluteX) * (third.AbsoluteY - first.AbsoluteY)) -
            ((third.AbsoluteX - first.AbsoluteX) * (second.AbsoluteY - first.AbsoluteY))) / 2d;
    }

    private static double DistanceToTriangle(
        (double X, double Y) point,
        WallHoleDefinition first,
        WallHoleDefinition second,
        WallHoleDefinition third)
    {
        return DistanceToTriangleEdges(point, first, second, third);
    }

    private static double DistanceToTriangleEdges(
        (double X, double Y) point,
        WallHoleDefinition first,
        WallHoleDefinition second,
        WallHoleDefinition third)
    {
        return Math.Min(
            DistanceToSegment(point, first, second),
            Math.Min(
                DistanceToSegment(point, second, third),
                DistanceToSegment(point, third, first)));
    }

    private static double DistanceToSegment(
        (double X, double Y) point,
        WallHoleDefinition segmentStart,
        WallHoleDefinition segmentEnd)
    {
        var deltaX = segmentEnd.AbsoluteX - segmentStart.AbsoluteX;
        var deltaY = segmentEnd.AbsoluteY - segmentStart.AbsoluteY;
        var squaredLength = (deltaX * deltaX) + (deltaY * deltaY);
        if (squaredLength <= 0.000001d)
        {
            return Distance(point.X, point.Y, segmentStart.AbsoluteX, segmentStart.AbsoluteY);
        }

        var projection = (((point.X - segmentStart.AbsoluteX) * deltaX) +
                          ((point.Y - segmentStart.AbsoluteY) * deltaY)) / squaredLength;
        var clampedProjection = Math.Clamp(projection, 0d, 1d);
        var closestX = segmentStart.AbsoluteX + (clampedProjection * deltaX);
        var closestY = segmentStart.AbsoluteY + (clampedProjection * deltaY);
        return Distance(point.X, point.Y, closestX, closestY);
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

    private static double GetCenterConfidence(NextHoldSuggestionRequest request, FootSupportSelection footSupport)
    {
        if (request.CenterX.HasValue && request.CenterY.HasValue)
        {
            return 1.00d;
        }

        if (footSupport.ContainsCenter)
        {
            return footSupport.Biomechanics?.IsReachFeasible == true ? 0.98d : 0.85d;
        }

        return footSupport.FootCount switch
        {
            2 => 0.75d,
            1 => 0.55d,
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
        WallHoleDefinition? firstFoot,
        WallHoleDefinition? secondFoot)
    {
        var confidences = new List<double>
        {
            candidate.HasEstimatedHoldMetadata ? 0.45d : 1.00d,
            movingHole.HasEstimatedHoldMetadata ? 0.65d : 1.00d,
            supportHandHole.HasEstimatedHoldMetadata ? 0.65d : 1.00d
        };

        if (firstFoot.HasValue)
        {
            confidences.Add(firstFoot.Value.HasEstimatedHoldMetadata ? 0.75d : 1.00d);
        }

        if (secondFoot.HasValue)
        {
            confidences.Add(secondFoot.Value.HasEstimatedHoldMetadata ? 0.75d : 1.00d);
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

        if (deltaY < 0 && deltaX < 220d)
        {
            return 1.00d;
        }

        if (deltaY < 40d)
        {
            return 0.70d;
        }

        return 0.35d;
    }

    private static double GetWallDifficulty(double wallAngleDegrees, double extensionRatio, double heightDelta)
    {
        var absoluteAnglePenalty = Math.Abs(wallAngleDegrees) / 45d;
        var overheadPenalty = wallAngleDegrees > 0 ? absoluteAnglePenalty * 1.2d : absoluteAnglePenalty * 0.8d;
        var reachPenalty = extensionRatio > 0.85d ? 0.35d : 0d;
        var upwardPenalty = heightDelta < 0 ? Math.Min(0.25d, -heightDelta / 1200d) : 0d;

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

    private static double GetFootSupportScore(FootSupportSelection footSupport)
    {
        var supports = new[] { footSupport.FirstFoot, footSupport.SecondFoot }
            .Where(hole => hole.HasValue)
            .Select(hole => GetHoldQuality(hole!.Value.HoldType))
            .ToList();

        if (supports.Count == 0)
        {
            return 0.30d;
        }

        return supports.Average();
    }

    private static double GetSupportTriangleScore(FootSupportSelection footSupport)
    {
        if (footSupport.FootCount < 2)
        {
            return footSupport.FootCount == 1 ? 0.25d : 0d;
        }

        if (footSupport.ContainsCenter)
        {
            return 1d + Math.Min(0.35d, footSupport.StabilityMargin / 400d);
        }

        return Math.Max(0d, 1d - (footSupport.DistanceToTriangle / 500d));
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
        var vertical = Math.Abs(deltaY) < 30d ? string.Empty : deltaY < 0 ? "Up" : "Down";

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

    private static string BuildSecondaryReason(
        double supportHandQuality,
        double footSupportScore,
        double transitionScore,
        string movementDirection,
        double difficultyFactor,
        double holdMetadataConfidence,
        double centerConfidence,
        FootSupportSelection footSupport)
    {
        if (footSupport.FootCount >= 2)
        {
            var holes = string.Join(", ", footSupport.GetHoleNumbers());
            if (!footSupport.IsTransitionReachable)
            {
                return footSupport.PreparationBiomechanics?.IsReachFeasible == false
                    ? $"Spostamento preparatorio sui piedi {holes} biomeccanicamente al limite"
                    : $"Movimento della mano con piedi {holes} biomeccanicamente al limite";
            }

            return footSupport.ContainsCenter
                ? $"Baricentro nel triangolo di appoggio con piedi {holes}"
                : $"Triangolo piedi {holes} piu vicino al baricentro: {footSupport.DistanceToTriangle:0} mm";
        }

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

    private sealed record FootSupportSelection(
        WallHoleDefinition? FirstFoot,
        WallHoleDefinition? SecondFoot,
        bool ContainsCenter,
        double DistanceToTriangle,
        double StabilityMargin,
        double TriangleArea,
        BiomechanicalCenterOfMassResult? Biomechanics)
    {
        public static FootSupportSelection Empty { get; } = new(null, null, false, 0d, 0d, 0d, null);

        public bool HasPreparationEvaluation { get; init; }

        public bool PreparationContainsCenter { get; init; }

        public double PreparationDistanceToTriangle { get; init; }

        public double PreparationStabilityMargin { get; init; }

        public BiomechanicalCenterOfMassResult? PreparationBiomechanics { get; init; }

        public int FootMoveCount { get; init; }

        public double FootRepositionDistance { get; init; }

        public int FootCount => (FirstFoot.HasValue ? 1 : 0) + (SecondFoot.HasValue ? 1 : 0);

        public bool IsTransitionReachable =>
            Biomechanics?.IsReachFeasible == true &&
            (!HasPreparationEvaluation || PreparationBiomechanics?.IsReachFeasible == true);

        public double TotalReachPenalty =>
            (Biomechanics?.ReachPenalty ?? 0d) +
            (HasPreparationEvaluation ? PreparationBiomechanics?.ReachPenalty ?? 0d : 0d);

        public IReadOnlyList<int> GetHoleNumbers()
        {
            return GetFeet()
                .Select(foot => foot.Number)
                .OrderBy(number => number)
                .ToList();
        }

        public IReadOnlyList<WallHoleDefinition> GetFeet()
        {
            return new[] { FirstFoot, SecondFoot }
                .Where(foot => foot.HasValue)
                .Select(foot => foot!.Value)
                .OrderBy(foot => foot.Number)
                .ToList();
        }
    }

    private sealed record FootTransition(
        WallHoleDefinition From,
        WallHoleDefinition To);

    private sealed record FootSupportEvaluation(
        bool ContainsCenter,
        double DistanceToTriangle,
        double StabilityMargin,
        double TriangleArea,
        BiomechanicalCenterOfMassResult Biomechanics);

    private static double Distance(double x1, double y1, double x2, double y2)
    {
        var deltaX = x2 - x1;
        var deltaY = y2 - y1;
        return Math.Sqrt((deltaX * deltaX) + (deltaY * deltaY));
    }
}

