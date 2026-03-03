using System;
using UnityEngine;

[Serializable]
public class FantasySportRules
{
    public struct FieldGeometry
    {
        public Rect playable;
        public Rect goalLeft;
        public Rect goalRight;
        public float halfWidth;
        public float halfHeight;
        public float goalMouthHalfH;
        public float goalDepth;
    }

    public float athleteSpeedOffense = 7.5f;
    public float athleteSpeedDefense = 6.5f;
    public float accel = 18f;
    public float pickupRadius = 1.25f;
    public float tackleRadius = 1.35f;
    public float tackleImpulse = 11f;
    public float tackleCooldownSeconds = 1.5f;
    public float stunSeconds = 0.7f;
    public float ballDamping = 1.2f;
    public float ballBounce = 0.85f;
    public float goalDepth = 1.5f;
    public float goalHeight = 12f;
    public float matchSeconds = 120f;
    public float carrierForwardOffset = 0.8f;

    public static FieldGeometry Compute(ScenarioConfig cfg, float halfWidth, float halfHeight)
    {
        const float playableInset = 0.6f;
        var goalMouthHalfH = Mathf.Clamp(halfHeight * 0.18f, 3.5f, 5.0f);
        var goalRectHalfH = goalMouthHalfH * 1.5f;
        var goalDepth = Mathf.Clamp(halfWidth * 0.12f, 5.5f, 8.5f);

        var playable = Rect.MinMaxRect(
            -halfWidth + playableInset,
            -halfHeight + playableInset,
            halfWidth - playableInset,
            halfHeight - playableInset);

        var goalLeft = Rect.MinMaxRect(
            -halfWidth,
            -goalRectHalfH,
            -halfWidth + goalDepth,
            goalRectHalfH);

        var goalRight = Rect.MinMaxRect(
            halfWidth - goalDepth,
            -goalRectHalfH,
            halfWidth,
            goalRectHalfH);

        return new FieldGeometry
        {
            playable = playable,
            goalLeft = goalLeft,
            goalRight = goalRight,
            halfWidth = halfWidth,
            halfHeight = halfHeight,
            goalMouthHalfH = goalMouthHalfH,
            goalDepth = goalDepth
        };
    }

    public static bool IsGoal(Rect goalRect, Vector2 prevBall, Vector2 ball)
    {
        return !goalRect.Contains(prevBall) && goalRect.Contains(ball);
    }

    public static void ClampToPlayable(ref Vector2 pos, ref Vector2 vel, Rect playable)
    {
        var clampedX = pos.x < playable.xMin || pos.x > playable.xMax;
        var clampedY = pos.y < playable.yMin || pos.y > playable.yMax;

        pos.x = Mathf.Clamp(pos.x, playable.xMin, playable.xMax);
        pos.y = Mathf.Clamp(pos.y, playable.yMin, playable.yMax);

        if (clampedX)
        {
            vel.x = 0f;
        }

        if (clampedY)
        {
            vel.y = 0f;
        }
    }
}
