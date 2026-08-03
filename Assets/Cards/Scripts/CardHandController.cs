using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using System;

namespace Cyan.Cards
{
    public class CardHandController : MonoBehaviour
    {
        [Header("API")]
        public Action<CardData> OnCardPlayed;
        public Action OnNotEnoughMana;


        [Header("Gameplay Settings")]
        public int mana = 3;
        public bool canUseCards = true;
        public bool canSelectCards = true;
        public int maxHandSize = 10;

        [Header("Settings")]
        [SerializeField] private bool cardUprightWhenSelected = true;
        [SerializeField] private bool cardTilt = true;
        [SerializeField][Range(0, 5)] private float selectionSpacing = 1;
        private bool updateHierarchyOrder = false;

        [SerializeField] private Vector3 curveStart = new Vector3(2f, -0.7f, 0), curveEnd = new Vector3(-2f, -0.7f, 0);
        [SerializeField] private Vector2 handOffset = new Vector2(0, -0.3f), handSize = new Vector2(9, 1.7f);

        [Header("References")]
        [SerializeField] private Camera cam = null;
        [SerializeField] private Material inactiveCardMaterial = null;

        [Header("Card Cycle Settings")]
        public Transform deckAnchor;
        public Card cardPrefab;
        public int initialDeckSize = 10;

        [HideInInspector] public List<Card> deck = new List<Card>();
        [HideInInspector] public List<Card> discardPile = new List<Card>();
        [HideInInspector] public List<Card> exhaustPile = new List<Card>();

        public UnityEvent<int, int> OnDeckAndDiscardCountChanged;

        private Plane plane;
        private Vector3 a, b, c;
        private List<Card> hand;

        private int selected = -1;
        private int dragged = -1;
        private Card heldCard;
        private Vector3 heldCardOffset;
        private Vector2 heldCardTilt;
        private Vector2 force;
        private Vector3 mouseWorldPos;
        private Vector2 prevMousePos;
        private Vector2 mousePosDelta;
        private Rect handBounds;
        private bool mouseInsideHand;

        private void Start()
        {
            a = transform.TransformPoint(curveStart);
            b = transform.position;
            c = transform.TransformPoint(curveEnd);
            handBounds = new Rect((handOffset - handSize / 2), handSize);
            plane = new Plane(-Vector3.forward, transform.position);
            prevMousePos = Input.mousePosition;

            if (cam == null) cam = Camera.main;

            int count = transform.childCount;
            hand = new List<Card>(count);
            for (int i = 0; i < count; i++)
            {
                Transform cardTransform = transform.GetChild(i);
                Card card = cardTransform.GetComponent<Card>();
                if (card != null) hand.Add(card);
            }

            if (cardPrefab != null)
            {
                for (int i = 0; i < initialDeckSize; i++)
                {
                    Card newCard = Instantiate(cardPrefab);
                    if (deckAnchor != null)
                    {
                        newCard.transform.SetParent(deckAnchor);
                        newCard.transform.position = deckAnchor.position;
                    }
                    newCard.gameObject.SetActive(false);
                    deck.Add(newCard);
                }
                ShuffleDeck();
            }
            UpdatePileUI();
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Alpha1))
            {
                Debug.Log($"\n🔘 [调试] 按下数字键1：模拟回合开始，尝试抽 5 张牌...");
                DrawCards(5);
            }
            if (Input.GetKeyDown(KeyCode.Alpha2))
            {
                Debug.Log($"\n🔘 [调试] 按下数字键2：尝试抽 1 张牌...");
                DrawCards(1);
            }
            if (Input.GetKeyDown(KeyCode.Alpha3))
            {
                Debug.Log($"\n🔘 [调试] 按下数字键3：模拟回合结束，丢弃所有手牌...");
                DiscardAllHand();
            }
            // ==========================================

            Vector2 mousePos = Input.mousePosition;
            mousePos.x = Mathf.Clamp(mousePos.x, 0, Screen.width);
            mousePos.y = Mathf.Clamp(mousePos.y, 0, Screen.height);

            if (cardTilt)
            {
                mousePosDelta = (mousePos - prevMousePos) * new Vector2(1600f / Screen.width, 900f / Screen.height) * Time.deltaTime;
                prevMousePos = mousePos;
                float tiltStrength = 3f;
                float tiltDrag = 3f;
                float tiltSpeed = 50f;
                force += (mousePosDelta * tiltStrength - heldCardTilt) * Time.deltaTime;
                force *= 1 - tiltDrag * Time.deltaTime;
                heldCardTilt += force * Time.deltaTime * tiltSpeed;
            }

            if (cam != null)
            {
                Ray ray = cam.ScreenPointToRay(mousePos);
                if (plane.Raycast(ray, out float enter)) mouseWorldPos = ray.GetPoint(enter);
            }

            int count = hand.Count;
            float sqrDistance = 1000;
            if (selected >= 0 && selected < count)
            {
                float t = (selected + 0.5f) / count;
                Vector3 p = GetCurvePoint(a, b, c, t);
                sqrDistance = (p - mouseWorldPos).sqrMagnitude;
            }

            Vector3 point = transform.InverseTransformPoint(mouseWorldPos);
            mouseInsideHand = handBounds.Contains(point);
            bool mouseButton = Input.GetMouseButton(0);

            for (int i = 0; i < count; i++)
            {
                Card card = hand[i];
                if (card == null) continue;

                Transform cardTransform = card.transform;
                card.SetInactiveMaterialState(mana < card.mana, inactiveCardMaterial);

                bool noCardHeld = (heldCard == null);
                bool onSelectedCard = (noCardHeld && selected == i);
                bool onDraggedCard = (noCardHeld && dragged == i);

                float selectOffset = 0;
                if (noCardHeld) selectOffset = 0.02f * Mathf.Clamp01(1 - Mathf.Abs(Mathf.Abs(i - selected) - 1) / (float)count * 3) * Mathf.Sign(i - selected);
                float t = (i + 0.5f) / count + selectOffset * selectionSpacing;
                Vector3 p = GetCurvePoint(a, b, c, t);

                float d = (p - mouseWorldPos).sqrMagnitude;
                bool mouseCloseToCard = d < 0.5f;
                bool mouseHoveringOnSelected = onSelectedCard && mouseCloseToCard && mouseInsideHand;

                Vector3 cardUp = GetCurveNormal(a, b, c, t);
                Vector3 cardPos = p + (mouseHoveringOnSelected ? cardTransform.up * 0.3f : Vector3.zero);
                Vector3 cardForward = Vector3.forward;

                if (mouseHoveringOnSelected || onDraggedCard)
                {
                    if (cardUprightWhenSelected) cardUp = Vector3.up;
                    cardPos.z = transform.position.z - 0.2f;
                }
                else cardPos.z = transform.position.z + t * 0.5f;

                cardTransform.rotation = Quaternion.RotateTowards(cardTransform.rotation, Quaternion.LookRotation(cardForward, cardUp), 80f * Time.deltaTime);

                if (mouseHoveringOnSelected)
                {
                    if (Input.GetMouseButtonDown(0))
                    {
                        dragged = i;
                        heldCardOffset = cardTransform.position - mouseWorldPos;
                        heldCardOffset.z = -0.1f;
                    }
                }

                if (onDraggedCard && mouseButton) cardTransform.position = mouseWorldPos + heldCardOffset;
                else cardTransform.position = Vector3.MoveTowards(cardTransform.position, cardPos, 6f * Time.deltaTime);

                if (canSelectCards) { if (d < sqrDistance) { sqrDistance = d; selected = i; } }
                else { selected = -1; dragged = -1; }
            }

            if (!mouseButton) { heldCardOffset = Vector3.zero; dragged = -1; }

            if (dragged != -1)
            {
                Card card = hand[dragged];
                if (mouseButton && !mouseInsideHand)
                {
                    heldCard = card;
                    RemoveCardFromHand(dragged);
                    count--;
                    dragged = -1;
                }
            }

            if (heldCard == null && mouseButton && dragged != -1 && selected != -1 && dragged != selected)
            {
                MoveCardToIndex(dragged, selected);
                dragged = selected;
            }

            if (heldCard != null)
            {
                Transform cardTransform = heldCard.transform;
                Vector3 cardPos = mouseWorldPos + heldCardOffset;
                Vector3 cardForward = Vector3.forward;
                if (cardTilt && mouseButton) cardForward -= new Vector3(heldCardTilt.x, heldCardTilt.y, 0);

                cardPos.z = transform.position.z - 0.2f;
                cardTransform.rotation = Quaternion.RotateTowards(cardTransform.rotation, Quaternion.LookRotation(cardForward, Vector3.up), 80f * Time.deltaTime);
                cardTransform.position = cardPos;

                if (!canSelectCards || mouseInsideHand)
                {
                    AddCardToHand(heldCard, selected);
                    dragged = selected; selected = -1; heldCard = null;
                    return;
                }

                if (Input.GetMouseButtonUp(0))
                {
                    if (canUseCards && mana >= heldCard.mana)
                    {
                        mana -= heldCard.mana;
                        heldCard.Use();

                        OnCardPlayed?.Invoke(heldCard.data);

                        StartCoroutine(WaitAndDiscard(heldCard, 1.0f));
                    }
                    else
                    {
                        OnNotEnoughMana?.Invoke();
                        AddCardToHand(heldCard, selected);
                    }
                    heldCard = null;
                }
            }
        }

        public static Vector3 GetCurvePoint(Vector3 a, Vector3 b, Vector3 c, float t)
        {
            t = Mathf.Clamp01(t);
            float oneMinusT = 1f - t;
            return (oneMinusT * oneMinusT * a) + (2f * oneMinusT * t * b) + (t * t * c);
        }
        public static Vector3 GetCurveTangent(Vector3 a, Vector3 b, Vector3 c, float t) { return 2f * (1f - t) * (b - a) + 2f * t * (c - b); }
        public static Vector3 GetCurveNormal(Vector3 a, Vector3 b, Vector3 c, float t) { return Vector3.Cross(GetCurveTangent(a, b, c, t), Vector3.forward); }

        public void MoveCardToIndex(int currentIndex, int toIndex)
        {
            if (currentIndex == toIndex) return;
            Card card = hand[currentIndex];
            hand.RemoveAt(currentIndex);
            hand.Insert(toIndex, card);
            if (updateHierarchyOrder) card.transform.SetSiblingIndex(toIndex);
        }

        public void AddCardToHand(Card card, int index = -1)
        {
            if (index < 0 || index > hand.Count) { hand.Add(card); index = hand.Count - 1; }
            else hand.Insert(index, card);
            if (updateHierarchyOrder) { card.transform.SetParent(transform); card.transform.SetSiblingIndex(index); }
        }

        public void RemoveCardFromHand(int index)
        {
            if (updateHierarchyOrder) { hand[index].transform.SetParent(transform.parent); hand[index].transform.SetSiblingIndex(transform.GetSiblingIndex() + 1); }
            hand.RemoveAt(index);
        }

        public void DrawCards(int amount)
        {
            for (int i = 0; i < amount; i++)
            {
                if (deck.Count == 0) RecycleDiscardPile();
                if (deck.Count == 0) break;

                Card card = deck[0];
                deck.RemoveAt(0);

                if (hand.Count >= maxHandSize)
                {
                    if (deckAnchor != null) { card.transform.SetParent(deckAnchor); card.transform.position = deckAnchor.position; }
                    card.gameObject.SetActive(false);
                    discardPile.Add(card);
                }
                else
                {
                    card.transform.SetParent(transform);
                    card.gameObject.SetActive(true);
                    AddCardToHand(card);
                }
            }
            UpdatePileUI();
        }

        public void DiscardCard(Card card)
        {
            if (hand.Contains(card)) hand.Remove(card);
            if (deckAnchor != null) { card.transform.SetParent(deckAnchor); card.transform.position = deckAnchor.position; }
            card.gameObject.SetActive(false);
            discardPile.Add(card);
            UpdatePileUI();
        }

        public void DiscardAllHand()
        {
            for (int i = hand.Count - 1; i >= 0; i--) DiscardCard(hand[i]);
        }

        private void RecycleDiscardPile()
        {
            if (discardPile.Count == 0) return;
            deck.AddRange(discardPile);
            discardPile.Clear();
            ShuffleDeck();
        }

        public void ShuffleDeck()
        {
            for (int i = 0; i < deck.Count; i++)
            {
                Card temp = deck[i];
                int randomIndex = UnityEngine.Random.Range(i, deck.Count);
                deck[i] = deck[randomIndex];
                deck[randomIndex] = temp;
            }
            foreach (Card card in deck)
            {
                if (deckAnchor != null) card.transform.SetParent(deckAnchor);
                card.gameObject.SetActive(false);
            }
            UpdatePileUI();
        }

        private void UpdatePileUI() { OnDeckAndDiscardCountChanged?.Invoke(deck.Count, discardPile.Count); }

        private IEnumerator WaitAndDiscard(Card card, float delay)
        {
            yield return new WaitForSeconds(delay);
            if (card != null) DiscardCard(card);
        }

        public void API_StartTurn(int drawAmount, int maxManaCap)
        {
            this.mana = maxManaCap;
            DrawCards(drawAmount);
        }

        public void API_EndTurn()
        {
            DiscardAllHand();
        }

        public void API_ModifyMana(int amount)
        {
            this.mana += amount;
            if (this.mana < 0) this.mana = 0;
        }
    }
}