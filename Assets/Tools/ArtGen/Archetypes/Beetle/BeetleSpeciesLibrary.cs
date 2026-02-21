using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "BeetleSpeciesLibrary", menuName = "GSP/Art/Archetypes/Beetle Species Library")]
public sealed class BeetleSpeciesLibrary : ScriptableObject
{
    public List<BeetleSpeciesProfile> profiles = new();
}
