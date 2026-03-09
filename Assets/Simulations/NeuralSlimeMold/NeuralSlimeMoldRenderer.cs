using System.Collections.Generic;
using UnityEngine;

public sealed class NeuralSlimeMoldRenderer : MonoBehaviour
{
    [Header("Agent Visuals")]
    [SerializeField] private float agentScale = 0.2f;
    [SerializeField] private int maxVisibleAgents = 1500;
    [SerializeField] private Color agentColor = new(0.65f, 1f, 0.86f, 0.85f);

    [Header("Field Visuals")]
    [SerializeField] private Color fieldLowColor = new(0.01f, 0.02f, 0.04f, 1f);
    [SerializeField] private Color fieldHighColor = new(0.2f, 0.98f, 0.72f, 0.98f);
    [SerializeField] private float fieldExposure = 1.6f;
    [SerializeField] private int fieldTextureRefreshInterval = 1;

    [Header("World Marker Visuals")]
    [SerializeField] private Color foodActiveColor = new(0.85f, 1f, 0.23f, 1f);
    [SerializeField] private Color foodDepletedColor = new(0.66f, 0.35f, 0.16f, 0.92f);
    [SerializeField] private Color obstacleColor = new(0.4f, 0.48f, 0.55f, 0.9f);
    [SerializeField] private bool showFoodMarkers = true;
    [SerializeField, Min(0.1f)] private float foodMarkerScale = 0.42f;
    [SerializeField, Range(0f, 1f)] private float foodMarkerMinAlpha = 0.75f;

    [Header("Food Marker State Styling")]
    [SerializeField, Range(0.2f, 1.5f)] private float depletedScaleMultiplier = 0.72f;
    [SerializeField, Range(0.1f, 1f)] private float depletedAlphaMultiplier = 0.55f;
    [SerializeField, Range(0.2f, 2f)] private float activeScaleBoost = 1.08f;
    [SerializeField, Range(0f, 0.5f)] private float depletedThreshold01 = 0.08f;

    private bool foodInfluenceDebugVisuals;
    private Color backgroundColor = new(0.01f, 0.02f, 0.04f, 1f);

    [Header("Palette")]
    [SerializeField] private bool useGlowAgentShape = true;
    [SerializeField] private bool useFieldBlobSpriteForFieldOverlay = true;

    private readonly List<SpriteRenderer> agentRenderers = new();
    private readonly List<SpriteRenderer> foodNodeRenderers = new();
    private readonly List<SpriteRenderer> obstacleRenderers = new();

    private Texture2D fieldTexture;
    private SpriteRenderer fieldRenderer;
    private int frameCounter;
    private Vector2 worldSize;
    private Sprite fallbackSquareSprite;

    public void SetShapeToggles(bool glowAgents, bool fieldBlobOverlay)
    {
        useGlowAgentShape = glowAgents;
        useFieldBlobSpriteForFieldOverlay = fieldBlobOverlay;
    }

    public void SetFoodDebugVisuals(bool markersVisible)
    {
        showFoodMarkers = markersVisible;
    }

    public void SetFoodInfluenceDebugVisuals(bool enabled)
    {
        foodInfluenceDebugVisuals = enabled;
    }

    public void SetBackgroundColor(Color color)
    {
        backgroundColor = color;
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
        BuildObstacles(runner.Obstacles);
        BuildAgents(runner.AgentCount);
        BuildFoodNodes(runner.FoodNodes);
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
            var active = showFoodMarkers && i < foodNodes.Length;
            var sr = foodNodeRenderers[i];
            sr.gameObject.SetActive(active);

            if (!active)
            {
                continue;
            }

            var node = foodNodes[i];
            var capacity01 = Mathf.Clamp01(node.Capacity01);
            var isDepleted = capacity01 <= depletedThreshold01;

            var clamped = ClampNodeMarker(node.position);
            sr.transform.localPosition = new Vector3(clamped.x, clamped.y, -0.8f);

            var markerScaleBoost = foodInfluenceDebugVisuals ? 1.35f : 1f;
            var markerRadius = Mathf.Lerp(0.65f, 1.3f, Mathf.Clamp01(node.consumeRadius * 0.08f));

            var stateScale = isDepleted ? depletedScaleMultiplier : activeScaleBoost;
            sr.transform.localScale = Vector3.one * foodMarkerScale * markerScaleBoost * markerRadius * stateScale;

            var markerColor = Color.Lerp(foodDepletedColor, foodActiveColor, capacity01);

            if (isDepleted)
            {
                markerColor.a = Mathf.Max(0.15f, foodMarkerMinAlpha * depletedAlphaMultiplier);
            }
            else
            {
                markerColor.a = Mathf.Max(foodMarkerMinAlpha, markerColor.a);
            }

            sr.color = markerColor;
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

        var densitySprite = Sprite.Create(
            fieldTexture,
            new Rect(0, 0, width, height),
            new Vector2(0.5f, 0.5f),
            width / worldSize.x);

        fieldRenderer.sprite = densitySprite;
        fieldRenderer.color = Color.white;

        if (useFieldBlobSpriteForFieldOverlay)
        {
            Sprite overlaySprite = null;

            if (ShapeLibraryProvider.TryGetSprite(ShapeId.FieldBlob, out var blobSprite) && blobSprite != null)
            {
                overlaySprite = blobSprite;
            }
            else
            {
                overlaySprite = GetFallbackSquareSprite();
            }

            fieldRenderer.drawMode = SpriteDrawMode.Tiled;
            fieldRenderer.sprite = overlaySprite;
            fieldRenderer.size = worldSize;
            fieldRenderer.color = new Color(
                Mathf.Lerp(backgroundColor.r, fieldHighColor.r, 0.22f),
                Mathf.Lerp(backgroundColor.g, fieldHighColor.g, 0.22f),
                Mathf.Lerp(backgroundColor.b, fieldHighColor.b, 0.22f),
                0.24f);

            var fieldTextureGo = new GameObject("FieldDensityTexture");
            fieldTextureGo.transform.SetParent(transform, false);
            var densityRenderer = fieldTextureGo.AddComponent<SpriteRenderer>();
            densityRenderer.sortingOrder = -19;
            densityRenderer.sprite = densitySprite;
            densityRenderer.color = Color.white;
            fieldRenderer = densityRenderer;
        }
    }

    private void BuildObstacles(NeuralObstacle[] obstacles)
    {
        if (obstacles == null || obstacles.Length == 0)
        {
            return;
        }

        ShapeLibraryProvider.TryGetSprite(ShapeId.DotCore, out var dot);
        var square = GetFallbackSquareSprite();

        for (var i = 0; i < obstacles.Length; i++)
        {
            var obstacle = obstacles[i];
            var go = new GameObject($"Obstacle_{i:00}");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(obstacle.center.x, obstacle.center.y, -0.25f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = 1;
            sr.color = obstacleColor;

            if (obstacle.shape == NeuralObstacleShape.Rectangle)
            {
                sr.sprite = square;
                sr.drawMode = SpriteDrawMode.Sliced;
                sr.size = new Vector2(Mathf.Max(0.5f, obstacle.size.x), Mathf.Max(0.5f, obstacle.size.y));
                go.transform.localScale = Vector3.one;
            }
            else
            {
                sr.sprite = dot;
                var diameter = Mathf.Max(0.2f, obstacle.radius * 2f);
                go.transform.localScale = new Vector3(diameter, diameter, 1f);
            }

            obstacleRenderers.Add(sr);
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

    private void BuildFoodNodes(NeuralFoodNodeState[] foodNodes)
    {
        if (!ShapeLibraryProvider.TryGetSprite(ShapeId.DotGlowSmall, out var markerSprite))
        {
            ShapeLibraryProvider.TryGetSprite(ShapeId.DotCore, out markerSprite);
        }

        if (markerSprite == null)
        {
            markerSprite = GetFallbackSquareSprite();
        }

        for (var i = 0; i < foodNodes.Length; i++)
        {
            var go = new GameObject($"FoodNode_{i:00}");
            go.transform.SetParent(transform, false);
            go.transform.localScale = Vector3.one * foodMarkerScale;

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = 40;
            sr.sprite = markerSprite;
            sr.color = foodActiveColor;
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

    private Sprite GetFallbackSquareSprite()
    {
        if (fallbackSquareSprite != null)
        {
            return fallbackSquareSprite;
        }

        var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply(false, false);
        fallbackSquareSprite = Sprite.Create(tex, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
        return fallbackSquareSprite;
    }

    private void ClearChildren()
    {
        agentRenderers.Clear();
        foodNodeRenderers.Clear();
        obstacleRenderers.Clear();

        for (var i = transform.childCount - 1; i >= 0; i--)
        {
            var child = transform.GetChild(i);
            if (UnityEngine.Application.isPlaying)
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
            if (UnityEngine.Application.isPlaying)
            {
                Destroy(fieldTexture);
            }
            else
            {
                DestroyImmediate(fieldTexture);
            }

            fieldTexture = null;
        }

        if (fallbackSquareSprite != null)
        {
            if (UnityEngine.Application.isPlaying)
            {
                Destroy(fallbackSquareSprite.texture);
                Destroy(fallbackSquareSprite);
            }
            else
            {
                DestroyImmediate(fallbackSquareSprite.texture);
                DestroyImmediate(fallbackSquareSprite);
            }

            fallbackSquareSprite = null;
        }

        fieldRenderer = null;
        frameCounter = 0;
        worldSize = Vector2.one;
    }
}