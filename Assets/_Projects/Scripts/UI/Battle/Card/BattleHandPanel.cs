using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>屏幕底部手牌区。</summary>
public class BattleHandPanel : MonoBehaviour
{
    public static BattleHandPanel Instance { get; private set; }

    public bool canUseCards;
    public bool canSelectCards = true;

    [SerializeField] private RectTransform cardContainer;
    [SerializeField] private Vector2 cardSize = new Vector2(118f, 168f);
    [SerializeField] private float cardSpacing = 128f;
    [SerializeField] private float maxFanAngle = 12f;
    [SerializeField] private float selectedLift = 28f;
    [SerializeField] private float playDragUpPixels = 110f;

    private RectTransform panelRect;
    private readonly List<BattleHandCardWidget> cards = new List<BattleHandCardWidget>();
    private BattleHandCardWidget heldCard;
    private int selectedIndex = -1;
    private Vector2 dragOffset;

    public Func<BattleHandCardWidget, bool> CanAffordCard;
    public Action<BattleHandCardWidget> OnCardReleased;

    void Awake()
    {
        Instance = this;
        panelRect = GetComponent<RectTransform>();
        cardContainer ??= CreateCardContainer();
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    void Update()
    {
        RefreshAffordStates();
        UpdateHoverTooltip();

        if (BattleAbilityTestInput.IsAnyTargeting)
        {
            BattleHandCardTooltip.Instance?.Hide();
            return;
        }

        if (!canSelectCards && heldCard == null)
        {
            selectedIndex = -1;
            return;
        }

        if (Input.GetMouseButtonDown(0))
            TryBeginDrag();

        if (heldCard != null && Input.GetMouseButton(0))
            DragHeldCard();

        if (heldCard != null && Input.GetMouseButtonUp(0))
            ReleaseHeldCard();
    }

    private void UpdateHoverTooltip()
    {
        var tooltip = BattleHandCardTooltip.Instance;
        if (tooltip == null)
            return;

        if (heldCard != null || !canSelectCards)
        {
            tooltip.Hide();
            return;
        }

        var hovered = FindCardUnderPointer();
        if (hovered?.ability != null)
            tooltip.Show(hovered.ability, Input.mousePosition);
        else
            tooltip.Hide();
    }

    private BattleHandCardWidget FindCardUnderPointer()
    {
        for (int i = cards.Count - 1; i >= 0; i--)
        {
            var card = cards[i];
            if (card == null)
                continue;

            if (RectTransformUtility.RectangleContainsScreenPoint(card.Rect, Input.mousePosition, null))
                return card;
        }

        return null;
    }

    public bool IsPointerOverPanel()
    {
        panelRect ??= GetComponent<RectTransform>();
        if (heldCard != null)
            return true;

        return RectTransformUtility.RectangleContainsScreenPoint(panelRect, Input.mousePosition, null);
    }

    public void SetCards(IReadOnlyList<BattleHandCardWidget> nextCards)
    {
        cards.Clear();
        if (nextCards != null)
            cards.AddRange(nextCards);
        RefreshLayout();
    }

    public void RefreshLayout()
    {
        RefreshAffordStates();

        int count = cards.Count;
        if (count == 0)
            return;

        float startX = -(count - 1) * cardSpacing * 0.5f;

        for (int i = 0; i < count; i++)
        {
            var card = cards[i];
            if (card == null || (heldCard != null && card == heldCard))
                continue;

            var rect = card.Rect;
            rect.SetParent(cardContainer, false);
            rect.sizeDelta = cardSize;
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);

            float t = count <= 1 ? 0.5f : i / (float)(count - 1);
            float angle = Mathf.Lerp(maxFanAngle, -maxFanAngle, t);
            float lift = selectedIndex == i ? selectedLift : 0f;

            rect.anchoredPosition = new Vector2(startX + i * cardSpacing, lift);
            rect.localRotation = Quaternion.Euler(0f, 0f, angle);
            rect.localScale = Vector3.one;
        }
    }

    private void RefreshAffordStates()
    {
        for (int i = 0; i < cards.Count; i++)
        {
            var card = cards[i];
            if (card == null)
                continue;

            bool affordable = CanAffordCard == null || CanAffordCard(card);
            card.SetAffordable(affordable);
        }
    }

    private void TryBeginDrag()
    {
        BattleHandCardTooltip.Instance?.Hide();

        for (int i = cards.Count - 1; i >= 0; i--)
        {
            var card = cards[i];
            if (card == null)
                continue;

            if (!RectTransformUtility.RectangleContainsScreenPoint(card.Rect, Input.mousePosition, null))
                continue;

            selectedIndex = i;
            heldCard = card;
            dragOffset = card.Rect.anchoredPosition - ScreenToContainerLocal(Input.mousePosition);
            RefreshLayout();
            heldCard.Rect.SetAsLastSibling();
            heldCard.Rect.localRotation = Quaternion.identity;
            return;
        }
    }

    private void DragHeldCard()
    {
        if (heldCard == null)
            return;

        heldCard.Rect.anchoredPosition = ScreenToContainerLocal(Input.mousePosition) + dragOffset;
        heldCard.Rect.localRotation = Quaternion.identity;
    }

    private void ReleaseHeldCard()
    {
        var released = heldCard;
        heldCard = null;
        selectedIndex = -1;

        bool play = !IsPointerOverPanel() || released.Rect.anchoredPosition.y >= playDragUpPixels;
        if (play && canUseCards && CanAffordCard != null && CanAffordCard(released))
            OnCardReleased?.Invoke(released);

        RefreshLayout();
    }

    private Vector2 ScreenToContainerLocal(Vector2 screenPoint)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            cardContainer, screenPoint, null, out Vector2 local);
        return local;
    }

    private RectTransform CreateCardContainer()
    {
        var go = new GameObject("CardContainer", typeof(RectTransform));
        go.transform.SetParent(transform, false);
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = new Vector2(0f, 12f);
        rect.sizeDelta = new Vector2(900f, cardSize.y + 40f);
        return rect;
    }
}
