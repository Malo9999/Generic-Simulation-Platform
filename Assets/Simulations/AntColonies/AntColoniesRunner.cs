using System;
using UnityEngine;

public class AntColoniesRunner : MonoBehaviour, ITickableSimulationRunner
{
    private const int QueenCount = 1;
    private const int WorkerCount = 8;
    private const int WarriorCount = 3;
    private const int AntCount = QueenCount + WorkerCount + WarriorCount;
    private const int SpawnDebugCount = 5;
    private const float SpeedMultiplier = 0.20f;
    private const float IdleSpeedThreshold = 0.2f;
    private const float RunSpeedThreshold = 1.6f;

    [SerializeField] private bool logSpawnIdentity = true;

    private Transform[] ants;
    private SpriteRenderer[] antBaseRenderers;
    private SpriteRenderer[] antMaskRenderers;
    private Vector2[] positions;
    private Vector2[] velocities;
    private EntityIdentity[] identities;
    private AntRole[] roles;
    private int nextEntityId;
    private float halfWidth = 32f;
    private float halfHeight = 32f;

    private static Sprite debugFallbackSprite;
    private bool hasLoggedAssignedSpriteIds;
    private AntWorldState worldState;

    public void Initialize(ScenarioConfig config)
    {
        Shutdown();
        EnsureMainCamera();
        BuildWorld(config);
        BuildAnts(config);
        Debug.Log($"{nameof(AntColoniesRunner)} Initialize seed={config.seed}, scenario={config.scenarioName}");
    }

    public void Tick(int tickIndex, float dt)
    {
        if (ants == null)
        {
            return;
        }

        for (var i = 0; i < ants.Length; i++)
        {
            positions[i] += velocities[i] * dt;

            if (positions[i].x < -halfWidth || positions[i].x > halfWidth)
            {
                positions[i].x = Mathf.Clamp(positions[i].x, -halfWidth, halfWidth);
                velocities[i].x *= -1f;
            }

            if (positions[i].y < -halfHeight || positions[i].y > halfHeight)
            {
                positions[i].y = Mathf.Clamp(positions[i].y, -halfHeight, halfHeight);
                velocities[i].y *= -1f;
            }

            ants[i].localPosition = new Vector3(positions[i].x, positions[i].y, 0f);
            ants[i].localRotation = Quaternion.identity;
            ApplyVisual(i, tickIndex);
        }
    }

    public void Shutdown()
    {
        if (ants != null)
        {
            for (var i = 0; i < ants.Length; i++)
            {
                if (ants[i] != null)
                {
                    Destroy(ants[i].gameObject);
                }
            }
        }

        var worldView = transform.Find("AntWorldView");
        if (worldView != null)
        {
            Destroy(worldView.gameObject);
        }

        ants = null;
        antBaseRenderers = null;
        antMaskRenderers = null;
        positions = null;
        velocities = null;
        identities = null;
        roles = null;
        worldState = null;
        Debug.Log("AntColoniesRunner Shutdown");
    }


    private void BuildWorld(ScenarioConfig config)
    {
        worldState = AntWorldGenerator.Generate(config);
        AntWorldViewBuilder.BuildOrRefresh(transform, config, worldState);
    }

    private void BuildAnts(ScenarioConfig config)
    {
        nextEntityId = 0;
        hasLoggedAssignedSpriteIds = false;

        halfWidth = Mathf.Max(1f, (config?.world?.arenaWidth ?? 64) * 0.5f);
        halfHeight = Mathf.Max(1f, (config?.world?.arenaHeight ?? 64) * 0.5f);

        ants = new Transform[AntCount];
        antBaseRenderers = new SpriteRenderer[AntCount];
        antMaskRenderers = new SpriteRenderer[AntCount];
        positions = new Vector2[AntCount];
        velocities = new Vector2[AntCount];
        identities = new EntityIdentity[AntCount];
        roles = new AntRole[AntCount];

        for (var i = 0; i < AntCount; i++)
        {
            var role = i == 0 ? AntRole.Queen : (i <= WorkerCount ? AntRole.Worker : AntRole.Warrior);
            var roleId = RoleToStableString(role);
            var identity = IdentityService.Create(
                entityId: nextEntityId++,
                teamId: 0,
                role: roleId,
                variantCount: Enum.GetValues(typeof(AntRole)).Length,
                scenarioSeed: config?.seed ?? 0,
                simIdOrSalt: "AntColonies");

            identities[i] = identity;
            roles[i] = RoleFromIdentity(identity);

            var ant = new GameObject($"Ant_{role}_{i}");
            ant.transform.SetParent(transform, false);
            ant.transform.localScale = Vector3.one * GetRoleScale(roles[i]);
            ant.transform.localRotation = Quaternion.identity;

            var baseObject = new GameObject("Base");
            baseObject.transform.SetParent(ant.transform, false);
            var baseRenderer = baseObject.AddComponent<SpriteRenderer>();
            baseRenderer.sortingOrder = 0;
            baseRenderer.color = Color.white;

            var maskObject = new GameObject("Mask");
            maskObject.transform.SetParent(ant.transform, false);
            var maskRenderer = maskObject.AddComponent<SpriteRenderer>();
            maskRenderer.sortingOrder = baseRenderer.sortingOrder + 1;
            var colonyColor = GetRoleColor(roles[i], identity.teamId);
            maskRenderer.color = new Color(colonyColor.r, colonyColor.g, colonyColor.b, 0.75f);

            var startX = RngService.Global.Range(-halfWidth, halfWidth);
            var startY = RngService.Global.Range(-halfHeight, halfHeight);
            var speed = GetRoleSpeed(roles[i]);
            var angle = RngService.Global.Range(0f, Mathf.PI * 2f);

            positions[i] = new Vector2(startX, startY);
            velocities[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed;
            ant.transform.localPosition = new Vector3(startX, startY, 0f);

            ants[i] = ant.transform;
            antBaseRenderers[i] = baseRenderer;
            antMaskRenderers[i] = maskRenderer;
            ApplyVisual(i, 0);

            if (logSpawnIdentity && i < SpawnDebugCount)
            {
                Debug.Log($"{nameof(AntColoniesRunner)} spawn[{i}] {identity}");
            }
        }
    }

    private void ApplyVisual(int index, int tickIndex)
    {
        var role = roles[index];
        var roleId = RoleToStableString(role);
        var state = ResolveState(velocities[index].magnitude);
        var speciesId = ContentPackService.GetSpeciesId("ant", identities[index].variant);

        var fps = ContentPackService.GetClipFpsOrDefault("ant", roleId, "adult", state, 8);
        var frameCount = ResolveFrameCount(speciesId, roleId, state);
        var ticksPerFrame = Mathf.Max(1, Mathf.RoundToInt(60f / Mathf.Max(1, fps)));
        var frame = (tickIndex / ticksPerFrame) % frameCount;

        var baseId = $"agent:ant:{speciesId}:{roleId}:adult:{state}:{frame:00}";
        var maskId = baseId + "_mask";

        if (ContentPackService.TryGetSprite(baseId, out var baseSprite))
        {
            antBaseRenderers[index].sprite = baseSprite;
        }
        else
        {
            antBaseRenderers[index].sprite = GetDebugFallbackSprite();
        }

        antBaseRenderers[index].color = Color.white;

        antMaskRenderers[index].sprite = ContentPackService.TryGetSprite(maskId, out var maskSprite) ? maskSprite : null;
        var colonyColor = GetRoleColor(role, identities[index].teamId);
        antMaskRenderers[index].color = new Color(colonyColor.r, colonyColor.g, colonyColor.b, 0.75f);

        if (!hasLoggedAssignedSpriteIds && index == 0)
        {
            hasLoggedAssignedSpriteIds = true;
            Debug.Log($"{nameof(AntColoniesRunner)} sprite ids base={baseId}, mask={maskId}");
        }
    }

    private static string ResolveState(float speed)
    {
        if (speed < IdleSpeedThreshold)
        {
            return "idle";
        }

        return speed < RunSpeedThreshold ? "walk" : "run";
    }

    private static int ResolveFrameCount(string speciesId, string roleId, string state)
    {
        var keyPrefix = $"agent:ant:{speciesId}:{roleId}:adult:{state}";
        if (ContentPackService.Current != null)
        {
            if (ContentPackService.Current.TryGetClipMetadata($"agent:ant:{roleId}:adult:{state}", out var clip) && clip.frameCount > 0)
            {
                return clip.frameCount;
            }

            var inferred = ContentPackService.Current.InferFrameCountByPrefix(keyPrefix);
            if (inferred > 0)
            {
                return inferred;
            }
        }

        return state == "idle" ? 2 : 4;
    }

    private static AntRole RoleFromIdentity(EntityIdentity identity)
    {
        return identity.role switch
        {
            "queen" => AntRole.Queen,
            "soldier" => AntRole.Warrior,
            "warrior" => AntRole.Warrior,
            _ => AntRole.Worker
        };
    }

    private static string RoleToStableString(AntRole role)
    {
        return role switch
        {
            AntRole.Queen => "queen",
            AntRole.Warrior => "soldier",
            _ => "worker"
        };
    }

    private static float GetRoleSpeed(AntRole role)
    {
        float s = role switch
        {
            AntRole.Queen => RngService.Global.Range(3f, 6f),
            AntRole.Worker => RngService.Global.Range(8f, 13f),
            _ => RngService.Global.Range(6f, 10f)
        };

        return s * SpeedMultiplier;
    }

    private static float GetRoleScale(AntRole role)
    {
        return role switch
        {
            AntRole.Queen => 1.4f,
            AntRole.Warrior => 1.2f,
            _ => 1f
        };
    }

    private static Color GetRoleColor(AntRole role, int teamId)
    {
        var tintShift = (teamId % 3) * 0.03f;
        return role switch
        {
            AntRole.Queen => new Color(0.28f + tintShift, 0.22f, 0.4f),
            AntRole.Worker => new Color(0.45f + tintShift, 0.28f, 0.18f),
            _ => new Color(0.5f + tintShift, 0.12f, 0.12f)
        };
    }

    private static Sprite GetDebugFallbackSprite()
    {
        if (debugFallbackSprite != null)
        {
            return debugFallbackSprite;
        }

        var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };
        texture.SetPixel(0, 0, Color.magenta);
        texture.Apply(false, false);

        debugFallbackSprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
        return debugFallbackSprite;
    }

    private void EnsureMainCamera()
    {
        if (Camera.main != null)
        {
            return;
        }

        var cameraObject = new GameObject("Main Camera");
        cameraObject.tag = "MainCamera";

        var cameraComponent = cameraObject.AddComponent<Camera>();
        cameraComponent.orthographic = true;
        cameraComponent.orthographicSize = Mathf.Max(halfHeight + 2f, 10f);
        cameraObject.transform.position = new Vector3(0f, 0f, -10f);
    }
}
