using UnityEngine;

/// <summary>
/// 使 Transform 每帧面向战斗相机（世界空间血条用）。
/// </summary>
public class BillboardToCamera : MonoBehaviour
{
    [SerializeField] private bool yawOnly = true;

    void LateUpdate()
    {
        var cam = BattleCameraController.Instance != null
            ? BattleCameraController.Instance.ActiveCamera
            : Camera.main;

        if (cam == null)
            return;

        Vector3 forward = cam.transform.forward;
        if (yawOnly)
            forward.y = 0f;

        if (forward.sqrMagnitude < 0.0001f)
            forward = cam.transform.forward;

        transform.rotation = Quaternion.LookRotation(-forward.normalized, Vector3.up);
    }
}
