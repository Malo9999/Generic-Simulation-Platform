using UnityEngine;

public class SelectionFollowBridge : MonoBehaviour
{
    public SelectionService selection;
    public CameraFollowController follow;
    public bool snapOnHierarchySelect = true;

    private void OnEnable()
    {
        if (selection == null)
        {
            selection = SelectionService.Instance;
        }

        if (selection == null)
        {
            return;
        }

        selection.SelectedChanged += OnSelectedChanged;
        OnSelectedChanged(selection.Selected);
    }

    private void OnDisable()
    {
        if (selection != null)
        {
            selection.SelectedChanged -= OnSelectedChanged;
        }
    }

    private void OnSelectedChanged(Transform selected)
    {
        if (follow == null)
        {
            follow = FindFirstObjectByType<CameraFollowController>();
        }

        if (follow == null)
        {
            return;
        }

        follow.target = selected;

        if (selected != null && snapOnHierarchySelect && selection != null && selection.LastSource == SelectionSource.Hierarchy)
        {
            follow.SnapToTargetNow();
        }
    }
}
