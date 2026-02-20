using UnityEngine;

public class AntColoniesRunner : MonoBehaviour, ITickableSimulationRunner
{
    private const int QueenCount = 1;
    private const int WorkerCount = 8;
    private const int WarriorCount = 3;
    private const int AntCount = QueenCount + WorkerCount + WarriorCount;

    private Transform[] ants;
    private SpriteRenderer[] antOutlines;
    private SpriteRenderer[] antFills;
    private Vector2[] positions;
    private Vector2[] velocities;
    private AntRole[] roles;
    private int[] currentDirs;
    private float halfWidth = 32f;
    private float halfHeight = 32f;

    public void Initialize(ScenarioConfig config)
    {
        EnsureMainCamera();
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

            var dir = Direction8.FromVector(velocities[i]);
            if (dir == currentDirs[i])
            {
                continue;
            }

            currentDirs[i] = dir;
            antOutlines[i].sprite = AntAtlasLibrary.GetOutline(roles[i], dir);
            antFills[i].sprite = AntAtlasLibrary.GetFill(roles[i], dir);
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

        ants = null;
        antOutlines = null;
        antFills = null;
        positions = null;
        velocities = null;
        roles = null;
        currentDirs = null;
        Debug.Log("AntColoniesRunner Shutdown");
    }

    private void BuildAnts(ScenarioConfig config)
    {
        Shutdown();

        halfWidth = Mathf.Max(1f, (config?.world?.arenaWidth ?? 64) * 0.5f);
        halfHeight = Mathf.Max(1f, (config?.world?.arenaHeight ?? 64) * 0.5f);

        ants = new Transform[AntCount];
        antOutlines = new SpriteRenderer[AntCount];
        antFills = new SpriteRenderer[AntCount];
        positions = new Vector2[AntCount];
        velocities = new Vector2[AntCount];
        roles = new AntRole[AntCount];
        currentDirs = new int[AntCount];

        for (var i = 0; i < AntCount; i++)
        {
            var role = i == 0 ? AntRole.Queen : (i <= WorkerCount ? AntRole.Worker : AntRole.Warrior);
            roles[i] = role;

            var ant = new GameObject($"Ant_{role}_{i}");
            ant.transform.SetParent(transform, false);
            ant.transform.localScale = Vector3.one * GetRoleScale(role);
            ant.transform.localRotation = Quaternion.identity;

            var outlineObject = new GameObject("Outline");
            outlineObject.transform.SetParent(ant.transform, false);
            var outlineRenderer = outlineObject.AddComponent<SpriteRenderer>();
            outlineRenderer.sortingOrder = 0;

            var fillObject = new GameObject("Fill");
            fillObject.transform.SetParent(ant.transform, false);
            var fillRenderer = fillObject.AddComponent<SpriteRenderer>();
            fillRenderer.sortingOrder = 1;
            fillRenderer.color = GetRoleColor(role);

            var startX = RngService.Global.Range(-halfWidth, halfWidth);
            var startY = RngService.Global.Range(-halfHeight, halfHeight);
            var speed = GetRoleSpeed(role);
            var angle = RngService.Global.Range(0f, Mathf.PI * 2f);

            positions[i] = new Vector2(startX, startY);
            velocities[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed;
            ant.transform.localPosition = new Vector3(startX, startY, 0f);

            var initialDir = Direction8.FromVector(velocities[i]);
            currentDirs[i] = initialDir;
            outlineRenderer.sprite = AntAtlasLibrary.GetOutline(role, initialDir);
            fillRenderer.sprite = AntAtlasLibrary.GetFill(role, initialDir);

            ants[i] = ant.transform;
            antOutlines[i] = outlineRenderer;
            antFills[i] = fillRenderer;
        }
    }

    private static float GetRoleSpeed(AntRole role)
    {
        return role switch
        {
            AntRole.Queen => RngService.Global.Range(3f, 6f),
            AntRole.Worker => RngService.Global.Range(8f, 13f),
            _ => RngService.Global.Range(6f, 10f)
        };
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

    private static Color GetRoleColor(AntRole role)
    {
        return role switch
        {
            AntRole.Queen => new Color(0.28f, 0.22f, 0.4f),
            AntRole.Worker => new Color(0.45f, 0.28f, 0.18f),
            _ => new Color(0.5f, 0.12f, 0.12f)
        };
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
