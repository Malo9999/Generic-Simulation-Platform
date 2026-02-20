using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public class MarbleRaceSimulation : ISimulation
{
    private const int DefaultMarbleCount = 32;
    private const int MaxMarbleCount = 200;
    private const int RequiredLapsToWin = 3;
    private const float MarbleRadius = 0.24f;
    private const float WaypointReachRadius = 0.45f;
    private const float UiUpdateInterval = 0.2f;
    private const float LogFallbackInterval = 3f;

    private readonly List<MarbleAgent> marbles = new();
    private readonly List<Vector2> waypoints = new();

    private ScenarioConfig config;
    private Transform simRoot;
    private Transform raceRoot;
    private System.Random random;

    private Sprite pixelSprite;
    private string agentsSortingLayer;

    private float elapsedTime;
    private float uiUpdateTimer;
    private float fallbackLogTimer;

    private bool raceFinished;
    private List<MarbleAgent> finishOrder;

    private Canvas scoreboardCanvas;
    private Text scoreboardText;

    public string Id => "MarbleRace";

    public void Initialize(ScenarioConfig cfg, Transform root)
    {
        config = cfg ?? new ScenarioConfig();
        simRoot = root;
        random = new System.Random(config.seed);
        finishOrder = new List<MarbleAgent>();

        raceRoot = new GameObject("MarbleRace").transform;
        raceRoot.SetParent(simRoot, false);

        pixelSprite = CreatePixelSprite();
        agentsSortingLayer = ResolveAgentsSortingLayer();

        var worldWidth = Mathf.Max(24, config.world?.arenaWidth ?? 64);
        var worldHeight = Mathf.Max(24, config.world?.arenaHeight ?? 64);

        CreateTrackWaypoints(worldWidth, worldHeight);
        CreateTrackVisual(raceRoot);
        SpawnMarbles(raceRoot, worldWidth, worldHeight);
        CreateScoreboard();

        Debug.Log($"MarbleRace initialized with {marbles.Count} marbles, {waypoints.Count} waypoints, seed {config.seed}.");
    }

    public void Tick(float dt)
    {
        if (raceFinished || marbles.Count == 0)
        {
            return;
        }

        elapsedTime += dt;
        uiUpdateTimer += dt;
        fallbackLogTimer += dt;

        for (var i = 0; i < marbles.Count; i++)
        {
            UpdateMarble(marbles[i], dt);
        }

        if (uiUpdateTimer >= UiUpdateInterval)
        {
            uiUpdateTimer = 0f;
            UpdateScoreboard();
        }

        if (scoreboardText == null && fallbackLogTimer >= LogFallbackInterval)
        {
            fallbackLogTimer = 0f;
            Debug.Log(BuildScoreboardText());
        }
    }

    public void Dispose()
    {
        if (scoreboardCanvas != null)
        {
            UnityEngine.Object.Destroy(scoreboardCanvas.gameObject);
            scoreboardCanvas = null;
            scoreboardText = null;
        }

        if (raceRoot != null)
        {
            UnityEngine.Object.Destroy(raceRoot.gameObject);
            raceRoot = null;
        }

        if (pixelSprite != null)
        {
            UnityEngine.Object.Destroy(pixelSprite.texture);
            UnityEngine.Object.Destroy(pixelSprite);
        }

        marbles.Clear();
        waypoints.Clear();
    }

    private void UpdateMarble(MarbleAgent marble, float dt)
    {
        if (marble.Finished)
        {
            return;
        }

        var target = waypoints[marble.NextWaypointIndex];
        var currentPosition = (Vector2)marble.Root.position;
        var toTarget = target - currentPosition;

        if (toTarget.sqrMagnitude <= WaypointReachRadius * WaypointReachRadius)
        {
            marble.NextWaypointIndex = (marble.NextWaypointIndex + 1) % waypoints.Count;
            if (marble.NextWaypointIndex == 0)
            {
                marble.LapCount += 1;
            }

            if (marble.LapCount >= RequiredLapsToWin)
            {
                marble.Finished = true;
                marble.FinishTime = elapsedTime;
                finishOrder.Add(marble);

                if (finishOrder.Count == 1)
                {
                    raceFinished = true;
                    for (var i = 0; i < marbles.Count; i++)
                    {
                        if (!marbles[i].Finished)
                        {
                            marbles[i].Finished = true;
                            marbles[i].FinishTime = elapsedTime + Vector2.Distance(marbles[i].Root.position, waypoints[marbles[i].NextWaypointIndex]) / Mathf.Max(0.001f, marbles[i].Speed);
                            finishOrder.Add(marbles[i]);
                        }
                    }

                    finishOrder = finishOrder
                        .OrderBy(m => m.Finished && m == marble ? 0 : 1)
                        .ThenByDescending(m => m.LapCount)
                        .ThenByDescending(m => m.NextWaypointIndex)
                        .ThenBy(m => Vector2.Distance(m.Root.position, waypoints[m.NextWaypointIndex]))
                        .ToList();

                    OnRaceFinished();
                }
            }

            target = waypoints[marble.NextWaypointIndex];
            toTarget = target - (Vector2)marble.Root.position;
        }

        if (toTarget.sqrMagnitude < 0.0001f)
        {
            return;
        }

        var desiredDirection = toTarget.normalized;
        marble.Forward = RotateTowards(marble.Forward, desiredDirection, marble.TurnRateRadians * dt);
        marble.Root.position += (Vector3)(marble.Forward * marble.Speed * dt);

        var angleDeg = Mathf.Atan2(marble.Forward.y, marble.Forward.x) * Mathf.Rad2Deg;
        marble.Root.rotation = Quaternion.Euler(0f, 0f, angleDeg);
    }

    private static Vector2 RotateTowards(Vector2 currentDirection, Vector2 targetDirection, float maxRadiansDelta)
    {
        if (currentDirection.sqrMagnitude < 0.0001f)
        {
            return targetDirection.normalized;
        }

        if (targetDirection.sqrMagnitude < 0.0001f)
        {
            return currentDirection.normalized;
        }

        var currentAngle = Mathf.Atan2(currentDirection.y, currentDirection.x);
        var targetAngle = Mathf.Atan2(targetDirection.y, targetDirection.x);
        var nextAngle = Mathf.MoveTowardsAngle(currentAngle * Mathf.Rad2Deg, targetAngle * Mathf.Rad2Deg, maxRadiansDelta * Mathf.Rad2Deg) * Mathf.Deg2Rad;

        return new Vector2(Mathf.Cos(nextAngle), Mathf.Sin(nextAngle));
    }

    private void OnRaceFinished()
    {
        UpdateScoreboard();

        var winner = finishOrder.Count > 0 ? finishOrder[0] : null;
        if (winner != null)
        {
            Debug.Log($"MarbleRace winner: {winner.Name} at {winner.FinishTime:0.00}s");
        }

        WriteRunOutput();
    }

    private void SpawnMarbles(Transform parent, int width, int height)
    {
        var worldArea = width * height;
        var scaledCount = Mathf.RoundToInt(worldArea / 128f);
        var count = Mathf.Clamp(Mathf.Max(DefaultMarbleCount, scaledCount), 8, MaxMarbleCount);

        var startPoint = waypoints[0];
        var startDirection = (waypoints[1] - waypoints[0]).normalized;
        var perp = new Vector2(-startDirection.y, startDirection.x);

        for (var i = 0; i < count; i++)
        {
            var marbleName = $"M{i + 1:000}";
            var marbleRoot = new GameObject(marbleName).transform;
            marbleRoot.SetParent(parent, false);

            var row = i / 8;
            var col = i % 8;
            var offset = (-perp * (col - 3.5f) * 0.6f) - (startDirection * row * 0.6f);
            marbleRoot.position = (Vector3)(startPoint + offset);

            var baseColor = Color.HSVToRGB((float)random.NextDouble(), 0.6f + (float)random.NextDouble() * 0.25f, 0.85f + (float)random.NextDouble() * 0.15f);
            BuildMarbleVisual(marbleRoot, baseColor, i);

            marbles.Add(new MarbleAgent
            {
                Name = marbleName,
                Root = marbleRoot,
                Speed = 2.8f + (float)random.NextDouble() * 1.2f,
                TurnRateRadians = Mathf.Deg2Rad * (120f + (float)random.NextDouble() * 110f),
                NextWaypointIndex = 1,
                Forward = startDirection,
                LapCount = 0
            });
        }
    }

    private void BuildMarbleVisual(Transform marbleRoot, Color bodyColor, int index)
    {
        var radius = MarbleRadius;
        var pixelSize = radius * 0.42f;

        var bodyContainer = new GameObject("Body").transform;
        bodyContainer.SetParent(marbleRoot, false);

        var circlePoints = new[]
        {
            new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(-1f, 0f), new Vector2(0f, 1f), new Vector2(0f, -1f),
            new Vector2(1f, 1f), new Vector2(-1f, 1f), new Vector2(1f, -1f), new Vector2(-1f, -1f),
            new Vector2(2f, 0f), new Vector2(-2f, 0f), new Vector2(0f, 2f), new Vector2(0f, -2f)
        };

        for (var i = 0; i < circlePoints.Length; i++)
        {
            var p = circlePoints[i];
            if (p.magnitude > 2.1f)
            {
                continue;
            }

            var pixel = new GameObject($"Px{i}").transform;
            pixel.SetParent(bodyContainer, false);
            pixel.localPosition = new Vector3(p.x * pixelSize, p.y * pixelSize, 0f);
            var sr = pixel.gameObject.AddComponent<SpriteRenderer>();
            sr.sprite = pixelSprite;
            sr.color = bodyColor;
            sr.sortingLayerName = agentsSortingLayer;
            sr.sortingOrder = 0;
            pixel.localScale = Vector3.one * pixelSize;
        }

        var highlight = new GameObject("Highlight").transform;
        highlight.SetParent(marbleRoot, false);
        highlight.localPosition = new Vector3(-radius * 0.42f, radius * 0.42f, -0.01f);
        var highlightRenderer = highlight.gameObject.AddComponent<SpriteRenderer>();
        highlightRenderer.sprite = pixelSprite;
        highlightRenderer.color = new Color(1f, 1f, 1f, 0.45f);
        highlightRenderer.sortingLayerName = agentsSortingLayer;
        highlightRenderer.sortingOrder = 2;
        highlight.localScale = Vector3.one * radius * 0.75f;

        CreateIdentityPattern(marbleRoot, bodyColor, index);
    }

    private void CreateIdentityPattern(Transform parent, Color bodyColor, int index)
    {
        var accent = Color.Lerp(bodyColor, Color.white, 0.45f);
        accent.a = 1f;

        var patternType = index % 3;
        if (patternType == 0)
        {
            var stripe = new GameObject("PatternStripe").transform;
            stripe.SetParent(parent, false);
            stripe.localPosition = Vector3.zero;
            var sr = stripe.gameObject.AddComponent<SpriteRenderer>();
            sr.sprite = pixelSprite;
            sr.color = accent;
            sr.sortingLayerName = agentsSortingLayer;
            sr.sortingOrder = 1;
            stripe.localScale = new Vector3(MarbleRadius * 0.45f, MarbleRadius * 1.15f, 1f);
        }
        else if (patternType == 1)
        {
            var dot = new GameObject("PatternDot").transform;
            dot.SetParent(parent, false);
            dot.localPosition = new Vector3(0.05f, -0.02f, -0.01f);
            var sr = dot.gameObject.AddComponent<SpriteRenderer>();
            sr.sprite = pixelSprite;
            sr.color = accent;
            sr.sortingLayerName = agentsSortingLayer;
            sr.sortingOrder = 1;
            dot.localScale = Vector3.one * MarbleRadius * 0.6f;
        }
        else
        {
            var split = new GameObject("PatternSplit").transform;
            split.SetParent(parent, false);
            split.localPosition = new Vector3(0f, -MarbleRadius * 0.2f, -0.01f);
            var sr = split.gameObject.AddComponent<SpriteRenderer>();
            sr.sprite = pixelSprite;
            sr.color = accent;
            sr.sortingLayerName = agentsSortingLayer;
            sr.sortingOrder = 1;
            split.localScale = new Vector3(MarbleRadius * 1.1f, MarbleRadius * 0.5f, 1f);
        }
    }

    private void CreateTrackWaypoints(int width, int height)
    {
        waypoints.Clear();

        var marginX = Mathf.Max(4f, width * 0.12f);
        var marginY = Mathf.Max(4f, height * 0.12f);

        waypoints.Add(new Vector2(marginX, marginY));
        waypoints.Add(new Vector2(width * 0.5f, marginY));
        waypoints.Add(new Vector2(width - marginX, marginY));
        waypoints.Add(new Vector2(width - marginX, height * 0.5f));
        waypoints.Add(new Vector2(width - marginX, height - marginY));
        waypoints.Add(new Vector2(width * 0.5f, height - marginY));
        waypoints.Add(new Vector2(marginX, height - marginY));
        waypoints.Add(new Vector2(marginX, height * 0.5f));
    }

    private void CreateTrackVisual(Transform parent)
    {
        var trackObject = new GameObject("Track");
        trackObject.transform.SetParent(parent, false);

        var line = trackObject.AddComponent<LineRenderer>();
        line.material = new Material(Shader.Find("Sprites/Default"));
        line.useWorldSpace = true;
        line.loop = true;
        line.positionCount = waypoints.Count;
        line.widthMultiplier = 0.15f;
        line.startColor = new Color(0.18f, 0.2f, 0.24f, 1f);
        line.endColor = line.startColor;
        line.sortingLayerName = agentsSortingLayer;
        line.sortingOrder = -2;

        for (var i = 0; i < waypoints.Count; i++)
        {
            line.SetPosition(i, new Vector3(waypoints[i].x, waypoints[i].y, 0f));
        }
    }

    private void CreateScoreboard()
    {
        var canvasObject = new GameObject("MarbleRaceScoreboard");
        scoreboardCanvas = canvasObject.AddComponent<Canvas>();
        scoreboardCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasObject.AddComponent<CanvasScaler>();
        canvasObject.AddComponent<GraphicRaycaster>();

        var textObject = new GameObject("RaceText");
        textObject.transform.SetParent(canvasObject.transform, false);

        scoreboardText = textObject.AddComponent<Text>();
        scoreboardText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (scoreboardText.font == null)
        {
            scoreboardText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        if (scoreboardText.font == null)
        {
            Debug.LogWarning("MarbleRace: Could not create onscreen scoreboard font; using Debug.Log fallback.");
            UnityEngine.Object.Destroy(canvasObject);
            scoreboardCanvas = null;
            scoreboardText = null;
            return;
        }

        scoreboardText.alignment = TextAnchor.UpperLeft;
        scoreboardText.color = new Color(0.92f, 0.95f, 1f, 0.95f);
        scoreboardText.fontSize = 14;
        scoreboardText.horizontalOverflow = HorizontalWrapMode.Wrap;
        scoreboardText.verticalOverflow = VerticalWrapMode.Overflow;

        var rect = scoreboardText.rectTransform;
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(8f, -8f);
        rect.sizeDelta = new Vector2(360f, 180f);

        UpdateScoreboard();
    }

    private void UpdateScoreboard()
    {
        if (scoreboardText == null)
        {
            return;
        }

        scoreboardText.text = BuildScoreboardText();
    }

    private string BuildScoreboardText()
    {
        var ranking = marbles
            .OrderByDescending(m => m.LapCount)
            .ThenByDescending(m => m.NextWaypointIndex)
            .ThenBy(m => Vector2.Distance(m.Root.position, waypoints[m.NextWaypointIndex]))
            .Take(8)
            .ToList();

        var lines = new List<string>
        {
            $"MarbleRace  |  Laps to win: {RequiredLapsToWin}",
            $"Time: {elapsedTime:0.0}s"
        };

        for (var i = 0; i < ranking.Count; i++)
        {
            var marble = ranking[i];
            lines.Add($"{i + 1}. {marble.Name}  L{marble.LapCount} W{marble.NextWaypointIndex + 1}/{waypoints.Count}");
        }

        if (raceFinished && finishOrder.Count > 0)
        {
            lines.Add($"Winner: {finishOrder[0].Name}");
        }

        return string.Join("\n", lines);
    }

    private void WriteRunOutput()
    {
        var outputRoot = config.recording?.outputRoot;
        if (string.IsNullOrWhiteSpace(outputRoot))
        {
            outputRoot = "Runs";
        }

        var projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
        var outputDirectory = Path.GetFullPath(Path.Combine(projectRoot, outputRoot));
        Directory.CreateDirectory(outputDirectory);

        var result = new MarbleRaceResult
        {
            simulationId = Id,
            seed = config.seed,
            elapsedTime = elapsedTime,
            requiredLaps = RequiredLapsToWin,
            finishOrder = finishOrder.Select((m, idx) => new MarbleResultEntry
            {
                place = idx + 1,
                marble = m.Name,
                lapCount = m.LapCount,
                waypointIndex = m.NextWaypointIndex,
                finishTime = m.FinishTime
            }).ToArray()
        };

        var json = JsonUtility.ToJson(result, true);
        var fileName = $"marble_race_{DateTime.UtcNow:yyyyMMdd_HHmmss}_seed{config.seed}.json";
        var filePath = Path.Combine(outputDirectory, fileName);
        File.WriteAllText(filePath, json);

        Debug.Log($"MarbleRace wrote race results to '{filePath}'.");
    }

    private Sprite CreatePixelSprite()
    {
        var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();

        return Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
    }

    private static string ResolveAgentsSortingLayer()
    {
        foreach (var layer in SortingLayer.layers)
        {
            if (layer.name == "Agents")
            {
                return "Agents";
            }
        }

        return "Default";
    }

    private class MarbleAgent
    {
        public string Name;
        public Transform Root;
        public float Speed;
        public float TurnRateRadians;
        public int NextWaypointIndex;
        public int LapCount;
        public Vector2 Forward;
        public bool Finished;
        public float FinishTime;
    }

    [Serializable]
    private class MarbleRaceResult
    {
        public string simulationId;
        public int seed;
        public float elapsedTime;
        public int requiredLaps;
        public MarbleResultEntry[] finishOrder;
    }

    [Serializable]
    private class MarbleResultEntry
    {
        public int place;
        public string marble;
        public int lapCount;
        public int waypointIndex;
        public float finishTime;
    }
}
