using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Tools/Reference Fetch/Reference Needs", fileName = "ReferenceNeeds")]
public sealed class ReferenceNeeds : ScriptableObject
{
    public string simulationName = "AntColonies";
    public List<string> assets = new() { "FireAnt", "CarpenterAnt", "PharaohAnt" };
    public int imagesPerAsset = 12;
    public bool allowCCBY;
    public bool allowCCBYSA;
    public bool allowCC0 = true;
    public bool allowPublicDomain = true;
    public int minWidth = 800;
}
