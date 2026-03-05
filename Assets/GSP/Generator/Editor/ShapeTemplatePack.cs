using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "GSP/Generator/Shape Template Pack", fileName = "ShapeTemplatePack")]
public class ShapeTemplatePack : ScriptableObject
{
    public Color tint = Color.white;
    public List<ShapeTemplateBase> templates = new();
}
