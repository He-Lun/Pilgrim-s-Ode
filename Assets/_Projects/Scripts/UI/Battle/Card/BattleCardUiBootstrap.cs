using UnityEngine;
using UnityEngine.UI;

/// <summary>手牌面板 Bootstrap。</summary>
public static class BattleCardUiBootstrap
{
    public static void Ensure(Transform battleUiRoot)
    {
        var legacy = battleUiRoot.Find("BattleCardUi");
        if (legacy != null)
            Object.Destroy(legacy.gameObject);

        var canvas = battleUiRoot.GetComponentInChildren<Canvas>(true);
        if (canvas == null)
        {
            Debug.LogError("[BattleCardUiBootstrap] 未找到 Overlay Canvas。");
            return;
        }

        var panelTransform = canvas.transform.Find("BattleHandPanel");
        if (panelTransform == null)
            panelTransform = CreateHandPanel(canvas.transform).transform;

        var bridge = panelTransform.GetComponent<BattleHandViewBridge>();
        if (bridge == null)
            bridge = panelTransform.gameObject.AddComponent<BattleHandViewBridge>();

        bridge.Resync();
        BattleHandCardTooltip.Ensure(canvas.transform);
    }

    private static GameObject CreateHandPanel(Transform canvasTransform)
    {
        var panelGo = new GameObject(
            "BattleHandPanel",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(BattleHandPanel),
            typeof(BattleHandViewBridge));

        panelGo.transform.SetParent(canvasTransform, false);

        var rect = panelGo.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = new Vector2(0f, 24f);
        rect.sizeDelta = new Vector2(960f, 220f);

        var bg = panelGo.GetComponent<Image>();
        bg.color = Color.clear;
        bg.raycastTarget = false;

        return panelGo;
    }
}
