using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "ReferencePack2D", menuName = "GSP/Art/References/Reference Pack 2D")]
public sealed class ReferencePack2D : ScriptableObject
{
    public string packKind = "ant";
    public string speciesId;
    public string displayName;
    public List<StateSheet> sheets = new();
    [TextArea(2, 6)]
    public string notes;
    public string version = "v1";

    [Serializable]
    public sealed class StateSheet
    {
        public string stateId;
        public Texture2D texture;
        public int frameCount;
        public int fps;
    }
}
