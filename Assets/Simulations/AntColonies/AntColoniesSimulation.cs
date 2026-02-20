using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

public class AntColoniesSimulation : ISimulation
{
    public string Id => "AntColonies";

    private const int MaxTotalAnts = 400;
    private const float MatchDurationSeconds = 180f;
    private const float SummaryIntervalSeconds = 5f;
    private const float FoodRespawnIntervalSeconds = 1.1f;
    private const int MaxFoodPellets = 80;

    private readonly List<Colony> colonies = new();
    private readonly List<AntAgent> ants = new();
    private readonly List<FoodPellet> foods = new();

    private ScenarioConfig config;
    private Transform simRoot;
    private Transform antsRoot;
    private Transform foodRoot;
    private Transform nestRoot;

    private System.Random random;
    private float arenaWidth;
    private float arenaHeight;
    private float elapsed;
    private float summaryTimer;
    private float foodRespawnTimer;
    private bool isFinished;

    private Texture2D circleTexture;
    private Texture2D whiteTexture;
    private Sprite circleSprite;
    private Sprite squareSprite;

    public void Initialize(ScenarioConfig cfg, Transform root)
    {
        config = cfg ?? new ScenarioConfig();
        simRoot = root;
        arenaWidth = Mathf.Max(10f, config.world?.arenaWidth ?? 64);
        arenaHeight = Mathf.Max(10f, config.world?.arenaHeight ?? 64);
        random = new System.Random(config.seed == 0 ? Environment.TickCount : config.seed);

        BuildRoots();
        BuildSprites();
        SpawnColoniesAndAnts();
        SpawnInitialFood();

        Debug.Log($"AntColonies initialized: colonies={colonies.Count}, ants={ants.Count}, arena={arenaWidth}x{arenaHeight}");
    }

    public void Tick(float dt)
    {
        if (isFinished)
        {
            return;
        }

        elapsed += dt;
        summaryTimer += dt;
        foodRespawnTimer += dt;

        if (foodRespawnTimer >= FoodRespawnIntervalSeconds)
        {
            foodRespawnTimer = 0f;
            TrySpawnFood();
        }

        for (var i = ants.Count - 1; i >= 0; i--)
        {
            var ant = ants[i];
            if (!ant.IsAlive)
            {
                continue;
            }

            UpdateAnt(ant, dt);

            if (ant.Health <= 0f)
            {
                KillAnt(ant);
            }
        }

        if (summaryTimer >= SummaryIntervalSeconds)
        {
            summaryTimer = 0f;
            LogPeriodicSummary();
        }

        if (CheckEndCondition())
        {
            FinishMatch("end-condition");
        }
    }

    public void Dispose()
    {
        if (!isFinished && colonies.Count > 0)
        {
            FinishMatch("dispose");
        }

        foreach (var ant in ants)
        {
            if (ant.Root != null)
            {
                UnityEngine.Object.Destroy(ant.Root.gameObject);
            }
        }

        foreach (var food in foods)
        {
            if (food.Root != null)
            {
                UnityEngine.Object.Destroy(food.Root.gameObject);
            }
        }

        foreach (var colony in colonies)
        {
            if (colony.NestTransform != null)
            {
                UnityEngine.Object.Destroy(colony.NestTransform.gameObject);
            }
        }

        if (circleTexture != null)
        {
            UnityEngine.Object.Destroy(circleTexture);
        }

        if (whiteTexture != null)
        {
            UnityEngine.Object.Destroy(whiteTexture);
        }

        colonies.Clear();
        ants.Clear();
        foods.Clear();
    }

    private void BuildRoots()
    {
        antsRoot = CreateOrFind("Ants", simRoot);
        foodRoot = CreateOrFind("Food", simRoot);
        nestRoot = CreateOrFind("Nests", simRoot);
    }

    private void BuildSprites()
    {
        whiteTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };
        whiteTexture.SetPixel(0, 0, Color.white);
        whiteTexture.Apply();

        circleTexture = BuildCircleTexture(16);

        squareSprite = Sprite.Create(whiteTexture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
        circleSprite = Sprite.Create(circleTexture, new Rect(0, 0, circleTexture.width, circleTexture.height), new Vector2(0.5f, 0.5f), 16f);
    }

    private void SpawnColoniesAndAnts()
    {
        var area = arenaWidth * arenaHeight;
        var colonyCount = area >= 5200f ? 3 : 2;
        colonyCount = Mathf.Clamp(colonyCount, 2, 4);

        var antsPerColony = Mathf.Clamp(Mathf.RoundToInt(area / 90f), 30, 80);
        var totalAnts = colonyCount * antsPerColony;
        if (totalAnts > MaxTotalAnts)
        {
            antsPerColony = Mathf.Max(30, MaxTotalAnts / colonyCount);
        }

        var palette = new[]
        {
            new Color(0.84f, 0.26f, 0.23f),
            new Color(0.22f, 0.63f, 0.92f),
            new Color(0.21f, 0.77f, 0.35f),
            new Color(0.86f, 0.72f, 0.2f)
        };

        for (var i = 0; i < colonyCount; i++)
        {
            var nestPos = PickNestPosition(i, colonyCount);
            var colony = new Colony
            {
                Id = i,
                Name = $"Colony-{i + 1}",
                Color = palette[i % palette.Length],
                NestPosition = nestPos
            };
            colony.NestTransform = BuildNestVisual(colony);
            colonies.Add(colony);

            for (var j = 0; j < antsPerColony; j++)
            {
                var ant = CreateAnt(colony, nestPos + RandomInsideCircle(2.2f));
                ants.Add(ant);
            }
        }
    }

    private void SpawnInitialFood()
    {
        var targetFoodCount = Mathf.Clamp(Mathf.RoundToInt((arenaWidth * arenaHeight) / 95f), 20, MaxFoodPellets);
        for (var i = 0; i < targetFoodCount; i++)
        {
            SpawnFood();
        }
    }

    private void TrySpawnFood()
    {
        var desired = Mathf.Clamp(Mathf.RoundToInt((arenaWidth * arenaHeight) / 95f), 20, MaxFoodPellets);
        if (foods.Count(f => f.IsAvailable) < desired)
        {
            SpawnFood();
        }
    }

    private void SpawnFood()
    {
        var go = new GameObject($"Food-{foods.Count + 1}");
        go.transform.SetParent(foodRoot, false);
        var renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = circleSprite;
        renderer.color = new Color(0.82f, 0.95f, 0.28f);
        renderer.sortingOrder = 8;

        var pos = new Vector2(Rand(1.5f, arenaWidth - 1.5f), Rand(1.5f, arenaHeight - 1.5f));
        go.transform.localPosition = new Vector3(pos.x, pos.y, 0f);
        go.transform.localScale = new Vector3(0.35f, 0.35f, 1f);

        foods.Add(new FoodPellet
        {
            Root = go.transform,
            Position = pos,
            Renderer = renderer,
            IsAvailable = true
        });
    }

    private AntAgent CreateAnt(Colony colony, Vector2 spawnPos)
    {
        var root = new GameObject($"Ant-{colony.Id}-{colony.AliveAnts + 1}").transform;
        root.SetParent(antsRoot, false);
        root.localPosition = new Vector3(spawnPos.x, spawnPos.y, 0f);

        var bodyLength = 0.22f;

        var abdomen = CreateBodySegment("Abdomen", root, colony.Color, new Vector3(-bodyLength, 0f, 0f), 0.24f, 10);
        var thorax = CreateBodySegment("Thorax", root, colony.Color * 0.92f, Vector3.zero, 0.19f, 11);
        var head = CreateBodySegment("Head", root, colony.Color * 0.85f, new Vector3(bodyLength, 0f, 0f), 0.16f, 12);

        var marking = new GameObject("Marking");
        marking.transform.SetParent(root, false);
        var markingRenderer = marking.AddComponent<SpriteRenderer>();
        markingRenderer.sprite = squareSprite;
        markingRenderer.color = Color.white;
        markingRenderer.sortingOrder = 13;
        marking.transform.localPosition = new Vector3(0f, -0.02f, 0f);
        marking.transform.localScale = new Vector3(0.07f + 0.015f * colony.Id, 0.035f, 1f);

        var antennaLeft = CreateAntenna("AntennaLeft", root, new Vector3(bodyLength + 0.05f, 0.05f, 0f), 30f);
        var antennaRight = CreateAntenna("AntennaRight", root, new Vector3(bodyLength + 0.05f, -0.05f, 0f), -30f);

        var ant = new AntAgent
        {
            Colony = colony,
            Root = root,
            Position = spawnPos,
            Velocity = RandomInsideCircle(1f).normalized,
            Speed = Rand(2.4f, 3.4f),
            Health = 100f,
            IsAlive = true,
            WanderHeading = RandomInsideCircle(1f).normalized,
            Abdomen = abdomen,
            Thorax = thorax,
            Head = head,
            Marking = markingRenderer,
            AntennaLeft = antennaLeft,
            AntennaRight = antennaRight
        };

        colony.AliveAnts++;
        return ant;
    }

    private SpriteRenderer CreateBodySegment(string name, Transform parent, Color color, Vector3 offset, float scale, int sortingOrder)
    {
        var segment = new GameObject(name);
        segment.transform.SetParent(parent, false);
        segment.transform.localPosition = offset;
        segment.transform.localScale = new Vector3(scale, scale, 1f);

        var renderer = segment.AddComponent<SpriteRenderer>();
        renderer.sprite = circleSprite;
        renderer.color = color;
        renderer.sortingOrder = sortingOrder;
        return renderer;
    }

    private LineRenderer CreateAntenna(string name, Transform parent, Vector3 origin, float directionDegrees)
    {
        var antenna = new GameObject(name);
        antenna.transform.SetParent(parent, false);

        var line = antenna.AddComponent<LineRenderer>();
        line.material = new Material(Shader.Find("Sprites/Default"));
        line.useWorldSpace = false;
        line.positionCount = 2;
        line.widthMultiplier = 0.025f;
        line.startColor = new Color(0.9f, 0.9f, 0.9f, 0.95f);
        line.endColor = line.startColor;
        line.sortingOrder = 14;

        var dir = Quaternion.Euler(0f, 0f, directionDegrees) * Vector3.right;
        line.SetPosition(0, origin);
        line.SetPosition(1, origin + dir * 0.15f);
        return line;
    }

    private Transform BuildNestVisual(Colony colony)
    {
        var nest = new GameObject($"Nest-{colony.Id + 1}").transform;
        nest.SetParent(nestRoot, false);
        nest.localPosition = new Vector3(colony.NestPosition.x, colony.NestPosition.y, 0f);

        var outer = new GameObject("Outer");
        outer.transform.SetParent(nest, false);
        var outerRenderer = outer.AddComponent<SpriteRenderer>();
        outerRenderer.sprite = circleSprite;
        outerRenderer.color = colony.Color * 0.6f;
        outerRenderer.sortingOrder = 4;
        outer.transform.localScale = new Vector3(1.3f, 1.3f, 1f);

        var inner = new GameObject("Inner");
        inner.transform.SetParent(nest, false);
        var innerRenderer = inner.AddComponent<SpriteRenderer>();
        innerRenderer.sprite = circleSprite;
        innerRenderer.color = colony.Color;
        innerRenderer.sortingOrder = 5;
        inner.transform.localScale = new Vector3(0.75f, 0.75f, 1f);

        return nest;
    }

    private void UpdateAnt(AntAgent ant, float dt)
    {
        var nearestFood = FindNearestFood(ant.Position, 4.2f);
        var nearestEnemy = FindNearestEnemy(ant, 2.8f, out var enemyDistance);
        var sameColonyCenter = GetNearbySameColonyCenter(ant, 2.2f);

        var desiredDirection = ant.WanderHeading;
        ant.WanderTimer -= dt;

        if (nearestEnemy != null)
        {
            desiredDirection = (nearestEnemy.Position - ant.Position).normalized;
            ant.TargetEnemy = nearestEnemy;

            if (enemyDistance <= 0.25f)
            {
                var damage = 24f * dt;
                nearestEnemy.Health -= damage;
                ant.Health -= damage * 0.65f;
            }
        }
        else if (ant.CarryingFood)
        {
            desiredDirection = (ant.Colony.NestPosition - ant.Position).normalized;
            if (Vector2.Distance(ant.Position, ant.Colony.NestPosition) <= 0.9f)
            {
                ant.CarryingFood = false;
                ant.Colony.FoodDelivered++;
                ant.Head.color = ant.Colony.Color * 0.85f;
            }
        }
        else if (nearestFood != null)
        {
            ant.TargetFood = nearestFood;
            desiredDirection = (nearestFood.Position - ant.Position).normalized;

            if (Vector2.Distance(ant.Position, nearestFood.Position) <= 0.32f && nearestFood.IsAvailable)
            {
                nearestFood.IsAvailable = false;
                nearestFood.Renderer.enabled = false;
                ant.CarryingFood = true;
                ant.Head.color = new Color(0.95f, 0.95f, 0.4f);
            }
        }

        if (sameColonyCenter.HasValue)
        {
            var cohesionDirection = (sameColonyCenter.Value - ant.Position).normalized;
            desiredDirection = (desiredDirection * 0.85f + cohesionDirection * 0.15f).normalized;
        }

        if (ant.WanderTimer <= 0f)
        {
            ant.WanderTimer = Rand(0.6f, 1.8f);
            ant.WanderHeading = (ant.WanderHeading + RandomInsideCircle(0.8f)).normalized;
            desiredDirection = (desiredDirection + ant.WanderHeading * 0.2f).normalized;
        }

        var targetVelocity = desiredDirection * ant.Speed;
        ant.Velocity = Vector2.Lerp(ant.Velocity, targetVelocity, dt * 3.8f);
        ant.Position += ant.Velocity * dt;

        ClampToArena(ant);
        ant.Root.localPosition = new Vector3(ant.Position.x, ant.Position.y, 0f);

        if (ant.Velocity.sqrMagnitude > 0.001f)
        {
            var angle = Mathf.Atan2(ant.Velocity.y, ant.Velocity.x) * Mathf.Rad2Deg;
            ant.Root.localRotation = Quaternion.Euler(0f, 0f, angle);
        }
    }

    private FoodPellet FindNearestFood(Vector2 position, float radius)
    {
        FoodPellet nearest = null;
        var bestDist = radius;

        foreach (var food in foods)
        {
            if (!food.IsAvailable)
            {
                continue;
            }

            var dist = Vector2.Distance(position, food.Position);
            if (dist < bestDist)
            {
                bestDist = dist;
                nearest = food;
            }
        }

        return nearest;
    }

    private AntAgent FindNearestEnemy(AntAgent ant, float radius, out float foundDistance)
    {
        AntAgent nearest = null;
        var bestDist = radius;

        foreach (var other in ants)
        {
            if (!other.IsAlive || other.Colony == ant.Colony)
            {
                continue;
            }

            var dist = Vector2.Distance(ant.Position, other.Position);
            if (dist < bestDist)
            {
                bestDist = dist;
                nearest = other;
            }
        }

        foundDistance = bestDist;
        return nearest;
    }

    private Vector2? GetNearbySameColonyCenter(AntAgent ant, float radius)
    {
        var sum = Vector2.zero;
        var count = 0;

        foreach (var other in ants)
        {
            if (!other.IsAlive || other == ant || other.Colony != ant.Colony)
            {
                continue;
            }

            var dist = Vector2.Distance(ant.Position, other.Position);
            if (dist <= radius)
            {
                sum += other.Position;
                count++;
            }
        }

        return count > 0 ? sum / count : null;
    }

    private void ClampToArena(AntAgent ant)
    {
        var pos = ant.Position;
        if (pos.x < 0.4f || pos.x > arenaWidth - 0.4f)
        {
            ant.Velocity = new Vector2(-ant.Velocity.x, ant.Velocity.y);
            pos.x = Mathf.Clamp(pos.x, 0.4f, arenaWidth - 0.4f);
            ant.WanderHeading = Vector2.Reflect(ant.WanderHeading, Vector2.right).normalized;
        }

        if (pos.y < 0.4f || pos.y > arenaHeight - 0.4f)
        {
            ant.Velocity = new Vector2(ant.Velocity.x, -ant.Velocity.y);
            pos.y = Mathf.Clamp(pos.y, 0.4f, arenaHeight - 0.4f);
            ant.WanderHeading = Vector2.Reflect(ant.WanderHeading, Vector2.up).normalized;
        }

        ant.Position = pos;
    }

    private void KillAnt(AntAgent ant)
    {
        ant.IsAlive = false;
        ant.Colony.AliveAnts = Mathf.Max(0, ant.Colony.AliveAnts - 1);

        if (ant.Root != null)
        {
            ant.Root.gameObject.SetActive(false);
        }
    }

    private bool CheckEndCondition()
    {
        var aliveColonies = colonies.Count(c => c.AliveAnts > 0);
        return aliveColonies <= 1 || elapsed >= MatchDurationSeconds;
    }

    private void LogPeriodicSummary()
    {
        var colonyText = string.Join(", ", colonies.Select(c => $"{c.Name}: alive={c.AliveAnts}, food={c.FoodDelivered}"));
        Debug.Log($"[AntColonies t={elapsed:F1}s] {colonyText}");
    }

    private void FinishMatch(string reason)
    {
        isFinished = true;

        var winner = colonies
            .OrderByDescending(c => c.FoodDelivered + c.AliveAnts)
            .ThenByDescending(c => c.AliveAnts)
            .FirstOrDefault();

        var summary = new MatchSummary
        {
            scenarioName = config?.scenarioName ?? "AntColonies",
            reason = reason,
            elapsedSeconds = elapsed,
            winner = winner?.Name ?? "None",
            colonies = colonies.Select(c => new ColonySummary
            {
                name = c.Name,
                aliveAnts = c.AliveAnts,
                foodDelivered = c.FoodDelivered,
                score = c.FoodDelivered + c.AliveAnts
            }).ToArray()
        };

        Debug.Log($"AntColonies finished. Winner={summary.winner}, reason={reason}, elapsed={elapsed:F1}s");
        WriteSummary(summary);
    }

    private void WriteSummary(MatchSummary summary)
    {
        try
        {
            var projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Directory.GetCurrentDirectory();
            var outputRoot = config?.recording?.outputRoot;
            if (string.IsNullOrWhiteSpace(outputRoot))
            {
                outputRoot = "Runs";
            }

            var fullOutputRoot = Path.GetFullPath(Path.Combine(projectRoot, outputRoot));
            Directory.CreateDirectory(fullOutputRoot);

            var fileName = $"ant_colonies_{DateTime.Now:yyyyMMdd_HHmmss}.json";
            var outputPath = Path.Combine(fullOutputRoot, fileName);
            var json = JsonUtility.ToJson(summary, true);
            File.WriteAllText(outputPath, json);

            Debug.Log($"AntColonies summary written to: {outputPath}");
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"AntColonies failed to write summary: {ex.Message}");
        }
    }

    private Vector2 PickNestPosition(int index, int colonyCount)
    {
        var margin = 5f;
        if (colonyCount == 2)
        {
            return index == 0
                ? new Vector2(margin, margin)
                : new Vector2(arenaWidth - margin, arenaHeight - margin);
        }

        var presets = new[]
        {
            new Vector2(margin, margin),
            new Vector2(arenaWidth - margin, margin),
            new Vector2(arenaWidth * 0.5f, arenaHeight - margin),
            new Vector2(arenaWidth - margin, arenaHeight - margin)
        };

        return presets[Mathf.Clamp(index, 0, presets.Length - 1)];
    }

    private Transform CreateOrFind(string name, Transform parent)
    {
        var child = parent.Find(name);
        if (child != null)
        {
            return child;
        }

        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        return go.transform;
    }

    private Texture2D BuildCircleTexture(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };

        var center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        var radius = size * 0.5f;
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var dist = Vector2.Distance(new Vector2(x, y), center);
                var alpha = dist <= radius ? 1f : 0f;
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        tex.Apply();
        return tex;
    }

    private Vector2 RandomInsideCircle(float radius)
    {
        var angle = Rand(0f, Mathf.PI * 2f);
        var r = Mathf.Sqrt(Rand(0f, 1f)) * radius;
        return new Vector2(Mathf.Cos(angle) * r, Mathf.Sin(angle) * r);
    }

    private float Rand(float min, float max)
    {
        return min + (float)random.NextDouble() * (max - min);
    }

    [Serializable]
    private class MatchSummary
    {
        public string scenarioName;
        public string reason;
        public float elapsedSeconds;
        public string winner;
        public ColonySummary[] colonies;
    }

    [Serializable]
    private class ColonySummary
    {
        public string name;
        public int aliveAnts;
        public int foodDelivered;
        public int score;
    }

    private class Colony
    {
        public int Id;
        public string Name;
        public Color Color;
        public Vector2 NestPosition;
        public Transform NestTransform;
        public int AliveAnts;
        public int FoodDelivered;
    }

    private class AntAgent
    {
        public Colony Colony;
        public Transform Root;
        public Vector2 Position;
        public Vector2 Velocity;
        public Vector2 WanderHeading;
        public float WanderTimer;
        public float Speed;
        public float Health;
        public bool IsAlive;
        public bool CarryingFood;
        public FoodPellet TargetFood;
        public AntAgent TargetEnemy;

        public SpriteRenderer Abdomen;
        public SpriteRenderer Thorax;
        public SpriteRenderer Head;
        public SpriteRenderer Marking;
        public LineRenderer AntennaLeft;
        public LineRenderer AntennaRight;
    }

    private class FoodPellet
    {
        public Transform Root;
        public Vector2 Position;
        public SpriteRenderer Renderer;
        public bool IsAvailable;
    }
}
