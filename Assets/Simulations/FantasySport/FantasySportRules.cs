using System;
using UnityEngine;

[Serializable]
public class FantasySportRules
{
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
}
