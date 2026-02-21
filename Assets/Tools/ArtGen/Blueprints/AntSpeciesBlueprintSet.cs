using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "AntSpeciesBlueprintSet", menuName = "GSP/Art/Blueprints/Ant Species Blueprint Set")]
public sealed class AntSpeciesBlueprintSet : ScriptableObject
{
    [Serializable]
    public sealed class RoleBlueprints
    {
        public string roleId = "worker";
        public PixelBlueprint2D basePose;
        public PixelBlueprint2D baseStripeMask;
    }

    [Serializable]
    public sealed class FrameBlueprint
    {
        public PixelBlueprint2D blueprint;
    }

    [Serializable]
    public sealed class ClipBlueprint
    {
        public string roleId = "worker";
        public string clipId = "idle";
        public int fps = 8;
        public List<FrameBlueprint> frames = new();
    }

    public string speciesId = "black_garden_ant";
    public string displayName = "Black Garden Ant";

    [Header("Morphology (informational)")]
    public float headScale = 1f;
    public float legLength = 1f;
    public float abdomenScale = 1f;
    public float mandibleSize = 1f;

    public List<RoleBlueprints> roles = new();
    public List<ClipBlueprint> clips = new();

    public RoleBlueprints FindRole(string roleId)
    {
        foreach (var role in roles)
        {
            if (string.Equals(role.roleId, roleId, StringComparison.OrdinalIgnoreCase))
            {
                return role;
            }
        }

        return null;
    }

    public List<ClipBlueprint> FindClips(string roleId)
    {
        var outClips = new List<ClipBlueprint>();
        foreach (var clip in clips)
        {
            if (string.Equals(clip.roleId, roleId, StringComparison.OrdinalIgnoreCase))
            {
                outClips.Add(clip);
            }
        }

        return outClips;
    }
}
