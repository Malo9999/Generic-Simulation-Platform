using UnityEngine;

public static class FantasySportTactics
{
    public enum TeamPhase
    {
        BuildUp,
        Advance,
        FinalThird,
        Transition
    }

    public enum RoleGroup
    {
        Sweeper,
        Defender,
        Midfielder,
        Attacker,
        Keeper
    }

    private static readonly float[] LaneFractions = { -0.44f, -0.22f, 0f, 0.22f, 0.44f };

    public static TeamPhase ComputeTeamPhase(bool hasPossession, bool ballFree, float progress01)
    {
        if (ballFree || !hasPossession)
        {
            return TeamPhase.Transition;
        }

        if (progress01 < 0.33f)
        {
            return TeamPhase.BuildUp;
        }

        if (progress01 <= 0.66f)
        {
            return TeamPhase.Advance;
        }

        return TeamPhase.FinalThird;
    }

    public static Vector2 GetHomeTarget(RoleGroup role, int lane, int teamId, TeamPhase phase, Vector2 ballPos, float halfWidth, float halfHeight, float endzoneDepth, float progress01)
    {
        lane = Mathf.Clamp(lane, 0, LaneFractions.Length - 1);
        var laneFrac = LaneFractions[lane];
        var attacking = phase == TeamPhase.Advance || phase == TeamPhase.FinalThird;
        var defensive = phase == TeamPhase.Transition || phase == TeamPhase.BuildUp;

        var wingWidth = halfHeight * (attacking ? 0.42f : 0.34f);
        var centerWidth = halfHeight * 0.18f;
        var laneY = Mathf.Lerp(centerWidth * Mathf.Sign(laneFrac), laneFrac * wingWidth, Mathf.Abs(laneFrac) * 1.6f);

        var ballSlideY = Mathf.Clamp(ballPos.y * 0.20f, -halfHeight * 0.12f, halfHeight * 0.12f);
        laneY += ballSlideY;
        if (defensive)
        {
            laneY *= 0.88f;
        }

        var towardCenter = teamId == 0 ? 1f : -1f;
        var ownGoalX = teamId == 0 ? -halfWidth : halfWidth;
        var defDepth = endzoneDepth + 7f;
        var midDepth = endzoneDepth + 16f;
        var attDepth = endzoneDepth + 25f;
        var boxDepth = endzoneDepth + 30f;

        var pushByPhase = phase switch
        {
            TeamPhase.BuildUp => Mathf.Lerp(-0.5f, 3.5f, progress01),
            TeamPhase.Advance => Mathf.Lerp(2f, 8f, progress01),
            TeamPhase.FinalThird => Mathf.Lerp(7f, 11f, progress01),
            _ => 0f
        };

        var depthFromGoal = role switch
        {
            RoleGroup.Keeper => endzoneDepth * 0.56f,
            RoleGroup.Sweeper => defDepth - 2.5f,
            RoleGroup.Defender => defDepth,
            RoleGroup.Midfielder => midDepth,
            RoleGroup.Attacker => phase == TeamPhase.FinalThird ? boxDepth : attDepth,
            _ => midDepth
        };

        if (role == RoleGroup.Attacker && lane == 2)
        {
            laneY *= 0.65f;
        }

        var homeX = ownGoalX + (towardCenter * (depthFromGoal + pushByPhase));
        homeX = Mathf.Clamp(homeX, -halfWidth + 1.1f, halfWidth - 1.1f);
        laneY = Mathf.Clamp(laneY, -halfHeight + 2f, halfHeight - 2f);
        return new Vector2(homeX, laneY);
    }
}
