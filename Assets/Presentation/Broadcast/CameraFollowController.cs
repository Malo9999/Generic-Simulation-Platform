using UnityEngine;

public class CameraFollowController : MonoBehaviour
{
    public Camera mainCamera;
    public Transform target;
    public bool followEnabled = true;
    public Rect worldBounds = new Rect(-32f, -32f, 64f, 64f);
    public bool clampToBounds = true;
    public float smoothTime = 0f;

    private Vector3 velocity;

    private void LateUpdate()
    {
        if (!followEnabled || target == null || mainCamera == null)
        {
            return;
        }

        var currentPosition = mainCamera.transform.position;
        var desiredPosition = new Vector3(target.position.x, target.position.y, currentPosition.z);

        var nextPosition = smoothTime > 0f
            ? Vector3.SmoothDamp(currentPosition, desiredPosition, ref velocity, smoothTime)
            : desiredPosition;

        if (clampToBounds)
        {
            nextPosition.x = Mathf.Clamp(nextPosition.x, worldBounds.xMin, worldBounds.xMax);
            nextPosition.y = Mathf.Clamp(nextPosition.y, worldBounds.yMin, worldBounds.yMax);
        }

        mainCamera.transform.position = nextPosition;
    }
}
