#if UNITY_EDITOR
using System;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

public sealed class NewSimulationWizardWindow : EditorWindow
{
    private const string SimulationsRoot = "Assets/Simulations";
    private const string BootstrapFolder = "Assets/_Bootstrap";
    private const string CatalogPath = BootstrapFolder + "/SimulationCatalog.asset";
    private const string BootstrapOptionsPath = BootstrapFolder + "/BootstrapOptions.asset";
    private const string SimSettingsFolder = BootstrapFolder + "/SimSettings";

    private const string PendingPrefix = "GSP.NewSimulationWizard.Pending";
    private const string PendingIdKey = PendingPrefix + ".SimulationId";
    private const string PendingCreatePrefabKey = PendingPrefix + ".CreateRunnerPrefab";
    private const string PendingCreateVisualKey = PendingPrefix + ".CreateVisual";
    private const string PendingRegisterCatalogKey = PendingPrefix + ".RegisterCatalog";
    private const string PendingCreateSettingsKey = PendingPrefix + ".CreateSettings";

    [SerializeField] private string simulationId = "PredatorPreyDocu";
    [SerializeField] private bool createPreset = true;
    [SerializeField] private bool createRunnerScript = true;
    [SerializeField] private bool createRunnerPrefab = true;
    [SerializeField] private bool createSimVisualSettings = true;
    [SerializeField] private bool registerIntoCatalog = true;
    [SerializeField] private bool createSimSettings;
    [SerializeField] private string statusMessage;

    [MenuItem("GSP/Simulations/New Simulation Wizard...")]
    public static void Open() => GetWindow<NewSimulationWizardWindow>("New Simulation Wizard");

    [InitializeOnLoadMethod]
    private static void RegisterCompilationHook()
    {
        CompilationPipeline.compilationFinished -= OnCompilationFinished;
        CompilationPipeline.compilationFinished += OnCompilationFinished;
        EditorApplication.delayCall += TryAutoFinalizePending;
    }

    private static void OnCompilationFinished(object _)
    {
        TryAutoFinalizePending();
    }

    private static void TryAutoFinalizePending()
    {
        var pendingSimulationId = SessionState.GetString(PendingIdKey, string.Empty);
        if (string.IsNullOrWhiteSpace(pendingSimulationId) || EditorApplication.isCompiling)
        {
            return;
        }

        var request = BuildPendingRequest(pendingSimulationId);
        if (FinalizeAndRegister(request, true, out var message))
        {
            ClearPendingRequest();
            Debug.Log($"NewSimulationWizard: Auto-finalized '{pendingSimulationId}'. {message}");
        }
        else
        {
            Debug.LogWarning($"NewSimulationWizard: Auto-finalize for '{pendingSimulationId}' incomplete. {message}");
        }
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Scaffold a simulation by convention and register it.", EditorStyles.wordWrappedLabel);
        EditorGUILayout.Space();

        simulationId = EditorGUILayout.TextField("Simulation Id", simulationId);
        createPreset = EditorGUILayout.Toggle("Create preset", createPreset);
        createRunnerScript = EditorGUILayout.Toggle("Create runner script stub", createRunnerScript);
        createRunnerPrefab = EditorGUILayout.Toggle("Create runner prefab", createRunnerPrefab);
        createSimVisualSettings = EditorGUILayout.Toggle("Create SimVisualSettings asset", createSimVisualSettings);
        registerIntoCatalog = EditorGUILayout.Toggle("Register into SimulationCatalog", registerIntoCatalog);
        createSimSettings = EditorGUILayout.Toggle("Create SimSettings asset (optional)", createSimSettings);

        EditorGUILayout.Space();
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Create Skeleton"))
            {
                statusMessage = CreateSkeleton();
            }

            if (GUILayout.Button("Finalize/Register"))
            {
                var request = BuildRequestFromUi();
                if (FinalizeAndRegister(request, false, out var message))
                {
                    ClearPendingRequest();
                }

                statusMessage = message;
            }
        }

        EditorGUILayout.Space();
        if (!string.IsNullOrWhiteSpace(statusMessage))
        {
            EditorGUILayout.HelpBox(statusMessage, MessageType.Info);
        }
    }

    private string CreateSkeleton()
    {
        var id = simulationId?.Trim();
        if (string.IsNullOrWhiteSpace(id))
        {
            return "Simulation Id is required.";
        }

        var folder = $"{SimulationsRoot}/{id}";
        EnsureFolder(folder);
        EnsureFolder($"{folder}/Presets");

        if (createPreset)
        {
            var presetPath = $"{folder}/Presets/default.json";
            if (!File.Exists(presetPath))
            {
                var jsonBuilder = new StringBuilder();
                jsonBuilder.AppendLine("{");
                jsonBuilder.AppendLine($"  \"simulationId\": \"{id}\"");
                jsonBuilder.AppendLine("}");
                var json = jsonBuilder.ToString();
                File.WriteAllText(presetPath, json);
            }
        }

        if (createRunnerScript)
        {
            var scriptPath = $"{folder}/{id}Runner.cs";
            if (!File.Exists(scriptPath))
            {
                File.WriteAllText(scriptPath, BuildRunnerStubSource(id));
            }

            SetPendingRequest(BuildRequestFromUi());
        }

        AssetDatabase.Refresh();

        if (createRunnerScript && createRunnerPrefab)
        {
            return "Skeleton created. Waiting for compile; auto-finalize will attempt prefab + registration. If needed, click Finalize/Register after compile.";
        }

        return "Skeleton created. Click Finalize/Register to complete registration.";
    }

    private SimulationCreationRequest BuildRequestFromUi()
    {
        return new SimulationCreationRequest
        {
            simulationId = simulationId?.Trim(),
            createRunnerPrefab = createRunnerPrefab,
            createVisual = createSimVisualSettings,
            registerCatalog = registerIntoCatalog,
            createSettings = createSimSettings
        };
    }

    private static SimulationCreationRequest BuildPendingRequest(string pendingSimulationId)
    {
        return new SimulationCreationRequest
        {
            simulationId = pendingSimulationId,
            createRunnerPrefab = SessionState.GetBool(PendingCreatePrefabKey, false),
            createVisual = SessionState.GetBool(PendingCreateVisualKey, false),
            registerCatalog = SessionState.GetBool(PendingRegisterCatalogKey, false),
            createSettings = SessionState.GetBool(PendingCreateSettingsKey, false)
        };
    }

    private static void SetPendingRequest(SimulationCreationRequest request)
    {
        SessionState.SetString(PendingIdKey, request.simulationId ?? string.Empty);
        SessionState.SetBool(PendingCreatePrefabKey, request.createRunnerPrefab);
        SessionState.SetBool(PendingCreateVisualKey, request.createVisual);
        SessionState.SetBool(PendingRegisterCatalogKey, request.registerCatalog);
        SessionState.SetBool(PendingCreateSettingsKey, request.createSettings);
    }

    private static void ClearPendingRequest()
    {
        SessionState.EraseString(PendingIdKey);
        SessionState.EraseBool(PendingCreatePrefabKey);
        SessionState.EraseBool(PendingCreateVisualKey);
        SessionState.EraseBool(PendingRegisterCatalogKey);
        SessionState.EraseBool(PendingCreateSettingsKey);
    }

    private static bool FinalizeAndRegister(SimulationCreationRequest request, bool isAutoFinalize, out string message)
    {
        if (string.IsNullOrWhiteSpace(request.simulationId))
        {
            message = "Simulation Id is required.";
            return false;
        }

        EnsureFolder($"{SimulationsRoot}/{request.simulationId}");
        EnsureFolder(SimSettingsFolder);

        var runnerPrefab = request.createRunnerPrefab
            ? EnsureRunnerPrefab(request.simulationId, out var prefabError)
            : AssetDatabase.LoadAssetAtPath<GameObject>($"{SimulationsRoot}/{request.simulationId}/{request.simulationId}Runner.prefab");

        if (request.createRunnerPrefab && runnerPrefab == null)
        {
            message = isAutoFinalize
                ? $"Runner type not found yet ({prefabError}). Keep the wizard open and click Finalize/Register after compile."
                : $"Could not create runner prefab: {prefabError}";
            return false;
        }

        var visual = request.createVisual ? EnsureSimVisualSettingsAsset(request.simulationId) : null;

        if (request.createVisual || request.createSettings)
        {
            UpdateBootstrapOptionsBinding(request.simulationId, visual, null);
        }

        if (request.registerCatalog)
        {
            var preset = LoadDefaultPreset(request.simulationId);
            UpdateSimulationCatalog(request.simulationId, runnerPrefab, preset);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        message = $"Finalize/Register complete for '{request.simulationId}'.";
        return true;
    }

    private static TextAsset LoadDefaultPreset(string simulationId)
    {
        var presetPath = $"{SimulationsRoot}/{simulationId}/Presets/default.json";
        return AssetDatabase.LoadAssetAtPath<TextAsset>(presetPath);
    }

    private static SimVisualSettings EnsureSimVisualSettingsAsset(string simulationId)
    {
        var assetPath = $"{SimSettingsFolder}/{simulationId}SimVisualSettings.asset";
        var visual = AssetDatabase.LoadAssetAtPath<SimVisualSettings>(assetPath);
        if (visual == null)
        {
            visual = ScriptableObject.CreateInstance<SimVisualSettings>();
            AssetDatabase.CreateAsset(visual, assetPath);
        }

        visual.simulationId = simulationId;
        visual.usePrimitiveBaseline = true;
        visual.agentShape = BasicShapeKind.Circle;
        visual.agentOutline = true;
        visual.agentSizePx = 64;
        visual.defaultDebugMode = DebugPlaceholderMode.Overlay;
        visual.preferredAgentPack = null;
        EditorUtility.SetDirty(visual);
        return visual;
    }

    private static void UpdateBootstrapOptionsBinding(string simulationId, SimVisualSettings visual, SimSettingsBase settings)
    {
        var options = AssetDatabase.LoadAssetAtPath<BootstrapOptions>(BootstrapOptionsPath);
        if (options == null)
        {
            Debug.LogWarning($"NewSimulationWizard: Missing BootstrapOptions asset at '{BootstrapOptionsPath}'.");
            return;
        }

        var serialized = new SerializedObject(options);
        var listProp = serialized.FindProperty("extraSimulations");
        if (listProp == null || !listProp.isArray)
        {
            Debug.LogWarning("NewSimulationWizard: Could not find BootstrapOptions.extraSimulations.");
            return;
        }

        var index = FindBindingIndex(listProp, simulationId);
        if (index < 0)
        {
            index = listProp.arraySize;
            listProp.InsertArrayElementAtIndex(index);
        }

        var element = listProp.GetArrayElementAtIndex(index);
        element.FindPropertyRelative("simulationId").stringValue = simulationId;

        var settingsProp = element.FindPropertyRelative("settings");
        if (settings != null || settingsProp.objectReferenceValue == null)
        {
            settingsProp.objectReferenceValue = settings;
        }

        var visualProp = element.FindPropertyRelative("visual");
        if (visual != null || visualProp.objectReferenceValue == null)
        {
            visualProp.objectReferenceValue = visual;
        }

        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(options);
    }

    private static void UpdateSimulationCatalog(string simulationId, GameObject runnerPrefab, TextAsset preset)
    {
        var catalog = AssetDatabase.LoadAssetAtPath<SimulationCatalog>(CatalogPath);
        if (catalog == null)
        {
            Debug.LogWarning($"NewSimulationWizard: Missing SimulationCatalog asset at '{CatalogPath}'.");
            return;
        }

        var serialized = new SerializedObject(catalog);
        var simulationsProp = serialized.FindProperty("simulations");
        if (simulationsProp == null || !simulationsProp.isArray)
        {
            Debug.LogWarning("NewSimulationWizard: Could not find SimulationCatalog.simulations array.");
            return;
        }

        var index = FindCatalogEntryIndex(simulationsProp, simulationId);
        if (index < 0)
        {
            index = simulationsProp.arraySize;
            simulationsProp.InsertArrayElementAtIndex(index);
        }

        var entry = simulationsProp.GetArrayElementAtIndex(index);
        entry.FindPropertyRelative("simulationId").stringValue = simulationId;
        entry.FindPropertyRelative("runnerPrefab").objectReferenceValue = runnerPrefab;
        entry.FindPropertyRelative("defaultPreset").objectReferenceValue = preset;
        entry.FindPropertyRelative("defaultContentPack").objectReferenceValue = null;

        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(catalog);
    }

    private static GameObject EnsureRunnerPrefab(string simulationId, out string error)
    {
        var prefabPath = $"{SimulationsRoot}/{simulationId}/{simulationId}Runner.prefab";
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (existing != null)
        {
            error = null;
            return existing;
        }

        var runnerTypeName = $"{simulationId}Runner";
        var runnerType = FindTypeByName(runnerTypeName);
        if (runnerType == null)
        {
            error = $"type '{runnerTypeName}' not found";
            return null;
        }

        if (!typeof(MonoBehaviour).IsAssignableFrom(runnerType) || !typeof(ITickableSimulationRunner).IsAssignableFrom(runnerType))
        {
            error = $"type '{runnerTypeName}' must be MonoBehaviour + ITickableSimulationRunner";
            return null;
        }

        var temp = new GameObject(runnerTypeName);
        temp.AddComponent(runnerType);
        var prefab = PrefabUtility.SaveAsPrefabAsset(temp, prefabPath);
        DestroyImmediate(temp);

        AssetDatabase.ImportAsset(prefabPath);
        error = null;
        return prefab;
    }

    private static Type FindTypeByName(string typeName)
    {
        var assemblies = AppDomain.CurrentDomain.GetAssemblies();
        for (var i = 0; i < assemblies.Length; i++)
        {
            Type[] types;
            try
            {
                types = assemblies[i].GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                types = ex.Types;
            }

            for (var j = 0; j < types.Length; j++)
            {
                var type = types[j];
                if (type != null && string.Equals(type.Name, typeName, StringComparison.Ordinal))
                {
                    return type;
                }
            }
        }

        return null;
    }

    private static int FindCatalogEntryIndex(SerializedProperty simulationsProp, string simulationId)
    {
        for (var i = 0; i < simulationsProp.arraySize; i++)
        {
            var entry = simulationsProp.GetArrayElementAtIndex(i);
            var existingId = entry.FindPropertyRelative("simulationId")?.stringValue;
            if (string.Equals(existingId, simulationId, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return -1;
    }

    private static int FindBindingIndex(SerializedProperty bindingsProp, string simulationId)
    {
        for (var i = 0; i < bindingsProp.arraySize; i++)
        {
            var entry = bindingsProp.GetArrayElementAtIndex(i);
            var existingId = entry.FindPropertyRelative("simulationId")?.stringValue;
            if (string.Equals(existingId, simulationId, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return -1;
    }

    private static string BuildRunnerStubSource(string simulationId)
    {
        var runnerType = simulationId + "Runner";
        var source = new StringBuilder();
        source.AppendLine("using UnityEngine;");
        source.AppendLine();
        source.AppendLine($"public class {runnerType} : MonoBehaviour, ITickableSimulationRunner");
        source.AppendLine("{");
        source.AppendLine("    public void Initialize(ScenarioConfig config)");
        source.AppendLine("    {");
        source.AppendLine($"        SceneGraphUtil.PrepareRunner(transform, \"{simulationId}\");");
        source.AppendLine($"        Debug.Log(\"{runnerType} Initialize simulationId={simulationId}, seed=\" + (config != null ? config.seed : 0));");
        source.AppendLine("    }");
        source.AppendLine();
        source.AppendLine("    public void Tick(int tickIndex, float dt)");
        source.AppendLine("    {");
        source.AppendLine("    }");
        source.AppendLine();
        source.AppendLine("    public void Shutdown()");
        source.AppendLine("    {");
        source.AppendLine($"        Debug.Log(\"{runnerType} Shutdown\");");
        source.AppendLine("    }");
        source.AppendLine("}");
        return source.ToString();
    }

    private static void EnsureFolder(string assetFolderPath)
    {
        if (AssetDatabase.IsValidFolder(assetFolderPath))
        {
            return;
        }

        var normalizedPath = assetFolderPath.Replace("\\", "/");
        var parts = normalizedPath.Split('/');
        var current = parts[0];

        for (var i = 1; i < parts.Length; i++)
        {
            var next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, parts[i]);
            }

            current = next;
        }
    }

    private struct SimulationCreationRequest
    {
        public string simulationId;
        public bool createRunnerPrefab;
        public bool createVisual;
        public bool registerCatalog;
        public bool createSettings;
    }
}
#endif
