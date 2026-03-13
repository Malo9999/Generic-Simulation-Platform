using System.Collections.Generic;
using UnityEngine;

public sealed class NeuralSlimeMoldRenderer : MonoBehaviour
{
    [Header("Agent Visuals")]
    [SerializeField] private float agentScale = 0.2f;
    [SerializeField] private int maxVisibleAgents = 1500;
    [SerializeField] private Color agentColor = new(0.62f, 0.98f, 0.78f, 0.85f);

    [Header("Field Visuals")]
    [SerializeField] private Color fieldLowColor = new(0.06f, 0.07f, 0.05f, 1f);
    [SerializeField] private Color fieldHighColor = new(0.30f, 0.78f, 0.48f, 0.92f);
    [SerializeField] private float fieldExposure = 1.05f;
    [SerializeField] private int fieldTextureRefreshInterval = 1;

    [Header("Field Network Styling")]
    [SerializeField, Range(0.1f, 4f)] private float veinContrast = 1.8f;
    [SerializeField, Range(0f, 1f)] private float veinFloor = 0.08f;
    [SerializeField, Range(0.1f, 3f)] private float veinThicknessBoost = 1.35f;
    [SerializeField, Range(0f, 3f)] private float trafficGlowStrength = 1.3f;
    [SerializeField, Range(0f, 1f)] private float fieldAlphaSoftness = 0.88f;
    [SerializeField] private bool emphasizePrimaryTubes = true;
    [SerializeField] private bool showExplorationBranches = true;
    [SerializeField, Range(0.6f, 2.2f)] private float tubeExposure = 1.15f;
    [SerializeField, Range(0f, 1f)] private float staleTrailFade = 0.4f;
    [SerializeField, Range(0f, 1f)] private float branchAlphaBias = 0.62f;
    [SerializeField, Range(0f, 1f)] private float trunkThreshold01 = 0.62f;
    [SerializeField, Range(0f, 1f)] private float branchThreshold01 = 0.24f;

    [Header("Food Visuals")]
    [SerializeField] private bool showFoodMarkers = true;
    [SerializeField] private bool showFoodStateMarkers = true;
    [SerializeField, Min(0.1f)] private float foodMarkerScale = 0.42f;
    [SerializeField] private Color foodActiveColor = new(1f, 0.93f, 0.36f, 1f);
    [SerializeField] private Color foodDryingColor = new(1f, 0.62f, 0.18f, 0.98f);
    [SerializeField] private Color foodDepletedColor = new(0.28f, 0.16f, 0.10f, 0.65f);
    [SerializeField] private Color foodRegrowingColor = new(1f, 0.72f, 0.24f, 0.92f);
    [SerializeField, Range(0f, 0.5f)] private float depletedThreshold01 = 0.08f;
    [SerializeField, Range(0f, 0.7f)] private float lowFoodThreshold01 = 0.42f;
    [SerializeField, Range(0.4f, 1f)] private float recoveredFoodThreshold01 = 0.82f;
    [SerializeField, Range(0.1f, 10f)] private float foodPulseSpeed = 2.6f;
    [SerializeField, Range(0f, 0.4f)] private float foodHaloPulse = 0.14f;
    [SerializeField, Range(0f, 0.4f)] private float foodRingPulse = 0.20f;
    [SerializeField, Range(0f, 2f)] private float foodHaloScaleMultiplier = 3.2f;
    [SerializeField, Range(0f, 2f)] private float foodRingScaleMultiplier = 1.85f;
    [SerializeField, Range(0f, 2f)] private float foodCoreScaleMultiplier = 0.82f;
    [SerializeField, Range(0f, 2f)] private float foodScentVisualBoost = 0.55f;
    [SerializeField] private Color obstacleColor = new(0.28f, 0.24f, 0.20f, 0.92f);

    [Header("Palette")]
    [SerializeField] private bool useGlowAgentShape = true;
    [SerializeField] private bool useFieldBlobSpriteForFieldOverlay = true;

    [Header("Activity Focus")]
    [SerializeField] private bool showActivityFocus;
    [SerializeField] private Color activityFocusColor = new(0.52f, 0.94f, 0.9f, 0.55f);
    [SerializeField, Range(0.2f, 2f)] private float activityFocusScale = 1f;

    private bool foodInfluenceDebugVisuals;
    private Color backgroundColor = new(0.10f, 0.09f, 0.07f, 1f);

    private readonly List<SpriteRenderer> agentRenderers = new();
    private readonly List<SpriteRenderer> obstacleRenderers = new();
    private readonly List<FoodNodeVisual> foodNodeVisuals = new();

    private Texture2D fieldTexture;
    private Color32[] fieldPixels;
    private SpriteRenderer fieldRenderer;
    private SpriteRenderer activityFocusRenderer;
    private int frameCounter;
    private Vector2 worldSize;

    private Sprite fallbackSquareSprite;
    private Sprite circleSprite;
    private Sprite softCircleSprite;
    private Sprite ringSprite;

    private struct FoodNodeVisual
    {
        public GameObject root;
        public SpriteRenderer halo;
        public SpriteRenderer ring;
        public SpriteRenderer core;
    }

    public void SetShapeToggles(bool glowAgents, bool fieldBlobOverlay)
    {
        useGlowAgentShape = glowAgents;
        useFieldBlobSpriteForFieldOverlay = fieldBlobOverlay;
    }

    public void SetFoodDebugVisuals(bool markersVisible)
    {
        showFoodMarkers = markersVisible;
    }

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

    public void SetFoodInfluenceDebugVisuals(bool enabled)
    {
        foodInfluenceDebugVisuals = enabled;
    }

    public void SetBackgroundColor(Color color)
    {
        backgroundColor = color;
    }

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

        UpdateFoodNodes(runner);
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

    private void UpdateFoodNodes(NeuralSlimeMoldRunner runner)
    {
        var foodNodes = runner.FoodNodes;
        var time = Application.isPlaying ? Time.time : 0f;

        for (var i = 0; i < foodNodeVisuals.Count; i++)
        {
            var active = showFoodMarkers && showFoodStateMarkers && i < foodNodes.Length;
            var visual = foodNodeVisuals[i];
            if (visual.root == null)
            {
                continue;
            }

            visual.root.SetActive(active);
            if (!active)
            {
                continue;
            }

            var node = foodNodes[i];
            var capacity01 = Mathf.Clamp01(node.Capacity01);
            var isActive = runner.GetFoodIsActive(i);
            var respawn01 = runner.GetFoodRespawn01(i);
            var recentConsumption01 = runner.GetFoodRecentConsumption01(i);
            var activationFlash01 = runner.GetFoodActivationFlash01(i);
            var scent01 = runner.GetFoodScent01(i);

            var isDepleted = !isActive && respawn01 > 0.01f;
            var isRegrowing = !isActive && respawn01 <= 0.01f && capacity01 <= depletedThreshold01;
            var isDrying = isActive && capacity01 < lowFoodThreshold01;

            var clamped = ClampNodeMarker(node.position);
            visual.root.transform.localPosition = new Vector3(clamped.x, clamped.y, -0.8f);

            var baseRadius = Mathf.Lerp(0.95f, 1.9f, Mathf.Clamp01(node.consumeRadius * 0.09f));
            var pulse = Mathf.Sin((time * foodPulseSpeed) + (i * 0.71f)) * 0.5f + 0.5f;
            var discoveryPulse = activationFlash01 > 0f ? Mathf.Lerp(1f, 1.9f, activationFlash01) : 1f;
            var scentPulse = 1f + (pulse * foodHaloPulse * Mathf.Lerp(0.35f, 1f, scent01));
            var ringPulse = 1f + (pulse * foodRingPulse * Mathf.Lerp(0.25f, 1f, scent01));
            var consumeFlash = Mathf.Lerp(1f, 1.28f, Mathf.Clamp01(recentConsumption01));

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
                coreColor = Color.Lerp(foodActiveColor, foodDryingColor, Mathf.SmoothStep(0f, 1f, dryness));
                haloColor = Color.Lerp(foodActiveColor, foodDryingColor, Mathf.Clamp01(dryness * 0.8f));
                ringColor = Color.Lerp(new Color(1f, 1f, 0.7f, 1f), foodDryingColor, dryness);

                coreScale = baseRadius * foodCoreScaleMultiplier * Mathf.Lerp(0.95f, 1.15f, capacity01) * consumeFlash;
                haloScale = baseRadius * foodHaloScaleMultiplier * scentPulse * discoveryPulse * Mathf.Lerp(0.8f, 1.2f, scent01 + (recentConsumption01 * foodScentVisualBoost));
                ringScale = baseRadius * foodRingScaleMultiplier * ringPulse * Mathf.Lerp(0.85f, 1.15f, 1f - dryness);

                coreAlpha = Mathf.Lerp(0.88f, 1f, capacity01);
                haloAlpha = Mathf.Lerp(0.18f, 0.34f, scent01) + (recentConsumption01 * 0.10f) + (activationFlash01 * 0.18f);
                ringAlpha = Mathf.Lerp(0.22f, 0.72f, capacity01) + (activationFlash01 * 0.18f);
            }
            else if (isDepleted)
            {
                coreColor = foodDepletedColor;
                haloColor = Color.Lerp(foodDepletedColor, foodRegrowingColor, 0.1f);
                ringColor = foodDepletedColor;

                coreScale = baseRadius * 0.38f;
                haloScale = baseRadius * 0.95f;
                ringScale = baseRadius * 0.55f;

                coreAlpha = 0.45f;
                haloAlpha = 0.08f;
                ringAlpha = 0.12f;
            }
            else
            {
                var regrowPulse = 1f + (pulse * 0.22f);
                coreColor = foodRegrowingColor;
                haloColor = Color.Lerp(foodDepletedColor, foodRegrowingColor, 0.55f);
                ringColor = Color.Lerp(foodRegrowingColor, foodActiveColor, pulse);

                coreScale = baseRadius * 0.55f * regrowPulse;
                haloScale = baseRadius * 1.85f * regrowPulse;
                ringScale = baseRadius * 1.2f * (1f + (pulse * 0.18f));

                coreAlpha = 0.78f;
                haloAlpha = 0.18f;
                ringAlpha = 0.32f;
            }

            if (foodInfluenceDebugVisuals)
            {
                haloScale *= 1.15f;
                ringScale *= 1.1f;
            }

            visual.core.transform.localScale = Vector3.one * foodMarkerScale * coreScale;
            visual.halo.transform.localScale = Vector3.one * foodMarkerScale * haloScale;
            visual.ring.transform.localScale = Vector3.one * foodMarkerScale * ringScale;

            coreColor.a = Mathf.Clamp01(coreAlpha);
            haloColor.a = Mathf.Clamp01(haloAlpha);
            ringColor.a = Mathf.Clamp01(ringAlpha);

            visual.core.color = coreColor;
            visual.halo.color = haloColor;
            visual.ring.color = ringColor;
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

        var densitySprite = Sprite.Create(
            fieldTexture,
            new Rect(0, 0, width, height),
            new Vector2(0.5f, 0.5f),
            width / size.x);

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
        var haloSprite = GetSoftCircleSprite();
        var coreSprite = GetCircleSprite();
        var foodRingSprite = GetRingSprite();

        for (var i = 0; i < foodNodes.Length; i++)
        {
            var root = new GameObject($"FoodNode_{i:00}");
            root.transform.SetParent(transform, false);
            root.transform.localScale = Vector3.one;

            var haloGo = new GameObject("Halo");
            haloGo.transform.SetParent(root.transform, false);
            var halo = haloGo.AddComponent<SpriteRenderer>();
            halo.sortingOrder = 38;
            halo.sprite = haloSprite;
            halo.color = new Color(foodActiveColor.r, foodActiveColor.g, foodActiveColor.b, 0.22f);

            var ringGo = new GameObject("Ring");
            ringGo.transform.SetParent(root.transform, false);
            var ring = ringGo.AddComponent<SpriteRenderer>();
            ring.sortingOrder = 39;
            ring.sprite = foodRingSprite;
            ring.color = foodActiveColor;

            var coreGo = new GameObject("Core");
            coreGo.transform.SetParent(root.transform, false);
            var core = coreGo.AddComponent<SpriteRenderer>();
            core.sortingOrder = 40;
            core.sprite = coreSprite;
            core.color = foodActiveColor;

            foodNodeVisuals.Add(new FoodNodeVisual
            {
                root = root,
                halo = halo,
                ring = ring,
                core = core,
            });
        }

        var focusGo = new GameObject("ActivityFocus");
        focusGo.transform.SetParent(transform, false);
        focusGo.transform.localScale = Vector3.one;

        activityFocusRenderer = focusGo.AddComponent<SpriteRenderer>();
        activityFocusRenderer.sortingOrder = 35;
        activityFocusRenderer.sprite = GetSoftCircleSprite();
        activityFocusRenderer.drawMode = SpriteDrawMode.Simple;
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
                var glow = Mathf.Log(1f + (reinforced * trafficGlowStrength)) / Mathf.Log(1f + trafficGlowStrength + 1f);
                glow = Mathf.Clamp01(glow);
                var visible = Mathf.Clamp01(Mathf.Max(veinValue, glow * 0.9f));

                var trunkMask = Mathf.SmoothStep(trunkThreshold01 - 0.18f, trunkThreshold01 + 0.1f, visible);
                var branchMask = Mathf.SmoothStep(branchThreshold01, trunkThreshold01, visible) * (1f - trunkMask);
                var staleMask = Mathf.Clamp01(1f - trunkMask - branchMask) * Mathf.Clamp01(visible / Mathf.Max(0.0001f, branchThreshold01));

                var readabilityExposure = Mathf.Lerp(1f, tubeExposure, trunkMask);
                var branchExposure = showExplorationBranches ? Mathf.Lerp(0.72f, 1f, branchMask) : 0f;
                var staleExposure = showExplorationBranches ? Mathf.Lerp(0.08f, staleTrailFade, staleMask) : 0f;
                var visualStrength = Mathf.Clamp01((trunkMask * readabilityExposure) + (branchMask * branchExposure) + (staleMask * staleExposure));

                var alpha = Mathf.Pow(visible, Mathf.Lerp(1.9f, 0.60f, fieldAlphaSoftness));
                alpha *= Mathf.Lerp(branchAlphaBias, 1f, trunkMask);
                alpha *= Mathf.Lerp(staleTrailFade, 1f, trunkMask + (branchMask * 0.5f));
                alpha *= showExplorationBranches ? 1f : (0.35f + (0.65f * trunkMask));
                alpha = Mathf.Clamp01(alpha * visualStrength);

                if (alpha <= 0.003f)
                {
                    fieldPixels[index] = new Color32(0, 0, 0, 0);
                    continue;
                }

                var colorLerp = Mathf.Clamp01(Mathf.Lerp(visible, glow, 0.45f));
                var color = Color.Lerp(fieldLowColor, fieldHighColor, colorLerp);
                if (emphasizePrimaryTubes)
                {
                    color = Color.Lerp(color * 0.7f, color, 0.45f + (0.55f * trunkMask));
                }

                color.r = Mathf.Clamp01(color.r + glow * 0.03f);
                color.g = Mathf.Clamp01(color.g + glow * 0.07f);
                color.b = Mathf.Clamp01(color.b + glow * 0.02f);
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

    private Sprite GetCircleSprite()
    {
        if (circleSprite != null) return circleSprite;
        circleSprite = CreateProceduralSprite("FoodCircle", 64, false, true);
        return circleSprite;
    }

    private Sprite GetSoftCircleSprite()
    {
        if (softCircleSprite != null) return softCircleSprite;
        softCircleSprite = CreateProceduralSprite("FoodSoftCircle", 96, true, true);
        return softCircleSprite;
    }

    private Sprite GetRingSprite()
    {
        if (ringSprite != null) return ringSprite;
        ringSprite = CreateProceduralSprite("FoodRing", 96, false, false);
        return ringSprite;
    }

    private static Sprite CreateProceduralSprite(string name, int size, bool soft, bool filled)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            name = name + "_Tex"
        };

        var center = (size - 1) * 0.5f;
        var radius = center * 0.88f;
        var innerRadius = radius * 0.72f;

        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var dx = x - center;
                var dy = y - center;
                var dist = Mathf.Sqrt((dx * dx) + (dy * dy));
                float alpha;
                if (filled)
                {
                    alpha = soft ? Mathf.Clamp01(1f - Mathf.Pow(dist / radius, 2.2f)) : (dist <= radius ? 1f : 0f);
                }
                else
                {
                    var outer = dist <= radius ? 1f : 0f;
                    var inner = dist <= innerRadius ? 1f : 0f;
                    alpha = Mathf.Clamp01(outer - inner);
                    if (soft)
                    {
                        alpha *= Mathf.Clamp01(1f - Mathf.Abs((dist - ((innerRadius + radius) * 0.5f)) / (radius - innerRadius)));
                    }
                }
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        tex.Apply(false, false);
        return Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
    }

    private void ClearChildren()
    {
        agentRenderers.Clear();
        obstacleRenderers.Clear();
        foodNodeVisuals.Clear();

        for (var i = transform.childCount - 1; i >= 0; i--)
        {
            var child = transform.GetChild(i);
            if (Application.isPlaying) Destroy(child.gameObject); else DestroyImmediate(child.gameObject);
        }

        if (fieldTexture != null)
        {
            if (Application.isPlaying) Destroy(fieldTexture); else DestroyImmediate(fieldTexture);
            fieldTexture = null;
        }

        fieldPixels = null;
        DestroySprite(ref fallbackSquareSprite);
        DestroySprite(ref circleSprite);
        DestroySprite(ref softCircleSprite);
        DestroySprite(ref ringSprite);

        fieldRenderer = null;
        frameCounter = 0;
        worldSize = Vector2.one;
    }

    private void DestroySprite(ref Sprite sprite)
    {
        if (sprite == null) return;
        var tex = sprite.texture;
        if (Application.isPlaying)
        {
            if (tex != null) Destroy(tex);
            Destroy(sprite);
        }
        else
        {
            if (tex != null) DestroyImmediate(tex);
            DestroyImmediate(sprite);
        }
        sprite = null;
    }
}
