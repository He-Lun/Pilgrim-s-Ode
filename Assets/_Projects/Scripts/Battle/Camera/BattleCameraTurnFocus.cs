using UnityEngine;

/// <summary>
/// 每回合开始时，将战术相机 Pivot 平滑移动到当前行动角色中心。
/// </summary>
[RequireComponent(typeof(BattleCameraController))]
public class BattleCameraTurnFocus : MonoBehaviour
{
    [SerializeField] private BattleCameraController controller;
    [SerializeField] private BattleCameraInput cameraInput;

    private bool subscribed;

    void Awake()
    {
        controller ??= GetComponent<BattleCameraController>();
        cameraInput ??= GetComponent<BattleCameraInput>();
    }

    void OnEnable()
    {
        TrySubscribe();
    }

    void OnDisable()
    {
        Unsubscribe();
    }

    void Update()
    {
        TrySubscribe();
    }

    private void TrySubscribe()
    {
        if (subscribed || TurnManager.Instance == null)
            return;

        TurnManager.Instance.OnTurnBegan += HandleTurnBegan;
        subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!subscribed || TurnManager.Instance == null)
            return;

        TurnManager.Instance.OnTurnBegan -= HandleTurnBegan;
        subscribed = false;
    }

    private void HandleTurnBegan(AbilitySystemComponent actor)
    {
        if (controller == null || actor == null)
            return;

        if (!controller.Settings.turnFocusEnabled)
            return;

        if (cameraInput != null && cameraInput.IsControlling)
            return;

        controller.FocusOnActor(actor);
    }
}
