using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// BG3 风格相机输入：滚轮缩放、右键平移、中键旋转。
/// </summary>
[RequireComponent(typeof(BattleCameraController))]
public class BattleCameraInput : MonoBehaviour
{
    [SerializeField] private BattleCameraController controller;

    private Vector3 lastMousePosition;
    private bool panning;
    private bool rotating;

    public bool IsControlling => panning || rotating;

    void Awake()
    {
        if (controller == null)
            controller = GetComponent<BattleCameraController>();
    }

    void Update()
    {
        if (controller == null || controller.Pivot == null || controller.ActiveCamera == null)
            return;

        if (IsPointerOverUi())
        {
            panning = false;
            rotating = false;
            return;
        }

        HandleZoom();
        HandlePan();
        HandleRotate();
    }

    private void HandleZoom()
    {
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scroll) < 0.0001f)
            return;

        controller.AddZoomScroll(scroll);
    }

    private void HandlePan()
    {
        if (Input.GetMouseButtonDown(1))
        {
            panning = true;
            lastMousePosition = Input.mousePosition;
        }

        if (Input.GetMouseButtonUp(1))
            panning = false;

        if (!panning || !Input.GetMouseButton(1))
            return;

        Vector3 delta = Input.mousePosition - lastMousePosition;
        lastMousePosition = Input.mousePosition;

        if (delta.sqrMagnitude < 0.01f)
            return;

        PanByScreenDelta(delta);
    }

    private void HandleRotate()
    {
        if (Input.GetMouseButtonDown(2))
        {
            rotating = true;
            lastMousePosition = Input.mousePosition;
        }

        if (Input.GetMouseButtonUp(2))
            rotating = false;

        if (!rotating || !Input.GetMouseButton(2) || controller.Orbital == null)
            return;

        Vector3 delta = Input.mousePosition - lastMousePosition;
        lastMousePosition = Input.mousePosition;

        if (delta.sqrMagnitude < 0.01f)
            return;

        var settings = controller.Settings;
        var orbital = controller.Orbital;

        orbital.m_XAxis.Value += delta.x * settings.rotateSpeed;
        controller.OrbitPitch -= delta.y * settings.rotateSpeed * 0.002f;
    }

    private void PanByScreenDelta(Vector3 screenDelta)
    {
        var camera = controller.ActiveCamera;
        var pivot = controller.Pivot;
        var settings = controller.Settings;

        Vector3 right = camera.transform.right;
        right.y = 0f;
        right.Normalize();

        Vector3 forward = camera.transform.forward;
        forward.y = 0f;
        forward.Normalize();

        Vector3 move = (-right * screenDelta.x - forward * screenDelta.y) * settings.panSpeed;
        controller.SetPivotWorldPosition(pivot.position + move);
    }

    private static bool IsPointerOverUi()
    {
        return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
    }
}
