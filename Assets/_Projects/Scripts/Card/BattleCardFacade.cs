/// <summary>
/// 出牌编排：HandCardManager 的 PlayCardResult → ASC 激活 → 通知回合系统。
/// </summary>
public static class BattleCardFacade
{
    public static AbilityActivationResult TryPlay(
        AbilitySystemComponent caster,
        PlayCardResult play)
    {
        if (caster == null || !play.isValid || play.ability == null)
            return AbilityActivationResult.UnknownError;

        if (TurnManager.Instance != null
            && TurnManager.Instance.Phase != TurnPhase.TurnAction)
            return AbilityActivationResult.UnknownError;

        if (TurnManager.Instance != null
            && TurnManager.Instance.CurrentActor != caster)
            return AbilityActivationResult.UnknownError;

        var result = caster.ActivateAbility(play.ability, play.context);
        if (result != AbilityActivationResult.Success)
            return result;

        caster.HandCards?.CommitPlay(play.handIndex);
        TurnManager.Instance?.NotifyActionResolved();
        return AbilityActivationResult.Success;
    }

    /// <summary>无需选目标的即时出牌（Self / All* 等）。</summary>
    public static AbilityActivationResult TryPlayImmediate(
        AbilitySystemComponent caster,
        int handIndex)
    {
        if (caster?.HandCards == null)
            return AbilityActivationResult.UnknownError;

        var ability = caster.HandCards.GetAbilityAt(handIndex);
        if (ability == null)
            return AbilityActivationResult.UnknownError;

        var context = BuildDefaultContext(caster, ability);
        var play = caster.HandCards.PreparePlay(handIndex, context);
        return TryPlay(caster, play);
    }

    public static AbilityActivationContext BuildDefaultContext(
        AbilitySystemComponent caster,
        GameplayAbility ability)
    {
        if (ability == null || caster == null)
            return AbilityActivationContext.Self();

        switch (ability.GetEffectiveTargetScope(caster))
        {
            case TargetScope.Self:
            case TargetScope.AreaAroundSelf:
                return AbilityActivationContext.Self();
            case TargetScope.AllAllies:
            case TargetScope.AllEnemies:
                return AbilityActivationContext.Self();
            case TargetScope.DirectedRect:
            case TargetScope.DirectedSector:
                return AbilityActivationContext.WithAimDirection(caster.transform.forward);
            default:
                return AbilityActivationContext.Self();
        }
    }
}
