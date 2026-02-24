using UnityEngine;

public class MinimapSelectionBridge : MonoBehaviour
{
    public SelectionService selectionService;
    public MinimapMarkerOverlay overlay;

    private void OnEnable()
    {
        if (overlay == null)
        {
            overlay = GetComponent<MinimapMarkerOverlay>();
        }

        if (selectionService == null)
        {
            selectionService = SelectionService.Instance;
        }

        if (selectionService != null)
        {
            selectionService.SelectedChanged += OnSelectedChanged;
            OnSelectedChanged(selectionService.Selected);
        }
    }

    private void OnDisable()
    {
        if (selectionService != null)
        {
            selectionService.SelectedChanged -= OnSelectedChanged;
        }
    }

    private void OnSelectedChanged(Transform selected)
    {
        if (overlay != null)
        {
            overlay.target = selected;
        }
    }
}
