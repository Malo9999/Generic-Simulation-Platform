using UnityEngine;

public class CameraFollowController : MonoBehaviour
{
    public Camera mainCamera;
    public Transform target;
    public ArenaCameraPolicy arenaCameraPolicy;
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

        mainCamera.transform.position = GetTargetCameraPosition(mainCamera.transform.position);
    }

    public void SnapToTargetNow()
    {
        if (target == null || mainCamera == null)
        {
            return;
        }

        velocity = Vector3.zero;
        mainCamera.transform.position = GetTargetCameraPosition(mainCamera.transform.position);
    }

    private Vector3 GetTargetCameraPosition(Vector3 currentPosition)
    {
        var desiredPosition = new Vector3(target.position.x, target.position.y, currentPosition.z);

        var nextPosition = smoothTime > 0f
            ? Vector3.SmoothDamp(currentPosition, desiredPosition, ref velocity, smoothTime)
            : desiredPosition;

        if (clampToBounds)
        {
            var bounds = ResolveWorldBounds();
            nextPosition.x = Mathf.Clamp(nextPosition.x, bounds.xMin, bounds.xMax);
            nextPosition.y = Mathf.Clamp(nextPosition.y, bounds.yMin, bounds.yMax);
        }

        return nextPosition;
    }

    private Rect ResolveWorldBounds()
    {
        if (arenaCameraPolicy == null)
        {
            arenaCameraPolicy = FindAnyObjectByType<ArenaCameraPolicy>();
        }

        if (arenaCameraPolicy != null && arenaCameraPolicy.TryGetWorldBoundsRect(out var policyBounds))
        {
            worldBounds = policyBounds;
            return policyBounds;
        }

        return worldBounds;
    }
}
