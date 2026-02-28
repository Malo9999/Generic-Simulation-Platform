using System;
using System.Reflection;
using UnityEngine;

public static class PresentationBoundsSync
{
    private const float DefaultArenaSize = 64f;

    public static void ApplyFromConfig(ScenarioConfig config)
    {
        var width = Mathf.Max(1f, config?.world?.arenaWidth ?? (int)DefaultArenaSize);
        var height = Mathf.Max(1f, config?.world?.arenaHeight ?? (int)DefaultArenaSize);
        var bounds = new Rect(-width * 0.5f, -height * 0.5f, width, height);
        Apply(bounds);
    }

    public static void Apply(Rect bounds)
    {
        var behaviours = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        var minimapClickFound = false;

        foreach (var behaviour in behaviours)
        {
            if (behaviour == null)
            {
                continue;
            }

            var type = behaviour.GetType();
            var updated = SetWorldBoundsFieldOrProperty(behaviour, type, bounds);

            if (string.Equals(type.Name, "MinimapClickToPan", StringComparison.Ordinal))
            {
                minimapClickFound = true;
                EnsureMainCamera(behaviour, type);

                if (!updated)
                {
                    SetNamedField(behaviour, type, "worldBounds", bounds);
                    SetNamedProperty(behaviour, type, "worldBounds", bounds);
                }
            }
        }

        if (!minimapClickFound)
        {
            Debug.LogWarning("[PresentationBoundsSync] MinimapClickToPan not found; minimap click bounds were not applied.");
        }

        EnsureMinimapCamera(bounds);
        EnsureMainCameraFraming(bounds);
    }

    private static void EnsureMainCameraFraming(Rect bounds)
    {
        var mainCamera = Camera.main;
        if (mainCamera == null)
        {
            return;
        }

        var cameraBehaviours = mainCamera.GetComponents<MonoBehaviour>();
        foreach (var behaviour in cameraBehaviours)
        {
            if (behaviour == null || !behaviour.enabled)
            {
                continue;
            }

            var typeName = behaviour.GetType().Name;
            if (typeName.IndexOf("follow", StringComparison.OrdinalIgnoreCase) >= 0
                || typeName.IndexOf("cinemachine", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return;
            }
        }

        mainCamera.orthographic = true;
        var arenaCameraPolicy = UnityEngine.Object.FindAnyObjectByType<ArenaCameraPolicy>();
        var canRouteThroughPolicy = arenaCameraPolicy != null && arenaCameraPolicy.targetCamera == mainCamera;

        var position = mainCamera.transform.position;
        position.x = bounds.center.x;
        position.y = bounds.center.y;
        if (canRouteThroughPolicy)
        {
            arenaCameraPolicy.transform.position = position;
        }
        else
        {
            mainCamera.transform.position = position;
        }

        var halfH = bounds.height * 0.5f;
        var halfW = bounds.width * 0.5f;
        var aspect = Screen.height > 0 ? (Screen.width / (float)Screen.height) : 1.777f;
        var sizeToFitWidth = halfW / Mathf.Max(0.01f, aspect);
        var baseSize = Mathf.Max(halfH, sizeToFitWidth);
        var pad = Mathf.Clamp(baseSize * 0.08f, 1.5f, 6f);
        var framingOrtho = baseSize + pad;
        if (canRouteThroughPolicy)
        {
            arenaCameraPolicy.SetOrthoFromExternal(framingOrtho, "PresentationBoundsSync.EnsureMainCameraFraming", syncZoomLevel: true);
        }
        else
        {
            mainCamera.orthographicSize = framingOrtho;
        }
    }

    private static bool SetWorldBoundsFieldOrProperty(MonoBehaviour behaviour, Type type, Rect bounds)
    {
        var fieldUpdated = SetNamedField(behaviour, type, "worldBounds", bounds);
        var propertyUpdated = SetNamedProperty(behaviour, type, "worldBounds", bounds);
        return fieldUpdated || propertyUpdated;
    }

    private static bool SetNamedField(MonoBehaviour behaviour, Type type, string name, Rect bounds)
    {
        var field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public);
        if (field == null || field.FieldType != typeof(Rect))
        {
            return false;
        }

        field.SetValue(behaviour, bounds);
        return true;
    }

    private static bool SetNamedProperty(MonoBehaviour behaviour, Type type, string name, Rect bounds)
    {
        var property = type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public);
        if (property == null || property.PropertyType != typeof(Rect) || !property.CanWrite)
        {
            return false;
        }

        property.SetValue(behaviour, bounds);
        return true;
    }

    private static void EnsureMainCamera(MonoBehaviour behaviour, Type type)
    {
        var mainCameraField = type.GetField("mainCamera", BindingFlags.Instance | BindingFlags.Public);
        if (mainCameraField == null || mainCameraField.FieldType != typeof(Camera))
        {
            return;
        }

        var existingCamera = mainCameraField.GetValue(behaviour) as Camera;
        if (existingCamera == null && Camera.main != null)
        {
            mainCameraField.SetValue(behaviour, Camera.main);
        }
    }

    private static void EnsureMinimapCamera(Rect bounds)
    {
        var minimapCameraObject = GameObject.Find("MinimapCamera");
        if (minimapCameraObject == null)
        {
            return;
        }

        var minimapCamera = minimapCameraObject.GetComponent<Camera>();
        if (minimapCamera == null)
        {
            return;
        }

        minimapCamera.orthographic = true;
        minimapCamera.orthographicSize = Mathf.Max(bounds.width, bounds.height) * 0.5f;
    }
}
