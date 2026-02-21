using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class RecreateBroadcastUiMenu
{
    private const string PresentationRootName = "PresentationRoot";
    private const string CanvasName = "BroadcastCanvas";
    private const string MinimapFrameName = "MinimapFrame";
    private const string MinimapViewName = "MinimapView";
    private const string MinimapBorderName = "MinimapBorder";
    private const string HudTextName = "HUDText";
    private const string MinimapCameraName = "MinimapCamera";

    [MenuItem("Tools/GSP/Recreate Broadcast UI (Minimap + HUD)")]
    public static void RecreateBroadcastUi()
    {
        var rt = LoadMinimapRenderTexture();
        if (rt == null)
        {
            EditorUtility.DisplayDialog(
                "RT_Minimap not found",
                "Could not find RenderTexture 'RT_Minimap'. Expected at Assets/Presentation/Minimap/RT_Minimap.renderTexture (or by name search).",
                "OK");
            return;
        }

        var presentationRoot = GetOrCreateRoot(PresentationRootName, null);
        var canvasObject = GetOrCreateRoot(CanvasName, presentationRoot.transform);
        ConfigureCanvas(canvasObject);

        var frame = GetOrCreateUiObject(MinimapFrameName, canvasObject.transform, typeof(Image));
        ConfigureMinimapFrame(frame);

        var view = GetOrCreateUiObject(MinimapViewName, frame.transform, typeof(RawImage));
        ConfigureMinimapView(view, rt);

        var border = GetOrCreateUiObject(MinimapBorderName, frame.transform, typeof(Image));
        ConfigureMinimapBorder(border);

        var hudText = GetOrCreateUiObject(HudTextName, canvasObject.transform);
        ConfigureHudText(hudText);

        var minimapCamera = GetOrCreateRoot(MinimapCameraName, presentationRoot.transform);
        ConfigureMinimapCamera(minimapCamera, rt);

        EnsureEventSystem();
        AttachAndWireClickToPan(view);

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Selection.activeGameObject = canvasObject;
        Debug.Log("Recreate Broadcast UI complete: hierarchy ensured and wired.");
    }

    private static RenderTexture LoadMinimapRenderTexture()
    {
        const string expectedPath = "Assets/Presentation/Minimap/RT_Minimap.renderTexture";
        var byPath = AssetDatabase.LoadAssetAtPath<RenderTexture>(expectedPath);
        if (byPath != null)
        {
            return byPath;
        }

        var guid = AssetDatabase.FindAssets("RT_Minimap t:RenderTexture").FirstOrDefault();
        return string.IsNullOrEmpty(guid)
            ? null
            : AssetDatabase.LoadAssetAtPath<RenderTexture>(AssetDatabase.GUIDToAssetPath(guid));
    }

    private static GameObject GetOrCreateRoot(string name, Transform parent)
    {
        var existing = parent == null
            ? GameObject.Find(name)
            : parent.Find(name)?.gameObject;

        if (existing != null)
        {
            return existing;
        }

        var go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, $"Create {name}");
        if (parent != null)
        {
            go.transform.SetParent(parent, false);
        }

        return go;
    }

    private static GameObject GetOrCreateUiObject(string name, Transform parent, params Type[] requiredComponents)
    {
        var go = parent.Find(name)?.gameObject;
        if (go == null)
        {
            go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, $"Create {name}");
            go.transform.SetParent(parent, false);
            var rectTransform = go.GetComponent<RectTransform>();
            if (rectTransform == null)
            {
                go.AddComponent<RectTransform>();
            }
        }

        foreach (var componentType in requiredComponents)
        {
            if (go.GetComponent(componentType) == null)
            {
                go.AddComponent(componentType);
            }
        }

        return go;
    }

    private static void ConfigureCanvas(GameObject canvasObject)
    {
        var canvas = canvasObject.GetComponent<Canvas>() ?? canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        var scaler = canvasObject.GetComponent<CanvasScaler>() ?? canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(480f, 270f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        if (canvasObject.GetComponent<GraphicRaycaster>() == null)
        {
            canvasObject.AddComponent<GraphicRaycaster>();
        }

        var rect = canvasObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void ConfigureMinimapFrame(GameObject frame)
    {
        var image = frame.GetComponent<Image>() ?? frame.AddComponent<Image>();
        image.color = new Color(0f, 0f, 0f, 0.35f);

        var rect = frame.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 1f);
        rect.sizeDelta = new Vector2(160f, 160f);
        rect.anchoredPosition = new Vector2(-10f, -10f);
    }

    private static void ConfigureMinimapView(GameObject view, RenderTexture rt)
    {
        var image = view.GetComponent<RawImage>() ?? view.AddComponent<RawImage>();
        image.texture = rt;
        image.raycastTarget = true;

        var rect = view.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(6f, 6f);
        rect.offsetMax = new Vector2(-6f, -6f);
    }

    private static void ConfigureMinimapBorder(GameObject border)
    {
        var image = border.GetComponent<Image>() ?? border.AddComponent<Image>();
        image.color = new Color(1f, 1f, 1f, 0.75f);
        image.raycastTarget = false;

        var rect = border.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void ConfigureHudText(GameObject hudText)
    {
        var rect = hudText.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(10f, -10f);
        rect.sizeDelta = new Vector2(280f, 50f);

        var tmpType = Type.GetType("TMPro.TextMeshProUGUI, Unity.TextMeshPro");
        if (tmpType != null)
        {
            var tmp = hudText.GetComponent(tmpType) ?? hudText.AddComponent(tmpType);
            SetMember(tmp, "text", "Broadcast HUD");
            SetMember(tmp, "fontSize", 20f);
            SetMember(tmp, "color", Color.white);
            SetMember(tmp, "raycastTarget", false);
            SetMember(tmp, "alignment", GetTmpAlignmentTopLeftValue());
            RemoveComponentIfPresent(hudText, typeof(Text));
            return;
        }

        var text = hudText.GetComponent<Text>() ?? hudText.AddComponent<Text>();
        text.text = "Broadcast HUD";
        text.fontSize = 20;
        text.alignment = TextAnchor.UpperLeft;
        text.color = Color.white;
        text.raycastTarget = false;
        if (text.font == null)
        {
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }
    }

    private static int GetTmpAlignmentTopLeftValue()
    {
        var enumType = Type.GetType("TMPro.TextAlignmentOptions, Unity.TextMeshPro");
        if (enumType == null)
        {
            return 0;
        }

        return (int)Enum.Parse(enumType, "TopLeft");
    }

    private static void RemoveComponentIfPresent(GameObject go, Type type)
    {
        var component = go.GetComponent(type);
        if (component != null)
        {
            UnityEngine.Object.DestroyImmediate(component);
        }
    }

    private static void ConfigureMinimapCamera(GameObject cameraObject, RenderTexture rt)
    {
        var camera = cameraObject.GetComponent<Camera>() ?? cameraObject.AddComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = ResolveOrthographicSize();
        camera.targetTexture = rt;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.depth = 10f;

        var worldMask = LayerMask.GetMask("World", "Agents");
        camera.cullingMask = worldMask == 0 ? ~0 : worldMask;

        var mainCamera = Camera.main;
        if (mainCamera != null)
        {
            var p = mainCamera.transform.position;
            camera.transform.position = new Vector3(p.x, p.y, p.z);
        }
        else
        {
            camera.transform.position = new Vector3(0f, 0f, -10f);
        }
        camera.transform.rotation = Quaternion.identity;

        var listener = cameraObject.GetComponent<AudioListener>();
        if (listener != null)
        {
            listener.enabled = false;
        }
    }

    private static float ResolveOrthographicSize()
    {
        if (TryReadArenaSizeFromBootstrapper(out var width, out var height))
        {
            return Mathf.Max(width, height) * 0.5f;
        }

        return 32f;
    }

    private static bool TryReadArenaSizeFromBootstrapper(out float width, out float height)
    {
        width = 64f;
        height = 64f;

        var bootstrapperType = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(a =>
            {
                try
                {
                    return a.GetTypes();
                }
                catch (ReflectionTypeLoadException ex)
                {
                    return ex.Types.Where(t => t != null);
                }
            })
            .FirstOrDefault(t => t != null && t.Name == "Bootstrapper");

        if (bootstrapperType == null)
        {
            return false;
        }

        var bootstrapper = UnityEngine.Object.FindFirstObjectByType(bootstrapperType) as MonoBehaviour;
        if (bootstrapper == null)
        {
            return false;
        }

        var so = new SerializedObject(bootstrapper);
        var optionsProp = so.FindProperty("options");
        var optionsObj = optionsProp?.objectReferenceValue;
        if (optionsObj == null)
        {
            return false;
        }

        var optionsSo = new SerializedObject(optionsObj);
        var presetProp = optionsSo.FindProperty("presetJson");
        var preset = presetProp?.objectReferenceValue as TextAsset;
        if (preset == null || string.IsNullOrWhiteSpace(preset.text))
        {
            return false;
        }

        var world = JsonUtility.FromJson<ScenarioWorldWrapper>(preset.text)?.world;
        if (world == null)
        {
            return false;
        }

        width = world.arenaWidth > 0 ? world.arenaWidth : width;
        height = world.arenaHeight > 0 ? world.arenaHeight : height;
        return true;
    }

    [Serializable]
    private class ScenarioWorldWrapper
    {
        public ScenarioWorld world;
    }

    [Serializable]
    private class ScenarioWorld
    {
        public int arenaWidth = 64;
        public int arenaHeight = 64;
    }

    private static void EnsureEventSystem()
    {
        if (UnityEngine.Object.FindAnyObjectByType<EventSystem>() != null)
        {
            return;
        }

        var eventSystem = new GameObject("EventSystem");
        Undo.RegisterCreatedObjectUndo(eventSystem, "Create EventSystem");
        eventSystem.AddComponent<EventSystem>();

        var inputSystemModuleType = Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
        if (inputSystemModuleType != null)
        {
            eventSystem.AddComponent(inputSystemModuleType);
            return;
        }

        eventSystem.AddComponent<StandaloneInputModule>();
    }

    private static void AttachAndWireClickToPan(GameObject minimapView)
    {
        var scriptType = FindMinimapClickToPanType();
        if (scriptType == null)
        {
            Debug.LogWarning("Recreate Broadcast UI: Could not find a minimap click-to-pan MonoBehaviour script under Assets/Presentation/Minimap.");
            return;
        }

        var component = minimapView.GetComponent(scriptType) ?? minimapView.AddComponent(scriptType);
        var assignedAny = TryAssignKnownFields(component, minimapView);
        if (!assignedAny)
        {
            Debug.LogWarning($"Recreate Broadcast UI: Attached {scriptType.Name} to MinimapView, but could not auto-wire known fields. Fill references in Inspector if required.");
        }
    }

    private static Type FindMinimapClickToPanType()
    {
        var guids = AssetDatabase.FindAssets("t:MonoScript", new[] { "Assets/Presentation/Minimap" });
        foreach (var guid in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (path.EndsWith(".meta", StringComparison.OrdinalIgnoreCase) || !File.Exists(path))
            {
                continue;
            }

            var monoScript = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
            var type = monoScript != null ? monoScript.GetClass() : null;
            if (type == null || !typeof(MonoBehaviour).IsAssignableFrom(type))
            {
                continue;
            }

            if (typeof(IPointerClickHandler).IsAssignableFrom(type) || type.Name.Contains("Minimap", StringComparison.OrdinalIgnoreCase))
            {
                return type;
            }
        }

        return null;
    }

    private static bool TryAssignKnownFields(Component component, GameObject minimapView)
    {
        var assigned = false;
        var targetType = component.GetType();
        var cameraMain = Camera.main;
        var minimapCamera = GameObject.Find(MinimapCameraName)?.GetComponent<Camera>();
        var rect = minimapView.GetComponent<RectTransform>();
        var rawImage = minimapView.GetComponent<RawImage>();

        foreach (var field in targetType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
        {
            var name = field.Name.ToLowerInvariant();
            if (field.FieldType == typeof(Camera))
            {
                if (name.Contains("main") || name.Contains("target") || name.Contains("sim"))
                {
                    field.SetValue(component, cameraMain);
                    assigned = true;
                }
                else if (name.Contains("minimap"))
                {
                    field.SetValue(component, minimapCamera);
                    assigned = true;
                }
            }
            else if (field.FieldType == typeof(RectTransform) && (name.Contains("rect") || name.Contains("minimap")))
            {
                field.SetValue(component, rect);
                assigned = true;
            }
            else if (field.FieldType == typeof(RawImage) && (name.Contains("raw") || name.Contains("image") || name.Contains("minimap")))
            {
                field.SetValue(component, rawImage);
                assigned = true;
            }
            else if ((field.FieldType == typeof(float) || field.FieldType == typeof(int)) && (name.Contains("arenawidth") || name.EndsWith("width")))
            {
                if (TryReadArenaSizeFromBootstrapper(out var width, out _))
                {
                    if (field.FieldType == typeof(float)) field.SetValue(component, width);
                    else field.SetValue(component, Mathf.RoundToInt(width));
                    assigned = true;
                }
            }
            else if ((field.FieldType == typeof(float) || field.FieldType == typeof(int)) && (name.Contains("arenaheight") || name.EndsWith("height")))
            {
                if (TryReadArenaSizeFromBootstrapper(out _, out var height))
                {
                    if (field.FieldType == typeof(float)) field.SetValue(component, height);
                    else field.SetValue(component, Mathf.RoundToInt(height));
                    assigned = true;
                }
            }
        }

        return assigned;
    }

    private static void SetMember(object target, string memberName, object value)
    {
        var type = target.GetType();
        var property = type.GetProperty(memberName, BindingFlags.Public | BindingFlags.Instance);
        if (property != null && property.CanWrite)
        {
            property.SetValue(target, value);
            return;
        }

        var field = type.GetField(memberName, BindingFlags.Public | BindingFlags.Instance);
        field?.SetValue(target, value);
    }
}
