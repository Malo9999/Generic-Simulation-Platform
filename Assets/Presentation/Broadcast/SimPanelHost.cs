using System;
using System.Collections.Generic;
using UnityEngine;

public class SimPanelHost : MonoBehaviour
{
    [SerializeField] private Transform hostRoot;

    private readonly Dictionary<string, Transform> panels = new(StringComparer.OrdinalIgnoreCase);
    private Bootstrapper bootstrapper;

    public Transform HostRoot => hostRoot != null ? hostRoot : transform;
    public string ActiveSimId { get; private set; } = string.Empty;

    private void Awake()
    {
        if (hostRoot == null)
        {
            hostRoot = transform;
        }

        RefreshActiveSimId();
    }

    private void Update()
    {
        RefreshActiveSimId();
    }

    public Transform EnsurePanel(string panelKey, Func<Transform, Transform> factory)
    {
        if (string.IsNullOrWhiteSpace(panelKey))
        {
            throw new ArgumentException("Panel key is required.", nameof(panelKey));
        }

        if (factory == null)
        {
            throw new ArgumentNullException(nameof(factory));
        }

        if (panels.TryGetValue(panelKey, out var existingPanel) && existingPanel != null)
        {
            return existingPanel;
        }

        var createdPanel = factory(HostRoot);
        if (createdPanel == null)
        {
            throw new InvalidOperationException($"Panel factory returned null for key '{panelKey}'.");
        }

        createdPanel.SetParent(HostRoot, false);
        panels[panelKey] = createdPanel;
        return createdPanel;
    }

    public void ShowOnly(string panelKey)
    {
        foreach (var pair in panels)
        {
            if (pair.Value == null)
            {
                continue;
            }

            pair.Value.gameObject.SetActive(string.Equals(pair.Key, panelKey, StringComparison.OrdinalIgnoreCase));
        }
    }

    public void HideAll()
    {
        foreach (var pair in panels)
        {
            if (pair.Value != null)
            {
                pair.Value.gameObject.SetActive(false);
            }
        }
    }

    private void RefreshActiveSimId()
    {
        if (bootstrapper == null)
        {
            bootstrapper = UnityEngine.Object.FindFirstObjectByType<Bootstrapper>()
                ?? UnityEngine.Object.FindAnyObjectByType<Bootstrapper>();
        }

        ActiveSimId = bootstrapper != null ? bootstrapper.CurrentSimulationId : string.Empty;
    }
}
