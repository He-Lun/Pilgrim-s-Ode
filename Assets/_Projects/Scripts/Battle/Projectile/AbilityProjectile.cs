using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 弹体飞行方式。
/// </summary>
public enum ProjectileMode
{
    /// <summary>追踪：锁定目标飞行（魔法飞弹）。</summary>
    Homing,
    /// <summary>直线：沿方向/朝落点飞行，沿途命中首个敌人（光剑/光束）。</summary>
    Straight
}

/// <summary>
/// 弹体发射参数 —— 由 ProjectileAbilityEffect 构造后传入。
/// </summary>
public class ProjectileSpec
{
    public AbilitySystemComponent caster;
    public GameplayAbility sourceAbility;

    public ProjectileMode mode = ProjectileMode.Homing;
    public AbilitySystemComponent homingTarget;
    public Vector3 direction = Vector3.forward;
    public bool hasTargetPoint;
    public Vector3 targetPoint;

    public float speed = 12f;
    public float hitRadius = 0.6f;
    public float maxRange = 10f;
    public float maxLifetime = 5f;

    public List<AbilityEffect> onImpactEffects;
    public GameObject impactVfx;
    public float impactVfxAutoDestroy = 3f;
}

/// <summary>
/// 技能弹体 —— 命中判定与命中时机由弹体自身的飞行/碰撞决定，而非角色动画事件。
/// 到达目标/落点时执行命中载荷(onImpactEffects) 并生成命中特效。
/// </summary>
public class AbilityProjectile : MonoBehaviour
{
    private ProjectileSpec spec;
    private Vector3 dir;
    private float traveled;
    private float age;
    private bool resolved;
    private bool launched;

    /// <summary>发射弹体。</summary>
    public void Launch(ProjectileSpec setup)
    {
        spec = setup;
        if (spec == null)
        {
            Destroy(gameObject);
            return;
        }

        dir = spec.direction;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f)
            dir = spec.caster != null ? spec.caster.transform.forward : Vector3.forward;
        dir.Normalize();

        // 直线朝落点时，最大射程收敛到落点距离，保证到点即结算(可用于范围技)。
        if (spec.mode == ProjectileMode.Straight && spec.hasTargetPoint)
        {
            float toPoint = BattleTargeting.HorizontalDistance(transform.position, spec.targetPoint);
            if (toPoint > 0.01f)
                spec.maxRange = toPoint;
        }

        transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
        launched = true;
    }

    void Update()
    {
        if (!launched || resolved) return;

        age += Time.deltaTime;

        // 施法者已销毁：静默取消，不结算。
        if (spec.caster == null)
        {
            Cancel();
            return;
        }

        if (age >= spec.maxLifetime)
        {
            Impact(null); // 超时按落点结算（范围技在末端爆炸；单体则无目标）
            return;
        }

        float step = spec.speed * Time.deltaTime;

        if (spec.mode == ProjectileMode.Homing)
            TickHoming(step);
        else
            TickStraight(step);
    }

    private void TickHoming(float step)
    {
        if (!BattleTargeting.IsAlive(spec.homingTarget))
        {
            Cancel(); // 目标已失效，静默消失
            return;
        }

        Vector3 aim = AimPoint(spec.homingTarget);
        Vector3 to = aim - transform.position;
        float dist = to.magnitude;

        if (dist <= Mathf.Max(spec.hitRadius, step))
        {
            transform.position = aim;
            Impact(new List<AbilitySystemComponent> { spec.homingTarget });
            return;
        }

        Vector3 moveDir = to / dist;
        transform.position += moveDir * step;
        transform.rotation = Quaternion.LookRotation(moveDir, Vector3.up);
    }

    private void TickStraight(float step)
    {
        transform.position += dir * step;
        traveled += step;

        var hits = BattleTargeting.FilterEnemiesInRadius(spec.caster, transform.position, spec.hitRadius);
        if (hits.Count > 0)
        {
            Impact(hits);
            return;
        }

        if (traveled >= spec.maxRange)
            Impact(null); // 到达末端/落点：范围载荷据此爆炸
    }

    private static Vector3 AimPoint(AbilitySystemComponent asc)
    {
        return asc.transform.position + Vector3.up * 1.0f;
    }

    /// <summary>命中结算：执行载荷 + 命中特效 + 销毁。hits 为 null 表示按落点(无锁定单体)结算。</summary>
    private void Impact(List<AbilitySystemComponent> hits)
    {
        if (resolved) return;
        resolved = true;

        var ctx = new AbilityActivationContext
        {
            explicitTargets = hits ?? new List<AbilitySystemComponent>(),
            targetWorldPoint = transform.position,
            hasTargetPoint = true
        };

        if (spec.caster != null && spec.onImpactEffects != null)
        {
            foreach (var effect in spec.onImpactEffects)
                effect?.Execute(spec.caster, spec.sourceAbility, ctx);
        }

        if (spec.impactVfx != null)
            AbilityVfxPlayer.SpawnOneShotAt(spec.impactVfx, transform.position, transform.rotation, spec.impactVfxAutoDestroy);

        Destroy(gameObject);
    }

    /// <summary>静默取消（施法者/目标失效），不结算不放特效。</summary>
    private void Cancel()
    {
        if (resolved) return;
        resolved = true;
        Destroy(gameObject);
    }
}
