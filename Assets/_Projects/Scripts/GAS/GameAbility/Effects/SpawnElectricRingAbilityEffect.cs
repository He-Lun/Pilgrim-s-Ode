using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 在友方身上生成跟随电环 — 敌人进入环带时造成法术伤害并击退。
/// </summary>
[System.Serializable]
public class SpawnElectricRingAbilityEffect : AbilityEffect
{
    [Tooltip("每跳伤害 = scaler × 施法者攻击力")]
    public float scaler = 2f;

    public GameplayTag damageType = new GameplayTag("DamageType.AP");

    [Tooltip("持续回合数（宿主回合结束递减）")]
    public int durationTurns = 2;

    [Tooltip("0 = 使用 GA 的 areaRadiusMeters")]
    public float radiusMetersOverride;

    [Tooltip("击退距离（米）")]
    public float knockbackDistanceMeters = 2f;

    [Tooltip("击退持续时间（秒）")]
    public float knockbackDurationSeconds = 0.35f;

    [Tooltip("跟随宿主的电环预制体（挂为宿主子物体）")]
    public GameObject ringVfxPrefab;

    public override void Execute(
        AbilitySystemComponent caster,
        GameplayAbility sourceAbility,
        AbilityActivationContext context)
    {
        if (!ShouldExecute(caster) || sourceAbility == null)
            return;

        var allies = ResolveTargets(caster, sourceAbility, context);
        if (allies == null || allies.Count == 0)
            return;

        float radius = radiusMetersOverride > 0f
            ? radiusMetersOverride
            : sourceAbility.GetAreaRadiusMeters();

        var hitVfx = targetVfx != null && targetVfx.IsValid ? targetVfx : null;

        foreach (var host in allies)
        {
            if (host == null || !BattleTargeting.IsAlive(host)) continue;

            ElectricRingManager.Instance.SpawnRing(
                caster,
                host,
                radius,
                durationTurns,
                scaler,
                damageType,
                knockbackDistanceMeters,
                knockbackDurationSeconds,
                ringVfxPrefab,
                hitVfx);
        }
    }

    public override void Execute(AbilitySystemComponent caster, List<AbilitySystemComponent> targets) { }
}
