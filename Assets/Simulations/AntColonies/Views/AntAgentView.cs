using UnityEngine;

public class AntAgentView
{
    public int antId;
    public Transform root;
    public GameObject pipelineRenderer;
    public SpriteRenderer baseRenderer;
    public SpriteRenderer maskRenderer;
    public SpriteRenderer hpBgRenderer;
    public SpriteRenderer hpFillRenderer;
    public VisualKey visualKey;
    public Vector2 lastPos;
    public bool hasLastPos;
}
