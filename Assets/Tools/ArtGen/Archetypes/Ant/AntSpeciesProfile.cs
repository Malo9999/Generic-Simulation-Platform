using System;
using UnityEngine;

[Serializable]
public sealed class AntSpeciesProfile
{
    public string speciesId;
    public string displayName;
    public string baseColorId;
    public float headScale = 1f;
    public float thoraxScale = 1f;
    public float abdomenScale = 1f;
    public float legLengthScale = 1f;
    public float antennaLengthScale = 1f;
    public float petiolePinchStrength = 1f;
    public float soldierHeadMultiplier = 1.2f;
    public float soldierMandibleMultiplier = 1.2f;
    public float queenAbdomenMultiplier = 1.3f;
    public ReferencePack2D referencePack;
}
