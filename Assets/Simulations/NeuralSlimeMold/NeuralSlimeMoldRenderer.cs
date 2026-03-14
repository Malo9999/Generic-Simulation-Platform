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
    [SerializeField, Range(0.1f, 3f)] private float veinThicknessBoost = 1.45f;
    [SerializeField, Range(0f, 3f)] private float trafficGlowStrength = 1.45f;
    [SerializeField, Range(0f, 1f)] private float fieldAlphaSoftness = 0.88f;
    //[SerializeField, Range(0f, 2f)] private float fieldBackgroundLift = 0.04f;
    [SerializeField] private bool emphasizePrimaryTubes = true;
    [SerializeField] private bool showExplorationBranches = true;
    [SerializeField, Range(0.6f, 2.2f)] private float tubeExposure = 1.3f;
    [SerializeField, Range(0f, 1f)] private float staleTrailFade = 0.34f;
    [SerializeField, Range(0f, 1f)] private float branchAlphaBias = 0.48f;
    [SerializeField, Range(0f, 1f)] private float trunkThreshold01 = 0.58f;
    [SerializeField, Range(0f, 1f)] private float branchThreshold01 = 0.3f;

    [Header("World Marker Visuals")]
    [SerializeField] private Color foodActiveColor = new(0.92f, 0.86f, 0.24f, 1f);
    [SerializeField] private Color foodDepletedColor = new(0.38f, 0.25f, 0.12f, 0.95f);
    [SerializeField] private Color foodRegrowingColor = new(0.76f, 0.56f, 0.22f, 0.98f);
    [SerializeField] private Color obstacleColor = new(0.28f, 0.24f, 0.20f, 0.92f);
    [SerializeField] private Color colonyHubCoreColor = new(0.46f, 0.98f, 1f, 0.98f);
    [SerializeField] private Color colonyHubRingColor = new(0.2f, 0.56f, 0.9f, 0.8f);
    [SerializeField, Min(0.2f)] private float colonyHubCoreScale = 0.5f;
    [SerializeField, Min(0.2f)] private float colonyHubRingScale = 1.02f;
    [SerializeField] private bool showFoodMarkers = true;
    [SerializeField] private bool showFoodStateMarkers = true;
    [SerializeField, Min(0.1f)] private float foodMarkerScale = 0.42f;
    [SerializeField, Range(0f, 1f)] private float foodMarkerMinAlpha = 0.75f;

    [Header("Food Marker State Styling")]
    [SerializeField, Range(0.2f, 1.5f)] private float depletedScaleMultiplier = 0.5f;
    [SerializeField, Range(0.1f, 1f)] private float depletedAlphaMultiplier = 0.35f;
    [SerializeField, Range(0.2f, 2f)] private float activeScaleBoost = 1.15f;
    [SerializeField, Range(0f, 0.5f)] private float depletedThreshold01 = 0.12f;
    [SerializeField, Range(0f, 0.5f)] private float lowFoodThreshold01 = 0.45f;
    [SerializeField, Range(0.5f, 1f)] private float recoveredFoodThreshold01 = 0.82f;
    [SerializeField] private Color foodDryingColor = new(0.62f, 0.38f, 0.14f, 0.96f);
    [SerializeField, Range(0f, 0.3f)] private float activePulseAmplitude = 0.16f;
    [SerializeField, Range(0f, 0.3f)] private float regrowPulseAmplitude = 0.12f;
    [SerializeField, Range(0.1f, 8f)] private float foodPulseSpeed = 2.2f;

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
    private readonly List<SpriteRenderer> foodNodeRenderers = new();
    private readonly List<SpriteRenderer> foodGlowRenderers = new();
    private readonly List<SpriteRenderer> obstacleRenderers = new();
    private SpriteRenderer colonyHubRingRenderer;
    private SpriteRenderer colonyHubAuraRenderer;
    private SpriteRenderer colonyHubCoreRenderer;

    private Texture2D fieldTexture;
    private Color32[] fieldPixels;
    private SpriteRenderer fieldRenderer;
    private SpriteRenderer activityFocusRenderer;
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
        BuildColonyHub(runner.ColonyHub, runner.ColonyHubRadius);
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

        UpdateFoodNodes(runner.FoodNodes, runner.FoodConsumerCounts);
        UpdateColonyHub(runner.ColonyHub, runner.ColonyHubRadius);
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

    private void UpdateFoodNodes(NeuralFoodNodeState[] foodNodes, int[] foodConsumerCounts)
    {
        var time = UnityEngine.Application.isPlaying ? Time.time : 0f;

        for (var i = 0; i < foodNodeRenderers.Count; i++)
        {
            var active = showFoodMarkers && showFoodStateMarkers && i < foodNodes.Length;
            var sr = foodNodeRenderers[i];
            var glow = i < foodGlowRenderers.Count ? foodGlowRenderers[i] : null;
            sr.gameObject.SetActive(active);
            if (glow != null)
            {
                glow.gameObject.SetActive(active);
            }

            if (!active)
            {
                continue;
            }

            var node = foodNodes[i];
            var capacity01 = Mathf.Clamp01(node.Capacity01);
            var isDepleted = capacity01 <= depletedThreshold01;
            var isLow = !isDepleted && capacity01 < lowFoodThreshold01;
            var isDrying = !isDepleted && capacity01 < lowFoodThreshold01;
            var isRegrowing = !isDepleted && !isDrying && capacity01 < recoveredFoodThreshold01;

            var clamped = ClampNodeMarker(node.position);
            sr.transform.localPosition = new Vector3(clamped.x, clamped.y, -0.8f);

            var markerScaleBoost = foodInfluenceDebugVisuals ? 1.35f : 1f;
            var markerRadius = Mathf.Lerp(0.9f, 1.6f, Mathf.Clamp01(node.consumeRadius * 0.08f));

            var perNodePhase = i * 0.73f;
            var pulse = Mathf.Sin((time * foodPulseSpeed) + perNodePhase) * 0.5f + 0.5f;
            var consumerCount = (foodConsumerCounts != null && i < foodConsumerCounts.Length) ? foodConsumerCounts[i] : 0;
            var harvestActive = consumerCount > 0;
            var harvestBoost01 = harvestActive ? Mathf.Clamp01(consumerCount / 6f) : 0f;

            Color markerColor;
            float stateScale;
            float stateAlpha;

            if (isDepleted)
            {
                markerColor = foodDepletedColor;
                stateScale = depletedScaleMultiplier;
                stateAlpha = Mathf.Max(0.15f, foodMarkerMinAlpha * depletedAlphaMultiplier);
            }
            else if (isLow)
            {
                markerColor = Color.Lerp(foodDepletedColor, foodDryingColor, Mathf.InverseLerp(depletedThreshold01, lowFoodThreshold01, capacity01));
                var regrowPulse = 1f + (pulse * regrowPulseAmplitude);
                stateScale = Mathf.Lerp(depletedScaleMultiplier, activeScaleBoost, Mathf.InverseLerp(depletedThreshold01, lowFoodThreshold01, capacity01)) * regrowPulse;
                stateAlpha = Mathf.Lerp(
                    Mathf.Max(0.35f, foodMarkerMinAlpha * 0.55f),
                    Mathf.Max(0.6f, foodMarkerMinAlpha * 0.9f),
                    Mathf.InverseLerp(depletedThreshold01, lowFoodThreshold01, capacity01));
            }
            else if (isRegrowing)
            {
                markerColor = Color.Lerp(foodRegrowingColor, foodActiveColor, Mathf.InverseLerp(lowFoodThreshold01, 1f, capacity01));
                var pulseScale = 1f + (pulse * regrowPulseAmplitude * 0.75f);
                stateScale = Mathf.Lerp(0.9f, activeScaleBoost, capacity01) * pulseScale;
                stateAlpha = Mathf.Lerp(
                    Mathf.Max(0.65f, foodMarkerMinAlpha * 0.9f),
                    Mathf.Max(foodMarkerMinAlpha, foodActiveColor.a),
                    capacity01);
            }
            else
            {
                markerColor = foodActiveColor;
                var harvestPulse = 1f + (pulse * Mathf.Lerp(activePulseAmplitude, activePulseAmplitude * 2.6f, harvestBoost01));
                stateScale = activeScaleBoost * harvestPulse * Mathf.Lerp(1.05f, 1.55f, harvestBoost01);
                stateAlpha = Mathf.Max(foodMarkerMinAlpha, foodActiveColor.a) * Mathf.Lerp(1.05f, 1.28f, harvestBoost01);
            }

            sr.transform.localScale = Vector3.one * foodMarkerScale * markerScaleBoost * markerRadius * stateScale;
            markerColor.a = stateAlpha;
            sr.color = markerColor;

            if (glow != null)
            {
                var glowColor = markerColor;
                glowColor.a *= isDepleted ? 0.20f : (isRegrowing ? 0.42f : Mathf.Lerp(0.58f, 0.9f, harvestBoost01));
                glow.transform.localPosition = new Vector3(clamped.x, clamped.y, -0.9f);
                glow.transform.localScale = Vector3.one * foodMarkerScale * markerScaleBoost * markerRadius * stateScale * (isDepleted ? 1.8f : 2.5f);
                glow.color = glowColor;
            }
        }
    }

    private void BuildColonyHub(Vector2 hub, float hubRadius)
    {
        Sprite markerSprite = null;
        if (!ShapeLibraryProvider.TryGetSprite(ShapeId.DotGlowSmall, out markerSprite) || markerSprite == null)
        {
            markerSprite = GetFallbackSquareSprite();
        }

        Sprite glowSprite = null;
        if (!ShapeLibraryProvider.TryGetSprite(ShapeId.DotGlow, out glowSprite) || glowSprite == null)
        {
            glowSprite = markerSprite;
        }

        var hubGo = new GameObject("ColonyHub");
        hubGo.transform.SetParent(transform, false);

        var auraGo = new GameObject("Aura");
        auraGo.transform.SetParent(hubGo.transform, false);
        colonyHubAuraRenderer = auraGo.AddComponent<SpriteRenderer>();
        colonyHubAuraRenderer.sortingOrder = 29;
        colonyHubAuraRenderer.sprite = glowSprite;

        var ringGo = new GameObject("Ring");
        ringGo.transform.SetParent(hubGo.transform, false);
        colonyHubRingRenderer = ringGo.AddComponent<SpriteRenderer>();
        colonyHubRingRenderer.sortingOrder = 30;
        colonyHubRingRenderer.sprite = markerSprite;

        var coreGo = new GameObject("Core");
        coreGo.transform.SetParent(hubGo.transform, false);
        colonyHubCoreRenderer = coreGo.AddComponent<SpriteRenderer>();
        colonyHubCoreRenderer.sortingOrder = 31;
        colonyHubCoreRenderer.sprite = markerSprite;

        UpdateColonyHub(hub, hubRadius);
    }

    private void UpdateColonyHub(Vector2 hub, float hubRadius)
    {
        if (colonyHubCoreRenderer == null || colonyHubRingRenderer == null || colonyHubAuraRenderer == null)
        {
            return;
        }

        var clamped = ClampNodeMarker(hub);
        colonyHubCoreRenderer.transform.localPosition = new Vector3(clamped.x, clamped.y, -0.62f);
        colonyHubRingRenderer.transform.localPosition = new Vector3(clamped.x, clamped.y, -0.64f);
        colonyHubAuraRenderer.transform.localPosition = new Vector3(clamped.x, clamped.y, -0.66f);

        var diameter = Mathf.Max(0.6f, hubRadius * 2f);
        colonyHubCoreRenderer.transform.localScale = Vector3.one * diameter * colonyHubCoreScale;
        colonyHubRingRenderer.transform.localScale = Vector3.one * diameter * colonyHubRingScale;
        colonyHubAuraRenderer.transform.localScale = Vector3.one * diameter * 0.76f;

        colonyHubCoreRenderer.color = colonyHubCoreColor;

        var pulsing = 0.98f + (Mathf.Sin(Time.time * 1.8f) * 0.03f + 0.03f);
        var ring = colonyHubRingColor;
        ring.a *= pulsing;
        colonyHubRingRenderer.color = ring;

        var aura = colonyHubRingColor;
        aura.a *= 0.055f;
        colonyHubAuraRenderer.color = aura;
    }

    private void BuildField(Vector2 size, int width, int height)
    {
        var fieldGo = new GameObject("Field");
        fieldGo.transform.SetParent(transform, false);
        fieldGo.transform.localScale = new Vector3(
            size.x / Mathf.Max(1f, width),
            size.y / Mathf.Max(1f, height),
            1f);
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
            1f);

        fieldRenderer.sprite = densitySprite;
        fieldRenderer.color = Color.white;

        if (useFieldBlobSpriteForFieldOverlay)
        {
            var overlaySprite = GetFallbackSquareSprite();

            fieldRenderer.drawMode = SpriteDrawMode.Sliced;
            fieldRenderer.sprite = overlaySprite;
            fieldRenderer.size = size;
            fieldRenderer.color = new Color(
                backgroundColor.r,
                backgroundColor.g,
                backgroundColor.b,
                0.96f);

            var fieldTextureGo = new GameObject("FieldDensityTexture");
            fieldTextureGo.transform.SetParent(transform, false);
            fieldTextureGo.transform.localScale = new Vector3(
                size.x / Mathf.Max(1f, width),
                size.y / Mathf.Max(1f, height),
                1f);
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
        Sprite markerSprite = null;
        if (!ShapeLibraryProvider.TryGetSprite(ShapeId.DotGlowSmall, out markerSprite) || markerSprite == null)
        {
            markerSprite = GetFallbackSquareSprite();
        }

        Sprite glowSprite = null;
        if (!ShapeLibraryProvider.TryGetSprite(ShapeId.DotGlow, out glowSprite) || glowSprite == null)
        {
            glowSprite = markerSprite;
        }

        for (var i = 0; i < foodNodes.Length; i++)
        {
            var root = new GameObject($"FoodNode_{i:00}");
            root.transform.SetParent(transform, false);
            root.transform.localScale = Vector3.one;

            var glowGo = new GameObject("Glow");
            glowGo.transform.SetParent(root.transform, false);
            var glow = glowGo.AddComponent<SpriteRenderer>();
            glow.sortingOrder = 39;
            glow.sprite = glowSprite;
            glow.color = new Color(foodActiveColor.r, foodActiveColor.g, foodActiveColor.b, 0.45f);
            foodGlowRenderers.Add(glow);

            var coreGo = new GameObject("Core");
            coreGo.transform.SetParent(root.transform, false);
            var sr = coreGo.AddComponent<SpriteRenderer>();
            sr.sortingOrder = 40;
            sr.sprite = markerSprite;
            sr.drawMode = SpriteDrawMode.Simple;
            sr.color = foodActiveColor;
            foodNodeRenderers.Add(sr);
        }

        var focusGo = new GameObject("ActivityFocus");
        focusGo.transform.SetParent(transform, false);
        focusGo.transform.localScale = Vector3.one;

        activityFocusRenderer = focusGo.AddComponent<SpriteRenderer>();
        activityFocusRenderer.sortingOrder = 35;
        activityFocusRenderer.sprite = markerSprite;
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
                var trunkMask = Mathf.SmoothStep(trunkThreshold01 - 0.14f, trunkThreshold01 + 0.06f, visible);
                var branchMask = Mathf.SmoothStep(branchThreshold01 + 0.02f, trunkThreshold01 - 0.04f, visible) * (1f - trunkMask);
                var staleMask = Mathf.Clamp01(1f - trunkMask - branchMask) * Mathf.Clamp01(visible / Mathf.Max(0.0001f, branchThreshold01));

                var trunkExposure = Mathf.Lerp(1f, tubeExposure * 1.08f, trunkMask);
                var branchExposure = showExplorationBranches ? Mathf.Lerp(0.24f, 0.62f, branchMask) : 0f;
                var staleExposure = showExplorationBranches ? Mathf.Lerp(0.015f, staleTrailFade * 0.28f, staleMask) : 0f;
                var visualStrength = (trunkMask * trunkExposure) + (branchMask * branchExposure) + (staleMask * staleExposure);
                visualStrength = Mathf.Clamp01(visualStrength);

                var alpha = Mathf.Pow(visible, Mathf.Lerp(2.2f, 0.62f, fieldAlphaSoftness));
                alpha *= Mathf.Lerp(branchAlphaBias * 0.48f, 1f, trunkMask);
                alpha *= Mathf.Lerp(staleTrailFade * 0.62f, 1f, trunkMask + (branchMask * 0.45f));
                alpha *= showExplorationBranches ? 1f : (0.35f + (0.65f * trunkMask));
                alpha = Mathf.Clamp01(alpha * visualStrength);
                alpha = Mathf.Clamp01(alpha);

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

    private void ClearChildren()
    {
        agentRenderers.Clear();
        foodNodeRenderers.Clear();
        foodGlowRenderers.Clear();
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

        fieldPixels = null;

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
