using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "ArachnidSpeciesLibrary", menuName = "GSP/Art/Archetypes/Arachnid Species Library")]
public sealed class ArachnidSpeciesLibrary : ScriptableObject
{
    public List<ArachnidSpeciesProfile> profiles = new();
}
