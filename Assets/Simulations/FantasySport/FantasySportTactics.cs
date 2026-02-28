using UnityEngine;

public static class FantasySportTactics
{
    public enum TeamPhase
    {
        Transition,
        BuildUp,
        Advance,
        FinalThird
    }

    public enum RoleGroup
    {
        Keeper,
        Sweeper,
        Defender,
        Midfielder,
        Attacker
    }

    public enum Lane
    {
        Left,
        LeftCenter,
        Center,
        RightCenter,
        Right
    }

    public enum IntentType
    {
        None,
        HoldWidth,
        SupportShort,
        OverlapRun,
        UnderlapRun,
        RunInBehind,
        SwitchOutlet,
        ResetBehindBall,
        MarkLane
    }

    public static float Progress01(Vector2 ballPos, int teamId, float halfWidth)
    {
        var progress = teamId == 0
            ? (ballPos.x + halfWidth) / (2f * halfWidth)
            : (-ballPos.x + halfWidth) / (2f * halfWidth);
        return Mathf.Clamp01(progress);
    }

    public static TeamPhase ComputePhase(bool hasPossession, bool ballFree, float progress01)
    {
        if (ballFree)
        {
            return TeamPhase.Transition;
        }

        if (!hasPossession)
        {
            return TeamPhase.Transition;
        }

        if (progress01 < 0.33f)
        {
            return TeamPhase.BuildUp;
        }

        if (progress01 < 0.66f)
        {
            return TeamPhase.Advance;
        }

        return TeamPhase.FinalThird;
    }

    public static float LaneY(Lane lane, float halfHeight)
    {
        var wingMax = halfHeight * 0.44f;
        var halfSp = halfHeight * 0.22f;
        return lane switch
        {
            Lane.Left => -wingMax,
            Lane.LeftCenter => -halfSp,
            Lane.Center => 0f,
            Lane.RightCenter => halfSp,
            Lane.Right => wingMax,
            _ => 0f
        };
    }

    public static float LineX(RoleGroup role, TeamPhase phase, float progress01, float endzoneDepth)
    {
        var baseDef = endzoneDepth + 7f;
        var baseMid = endzoneDepth + 15f;
        var baseAtt = endzoneDepth + 23f;
        var push = phase switch
        {
            TeamPhase.BuildUp => 1f + (progress01 * 3f),
            TeamPhase.Advance => 4f + (progress01 * 6f),
            TeamPhase.FinalThird => 8f + (progress01 * 9f),
            TeamPhase.Transition => 2f,
            _ => 2f
        };

        return role switch
        {
            RoleGroup.Defender => baseDef + (push * 0.7f),
            RoleGroup.Midfielder => baseMid + (push * 0.95f),
            RoleGroup.Attacker => baseAtt + (push * 1.1f),
            RoleGroup.Sweeper => endzoneDepth + 4f,
            RoleGroup.Keeper => endzoneDepth * 0.5f,
            _ => baseMid
        };
    }

    public static Vector2 HomeTarget(RoleGroup role, Lane lane, int teamId, TeamPhase phase, float progress01, float halfWidth, float halfHeight, float endzoneDepth, Vector2 ballPos)
    {
        var ownGoalX = teamId == 0 ? -halfWidth : halfWidth;
        var towardCenter = teamId == 0 ? 1f : -1f;
        var depth = LineX(role, phase, progress01, endzoneDepth);
        var x = ownGoalX + (towardCenter * depth);
        var y = LaneY(lane, halfHeight);
        y += Mathf.Clamp(ballPos.y * 0.12f, -halfHeight * 0.06f, halfHeight * 0.06f);

        return new Vector2(
            Mathf.Clamp(x, -halfWidth + 1.1f, halfWidth - 1.1f),
            Mathf.Clamp(y, -halfHeight + 2f, halfHeight - 2f));
    }

    // Compatibility wrappers used by existing runner.
    public static TeamPhase ComputeTeamPhase(bool hasPossession, bool ballFree, float progress01) => ComputePhase(hasPossession, ballFree, progress01);

    public static Vector2 GetHomeTarget(RoleGroup role, int lane, int teamId, TeamPhase phase, Vector2 ballPos, float halfWidth, float halfHeight, float endzoneDepth, float progress01)
    {
        return HomeTarget(role, (Lane)Mathf.Clamp(lane, 0, 4), teamId, phase, progress01, halfWidth, halfHeight, endzoneDepth, ballPos);
    }
}
