using System;
using UnityEngine;

/// <summary>
/// 月魂层数 + 月相同步 — 挂于 ASC，类似 InspirationTracker。
/// 层数变化时刷新互斥月相 tag 与对应永久属性加成。
/// </summary>
public class MoonSoulTracker
{
    public const int DefaultMaxStacks = 10;

    private AbilitySystemComponent owner;
    private MoonSoulConfigSO config;
    private int stacks;
    private MoonPhase phase = MoonPhase.NewMoon;
    private bool bound;

    /// <summary>最近一次 ConsumeAll 的层数（供消耗型技能读取倍率）。</summary>
    public int LastConsumedStacks { get; private set; }

    /// <summary>最近一次 ConsumeAll 的月相。</summary>
    public MoonPhase LastConsumedPhase { get; private set; } = MoonPhase.NewMoon;

    public int Stacks => stacks;
    public MoonPhase Phase => phase;
    public int MaxStacks => config != null ? Mathf.Max(1, config.maxStacks) : DefaultMaxStacks;
    public bool IsBound => bound;
    public MoonSoulConfigSO Config => config;

    public event Action<int, MoonPhase> OnChanged;
    public event Action<int, MoonPhase> OnConsumed;

    public void Initialize(AbilitySystemComponent asc, MoonSoulConfigSO moonConfig)
    {
        Dispose();

        owner = asc;
        config = moonConfig;
        stacks = 0;
        phase = MoonPhase.NewMoon;
        LastConsumedStacks = 0;
        LastConsumedPhase = MoonPhase.NewMoon;

        if (owner == null || config == null)
            return;

        bound = true;
        SyncPhasePresentation(force: true);
    }

    public void Dispose()
    {
        if (bound && owner != null)
            ClearPhasePresentation();

        bound = false;
        owner = null;
        config = null;
        stacks = 0;
        phase = MoonPhase.NewMoon;
    }

    public int Add(int amount)
    {
        if (!bound || amount == 0) return stacks;
        return Set(stacks + amount);
    }

    public int Remove(int amount)
    {
        if (!bound || amount <= 0) return stacks;
        return Set(stacks - amount);
    }

    public int Set(int value)
    {
        if (!bound) return stacks;

        int clamped = Mathf.Clamp(value, 0, MaxStacks);
        if (clamped == stacks)
        {
            // 开战首次仍要挂上新月 tag/加成
            SyncPhasePresentation(force: false);
            return stacks;
        }

        stacks = clamped;
        SyncPhasePresentation(force: false);
        OnChanged?.Invoke(stacks, phase);
        NotifyMoonSoulChanged();
        return stacks;
    }

    private void NotifyMoonSoulChanged()
    {
        if (owner == null) return;

        CombatEventBus.Instance.Raise(new CombatEvent
        {
            type = CombatEventType.MoonSoulChanged,
            instigator = owner,
            target = owner,
            intValue = stacks
        });
    }

    /// <summary>清空月魂，返回消耗前层数与月相；并记录 LastConsumed*。</summary>
    public (int stacks, MoonPhase phase) ConsumeAll()
    {
        if (!bound)
            return (0, MoonPhase.NewMoon);

        LastConsumedStacks = stacks;
        LastConsumedPhase = phase;
        var snapshot = (stacks, phase);

        if (stacks != 0)
            Set(0);
        else
            SyncPhasePresentation(force: true);

        OnConsumed?.Invoke(LastConsumedStacks, LastConsumedPhase);
        return snapshot;
    }

    public static MoonPhase ResolvePhase(int stackCount, MoonSoulConfigSO cfg)
    {
        if (cfg != null)
            return cfg.ResolvePhase(stackCount);

        if (stackCount <= 3) return MoonPhase.NewMoon;
        if (stackCount <= 8) return MoonPhase.HalfMoon;
        return MoonPhase.FullMoon;
    }

    private void SyncPhasePresentation(bool force)
    {
        if (owner == null || config == null) return;

        MoonPhase next = config.ResolvePhase(stacks);
        if (!force && next == phase && owner.HasTag(MoonSoulConfigSO.PhaseTag(phase)))
            return;

        ClearPhasePresentation();
        phase = next;
        ApplyPhasePresentation(phase);
    }

    private void ClearPhasePresentation()
    {
        if (owner?.Attributes == null) return;

        RemovePhase(MoonPhase.NewMoon);
        RemovePhase(MoonPhase.HalfMoon);
        RemovePhase(MoonPhase.FullMoon);
    }

    private void RemovePhase(MoonPhase p)
    {
        var tag = MoonSoulConfigSO.PhaseTag(p);
        owner.Attributes.RemoveModifier(tag);
        owner.RemoveTag(tag);
    }

    private void ApplyPhasePresentation(MoonPhase p)
    {
        var tag = MoonSoulConfigSO.PhaseTag(p);
        var buff = config.GetPhaseBuff(p);

        if (buff?.bonuses != null && owner.Attributes != null)
        {
            for (int i = 0; i < buff.bonuses.Count; i++)
            {
                var b = buff.bonuses[i];
                if (b == null || string.IsNullOrEmpty(b.attributeName)) continue;

                owner.Attributes.AddModifier(new AttributeModifier(
                    b.attributeName,
                    b.multiplicativeBonus,
                    ModifierOperation.Multiplicative,
                    tag,
                    0));
            }
        }

        owner.ApplyBuffTo(owner, tag, owner);
    }
}
