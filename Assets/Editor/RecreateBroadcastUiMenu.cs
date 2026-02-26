using System;
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
    private const string UiRightStripName = "UiRightStrip";
    private const string MinimapFrameName = "MinimapFrame";
    private const string MinimapViewName = "MinimapView";
    private const string MinimapBorderName = "MinimapBorder";
    private const string MinimapBorderRootName = "MinimapBorderRoot";
    private const string HudPanelName = "HUDPanel";
    private const string HudTextName = "HUDText";
    private const string MinimapCameraName = "MinimapCamera";
    private const float HudPanelHeight = 110f;
    private const float MinimapSize = 240f;

    [MenuItem("GSP/Dev/Recreate Broadcast UI (Minimap + HUD)")]
    public static void RecreateBroadcastUi()
    {
        if (Application.isPlaying)
        {
            EditorUtility.DisplayDialog(
                "Recreate Broadcast UI",
                "This tool modifies the scene and cannot run in Play Mode. Stop Play Mode and run it again.",
                "OK");
            return;
        }

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
        EnsureBroadcastViewportLayout(presentationRoot);

        var canvasObject = GetOrCreateRoot(CanvasName, presentationRoot.transform);
        ConfigureCanvas(canvasObject);

        var rightStrip = GetOrCreateUiObject(UiRightStripName, canvasObject.transform, typeof(Image));
        ConfigureRightStrip(rightStrip);

        var frame = GetOrCreateUiObject(MinimapFrameName, rightStrip.transform, typeof(Image));
        ConfigureMinimapFrame(frame);

        var view = GetOrCreateUiObject(MinimapViewName, frame.transform, typeof(RawImage));
        ConfigureMinimapView(view, rt);
        DisableDuplicateNamedObjects(frame.transform, MinimapViewName, view);

        CleanupLegacyMinimapBorder(frame.transform);
        var borderRoot = GetOrCreateUiObject(MinimapBorderRootName, frame.transform);
        ConfigureMinimapBorder(borderRoot);

        var hudPanel = GetOrCreateUiObject(HudPanelName, rightStrip.transform, typeof(Image));
        ConfigureHudPanel(hudPanel);

        var simPanelHost = GetOrCreateUiObject("SimPanelHost", rightStrip.transform);
        ConfigureSimPanelHost(simPanelHost);

        // Ensure minimap is both positioned below the HUD and drawn above it.
        frame.transform.SetAsLastSibling();

        var hudText = GetOrCreateHudText(rightStrip.transform, hudPanel.transform);
        ConfigureHudText(hudText);
        EnsureHudBinder(hudText);
        DisableDuplicateNamedObjects(canvasObject.transform, HudTextName, hudText);
        DisableLegacyHudOverlays(canvasObject.transform, hudText);

        var minimapCamera = GetOrCreateRoot(MinimapCameraName, presentationRoot.transform);
        ConfigureMinimapCamera(minimapCamera, rt);

        EnsureEventSystem();
        AttachAndWireClickToPan(view);
        AttachAndWireMinimapSelection(view, minimapCamera, presentationRoot);
        EnsureSelectionRuntime(presentationRoot, view, ResolveMainCamera());
        EnsureBroadcastHotkeys(presentationRoot, canvasObject, view);

        if (!Application.isPlaying)
        {
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        }
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

    private static T GetOrAdd<T>(GameObject go) where T : Component
    {
        var component = go.GetComponent<T>();
        return component != null ? component : go.AddComponent<T>();
    }

    private static void ConfigureCanvas(GameObject canvasObject)
    {
        GetOrAdd<RectTransform>(canvasObject);

        var canvas = GetOrAdd<Canvas>(canvasObject);
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        var scaler = GetOrAdd<CanvasScaler>(canvasObject);
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        GetOrAdd<GraphicRaycaster>(canvasObject);

        var rect = canvasObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void ConfigureRightStrip(GameObject rightStrip)
    {
        var image = rightStrip.GetComponent<Image>() ?? rightStrip.AddComponent<Image>();
        image.color = new Color(0f, 0f, 0f, 0.4f);
        image.raycastTarget = false;

        var rect = rightStrip.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.75f, 0f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 0.5f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void ConfigureMinimapFrame(GameObject frame)
    {
        var image = frame.GetComponent<Image>() ?? frame.AddComponent<Image>();
        image.color = new Color(0f, 0f, 0f, 0.35f);
        image.raycastTarget = false;

        var rect = frame.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 1f);
        rect.sizeDelta = new Vector2(MinimapSize, MinimapSize);
        rect.anchoredPosition = new Vector2(-10f, -(HudPanelHeight + 20f));
    }

    private static void ConfigureMinimapView(GameObject view, RenderTexture rt)
    {
        GetOrAdd<RectTransform>(view);

        var image = view.GetComponent<RawImage>() ?? view.AddComponent<RawImage>();
        image.texture = rt;
        image.color = Color.white;
        image.material = null;
        image.raycastTarget = true;

        if (!view.activeSelf)
        {
            view.SetActive(true);
        }

        view.transform.localScale = Vector3.one;

        var rect = view.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(6f, 6f);
        rect.offsetMax = new Vector2(-6f, -6f);
    }

    private static void ConfigureHudPanel(GameObject hudPanel)
    {
        var image = hudPanel.GetComponent<Image>() ?? hudPanel.AddComponent<Image>();
        image.color = new Color(0f, 0f, 0f, 0.45f);
        image.raycastTarget = false;

        var rect = hudPanel.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = Vector2.zero;
        rect.offsetMin = new Vector2(10f, -(10f + HudPanelHeight));
        rect.offsetMax = new Vector2(-10f, -10f);
    }

    private static void ConfigureSimPanelHost(GameObject simPanelHost)
    {
        var rect = GetOrAdd<RectTransform>(simPanelHost);
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.offsetMin = new Vector2(10f, 10f);
        rect.offsetMax = new Vector2(-10f, -(HudPanelHeight + MinimapSize + 30f));

        var layout = GetOrAdd<VerticalLayoutGroup>(simPanelHost);
        layout.padding = new RectOffset(0, 0, 0, 0);
        layout.spacing = 8f;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = true;

        var fitter = GetOrAdd<ContentSizeFitter>(simPanelHost);
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var image = simPanelHost.GetComponent<Image>();
        if (image != null)
        {
            image.enabled = false;
            image.raycastTarget = false;
        }

        if (simPanelHost.GetComponent<SimPanelHost>() == null)
        {
            simPanelHost.AddComponent<SimPanelHost>();
        }
    }

    private static void CleanupLegacyMinimapBorder(Transform frameTransform)
    {
        var oldBorder = frameTransform.Find(MinimapBorderName)?.gameObject;
        if (oldBorder == null)
        {
            return;
        }

        var image = oldBorder.GetComponent<Image>();
        if (image != null)
        {
            image.enabled = false;
            oldBorder.SetActive(false);
        }
    }

    private static void ConfigureMinimapBorder(GameObject borderRoot)
    {
        var rootRect = borderRoot.GetComponent<RectTransform>();
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;

        CreateBorderLine(borderRoot.transform, "BorderTop", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(0f, 0f), new Vector2(0f, 0f), 2f);
        CreateBorderLine(borderRoot.transform, "BorderBottom", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), 2f);
        CreateBorderLine(borderRoot.transform, "BorderLeft", new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(0f, 0f), new Vector2(0f, 0f), 2f, true);
        CreateBorderLine(borderRoot.transform, "BorderRight", new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(0f, 0f), new Vector2(0f, 0f), 2f, true);
    }

    private static void CreateBorderLine(
        Transform parent,
        string name,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 pivot,
        Vector2 offsetMin,
        Vector2 offsetMax,
        float thickness,
        bool vertical = false)
    {
        var line = GetOrCreateUiObject(name, parent, typeof(Image));
        var image = line.GetComponent<Image>();
        image.color = new Color(1f, 1f, 1f, 0.8f);
        image.raycastTarget = false;

        var rect = line.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
        rect.sizeDelta = vertical ? new Vector2(thickness, 0f) : new Vector2(0f, thickness);
    }

    private static GameObject GetOrCreateHudText(Transform canvasRoot, Transform hudPanel)
    {
        var inPanel = hudPanel.Find(HudTextName)?.gameObject;
        if (inPanel != null)
        {
            return inPanel;
        }

        var existingOnCanvas = canvasRoot.Find(HudTextName)?.gameObject;
        if (existingOnCanvas != null)
        {
            existingOnCanvas.transform.SetParent(hudPanel, false);
            return existingOnCanvas;
        }

        return GetOrCreateUiObject(HudTextName, hudPanel);
    }

    private static void ConfigureHudText(GameObject hudText)
    {
        var rect = hudText.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = new Vector2(8f, 8f);
        rect.offsetMax = new Vector2(-8f, -8f);

        var tmpType = Type.GetType("TMPro.TextMeshProUGUI, Unity.TextMeshPro");
        if (tmpType != null)
        {
            var tmp = hudText.GetComponent(tmpType) ?? hudText.AddComponent(tmpType);
            SetMember(tmp, "text", string.Empty);
            SetMember(tmp, "fontSize", 16f);
            SetMember(tmp, "color", Color.white);
            SetMember(tmp, "raycastTarget", false);
            SetMember(tmp, "alignment", GetTmpAlignmentTopLeftValue());
            RemoveComponentIfPresent(hudText, typeof(Text));
            return;
        }

        var text = hudText.GetComponent<Text>() ?? hudText.AddComponent<Text>();
        text.text = string.Empty;
        text.fontSize = 16;
        text.alignment = TextAnchor.UpperLeft;
        text.color = Color.white;
        text.raycastTarget = false;
        if (text.font == null)
        {
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }
    }

    private static void EnsureHudBinder(GameObject hudText)
    {
        var binderType = Type.GetType("BroadcastHudBinder, Assembly-CSharp");
        if (binderType == null)
        {
            return;
        }

        if (hudText.GetComponent(binderType) == null)
        {
            hudText.AddComponent(binderType);
        }
    }

    private static void EnsureBroadcastViewportLayout(GameObject presentationRoot)
    {
        var layoutType = Type.GetType("BroadcastViewportLayout, Assembly-CSharp");
        if (layoutType == null)
        {
            return;
        }

        var layout = presentationRoot.GetComponent(layoutType) as Behaviour;
        if (layout == null)
        {
            layout = presentationRoot.AddComponent(layoutType) as Behaviour;
        }

        if (layout != null)
        {
            layout.enabled = true;
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
        var camera = GetOrAdd<Camera>(cameraObject);
        camera.orthographic = true;
        camera.orthographicSize = ResolveOrthographicSize();
        camera.targetTexture = rt;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.11f, 0.14f, 0.19f, 1f);
        camera.depth = 10f;

        camera.cullingMask = ResolveMinimapCullingMask();
        camera.transform.position = new Vector3(0f, 0f, -10f);
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
            return Mathf.Max(width, height) * 0.5f + 1f;
        }

        return 32f;
    }

    private static int ResolveMinimapCullingMask()
    {
        var layerNames = new[] { "Default", "World", "Agents" };
        var mask = 0;

        foreach (var layerName in layerNames)
        {
            var layerIndex = LayerMask.NameToLayer(layerName);
            if (layerIndex >= 0)
            {
                mask |= 1 << layerIndex;
            }
        }

        return mask == 0 ? ~0 : mask;
    }

    private static void DisableLegacyHudOverlays(Transform root, GameObject canonicalHudText)
    {
        var legacyText = root.GetComponentsInChildren<Text>(true)
            .Where(t => t != null && t.gameObject != canonicalHudText)
            .Where(t => t.name == "Broadcast HUD" || t.fontSize >= 28 || string.Equals(t.text, "Broadcast HUD", StringComparison.Ordinal))
            .ToArray();

        foreach (var text in legacyText)
        {
            text.gameObject.SetActive(false);
        }

        var tmpType = Type.GetType("TMPro.TMP_Text, Unity.TextMeshPro");
        if (tmpType == null)
        {
            return;
        }

        foreach (var component in root.GetComponentsInChildren(tmpType, true))
        {
            var tmp = component as Component;
            if (tmp == null || tmp.gameObject == canonicalHudText)
            {
                continue;
            }

            var nameLooksLegacy = tmp.name == "Broadcast HUD";
            var textLooksLegacy = string.Equals(GetMember<string>(tmp, "text"), "Broadcast HUD", StringComparison.Ordinal);
            var fontSize = GetMember<float>(tmp, "fontSize");

            if (nameLooksLegacy || textLooksLegacy || fontSize >= 28f)
            {
                tmp.gameObject.SetActive(false);
            }
        }
    }

    private static T GetMember<T>(object target, string memberName)
    {
        var type = target.GetType();
        var property = type.GetProperty(memberName, BindingFlags.Public | BindingFlags.Instance);
        if (property != null && property.CanRead)
        {
            var value = property.GetValue(target);
            if (value is T typedValue)
            {
                return typedValue;
            }
        }

        var field = type.GetField(memberName, BindingFlags.Public | BindingFlags.Instance);
        if (field != null)
        {
            var value = field.GetValue(target);
            if (value is T typedValue)
            {
                return typedValue;
            }
        }

        return default;
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
        GetOrAdd<RectTransform>(minimapView);

        var clickToPan = GetOrAdd<MinimapClickToPan>(minimapView);
        clickToPan.mainCamera = ResolveMainCamera();
        if (clickToPan.mainCamera == null)
        {
            Debug.LogWarning("Recreate Broadcast UI: Could not find a main camera for MinimapClickToPan. Tag your primary camera as 'MainCamera'.");
        }

        if (TryReadArenaSizeFromBootstrapper(out var width, out var height))
        {
            clickToPan.worldBounds = new Rect(-width * 0.5f, -height * 0.5f, width, height);
            return;
        }

        clickToPan.worldBounds = new Rect(-32f, -32f, 64f, 64f);
    }

    private static void AttachAndWireMinimapSelection(GameObject minimapView, GameObject minimapCameraObject, GameObject presentationRoot)
    {
        var minimapRect = minimapView.GetComponent<RectTransform>();
        var minimapCamera = minimapCameraObject != null ? minimapCameraObject.GetComponent<Camera>() : null;
        var mainCamera = ResolveMainCamera();

        var overlay = GetOrAdd<MinimapMarkerOverlay>(minimapView);
        overlay.minimapRect = minimapRect;
        overlay.minimapCamera = minimapCamera;

        var followHost = mainCamera != null ? mainCamera.gameObject : presentationRoot;
        var followController = GetOrAdd<CameraFollowController>(followHost);
        followController.mainCamera = mainCamera;
        followController.followEnabled = true;

        var selectToFollow = GetOrAdd<MinimapSelectToFollow>(minimapView);
        selectToFollow.mainCamera = mainCamera;
        selectToFollow.overlay = overlay;
        selectToFollow.followController = followController;

        var clickToPan = minimapView.GetComponent<MinimapClickToPan>();
        var bounds = clickToPan != null ? clickToPan.worldBounds : new Rect(-32f, -32f, 64f, 64f);
        selectToFollow.worldBounds = bounds;
        followController.worldBounds = bounds;
    }

    private static void EnsureSelectionRuntime(GameObject presentationRoot, GameObject minimapView, Camera mainCamera)
    {
        if (presentationRoot == null)
        {
            return;
        }

        var selectionHost = GetOrCreateRoot("SelectionService", presentationRoot.transform);
        var selectionService = GetOrAdd<SelectionService>(selectionHost);

        var highlighterHost = GetOrCreateRoot("WorldSelectionHighlighter", presentationRoot.transform);
        var highlighter = GetOrAdd<WorldSelectionHighlighter>(highlighterHost);
        highlighter.selectionService = selectionService;

        if (mainCamera != null)
        {
            var clickSelector = GetOrAdd<GameClickSelector>(mainCamera.gameObject);
            clickSelector.worldCamera = mainCamera;
            clickSelector.selectionService = selectionService;
        }

        if (minimapView != null)
        {
            var overlay = GetOrAdd<MinimapMarkerOverlay>(minimapView);
            var bridge = GetOrAdd<MinimapSelectionBridge>(minimapView);
            bridge.selectionService = selectionService;
            bridge.overlay = overlay;

            var selector = GetOrAdd<MinimapSelectToFollow>(minimapView);
            selector.selectionService = selectionService;
        }

        var followController = UnityEngine.Object.FindAnyObjectByType<CameraFollowController>();
        if (followController != null)
        {
            var bridgeHost = GetOrCreateRoot("SelectionFollowBridge", presentationRoot.transform);
            var followBridge = GetOrAdd<SelectionFollowBridge>(bridgeHost);
            followBridge.selection = selectionService;
            followBridge.follow = followController;
            followBridge.snapOnHierarchySelect = true;

            foreach (var duplicate in presentationRoot.GetComponentsInChildren<SelectionFollowBridge>(true))
            {
                if (duplicate != null && duplicate != followBridge)
                {
                    UnityEngine.Object.DestroyImmediate(duplicate);
                }
            }
        }
    }

    private static void EnsureBroadcastHotkeys(GameObject presentationRoot, GameObject canvasObject, GameObject minimapView)
    {
        var host = presentationRoot != null ? presentationRoot : canvasObject;
        if (host == null)
        {
            return;
        }

        var hotkeys = host.GetComponent<BroadcastHotkeys>();
        if (hotkeys == null)
        {
            hotkeys = host.AddComponent<BroadcastHotkeys>();
        }

        if (presentationRoot != null)
        {
            foreach (var duplicate in presentationRoot.GetComponentsInChildren<BroadcastHotkeys>(true))
            {
                if (duplicate != null && duplicate != hotkeys)
                {
                    UnityEngine.Object.DestroyImmediate(duplicate);
                }
            }
        }

        if (canvasObject != null && canvasObject != host)
        {
            foreach (var duplicate in canvasObject.GetComponentsInChildren<BroadcastHotkeys>(true))
            {
                if (duplicate != null && duplicate != hotkeys)
                {
                    UnityEngine.Object.DestroyImmediate(duplicate);
                }
            }
        }

        hotkeys.followController = UnityEngine.Object.FindAnyObjectByType<CameraFollowController>();
        if (minimapView != null)
        {
            hotkeys.overlay = minimapView.GetComponent<MinimapMarkerOverlay>();
            hotkeys.selector = minimapView.GetComponent<MinimapSelectToFollow>();
        }
    }

    private static Camera ResolveMainCamera()
    {
        if (Camera.main != null)
        {
            return Camera.main;
        }

        var cameras = UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None);
        var taggedMain = cameras.FirstOrDefault(c => c != null && c.CompareTag("MainCamera"));
        if (taggedMain != null)
        {
            return taggedMain;
        }

        return cameras.FirstOrDefault(c => c != null && c.name.IndexOf("Main", StringComparison.OrdinalIgnoreCase) >= 0);
    }

    private static void DisableDuplicateNamedObjects(Transform root, string objectName, GameObject keepActive)
    {
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            if (child == null || child.gameObject == keepActive || child.name != objectName)
            {
                continue;
            }

            child.gameObject.SetActive(false);
        }
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
