using RouteLab.Models;

namespace RouteLab.Services;

public sealed class BiomechanicalCenterOfMassService : IBiomechanicalCenterOfMassService
{
    private const double Gravity = 9.80665d;

    public BiomechanicalCenterOfMassResult Estimate(BiomechanicalPoseRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Wall);
        ArgumentNullException.ThrowIfNull(request.Climber);

        var height = Math.Clamp(request.Climber.HeightMm, 1200d, 2300d);
        var armSpan = Math.Clamp(request.Climber.ArmSpanMm, 1200d, 2500d);
        var mass = Math.Clamp(request.Climber.MassKg, 30d, 180d);
        var wallDistance = Math.Clamp(request.Climber.BodyDistanceFromWallMm, 50d, 600d);

        var leftHand = ToPoint(request.LeftHand);
        var rightHand = ToPoint(request.RightHand);
        var orderedFeet = new[] { request.FirstFoot, request.SecondFoot }
            .OrderBy(foot => foot.AbsoluteX)
            .ToList();
        var leftFoot = ToPoint(orderedFeet[0]);
        var rightFoot = ToPoint(orderedFeet[1]);

        var handCenter = Midpoint(leftHand, rightHand);
        var footCenter = Midpoint(leftFoot, rightFoot);
        var bodyAxis = Normalize(footCenter - handCenter, new Point2(0d, 1d));
        var leftAxis = new Point2(-bodyAxis.Y, bodyAxis.X);

        var shoulderWidth = height * 0.23d;
        var pelvisWidth = height * 0.17d;
        var trunkLength = height * 0.288d;
        var headLength = height * 0.13d;
        var armReach = Math.Max(height * 0.32d, (armSpan - shoulderWidth) / 2d);
        var upperArmLength = armReach * 0.423d;
        var forearmAndHandLength = armReach - upperArmLength;
        var thighLength = height * 0.245d;
        var shankAndFootLength = height * 0.246d;
        var legLength = thighLength + shankAndFootLength;

        var anchorDistance = Length(footCenter - handCenter);
        var shoulderOffset = ((armReach * 0.55d) + anchorDistance - trunkLength - (legLength * 0.58d)) / 2d;
        shoulderOffset = Math.Clamp(shoulderOffset, 0d, armReach * 0.85d);
        var shoulderCenter = handCenter + (bodyAxis * shoulderOffset);
        var hipCenter = shoulderCenter + (bodyAxis * trunkLength);
        var torsoCenter = Midpoint(shoulderCenter, hipCenter);

        var leftShoulder = shoulderCenter + (leftAxis * (shoulderWidth / 2d));
        var rightShoulder = shoulderCenter - (leftAxis * (shoulderWidth / 2d));
        var leftHip = hipCenter + (leftAxis * (pelvisWidth / 2d));
        var rightHip = hipCenter - (leftAxis * (pelvisWidth / 2d));

        var leftElbow = SolveJoint(leftShoulder, leftHand, upperArmLength, forearmAndHandLength, torsoCenter, leftAxis);
        var rightElbow = SolveJoint(rightShoulder, rightHand, upperArmLength, forearmAndHandLength, torsoCenter, -leftAxis);
        var leftKnee = SolveJoint(leftHip, leftFoot, thighLength, shankAndFootLength, torsoCenter, leftAxis);
        var rightKnee = SolveJoint(rightHip, rightFoot, thighLength, shankAndFootLength, torsoCenter, -leftAxis);

        var accumulator = new CenterOfMassAccumulator();

        // Sex-neutral averages of de Leva's adjusted Zatsiorsky-Seluyanov segment data.
        accumulator.Add(Lerp(shoulderCenter, hipCenter, 0.5051d), 0.43015d);
        accumulator.Add(shoulderCenter - (bodyAxis * (headLength * 0.49215d)), 0.06810d);
        accumulator.Add(Lerp(leftShoulder, leftElbow, 0.57630d), 0.02630d);
        accumulator.Add(Lerp(rightShoulder, rightElbow, 0.57630d), 0.02630d);
        accumulator.Add(Lerp(leftElbow, leftHand, 0.45665d), 0.01500d);
        accumulator.Add(Lerp(rightElbow, rightHand, 0.45665d), 0.01500d);
        accumulator.Add(leftHand, 0.00585d);
        accumulator.Add(rightHand, 0.00585d);
        accumulator.Add(Lerp(leftHip, leftKnee, 0.38535d), 0.14470d);
        accumulator.Add(Lerp(rightHip, rightKnee, 0.38535d), 0.14470d);
        accumulator.Add(Lerp(leftKnee, leftFoot, 0.43735d), 0.04570d);
        accumulator.Add(Lerp(rightKnee, rightFoot, 0.43735d), 0.04570d);
        accumulator.Add(leftFoot, 0.01330d);
        accumulator.Add(rightFoot, 0.01330d);

        var center = accumulator.Resolve();
        var inclination = Math.Clamp(request.WallInclinationDegrees, -45d, 60d);
        var inclinationRadians = inclination * Math.PI / 180d;
        var effectiveCenterY = center.Y + (wallDistance * Math.Tan(inclinationRadians));
        if (request.Wall.Height > 0d)
        {
            effectiveCenterY = Math.Clamp(effectiveCenterY, 0d, request.Wall.Height);
        }

        var reachPenalty =
            CalculateReachPenalty(leftShoulder, leftHand, upperArmLength, forearmAndHandLength) +
            CalculateReachPenalty(rightShoulder, rightHand, upperArmLength, forearmAndHandLength) +
            CalculateReachPenalty(leftHip, leftFoot, thighLength, shankAndFootLength) +
            CalculateReachPenalty(rightHip, rightFoot, thighLength, shankAndFootLength);
        reachPenalty /= 4d;

        var weightNewton = mass * Gravity;
        var normalGravityForce = weightNewton * Math.Abs(Math.Sin(inclinationRadians));
        var gravityTorque = weightNewton * (wallDistance / 1000d) * Math.Abs(Math.Cos(inclinationRadians));

        return new BiomechanicalCenterOfMassResult
        {
            CenterX = center.X,
            CenterY = center.Y,
            EffectiveCenterX = center.X,
            EffectiveCenterY = effectiveCenterY,
            WallNormalDistanceMm = wallDistance,
            NormalGravityForceNewton = normalGravityForce,
            GravityTorqueNewtonMeter = gravityTorque,
            ReachPenalty = reachPenalty
        };
    }

    private static double CalculateReachPenalty(Point2 proximal, Point2 distal, double firstLength, double secondLength)
    {
        var distance = Length(distal - proximal);
        var maximumReach = firstLength + secondLength;
        var minimumReach = Math.Abs(firstLength - secondLength);
        if (distance > maximumReach)
        {
            return (distance - maximumReach) / Math.Max(1d, maximumReach);
        }

        if (distance < minimumReach)
        {
            return (minimumReach - distance) / Math.Max(1d, maximumReach);
        }

        return 0d;
    }

    private static Point2 SolveJoint(
        Point2 proximal,
        Point2 distal,
        double firstLength,
        double secondLength,
        Point2 bodyCenter,
        Point2 outwardDirection)
    {
        var delta = distal - proximal;
        var actualDistance = Length(delta);
        var direction = Normalize(delta, outwardDirection);
        var minimumDistance = Math.Abs(firstLength - secondLength) + 0.001d;
        var maximumDistance = Math.Max(minimumDistance, firstLength + secondLength - 0.001d);
        var solvedDistance = Math.Clamp(actualDistance, minimumDistance, maximumDistance);
        var along = ((firstLength * firstLength) - (secondLength * secondLength) + (solvedDistance * solvedDistance)) /
                    (2d * solvedDistance);
        var height = Math.Sqrt(Math.Max(0d, (firstLength * firstLength) - (along * along)));
        var basePoint = proximal + (direction * along);
        var perpendicular = new Point2(-direction.Y, direction.X);
        var firstCandidate = basePoint + (perpendicular * height);
        var secondCandidate = basePoint - (perpendicular * height);
        return Dot(firstCandidate - bodyCenter, outwardDirection) >= Dot(secondCandidate - bodyCenter, outwardDirection)
            ? firstCandidate
            : secondCandidate;
    }

    private static Point2 ToPoint(WallHoleDefinition hole) => new(hole.AbsoluteX, hole.AbsoluteY);

    private static Point2 Midpoint(Point2 first, Point2 second) => (first + second) * 0.5d;

    private static Point2 Lerp(Point2 first, Point2 second, double amount) => first + ((second - first) * amount);

    private static double Length(Point2 point) => Math.Sqrt((point.X * point.X) + (point.Y * point.Y));

    private static Point2 Normalize(Point2 point, Point2 fallback)
    {
        var length = Length(point);
        return length <= 0.000001d ? fallback : point * (1d / length);
    }

    private static double Dot(Point2 first, Point2 second) => (first.X * second.X) + (first.Y * second.Y);

    private readonly record struct Point2(double X, double Y)
    {
        public static Point2 operator +(Point2 left, Point2 right) => new(left.X + right.X, left.Y + right.Y);

        public static Point2 operator -(Point2 left, Point2 right) => new(left.X - right.X, left.Y - right.Y);

        public static Point2 operator -(Point2 value) => new(-value.X, -value.Y);

        public static Point2 operator *(Point2 value, double factor) => new(value.X * factor, value.Y * factor);
    }

    private sealed class CenterOfMassAccumulator
    {
        private double weightedX;
        private double weightedY;
        private double totalMassFraction;

        public void Add(Point2 point, double massFraction)
        {
            weightedX += point.X * massFraction;
            weightedY += point.Y * massFraction;
            totalMassFraction += massFraction;
        }

        public Point2 Resolve()
        {
            return totalMassFraction <= 0d
                ? new Point2()
                : new Point2(weightedX / totalMassFraction, weightedY / totalMassFraction);
        }
    }
}
