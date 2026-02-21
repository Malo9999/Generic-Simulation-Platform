using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "AntSpeciesLibrary", menuName = "GSP/Art/Archetypes/Ant Species Library")]
public sealed class AntSpeciesLibrary : ScriptableObject
{
    public List<AntSpeciesProfile> profiles = new();
}
