using UnityEngine;

public static class MainCameraRuntimeSetup
{
    public static ArenaCameraPolicy EnsureArenaCameraRig(Camera targetCamera = null)
    {
        var resolvedCamera = targetCamera != null ? targetCamera : Camera.main;
        if (resolvedCamera == null)
        {
            return null;
        }

        var cameraObject = resolvedCamera.gameObject;

        var policy = cameraObject.GetComponent<ArenaCameraPolicy>();
        if (policy == null)
        {
            policy = cameraObject.AddComponent<ArenaCameraPolicy>();
        }

        if (policy.targetCamera == null)
        {
            policy.targetCamera = resolvedCamera;
        }

        var controls = cameraObject.GetComponent<ArenaCameraControls>();
        if (controls == null)
        {
            controls = cameraObject.AddComponent<ArenaCameraControls>();
        }

        controls.policy = policy;

        var followController = cameraObject.GetComponent<CameraFollowController>();
        if (followController != null)
        {
            if (followController.mainCamera == null)
            {
                followController.mainCamera = resolvedCamera;
            }

            followController.arenaCameraPolicy = policy;
        }

        return policy;
    }
}
