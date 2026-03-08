using System.Collections.Generic;
using UnityEngine;

public sealed class NeuralSlimeMoldRenderer : MonoBehaviour
{
    [Header("Agent Visuals")]
    [SerializeField] private float agentScale = 0.2f;
    [SerializeField] private int maxVisibleAgents = 1500;
    [SerializeField] private Color agentColor = new(0.65f, 1f, 0.86f, 0.85f);

    [Header("Field Visuals")]
    [SerializeField] private Color fieldLowColor = new(0.03f, 0.05f, 0.09f, 1f);
    [SerializeField] private Color fieldHighColor = new(0.25f, 0.95f, 0.68f, 0.95f);
    [SerializeField] private float fieldExposure = 1.6f;
    [SerializeField] private int fieldTextureRefreshInterval = 1;

    [Header("Palette")]
    [SerializeField] private bool useGlowAgentShape = true;
    [SerializeField] private bool useFieldBlobSpriteForFieldOverlay = true;

    private readonly List<SpriteRenderer> agentRenderers = new();
    private readonly List<SpriteRenderer> foodNodeRenderers = new();
    private Texture2D fieldTexture;
    private SpriteRenderer fieldRenderer;
    private int frameCounter;
    private Vector2 worldSize;

    public void SetShapeToggles(bool glowAgents, bool fieldBlobOverlay)
    {
        useGlowAgentShape = glowAgents;
        useFieldBlobSpriteForFieldOverlay = fieldBlobOverlay;
    }

    public void Build(NeuralSlimeMoldRunner runner)
    {
        ClearChildren();

        if (runner == null || runner.Field == null)
        {
            return;
        }

        worldSize = runner.Field.WorldSize;
        BuildField(worldSize, runner.Field.Width, runner.Field.Height);
        BuildAgents(runner.AgentCount);
        BuildFoodNodes(runner.FoodNodes, runner.FoodRadius);
    }

    public void Render(NeuralSlimeMoldRunner runner)
    {
        if (runner == null || runner.Field == null)
        {
            return;
        }

        var agents = runner.Agents;
        var count = Mathf.Min(agentRenderers.Count, agents.Length);
        for (var i = 0; i < count; i++)
        {
            var state = agents[i];
            var t = agentRenderers[i].transform;
            t.localPosition = new Vector3(state.position.x, state.position.y, -0.1f);
            t.localRotation = Quaternion.Euler(0f, 0f, state.heading * Mathf.Rad2Deg);
        }

        var foodNodes = runner.FoodNodes;
        for (var i = 0; i < foodNodeRenderers.Count; i++)
        {
            var active = i < foodNodes.Length;
            foodNodeRenderers[i].gameObject.SetActive(active);
            if (active)
            {
                var clamped = ClampNodeMarker(foodNodes[i]);
                foodNodeRenderers[i].transform.localPosition = new Vector3(clamped.x, clamped.y, -0.3f);
            }
        }

        frameCounter++;
        if (fieldTextureRefreshInterval <= 1 || frameCounter % fieldTextureRefreshInterval == 0)
        {
            UpdateFieldTexture(runner.Field);
        }
    }

    private void BuildField(Vector2 worldSize, int width, int height)
    {
        var fieldGo = new GameObject("Field");
        fieldGo.transform.SetParent(transform, false);
        fieldRenderer = fieldGo.AddComponent<SpriteRenderer>();
        fieldRenderer.sortingOrder = -20;

        fieldTexture = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        var sprite = Sprite.Create(fieldTexture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), width / worldSize.x);
        fieldRenderer.sprite = sprite;
        fieldRenderer.color = Color.white;

        if (useFieldBlobSpriteForFieldOverlay && ShapeLibraryProvider.TryGetSprite(ShapeId.FieldBlob, out var blobSprite))
        {
            fieldRenderer.drawMode = SpriteDrawMode.Tiled;
            fieldRenderer.size = worldSize;
            fieldRenderer.sprite = blobSprite;
            fieldRenderer.color = new Color(0.2f, 0.34f, 0.28f, 0.2f);

            var fieldTextureGo = new GameObject("FieldDensityTexture");
            fieldTextureGo.transform.SetParent(transform, false);
            var densityRenderer = fieldTextureGo.AddComponent<SpriteRenderer>();
            densityRenderer.sortingOrder = -19;
            densityRenderer.sprite = sprite;
            densityRenderer.color = Color.white;
            fieldRenderer = densityRenderer;
        }
    }

    private void BuildAgents(int totalAgents)
    {
        var visibleCount = Mathf.Min(Mathf.Max(1, maxVisibleAgents), totalAgents);
        ShapeLibraryProvider.TryGetSprite(useGlowAgentShape ? ShapeId.DotGlowSmall : ShapeId.DotCore, out var agentSprite);
        if (agentSprite == null)
        {
            ShapeLibraryProvider.TryGetSprite(ShapeId.DotCore, out agentSprite);
        }

        for (var i = 0; i < visibleCount; i++)
        {
            var go = new GameObject($"Agent_{i:0000}");
            go.transform.SetParent(transform, false);
            go.transform.localScale = Vector3.one * agentScale;

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = 5;
            sr.sprite = agentSprite;
            sr.color = agentColor;
            agentRenderers.Add(sr);
        }
    }

    private void BuildFoodNodes(Vector2[] foodNodes, float foodRadius)
    {
        if (!ShapeLibraryProvider.TryGetSprite(ShapeId.DotGlowSmall, out var markerSprite))
        {
            ShapeLibraryProvider.TryGetSprite(ShapeId.DotCore, out markerSprite);
        }

        if (markerSprite == null)
        {
            return;
        }

        var markerScale = Mathf.Clamp(foodRadius * 0.1f, 0.18f, 0.55f);

        for (var i = 0; i < foodNodes.Length; i++)
        {
            var go = new GameObject($"FoodNode_{i:00}");
            go.transform.SetParent(transform, false);
            go.transform.localScale = Vector3.one * markerScale;

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = 2;
            sr.sprite = markerSprite;
            sr.color = new Color(0.95f, 0.85f, 0.35f, 0.65f);
            foodNodeRenderers.Add(sr);
        }
    }

    private Vector2 ClampNodeMarker(Vector2 position)
    {
        var halfX = (worldSize.x * 0.5f) - 0.6f;
        var halfY = (worldSize.y * 0.5f) - 0.6f;

        position.x = Mathf.Clamp(position.x, -halfX, halfX);
        position.y = Mathf.Clamp(position.y, -halfY, halfY);
        return position;
    }

    private void UpdateFieldTexture(NeuralFieldGrid field)
    {
        var values = field.Raw;
        if (values == null || values.Length == 0 || fieldTexture == null)
        {
            return;
        }

        var max = 0.001f;
        for (var i = 0; i < values.Length; i++)
        {
            if (values[i] > max)
            {
                max = values[i];
            }
        }

        var pixels = fieldTexture.GetPixels32();
        for (var i = 0; i < values.Length; i++)
        {
            var normalized = Mathf.Clamp01((values[i] / max) * fieldExposure);
            var color = Color.Lerp(fieldLowColor, fieldHighColor, normalized);
            color.a = normalized;
            pixels[i] = color;
        }

        fieldTexture.SetPixels32(pixels);
        fieldTexture.Apply(false, false);
    }

    private void ClearChildren()
    {
        agentRenderers.Clear();
        foodNodeRenderers.Clear();

        for (var i = transform.childCount - 1; i >= 0; i--)
        {
            var child = transform.GetChild(i);
            if (Application.isPlaying)
            {
                Destroy(child.gameObject);
            }
            else
            {
                DestroyImmediate(child.gameObject);
            }
        }

        if (fieldTexture != null)
        {
            if (Application.isPlaying)
            {
                Destroy(fieldTexture);
            }
            else
            {
                DestroyImmediate(fieldTexture);
            }

            fieldTexture = null;
        }

        fieldRenderer = null;
        frameCounter = 0;
        worldSize = Vector2.one;
    }
}
