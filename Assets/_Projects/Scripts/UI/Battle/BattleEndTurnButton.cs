using Mirror;
using UnityEngine;
using UnityEngine.UI;

/// <summary>结束回合按钮。</summary>
[DisallowMultipleComponent]
public class BattleEndTurnButton : MonoBehaviour
{
    private Button button;
    private Text label;

    public static BattleEndTurnButton Ensure(Transform battleUiRoot)
    {
        if (battleUiRoot == null)
            return null;

        var canvas = battleUiRoot.GetComponentInChildren<Canvas>(true);
        if (canvas == null)
            return null;

        var existing = canvas.GetComponentInChildren<BattleEndTurnButton>(true);
        if (existing != null)
            return existing;

        return Create(canvas.transform);
    }

    public static BattleEndTurnButton Create(Transform canvasTransform)
    {
        const float size = 132f;

        var rootGo = new GameObject(
            "BattleEndTurnButton",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(BattleEndTurnButton));

        rootGo.transform.SetParent(canvasTransform, false);
        rootGo.transform.SetAsLastSibling();

        var rect = rootGo.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 0f);
        rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(1f, 0f);
        rect.anchoredPosition = new Vector2(-28f, 28f);
        rect.sizeDelta = new Vector2(size, size);

        var image = rootGo.GetComponent<Image>();
        var circleSprite = Resources.GetBuiltinResource<Sprite>("UI/Skin/Knob.psd");
        if (circleSprite != null)
            image.sprite = circleSprite;
        image.type = Image.Type.Simple;
        image.color = new Color(0.72f, 0.48f, 0.14f, 0.94f);

        var buttonComponent = rootGo.AddComponent<Button>();
        buttonComponent.targetGraphic = image;

        var colors = buttonComponent.colors;
        colors.normalColor = image.color;
        colors.highlightedColor = new Color(0.82f, 0.58f, 0.2f, 0.98f);
        colors.pressedColor = new Color(0.58f, 0.38f, 0.1f, 1f);
        colors.disabledColor = new Color(0.35f, 0.35f, 0.38f, 0.72f);
        buttonComponent.colors = colors;

        var labelGo = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        labelGo.transform.SetParent(rootGo.transform, false);
        var labelRect = labelGo.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(16f, 16f);
        labelRect.offsetMax = new Vector2(-16f, -16f);

        var labelText = labelGo.GetComponent<Text>();
        labelText.alignment = TextAnchor.MiddleCenter;
        labelText.fontSize = 24;
        labelText.fontStyle = FontStyle.Bold;
        labelText.color = new Color(0.98f, 0.96f, 0.92f, 1f);
        labelText.horizontalOverflow = HorizontalWrapMode.Wrap;
        labelText.verticalOverflow = VerticalWrapMode.Truncate;
        BattleUiFonts.ApplyToLabel(labelText, "结束回合");

        var widget = rootGo.GetComponent<BattleEndTurnButton>();
        widget.button = buttonComponent;
        widget.label = labelText;
        widget.button.onClick.AddListener(widget.OnClickEndTurn);
        widget.RefreshInteractable();
        return widget;
    }

    void Update()
    {
        RefreshInteractable();
    }

    private void OnClickEndTurn()
    {
        NetworkBattleController.RequestEndTurnFromInput();
    }

    private void RefreshInteractable()
    {
        if (button == null)
            return;

        bool canEndTurn = CanEndTurn();
        if (button.interactable != canEndTurn)
            button.interactable = canEndTurn;

        if (label != null)
        {
            float alpha = canEndTurn ? 1f : 0.55f;
            var color = label.color;
            color.a = alpha;
            label.color = color;
        }
    }

    private static bool CanEndTurn()
    {
        var tm = TurnManager.Instance;
        if (tm == null || tm.Phase != TurnPhase.TurnAction)
            return false;

        var actor = tm.CurrentActor;
        if (actor == null || !BattleNetworkGate.CanLocalControlActor(actor))
            return false;

        var movement = actor.GetComponent<CharacterMovementController>();
        if (movement != null && movement.IsMoving)
            return false;

        if (BattleNetworkGate.IsNetworkBattleActive
            && NetworkBattleController.Instance == null
            && BattleNetworkRuntimeSpawner.ResolveController() == null
            && !NetworkServer.active)
            return false;

        return true;
    }
}
