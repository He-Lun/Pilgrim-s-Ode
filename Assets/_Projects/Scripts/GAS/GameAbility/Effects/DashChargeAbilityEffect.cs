using UnityEngine;

/// <summary>
/// 突进冲锋 — Immediate 调度；表现阶段平滑位移，路径上逐敌结算伤害与侧向击退。
/// </summary>
[System.Serializable]
public class DashChargeAbilityEffect : AbilityEffect
{
    [Tooltip("突进距离（米）；0 = 使用 GA areaRadiusMeters")]
    public float dashDistanceMeters;

    [Tooltip("突进速度（米/秒）")]
    [Min(0.1f)]
    public float dashSpeedMetersPerSecond = 12f;

    [Tooltip("路径半宽（米），判定碰到的敌人")]
    [Min(0.1f)]
    public float pathHalfWidthMeters = 0.75f;

    [Tooltip("伤害 = scaler × 攻击力")]
    public float damageScaler = 1.5f;

    public GameplayTag damageType = new GameplayTag("DamageType.Physical");

    [Tooltip("侧向击退距离（米）")]
    public float knockbackDistanceMeters = 1.2f;

    [Tooltip("侧向击退持续时间（秒）")]
    public float knockbackDurationSeconds = 0.28f;

    [Tooltip("突进期间施法者霸体 — 穿激光等仍受伤，但不进受击、不打断突进")]
    public bool grantCasterHyperArmor = true;

    public override void Execute(
        AbilitySystemComponent caster,
        GameplayAbility sourceAbility,
        AbilityActivationContext context)
    {
        if (!ShouldExecute(caster) || sourceAbility == null)
            return;

        if (!TryResolveDirection(caster, context, out Vector3 direction))
            return;

        float distance = dashDistanceMeters > 0f
            ? dashDistanceMeters
            : sourceAbility.GetAreaRadiusMeters();

        if (distance <= 0f || dashSpeedMetersPerSecond <= 0f)
            return;

        var motor = caster.GetComponent<CharacterMotor>();
        if (motor == null) return;

        motor.ScheduleDashCharge(new DashChargeSpec
        {
            direction = direction,
            distanceMeters = distance,
            speedMetersPerSecond = dashSpeedMetersPerSecond,
            pathHalfWidthMeters = pathHalfWidthMeters,
            damageScaler = damageScaler,
            damageType = damageType,
            knockbackDistanceMeters = knockbackDistanceMeters,
            knockbackDurationSeconds = knockbackDurationSeconds,
            hitVfx = targetVfx,
            grantCasterHyperArmor = grantCasterHyperArmor
        });
    }

    public override void Execute(AbilitySystemComponent caster, System.Collections.Generic.List<AbilitySystemComponent> targets) { }

    private static bool TryResolveDirection(
        AbilitySystemComponent caster,
        AbilityActivationContext context,
        out Vector3 direction)
    {
        direction = DirectedRectUtility.ResolveAimDirection(context, caster.transform.position);
        return direction.sqrMagnitude > 0.0001f;
    }
}
