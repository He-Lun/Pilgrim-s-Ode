using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>手牌 UI 桥接。</summary>
[RequireComponent(typeof(BattleHandPanel))]
public class BattleHandViewBridge : MonoBehaviour
{
    [SerializeField] private BattleHandPanel handPanel;
    [SerializeField] private BattleAbilityTestInput abilityInput;

    private HandCardManager boundHand;
    private AbilitySystemComponent boundActor;
    private bool subscribed;
    private Coroutine pendingTargetedPlay;
    private readonly List<BattleHandCardWidget> widgets = new List<BattleHandCardWidget>();

    void Awake()
    {
        handPanel ??= GetComponent<BattleHandPanel>();
        abilityInput ??= FindObjectOfType<BattleAbilityTestInput>();
        handPanel.CanAffordCard = CanAffordCard;
        handPanel.OnCardReleased = HandleCardReleased;
    }

    void OnEnable()
    {
        TrySubscribe();
        SyncIfBattleInProgress();
    }

    void OnDisable()
    {
        if (pendingTargetedPlay != null)
        {
            StopCoroutine(pendingTargetedPlay);
            pendingTargetedPlay = null;
        }

        Unsubscribe();
        UnbindHand();
    }

    public void Resync()
    {
        TrySubscribe();
        if (TurnManager.Instance?.CurrentActor is { } actor && boundActor != actor)
            BindActor(actor);
        SyncView();
    }

    private void TrySubscribe()
    {
        if (subscribed || TurnManager.Instance == null)
            return;

        TurnManager.Instance.OnTurnBegan += HandleTurnBegan;
        TurnManager.Instance.OnTurnEnded += HandleTurnEnded;
        TurnManager.Instance.OnPhaseChanged += HandlePhaseChanged;
        subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!subscribed || TurnManager.Instance == null)
            return;

        TurnManager.Instance.OnTurnBegan -= HandleTurnBegan;
        TurnManager.Instance.OnTurnEnded -= HandleTurnEnded;
        TurnManager.Instance.OnPhaseChanged -= HandlePhaseChanged;
        subscribed = false;
    }

    private void SyncIfBattleInProgress()
    {
        var tm = TurnManager.Instance;
        if (tm == null || tm.CurrentActor == null)
            return;
        if (tm.Phase != TurnPhase.TurnDraw && tm.Phase != TurnPhase.TurnAction)
            return;
        if (boundActor == tm.CurrentActor)
            return;

        BindActor(tm.CurrentActor);
        HandlePhaseChanged(tm.Phase);
    }

    private void HandleTurnBegan(AbilitySystemComponent actor) => BindActor(actor);

    private void HandleTurnEnded(AbilitySystemComponent _) => ClearView();

    private void HandlePhaseChanged(TurnPhase phase)
    {
        if (TurnManager.Instance?.CurrentActor is { } current && boundActor != current)
            BindActor(current);

        switch (phase)
        {
            case TurnPhase.TurnDraw:
                handPanel.canUseCards = false;
                handPanel.canSelectCards = true;
                SyncView();
                break;
            case TurnPhase.TurnAction:
                handPanel.canUseCards = true;
                handPanel.canSelectCards = true;
                SyncView();
                break;
            case TurnPhase.TurnSettle:
            case TurnPhase.BattleEnd:
                ClearView();
                UnbindHand();
                break;
        }
    }

    private void BindActor(AbilitySystemComponent actor)
    {
        UnbindHand();
        boundActor = actor;

        // Host 同时跑 Server，对手 HandCards 里有真实底牌，不能绑到 UI 上。
        if (!BattleNetworkGate.CanLocalViewHand(actor))
        {
            ClearView();
            return;
        }

        boundHand = actor.HandCards;
        boundHand.HandChanged += SyncView;
        SyncView();
    }

    private void UnbindHand()
    {
        if (boundHand != null)
            boundHand.HandChanged -= SyncView;
        boundHand = null;
        boundActor = null;
    }

    private void SyncView()
    {
        if (boundHand == null || !BattleNetworkGate.CanLocalViewHand(boundActor))
        {
            ClearView();
            return;
        }

        widgets.RemoveAll(w => w == null);
        int count = boundHand.HandCount;

        if (count == 0)
        {
            ClearView();
            return;
        }

        while (widgets.Count < count)
            widgets.Add(CreateWidget());

        while (widgets.Count > count)
        {
            var last = widgets[widgets.Count - 1];
            widgets.RemoveAt(widgets.Count - 1);
            if (last != null)
                Destroy(last.gameObject);
        }

        for (int i = 0; i < count; i++)
        {
            var ability = boundHand.Hand[i].ability;
            widgets[i].gameObject.SetActive(true);
            widgets[i].Bind(ability, i, ability != null ? ability.icon : null);
        }

        handPanel.SetCards(widgets);
    }

    private BattleHandCardWidget CreateWidget()
    {
        var go = new GameObject("HandCard", typeof(RectTransform), typeof(BattleHandCardWidget));
        go.transform.SetParent(handPanel.transform, false);
        return go.GetComponent<BattleHandCardWidget>();
    }

    private void ClearView()
    {
        for (int i = widgets.Count - 1; i >= 0; i--)
        {
            if (widgets[i] != null)
                Destroy(widgets[i].gameObject);
        }

        widgets.Clear();
        handPanel.SetCards(widgets);
    }

    private bool CanAffordCard(BattleHandCardWidget card)
    {
        if (boundActor == null || card.ability == null)
            return false;
        if (TurnManager.Instance == null)
            return false;
        if (TurnManager.Instance.Phase != TurnPhase.TurnAction)
            return false;
        if (TurnManager.Instance.CurrentActor != boundActor)
            return false;
        if (!BattleNetworkGate.CanLocalControlActor(boundActor))
            return false;

        return boundActor.TeamResource.CurrentActionPoints >= card.ability.actionPointCost
               && card.ability.CanActivate(boundActor) == AbilityActivationResult.Success;
    }

    private void HandleCardReleased(BattleHandCardWidget card)
    {
        abilityInput ??= FindObjectOfType<BattleAbilityTestInput>();

        if (IsImmediateScope(card.ability))
        {
            TryPlayImmediateFromHand(card.handIndex);
            handPanel.RefreshLayout();
            return;
        }

        if (abilityInput == null)
        {
            Debug.LogWarning("[BattleHandViewBridge] 缺少 BattleAbilityTestInput，无法选目标。");
            handPanel.RefreshLayout();
            return;
        }

        if (pendingTargetedPlay != null)
            StopCoroutine(pendingTargetedPlay);

        // 等松手帧结束再进入选目标，避免与 BattleHandPanel / BattleInputController 抢同一帧鼠标事件
        pendingTargetedPlay = StartCoroutine(BeginTargetedPlayNextFrame(card));
        handPanel.RefreshLayout();
    }

    private IEnumerator BeginTargetedPlayNextFrame(BattleHandCardWidget card)
    {
        yield return null;
        pendingTargetedPlay = null;

        if (card == null || boundActor == null || abilityInput == null)
            yield break;

        abilityInput.TryBeginPlayFromHand(card.handIndex, card.ability);
    }

    private bool IsImmediateScope(GameplayAbility ability)
    {
        switch (ability.GetEffectiveTargetScope(boundActor))
        {
            case TargetScope.Self:
            case TargetScope.AllAllies:
            case TargetScope.AllEnemies:
                return true;
            default:
                return false;
        }
    }

    private void TryPlayImmediateFromHand(int handIndex)
    {
        if (boundActor == null)
            return;

        if (BattleNetworkGate.IsNetworkBattleActive && NetworkBattleController.Instance != null)
        {
            int slot = NetworkBattleActor.GetSlotIndex(boundActor);
            var context = NetAbilityContext.From(handIndex, AbilityActivationContext.Self());
            NetworkBattleController.Instance.RequestPlayCard(slot, context);
            return;
        }

        if (BattleCardFacade.TryPlayImmediate(boundActor, handIndex) != AbilityActivationResult.Success)
            handPanel.RefreshLayout();
    }
}
