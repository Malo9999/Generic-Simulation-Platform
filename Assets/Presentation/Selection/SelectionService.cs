using System;
using UnityEngine;

public class SelectionService : MonoBehaviour
{
    private static SelectionService s_instance;

    public static SelectionService Instance
    {
        get
        {
            if (s_instance != null)
            {
                return s_instance;
            }

            s_instance = FindFirstObjectByType<SelectionService>();
            return s_instance;
        }
    }

    public Transform Selected { get; private set; }

    public event Action<Transform> SelectedChanged;

    private void Awake()
    {
        if (s_instance != null && s_instance != this)
        {
            Destroy(this);
            return;
        }

        s_instance = this;
    }

    public void SetSelected(Transform target)
    {
        if (target != null)
        {
            var entitiesRoot = SelectionRules.FindEntitiesRoot();
            target = SelectionRules.ResolveSelectableSim(target, entitiesRoot);
        }

        if (Selected == target)
        {
#if UNITY_EDITOR
            UnityEditor.Selection.activeGameObject = target != null ? target.gameObject : null;
#endif
            return;
        }

        Selected = target;
        SelectedChanged?.Invoke(Selected);

#if UNITY_EDITOR
        UnityEditor.Selection.activeGameObject = Selected != null ? Selected.gameObject : null;
#endif
    }

    public void Clear()
    {
        if (Selected == null)
        {
#if UNITY_EDITOR
            UnityEditor.Selection.activeGameObject = null;
#endif
            return;
        }

        Selected = null;
        SelectedChanged?.Invoke(null);

#if UNITY_EDITOR
        UnityEditor.Selection.activeGameObject = null;
#endif
    }
}
