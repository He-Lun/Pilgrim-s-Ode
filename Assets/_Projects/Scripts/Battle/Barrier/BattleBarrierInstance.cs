using UnityEngine;

/// <summary>
/// 平面屏障 — 水平墙段垂直于 Forward，攻击路径穿过则视为外部攻击。
/// </summary>
public sealed class BattleBarrierInstance
{
    public Vector3 Center { get; }
    public Vector3 Forward { get; }
    public float WidthMeters { get; }
    public float ThicknessMeters { get; }
    public float DamageReduction { get; }
    public bool ProtectAlliesOnly { get; }
    public AbilitySystemComponent Instigator { get; }
    public GameplayTag BarrierTag { get; }
    public int RemainingTurns { get; private set; }

    internal GameObject VfxInstance { get; private set; }

    public BattleBarrierInstance(
        Vector3 center,
        Vector3 forward,
        float widthMeters,
        float thicknessMeters,
        float damageReduction,
        bool protectAlliesOnly,
        int durationTurns,
        AbilitySystemComponent instigator,
        GameplayTag barrierTag)
    {
        Center = center;
        Forward = Flatten(forward);
        WidthMeters = widthMeters;
        ThicknessMeters = thicknessMeters;
        DamageReduction = Mathf.Clamp01(damageReduction);
        ProtectAlliesOnly = protectAlliesOnly;
        RemainingTurns = durationTurns;
        Instigator = instigator;
        BarrierTag = barrierTag;
    }

    public void GetWallSegment(out Vector3 a, out Vector3 b)
    {
        Vector3 right = Vector3.Cross(Vector3.up, Forward).normalized;
        float half = WidthMeters * 0.5f;
        a = Center - right * half;
        b = Center + right * half;
    }

    public bool AppliesTo(AbilitySystemComponent attacker, AbilitySystemComponent victim)
    {
        if (attacker == null || victim == null || Instigator == null)
            return false;
        if (ProtectAlliesOnly && !victim.IsAlly(Instigator))
            return false;

        return BarrierLineMath.PathCrossesWall(
            attacker.transform.position,
            victim.transform.position,
            this);
    }

    public bool TickInstigatorTurnEnd()
    {
        if (RemainingTurns <= 0)
            return true;

        RemainingTurns--;
        return RemainingTurns <= 0;
    }

    internal void AttachVfx(GameObject instance) => VfxInstance = instance;

    internal void DestroyVfx(bool immediate = false)
    {
        if (VfxInstance == null) return;

        if (immediate)
            WorldVfxSpawner.DestroyInstance(VfxInstance);
        else
            WorldVfxSpawner.BeginExpire(VfxInstance);

        VfxInstance = null;
    }

    private static Vector3 Flatten(Vector3 v)
    {
        v.y = 0f;
        return v.sqrMagnitude < 0.0001f ? Vector3.forward : v.normalized;
    }
}
