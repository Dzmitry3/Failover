using UnityEngine;

public sealed class WeaponAimSolver
{
    private const float MinHorizontalDirectionSqrMagnitude = 0.0001f;
    private const float MinShotDirectionSqrMagnitude = 0.0001f;
    private const float MinGamepadAimInputSqrMagnitude = 0.04f;

    private readonly Camera aimCamera;
    private readonly Transform rotateRoot;
    private readonly LayerMask aimGroundMask;
    private readonly float maxAimRayDistance;

    public WeaponAimSolver(Camera aimCamera, Transform rotateRoot, LayerMask aimGroundMask, float maxAimRayDistance)
    {
        this.aimCamera = aimCamera;
        this.rotateRoot = rotateRoot;
        this.aimGroundMask = aimGroundMask;
        this.maxAimRayDistance = maxAimRayDistance;
    }

    public bool TryGetAimPoint(
        Vector2 pointerInput,
        bool isPointerInputEnabled,
        Vector2 lookInput,
        bool isLookInputEnabled,
        out Vector3 aimPoint)
    {
        aimPoint = default;

        if (aimCamera == null || rotateRoot == null)
            return false;

        if (isPointerInputEnabled && TryGetPointerAimPoint(pointerInput, out aimPoint))
            return true;

        if (isLookInputEnabled && TryGetLookAimPoint(lookInput, out aimPoint))
            return true;

        return false;
    }

    public bool TryGetDirection(Vector3 from, Vector3 to, bool ignoreVertical, out Vector3 direction)
    {
        direction = to - from;

        if (!ignoreVertical)
            return direction.sqrMagnitude >= MinShotDirectionSqrMagnitude;

        direction.y = 0f;
        if (direction.sqrMagnitude < MinHorizontalDirectionSqrMagnitude)
            return false;

        direction.Normalize();
        return true;
    }

    public void RotateTowards(Vector3 worldPoint)
    {
        if (rotateRoot == null || !TryGetDirection(rotateRoot.position, worldPoint, ignoreVertical: true, out Vector3 direction))
            return;

        rotateRoot.rotation = Quaternion.LookRotation(direction, Vector3.up);
    }

    private bool TryGetPointerAimPoint(Vector2 screenPosition, out Vector3 aimPoint)
    {
        aimPoint = default;
        if (screenPosition.sqrMagnitude < MinHorizontalDirectionSqrMagnitude)
            return false;

        Ray ray = aimCamera.ScreenPointToRay(screenPosition);
        if (Physics.Raycast(ray, out RaycastHit hit, maxAimRayDistance, aimGroundMask, QueryTriggerInteraction.Ignore))
        {
            aimPoint = hit.point;
            return true;
        }

        Plane plane = new Plane(Vector3.up, new Vector3(0f, rotateRoot.position.y, 0f));
        if (!plane.Raycast(ray, out float enter))
            return false;

        aimPoint = ray.GetPoint(enter);
        return true;
    }

    private bool TryGetLookAimPoint(Vector2 lookInput, out Vector3 aimPoint)
    {
        aimPoint = default;
        if (lookInput.sqrMagnitude < MinGamepadAimInputSqrMagnitude)
            return false;

        Vector3 cameraForward = aimCamera.transform.forward;
        Vector3 cameraRight = aimCamera.transform.right;
        cameraForward.y = 0f;
        cameraRight.y = 0f;

        if (cameraForward.sqrMagnitude < MinHorizontalDirectionSqrMagnitude ||
            cameraRight.sqrMagnitude < MinHorizontalDirectionSqrMagnitude)
        {
            return false;
        }

        cameraForward.Normalize();
        cameraRight.Normalize();

        Vector3 aimDirection = cameraRight * lookInput.x + cameraForward * lookInput.y;
        if (aimDirection.sqrMagnitude < MinHorizontalDirectionSqrMagnitude)
            return false;

        aimPoint = rotateRoot.position + aimDirection.normalized * maxAimRayDistance;
        return true;
    }
}
