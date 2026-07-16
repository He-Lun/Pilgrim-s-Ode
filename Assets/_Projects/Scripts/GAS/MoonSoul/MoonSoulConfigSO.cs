using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 月魂 / 月相数值配置 — 挂到 CharacterDataSO.moonSoulConfig（露娜）。
/// </summary>
[CreateAssetMenu(fileName = "MoonSoulConfig", menuName = "巡礼之诗/月魂配置")]
public class MoonSoulConfigSO : ScriptableObject
{
    [Serializable]
    public class StatBonus
    {
        public string attributeName = "Attack";
        [Tooltip("乘法加成：最终 = 基础 × (1 + 值)")]
        public float multiplicativeBonus = 0.1f;
    }

    [Serializable]
    public class PhaseBuff
    {
        public MoonPhase phase = MoonPhase.NewMoon;
        public List<StatBonus> bonuses = new List<StatBonus>();
    }

    [Header("层数")]
    [Min(1)] public int maxStacks = 10;

    [Tooltip("新月上限（含）：0~该值")]
    [Min(0)] public int newMoonMaxStacks = 3;

    [Tooltip("弦月上限（含）：newMoonMax+1 ~ 该值；之上为满月")]
    [Min(0)] public int halfMoonMaxStacks = 8;

    [Header("各月相被动加成")]
    public List<PhaseBuff> phaseBuffs = new List<PhaseBuff>
    {
        new PhaseBuff
        {
            phase = MoonPhase.NewMoon,
            bonuses = new List<StatBonus>
            {
                new StatBonus { attributeName = "Attack", multiplicativeBonus = 0.1f }
            }
        },
        new PhaseBuff
        {
            phase = MoonPhase.HalfMoon,
            bonuses = new List<StatBonus>
            {
                new StatBonus { attributeName = "Attack", multiplicativeBonus = 0.25f },
                new StatBonus { attributeName = "Defense", multiplicativeBonus = 0.1f }
            }
        },
        new PhaseBuff
        {
            phase = MoonPhase.FullMoon,
            bonuses = new List<StatBonus>
            {
                new StatBonus { attributeName = "Attack", multiplicativeBonus = 0.5f },
                new StatBonus { attributeName = "Defense", multiplicativeBonus = 0.2f },
                new StatBonus { attributeName = "Speed", multiplicativeBonus = 0.1f }
            }
        }
    };

    public MoonPhase ResolvePhase(int stacks)
    {
        stacks = Mathf.Clamp(stacks, 0, maxStacks);
        if (stacks <= newMoonMaxStacks)
            return MoonPhase.NewMoon;
        if (stacks <= halfMoonMaxStacks)
            return MoonPhase.HalfMoon;
        return MoonPhase.FullMoon;
    }

    public static GameplayTag PhaseTag(MoonPhase phase)
    {
        switch (phase)
        {
            case MoonPhase.HalfMoon: return GameplayTag.Buff.MoonPhase_HalfMoon;
            case MoonPhase.FullMoon: return GameplayTag.Buff.MoonPhase_FullMoon;
            default: return GameplayTag.Buff.MoonPhase_NewMoon;
        }
    }

    public PhaseBuff GetPhaseBuff(MoonPhase phase)
    {
        if (phaseBuffs == null) return null;
        for (int i = 0; i < phaseBuffs.Count; i++)
        {
            if (phaseBuffs[i] != null && phaseBuffs[i].phase == phase)
                return phaseBuffs[i];
        }

        return null;
    }
}
