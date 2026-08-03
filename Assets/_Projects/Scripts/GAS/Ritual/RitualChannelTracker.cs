using UnityEngine;

/// <summary>
/// 祈福引导 — 施法者 State.Channeling，目标 Buff.BlessingWard（冻结 Buff.* 修改器 Tick）。
/// 被动打断：施法者进入受击/眩晕、任一方死亡、持续耗尽。
/// </summary>
public class RitualChannelTracker
{
    private AbilitySystemComponent caster;
    private AbilitySystemComponent wardTarget;
    private GameplayTag channelTag;
    private GameplayTag wardTag;
    private int turnsRemaining;
    private bool active;
    private bool subscribed;

    public bool IsActive => active;

    public void Begin(
        AbilitySystemComponent ritualCaster,
        AbilitySystemComponent target,
        GameplayTag channel,
        GameplayTag ward,
        int durationTurns)
    {
        if (ritualCaster == null || target == null) return;

        End();

        caster = ritualCaster;
        wardTarget = target;
        channelTag = channel;
        wardTag = ward;
        turnsRemaining = durationTurns;
        active = true;

        ritualCaster.ApplyBuffTo(ritualCaster, channelTag, ritualCaster);
        ritualCaster.ApplyBuffTo(target, wardTag, ritualCaster);

        CombatEventBus.Instance.OnEvent += HandleCombatEvent;
        subscribed = true;
    }

    public void End()
    {
        if (!active) return;
        active = false;

        if (subscribed)
        {
            CombatEventBus.Instance.OnEvent -= HandleCombatEvent;
            subscribed = false;
        }

        caster?.RemoveTag(channelTag);
        wardTarget?.RemoveTag(wardTag);

        caster = null;
        wardTarget = null;
    }

    public void Interrupt()
    {
        if (!active) return;
        var c = caster;
        End();
        c?.GetComponent<CharacterMotor>()?.ReleaseFromChannel();
    }

    public void OnCasterTurnEnded()
    {
        if (!active || turnsRemaining <= 0) return;
        turnsRemaining--;
        if (turnsRemaining <= 0)
            End();
    }

    public void Dispose() => End();

    private void HandleCombatEvent(CombatEvent evt)
    {
        if (!active) return;

        if (evt.type == CombatEventType.CharacterKilled
            && (evt.target == caster || evt.target == wardTarget))
        {
            Interrupt();
            return;
        }

        if (evt.target != caster) return;

        if (evt.type == CombatEventType.HitReacted || evt.type == CombatEventType.StunEntered)
            Interrupt();
    }
}
