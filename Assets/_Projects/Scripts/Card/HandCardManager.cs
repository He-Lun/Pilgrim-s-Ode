using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>手牌运行时管理，UI 由 BattleHandViewBridge 同步。</summary>
[DisallowMultipleComponent]
public class HandCardManager : MonoBehaviour
{
    private AbilitySystemComponent owner;
    private int handLimit = 5;
    private readonly List<GameplayAbility> drawPile = new List<GameplayAbility>();
    private readonly List<GameplayAbility> discardPile = new List<GameplayAbility>();
    private readonly List<HandCardSlot> hand = new List<HandCardSlot>();

    public event Action HandChanged;

    public int HandLimit => handLimit;
    public int HandCount => hand.Count;
    public IReadOnlyList<HandCardSlot> Hand => hand;

    public void Bind(AbilitySystemComponent asc) => owner = asc;

    /// <summary>开战：洗牌，手牌为空。</summary>
    public void InitializeForBattle(int limit)
    {
        handLimit = Mathf.Max(1, limit);
        hand.Clear();
        discardPile.Clear();
        RebuildDrawPile();
        NotifyHandChanged();
    }

    /// <summary>回合抽牌阶段：抽 drawCount 张（不超过手牌上限）。</summary>
    public void DrawForTurn(int drawCount)
    {
        int drawn = 0;
        while (drawn < drawCount && hand.Count < handLimit && TryDrawOne())
            drawn++;
        NotifyHandChanged();
    }

    public bool ContainsInHand(GameplayAbility ability)
    {
        if (ability == null) return false;
        for (int i = 0; i < hand.Count; i++)
        {
            if (hand[i].ability == ability)
                return true;
        }
        return false;
    }

    public GameplayAbility GetAbilityAt(int handIndex)
    {
        if (handIndex < 0 || handIndex >= hand.Count)
            return null;
        return hand[handIndex].ability;
    }

    public IReadOnlyList<GameplayAbility> GetHandAbilities()
    {
        var list = new List<GameplayAbility>(hand.Count);
        for (int i = 0; i < hand.Count; i++)
        {
            if (hand[i].ability != null)
                list.Add(hand[i].ability);
        }
        return list;
    }

    /// <summary>Client 按 Server 下发顺序重建手牌镜像。</summary>
    public void ApplyNetworkHand(IReadOnlyList<GameplayAbility> abilities, IReadOnlyList<bool> inspirationFlags)
    {
        if (BattleNetworkGate.IsSimulationServer)
            return;

        hand.Clear();

        if (abilities != null)
        {
            for (int i = 0; i < abilities.Count; i++)
            {
                if (abilities[i] == null)
                    continue;

                bool inspiration = inspirationFlags != null
                    && i < inspirationFlags.Count
                    && inspirationFlags[i];
                hand.Add(HandCardSlot.FromAbility(abilities[i], inspiration));
            }
        }

        handLimit = Mathf.Max(handLimit, hand.Count);
        NotifyHandChanged();
    }

    public bool TryGrantInspirationCard(GameplayAbility ability)
    {
        if (ability == null || hand.Count >= handLimit)
            return false;

        hand.Add(HandCardSlot.FromAbility(ability, inspiration: true));
        NotifyHandChanged();
        return true;
    }

    public PlayCardResult PreparePlay(int handIndex, AbilityActivationContext context)
    {
        if (owner == null)
            return PlayCardResult.Invalid(handIndex);

        if (handIndex < 0 || handIndex >= hand.Count)
            return PlayCardResult.Invalid(handIndex);

        var ability = hand[handIndex].ability;
        if (ability == null)
            return PlayCardResult.Invalid(handIndex);

        if (!owner.KnowsAbility(ability))
            return PlayCardResult.Invalid(handIndex);

        if (ability.CanActivate(owner, context) != AbilityActivationResult.Success)
            return PlayCardResult.Invalid(handIndex);

        return PlayCardResult.Ready(handIndex, ability, context);
    }

    public void CommitPlay(int handIndex)
    {
        if (handIndex < 0 || handIndex >= hand.Count)
            return;

        var ability = hand[handIndex].ability;
        hand.RemoveAt(handIndex);
        if (ability != null)
            discardPile.Add(ability);

        NotifyHandChanged();
    }

    private bool TryDrawOne()
    {
        if (drawPile.Count == 0)
            RecycleDiscardIntoDrawPile();

        if (drawPile.Count == 0)
            return false;

        var ability = drawPile[0];
        drawPile.RemoveAt(0);
        hand.Add(HandCardSlot.FromAbility(ability));
        return true;
    }

    private void RecycleDiscardIntoDrawPile()
    {
        if (discardPile.Count == 0)
            return;

        drawPile.AddRange(discardPile);
        discardPile.Clear();
        Shuffle(drawPile);
    }

    private void RebuildDrawPile()
    {
        drawPile.Clear();
        var data = owner?.CharacterData;
        if (data?.battleDeck == null || data.battleDeck.Count == 0)
        {
            Debug.LogWarning($"[HandCardManager] {owner.name} 的 CharacterData.battleDeck 为空，无法抽牌。");
            return;
        }

        for (int i = 0; i < data.battleDeck.Count; i++)
        {
            var ability = data.battleDeck[i];
            if (ability != null)
                drawPile.Add(ability);
        }

        Shuffle(drawPile);
    }

    private static void Shuffle(List<GameplayAbility> pile)
    {
        for (int i = pile.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            (pile[i], pile[j]) = (pile[j], pile[i]);
        }
    }

    private void NotifyHandChanged() => HandChanged?.Invoke();
}
