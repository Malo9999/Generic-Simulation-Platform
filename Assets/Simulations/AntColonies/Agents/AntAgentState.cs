using System;
using UnityEngine;

public enum AntBehaviorState
{
    Wander = 0,
    ReturnHome = 1,
    Fight = 2
}

[Serializable]
public class AntAgentState
{
    public int id;
    public int speciesId;
    public int teamId;
    public int homeNestId;
    public EntityIdentity identity;

    public Vector2 position;
    public Vector2 velocity;

    public float hp;
    public float maxHp;
    public int ageTicks;

    public bool carrying;
    public int carriedAmount;

    public AntBehaviorState state;
    public int fightTicksRemaining;
    public int fightTargetAntId;
    public int fightTargetNestId;

    public int nextTurnTick;
    public float wanderHeading;

    public bool isAlive = true;
}
