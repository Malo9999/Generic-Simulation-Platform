using UnityEngine;

public static class AntPoseLibrary
{
    public readonly struct AntPose
    {
        public readonly int bodyShiftX;
        public readonly int bodyShiftY;
        public readonly int forwardLeanPx;
        public readonly int[] legReach;
        public readonly int[] legLift;
        public readonly int[] antennaSweep;
        public readonly int mandibleSpread;

        public AntPose(int bodyShiftX, int bodyShiftY, int forwardLeanPx, int[] legReach, int[] legLift, int[] antennaSweep, int mandibleSpread)
        {
            this.bodyShiftX = bodyShiftX;
            this.bodyShiftY = bodyShiftY;
            this.forwardLeanPx = forwardLeanPx;
            this.legReach = legReach;
            this.legLift = legLift;
            this.antennaSweep = antennaSweep;
            this.mandibleSpread = mandibleSpread;
        }
    }

    private static readonly int[] TripodAReach = { -1, 2, -1, 1, -2, 1 };
    private static readonly int[] TripodBReach = { 1, -2, 1, -1, 2, -1 };
    private static readonly int[] TripodNeutralReach = { 0, 0, 0, 0, 0, 0 };

    private static readonly int[] TripodALift = { 0, -1, 1, 0, -1, 1 };
    private static readonly int[] TripodBLift = { 0, 1, -1, 0, 1, -1 };
    private static readonly int[] TripodNeutralLift = { 0, 0, 0, 0, 0, 0 };

    public static AntPose Get(string state, int frameIndex)
    {
        switch ((state ?? string.Empty).ToLowerInvariant())
        {
            case "idle":
                return (Mathf.Abs(frameIndex) % 2) == 0
                    ? new AntPose(0, 0, 0, TripodNeutralReach, TripodNeutralLift, new[] { -1, 1 }, 0)
                    : new AntPose(0, 0, 0, TripodNeutralReach, TripodNeutralLift, new[] { 1, -1 }, 0);
            case "walk":
                return Mathf.Abs(frameIndex % 3) switch
                {
                    0 => new AntPose(0, 0, 0, TripodAReach, TripodALift, new[] { -1, 1 }, 0),
                    1 => new AntPose(0, 0, 0, TripodNeutralReach, TripodNeutralLift, new[] { 0, 0 }, 0),
                    _ => new AntPose(0, 0, 0, TripodBReach, TripodBLift, new[] { 1, -1 }, 0)
                };
            case "run":
                return Mathf.Abs(frameIndex % 4) switch
                {
                    0 => new AntPose(0, -1, -1, new[] { -2, 3, -1, 2, -3, 1 }, TripodALift, new[] { -1, 1 }, 0),
                    1 => new AntPose(0, -1, -1, new[] { -1, 1, -1, 1, -1, 1 }, TripodNeutralLift, new[] { 0, 0 }, 0),
                    2 => new AntPose(0, -1, -1, new[] { 1, -1, 1, -1, 1, -1 }, TripodNeutralLift, new[] { 0, 0 }, 0),
                    _ => new AntPose(0, -1, -1, new[] { 2, -3, 1, -2, 3, -1 }, TripodBLift, new[] { 1, -1 }, 0)
                };
            case "fight":
                return new AntPose(0, 0, 0, new[] { -2, 0, 1, 2, 0, 1 }, new[] { -1, 0, 0, -1, 0, 0 }, new[] { -1, 1 }, 3);
            default:
                return new AntPose(0, 0, 0, TripodNeutralReach, TripodNeutralLift, new[] { 0, 0 }, 0);
        }
    }
}
