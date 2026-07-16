using UnityEngine;

/// <summary>
/// 突进冲锋运行时参数 — 由 DashChargeAbilityEffect 写入 CharacterMotor。
/// </summary>
public struct DashChargeSpec
{
    public Vector3 direction;
    public float distanceMeters;
    public float speedMetersPerSecond;
    public float pathHalfWidthMeters;
    public float damageScaler;
    public GameplayTag damageType;
    public float knockbackDistanceMeters;
    public float knockbackDurationSeconds;
    /// <summary>路径命中敌人时播的特效（与 DamageEffect.targetVfx 同语义）。</summary>
    public VfxSpawnEntry hitVfx;
    /// <summary>突进期间施法者霸体（受伤但不进受击、不打断位移）。</summary>
    public bool grantCasterHyperArmor;
}
