using System.Collections.Generic;
using UnityEngine;

public sealed class NeuralSlimeMoldRenderer : MonoBehaviour
{
    [Header("Agent Visuals")]
    [SerializeField] private float agentScale = 0.2f;
    [SerializeField] private int maxVisibleAgents = 1500;
    [SerializeField] private Color agentColor = new(0.62f, 0.98f, 0.78f, 0.85f);

    [Header("Field Visuals")]
    [SerializeField] private Color fieldLowColor = new(0.05f, 0.07f, 0.05f, 1f);
    [SerializeField] private Color fieldHighColor = new(0.34f, 0.92f, 0.56f, 0.96f);
    [SerializeField] private float fieldExposure = 1.35f;
    [SerializeField] private int fieldTextureRefreshInterval = 1;

    [Header("Field Network Styling")]
    [SerializeField, Range(0.1f, 4f)] private float veinContrast = 2.1f;
    [SerializeField, Range(0f, 1f)] private float veinFloor = 0.06f;
    [SerializeField, Range(0.1f, 3f)] private float veinThicknessBoost = 1.6f;
    [SerializeField, Range(0f, 3f)] private float trafficGlowStrength = 1.7f;
    [SerializeField, Range(0f, 1f)] private float fieldAlphaSoftness = 0.9f;
    [SerializeField] private bool emphasizePrimaryTubes = true;
    [SerializeField] private bool showExplorationBranches = true;
    [SerializeField, Range(0.6f, 2.2f)] private float tubeExposure = 1.28f;
    [SerializeField, Range(0f, 1f)] private float staleTrailFade = 0.28f;
    [SerializeField, Range(0f, 1f)] private float branchAlphaBias = 0.56f;
    [SerializeField, Range(0f, 1f)] private float trunkThreshold01 = 0.54f;
    [SerializeField, Range(0f, 1f)] private float branchThreshold01 = 0.18f;

    [Header("Food Marker Visuals")]
    [SerializeField] private Color foodActiveColor = new(1.00f, 0.93f, 0.26f, 1f);
    [SerializeField] private Color foodDryingColor = new(1.00f, 0.58f, 0.10f, 0.98f);
    [SerializeField] private Color foodDepletedColor = new(0.24f, 0.16f, 0.10f, 0.82f);
    [SerializeField] private Color foodRegrowingColor = new(0.96f, 0.70f, 0.22f, 0.95f);
    [SerializeField] private Color obstacleColor = new(0.28f, 0.24f, 0.20f, 0.92f);
    [SerializeField] private bool showFoodMarkers = true;
    [SerializeField] private bool showFoodStateMarkers = true;
    [SerializeField, Min(0.1f)] private float foodMarkerScale = 0.42f;
    [SerializeField, Range(0f, 1f)] private float foodMarkerMinAlpha = 0.75f;
    [SerializeField, Range(0f, 0.3f)] private float activePulseAmplitude = 0.09f;
    [SerializeField, Range(0f, 0.3f)] private float regrowPulseAmplitude = 0.14f;
    [SerializeField, Range(0.1f, 8f)] private float foodPulseSpeed = 2.35f;

    private bool foodInfluenceDebugVisuals;
    private Color backgroundColor = new(0.10f, 0.09f, 0.07f, 1f);

    [Header("Palette")]
    [SerializeField] private bool useGlowAgentShape = true;
    [SerializeField] private bool useFieldBlobSpriteForFieldOverlay = true;

    [Header("Activity Focus")]
    [SerializeField] private bool showActivityFocus;
    [SerializeField] private Color activityFocusColor = new(0.52f, 0.94f, 0.9f, 0.55f);
    [SerializeField, Range(0.2f, 2f)] private float activityFocusScale = 1f;

    private readonly List<SpriteRenderer> agentRenderers = new();
    private readonly List<SpriteRenderer> obstacleRenderers = new();
    private readonly List<FoodMarkerVisual> foodMarkers = new();

    private Texture2D fieldTexture;
    private Color32[] fieldPixels;
    private SpriteRenderer fieldRenderer;
    private SpriteRenderer activityFocusRenderer;
    private int frameCounter;
    private Vector2 worldSize;
    private Sprite fallbackSquareSprite;

    private struct FoodMarkerVisual
    {
        public Transform root;
        public SpriteRenderer halo;
        public SpriteRenderer ring;
        public SpriteRenderer core;
    }

    public void SetShapeToggles(bool glowAgents, bool fieldBlobOverlay)
    {
        useGlowAgentShape = glowAgents;
        useFieldBlobSpriteForFieldOverlay = fieldBlobOverlay;
    }

    public void SetFoodDebugVisuals(bool markersVisible) => showFoodMarkers = markersVisible;

    public void SetReadabilityToggles(bool primaryTubes, bool explorationBranches, bool foodStateMarkers, bool activityFocus)
    {
        emphasizePrimaryTubes = primaryTubes;
        showExplorationBranches = explorationBranches;
        showFoodStateMarkers = foodStateMarkers;
        showActivityFocus = activityFocus;
    }

    public void SetReadabilityTuning(float exposure, float staleFade, float alphaBias)
    {
        tubeExposure = Mathf.Max(0.1f, exposure);
        staleTrailFade = Mathf.Clamp01(staleFade);
        branchAlphaBias = Mathf.Clamp01(alphaBias);
    }

    public void SetFoodInfluenceDebugVisuals(bool enabled) => foodInfluenceDebugVisuals = enabled;
    public void SetBackgroundColor(Color color) => backgroundColor = color;

    public void SetPerformanceOptions(int textureRefreshInterval, int visibleAgentLimit)
    {
        fieldTextureRefreshInterval = Mathf.Max(1, textureRefreshInterval);
        maxVisibleAgents = Mathf.Max(1, visibleAgentLimit);
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

        UpdateFoodNodes(runner.FoodNodes);
        UpdateActivityFocus(runner.ActivityCenter, runner.ActivityRadius);

        frameCounter++;
        if (fieldTextureRefreshInterval <= 1 || frameCounter % fieldTextureRefreshInterval == 0)
        {
            UpdateFieldTexture(runner.Field);
        }
    }

    private void UpdateActivityFocus(Vector2 center, float radius)
    {
        if (activityFocusRenderer == null)
        {
            return;
        }

        activityFocusRenderer.gameObject.SetActive(showActivityFocus);
        if (!showActivityFocus)
        {
            return;
        }

        activityFocusRenderer.transform.localPosition = new Vector3(center.x, center.y, -0.7f);
        var diameter = Mathf.Max(1f, radius * 2f * Mathf.Max(0.2f, activityFocusScale));
        activityFocusRenderer.transform.localScale = new Vector3(diameter, diameter, 1f);
        activityFocusRenderer.color = activityFocusColor;
    }

    private void UpdateFoodNodes(NeuralFoodNodeState[] foodNodes)
    {
        var time = Application.isPlaying ? Time.time : 0f;

        for (var i = 0; i < foodMarkers.Count; i++)
        {
            var marker = foodMarkers[i];
            var visible = showFoodMarkers && showFoodStateMarkers && i < foodNodes.Length;
            marker.root.gameObject.SetActive(visible);
            if (!visible)
            {
                continue;
            }

            var node = foodNodes[i];
            var capacity01 = Mathf.Clamp01(node.Capacity01);
            var respawn01 = Mathf.Clamp01(node.RespawnProgress01);
            var recentConsumption = Mathf.Clamp01(node.recentConsumption01);
            var energy01 = Mathf.Clamp01(node.visualEnergy01);

            var isActive = node.isActive && capacity01 > 0.001f;
            var isRegrowing = !isActive && respawn01 > 0.05f && respawn01 < 0.98f;
            var isDepleted = !isActive && !isRegrowing;

            var clamped = ClampNodeMarker(node.position);
            marker.root.localPosition = new Vector3(clamped.x, clamped.y, -0.8f);

            var baseRadius = Mathf.Lerp(0.9f, 1.65f, Mathf.Clamp01(node.consumeRadius * 0.08f));
            var markerScaleBoost = foodInfluenceDebugVisuals ? 1.35f : 1f;
            var pulse = Mathf.Sin((time * foodPulseSpeed) + (i * 0.73f)) * 0.5f + 0.5f;
            var activePulse = 1f + (pulse * activePulseAmplitude);
            var regrowPulse = 1f + (pulse * regrowPulseAmplitude);

            Color coreColor;
            Color haloColor;
            Color ringColor;
            float coreScale;
            float haloScale;
            float ringScale;
            float coreAlpha;
            float haloAlpha;
            float ringAlpha;

            if (isActive)
            {
                var dryness = 1f - capacity01;
                coreColor = Color.Lerp(foodDryingColor, foodActiveColor, Mathf.InverseLerp(0f, 0.55f, capacity01));
                haloColor = coreColor;
                ringColor = coreColor;

                coreScale = (0.74f + (capacity01 * 0.4f) + (recentConsumption * 0.08f)) * activePulse;
                haloScale = (1.45f + (capacity01 * 0.55f) + (recentConsumption * 0.35f)) * activePulse;
                ringScale = (1.18f + (dryness * 0.3f)) * activePulse;
                coreAlpha = Mathf.Lerp(0.9f, 1f, energy01);
                haloAlpha = Mathf.Lerp(0.18f, 0.42f, energy01) + (recentConsumption * 0.08f);
                ringAlpha = Mathf.Lerp(0.2f, 0.64f, dryness) * (0.85f + (pulse * 0.15f));
            }
            else if (isRegrowing)
            {
                coreColor = Color.Lerp(foodDepletedColor, foodRegrowingColor, respawn01);
                haloColor = coreColor;
                ringColor = coreColor;

                coreScale = (0.42f + (respawn01 * 0.42f)) * regrowPulse;
                haloScale = (0.95f + (respawn01 * 0.55f)) * regrowPulse;
                ringScale = (0.78f + (respawn01 * 0.45f)) * regrowPulse;
                coreAlpha = Mathf.Lerp(0.18f, 0.82f, respawn01);
                haloAlpha = Mathf.Lerp(0.02f, 0.26f, respawn01);
                ringAlpha = Mathf.Lerp(0.04f, 0.36f, respawn01) * (0.85f + (pulse * 0.15f));
            }
            else
            {
                coreColor = foodDepletedColor;
                haloColor = foodDepletedColor;
                ringColor = foodDepletedColor;
                coreScale = 0.34f;
                haloScale = 0.56f;
                ringScale = 0.48f;
                coreAlpha = 0.15f;
                haloAlpha = 0.03f;
                ringAlpha = 0.06f;
            }

            var finalScale = foodMarkerScale * markerScaleBoost * baseRadius;

            marker.core.transform.localScale = Vector3.one * finalScale * coreScale;
            marker.halo.transform.localScale = Vector3.one * finalScale * haloScale;
            marker.ring.transform.localScale = Vector3.one * finalScale * ringScale;

            coreColor.a = Mathf.Clamp01(Mathf.Max(foodMarkerMinAlpha * 0.2f, coreAlpha));
            haloColor.a = Mathf.Clamp01(haloAlpha);
            ringColor.a = Mathf.Clamp01(ringAlpha);
            marker.core.color = coreColor;
            marker.halo.color = haloColor;
            marker.ring.color = ringColor;
            marker.halo.enabled = haloColor.a > 0.01f;
            marker.ring.enabled = ringColor.a > 0.01f;
        }
    }

    private void BuildField(Vector2 size, int width, int height)
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
        fieldPixels = new Color32[width * height];

        var densitySprite = Sprite.Create(fieldTexture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), width / size.x);
        fieldRenderer.sprite = densitySprite;
        fieldRenderer.color = Color.white;

        if (useFieldBlobSpriteForFieldOverlay)
        {
            var overlaySprite = GetFallbackSquareSprite();
            fieldRenderer.drawMode = SpriteDrawMode.Sliced;
            fieldRenderer.sprite = overlaySprite;
            fieldRenderer.size = size;
            fieldRenderer.color = new Color(backgroundColor.r, backgroundColor.g, backgroundColor.b, 0.96f);

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
        var markerSprite = GetFallbackSquareSprite();

        for (var i = 0; i < foodNodes.Length; i++)
        {
            var root = new GameObject($"FoodNode_{i:00}");
            root.transform.SetParent(transform, false);

            var haloGo = new GameObject("Halo");
            haloGo.transform.SetParent(root.transform, false);
            var halo = haloGo.AddComponent<SpriteRenderer>();
            halo.sortingOrder = 38;
            halo.sprite = markerSprite;

            var ringGo = new GameObject("Ring");
            ringGo.transform.SetParent(root.transform, false);
            var ring = ringGo.AddComponent<SpriteRenderer>();
            ring.sortingOrder = 39;
            ring.sprite = markerSprite;

            var coreGo = new GameObject("Core");
            coreGo.transform.SetParent(root.transform, false);
            var core = coreGo.AddComponent<SpriteRenderer>();
            core.sortingOrder = 40;
            core.sprite = markerSprite;

            foodMarkers.Add(new FoodMarkerVisual { root = root.transform, halo = halo, ring = ring, core = core });
        }

        var focusGo = new GameObject("ActivityFocus");
        focusGo.transform.SetParent(transform, false);
        activityFocusRenderer = focusGo.AddComponent<SpriteRenderer>();
        activityFocusRenderer.sortingOrder = 35;
        activityFocusRenderer.sprite = markerSprite;
        activityFocusRenderer.color = activityFocusColor;
        activityFocusRenderer.gameObject.SetActive(showActivityFocus);
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

        var width = field.Width;
        var height = field.Height;
        var max = 0.001f;
        for (var i = 0; i < values.Length; i++)
        {
            if (values[i] > max)
            {
                max = values[i];
            }
        }

        if (fieldPixels == null || fieldPixels.Length != values.Length)
        {
            fieldPixels = new Color32[values.Length];
        }

        for (var y = 0; y < height; y++)
        {
            var yOffset = y * width;
            for (var x = 0; x < width; x++)
            {
                var index = yOffset + x;
                var raw = values[index];
                var normalized = Mathf.Clamp01((raw / max) * fieldExposure);

                var center = normalized;
                var left = x > 0 ? Mathf.Clamp01((values[index - 1] / max) * fieldExposure) : center;
                var right = x < width - 1 ? Mathf.Clamp01((values[index + 1] / max) * fieldExposure) : center;
                var down = y > 0 ? Mathf.Clamp01((values[index - width] / max) * fieldExposure) : center;
                var up = y < height - 1 ? Mathf.Clamp01((values[index + width] / max) * fieldExposure) : center;

                var neighborAverage = (left + right + up + down) * 0.25f;
                var reinforced = Mathf.Lerp(center, Mathf.Max(center, neighborAverage), veinThicknessBoost * 0.5f);

                var contrast = Mathf.Pow(Mathf.Max(0f, reinforced), veinContrast);
                var thicknessValue = Mathf.Sqrt(Mathf.Max(0f, contrast));
                var veinValue = Mathf.InverseLerp(veinFloor, 1f, thicknessValue);

                var glow = Mathf.Log(1f + (reinforced * trafficGlowStrength)) / Mathf.Log(2f + trafficGlowStrength);
                glow = Mathf.Clamp01(glow);

                var visible = Mathf.Clamp01(Mathf.Max(veinValue, glow * 0.92f));
                var trunkMask = Mathf.SmoothStep(trunkThreshold01 - 0.16f, trunkThreshold01 + 0.08f, visible);
                var branchMask = Mathf.SmoothStep(branchThreshold01, trunkThreshold01, visible) * (1f - trunkMask);
                var staleMask = Mathf.Clamp01(1f - trunkMask - branchMask) * Mathf.Clamp01(visible / Mathf.Max(0.0001f, branchThreshold01));

                var readabilityExposure = Mathf.Lerp(1f, tubeExposure, trunkMask);
                var branchExposure = showExplorationBranches ? Mathf.Lerp(0.72f, 1f, branchMask) : 0f;
                var staleExposure = showExplorationBranches ? Mathf.Lerp(0.05f, staleTrailFade, staleMask) : 0f;
                var visualStrength = Mathf.Clamp01((trunkMask * readabilityExposure) + (branchMask * branchExposure) + (staleMask * staleExposure));

                var alpha = Mathf.Pow(visible, Mathf.Lerp(1.9f, 0.58f, fieldAlphaSoftness));
                alpha *= Mathf.Lerp(branchAlphaBias, 1f, trunkMask);
                alpha *= Mathf.Lerp(staleTrailFade, 1f, trunkMask + (branchMask * 0.45f));
                alpha *= showExplorationBranches ? 1f : (0.35f + (0.65f * trunkMask));
                alpha = Mathf.Clamp01(alpha * visualStrength);

                if (alpha <= 0.003f)
                {
                    fieldPixels[index] = new Color32(0, 0, 0, 0);
                    continue;
                }

                var colorLerp = Mathf.Clamp01(Mathf.Lerp(visible, glow, 0.5f));
                var color = Color.Lerp(fieldLowColor, fieldHighColor, colorLerp);

                if (emphasizePrimaryTubes)
                {
                    color = Color.Lerp(color * 0.68f, color, 0.45f + (0.55f * trunkMask));
                }

                color.r = Mathf.Clamp01(color.r + (glow * 0.05f));
                color.g = Mathf.Clamp01(color.g + (glow * 0.08f));
                color.b = Mathf.Clamp01(color.b + (glow * 0.03f));
                color.a = alpha;
                fieldPixels[index] = color;
            }
        }

        fieldTexture.SetPixels32(fieldPixels);
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
        obstacleRenderers.Clear();
        foodMarkers.Clear();

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

        fieldPixels = null;

        if (fallbackSquareSprite != null)
        {
            if (Application.isPlaying)
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
        activityFocusRenderer = null;
        frameCounter = 0;
        worldSize = Vector2.one;
    }
}
