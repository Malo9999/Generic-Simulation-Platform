using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class SelectionSyncEditor
{
    private static bool s_applyingSelection;

    static SelectionSyncEditor()
    {
        Selection.selectionChanged += OnEditorSelectionChanged;
    }

    private static void OnEditorSelectionChanged()
    {
        if (!Application.isPlaying || s_applyingSelection)
        {
            return;
        }

        var service = Object.FindFirstObjectByType<SelectionService>();
        if (service == null)
        {
            return;
        }

        var active = Selection.activeGameObject;
        if (active == null)
        {
            Apply(() => service.Clear());
            return;
        }

        var entitiesRoot = SelectionRules.FindEntitiesRoot();
        var selectable = SelectionRules.ResolveSelectableSim(active.transform, entitiesRoot);
        if (selectable == null)
        {
            return;
        }

        Apply(() => service.SetSelected(selectable));
    }

    private static void Apply(System.Action action)
    {
        s_applyingSelection = true;
        try
        {
            action?.Invoke();
        }
        finally
        {
            s_applyingSelection = false;
        }
    }
}
