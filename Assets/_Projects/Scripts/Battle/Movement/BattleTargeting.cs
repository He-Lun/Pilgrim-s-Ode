using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 米制战斗目标查询 — 范围技能、距离判定。
/// </summary>
public static class BattleTargeting
{
    private static readonly Collider[] OverlapBuffer = new Collider[32];

    public static List<AbilitySystemComponent> FindAbilitySystemsInRadius(Vector3 center, float radiusMeters)
    {
        var result = new List<AbilitySystemComponent>();
        int count = Physics.OverlapSphereNonAlloc(center, radiusMeters, OverlapBuffer);

        for (int i = 0; i < count; i++)
        {
            var col = OverlapBuffer[i];
            if (col == null) continue;

            var asc = col.GetComponentInParent<AbilitySystemComponent>();
            if (asc != null && !result.Contains(asc))
                result.Add(asc);
        }

        return result;
    }

    public static float HorizontalDistance(Vector3 a, Vector3 b)
    {
        return BattleOccupancy.HorizontalDistance(a, b);
    }

    public static bool IsAlive(AbilitySystemComponent asc)
    {
        return asc != null && asc.Attributes != null && !asc.Attributes.IsDead();
    }

    /// <summary>圆心半径内过滤存活角色（可按阵营筛选）。</summary>
    public static List<AbilitySystemComponent> FilterActorsInRadius(
        AbilitySystemComponent caster,
        Vector3 center,
        float radiusMeters,
        AreaAffiliationFilter affiliation)
    {
        var result = new List<AbilitySystemComponent>();
        if (caster == null || radiusMeters <= 0f) return result;

        foreach (var asc in FindAbilitySystemsInRadius(center, radiusMeters))
        {
            if (asc == null || !IsAlive(asc)) continue;
            if (!MatchesAffiliation(caster, asc, affiliation)) continue;
            if (!result.Contains(asc))
                result.Add(asc);
        }

        return result;
    }

    /// <summary>DirectedRect 内过滤存活角色（可按阵营筛选）。</summary>
    public static List<AbilitySystemComponent> FilterActorsInDirectedRect(
        AbilitySystemComponent caster,
        Vector3 origin,
        Vector3 aimDirection,
        float lengthMeters,
        float widthMeters,
        AreaAffiliationFilter affiliation)
    {
        var result = new List<AbilitySystemComponent>();
        if (caster == null || lengthMeters <= 0f || widthMeters <= 0f)
            return result;

        var rect = DirectedRectUtility.Build(origin, aimDirection, lengthMeters, widthMeters);
        float searchRadius = Mathf.Sqrt(lengthMeters * lengthMeters + (widthMeters * 0.5f) * (widthMeters * 0.5f));

        foreach (var asc in FindAbilitySystemsInRadius(origin, searchRadius))
        {
            if (asc == null || !IsAlive(asc)) continue;
            if (!MatchesAffiliation(caster, asc, affiliation)) continue;
            if (!DirectedRectUtility.ContainsPoint(rect, asc.transform.position)) continue;
            if (!result.Contains(asc))
                result.Add(asc);
        }

        return result;
    }

    private static bool MatchesAffiliation(
        AbilitySystemComponent caster,
        AbilitySystemComponent candidate,
        AreaAffiliationFilter affiliation)
    {
        switch (affiliation)
        {
            case AreaAffiliationFilter.AlliesOnly:
                return candidate.TeamId == caster.TeamId;
            case AreaAffiliationFilter.EnemiesOnly:
                return caster.IsEnemy(candidate);
            default:
                return true;
        }
    }

    /// <summary>同阵营存活角色（含施法者）。</summary>
    public static List<AbilitySystemComponent> FilterAllies(
        AbilitySystemComponent caster,
        bool includeCaster = true)
    {
        var result = new List<AbilitySystemComponent>();
        if (caster == null) return result;

        foreach (var asc in FindAllBattleActors())
        {
            if (asc == null) continue;
            if (!includeCaster && asc == caster) continue;
            if (asc.TeamId != caster.TeamId) continue;
            if (!result.Contains(asc))
                result.Add(asc);
        }

        return result;
    }

    /// <summary>敌对阵营存活角色。</summary>
    public static List<AbilitySystemComponent> FilterEnemies(AbilitySystemComponent caster)
    {
        var result = new List<AbilitySystemComponent>();
        if (caster == null) return result;

        foreach (var asc in FindAllBattleActors())
        {
            if (asc == null || asc == caster) continue;
            if (!caster.IsEnemy(asc)) continue;
            if (!result.Contains(asc))
                result.Add(asc);
        }

        return result;
    }

    /// <summary>圆心半径内过滤存活敌人（不含施法者）。</summary>
    public static List<AbilitySystemComponent> FilterEnemiesInRadius(
        AbilitySystemComponent caster,
        Vector3 center,
        float radiusMeters)
    {
        var result = new List<AbilitySystemComponent>();
        if (caster == null || radiusMeters <= 0f) return result;

        foreach (var asc in FindAbilitySystemsInRadius(center, radiusMeters))
        {
            if (asc == null || asc == caster) continue;
            if (!IsAlive(asc)) continue;
            if (!caster.IsEnemy(asc)) continue;
            if (!result.Contains(asc))
                result.Add(asc);
        }

        return result;
    }

    /// <summary>DirectedRect 矩形内过滤存活敌人（不含施法者）。</summary>
    public static List<AbilitySystemComponent> FilterEnemiesInDirectedRect(
        AbilitySystemComponent caster,
        Vector3 origin,
        Vector3 aimDirection,
        float lengthMeters,
        float widthMeters)
    {
        var result = new List<AbilitySystemComponent>();
        if (caster == null || lengthMeters <= 0f || widthMeters <= 0f)
            return result;

        var rect = DirectedRectUtility.Build(origin, aimDirection, lengthMeters, widthMeters);
        float searchRadius = Mathf.Sqrt(lengthMeters * lengthMeters + (widthMeters * 0.5f) * (widthMeters * 0.5f));

        foreach (var asc in FindAbilitySystemsInRadius(origin, searchRadius))
        {
            if (asc == null || asc == caster) continue;
            if (!IsAlive(asc)) continue;
            if (!caster.IsEnemy(asc)) continue;
            if (!DirectedRectUtility.ContainsPoint(rect, asc.transform.position)) continue;
            if (!result.Contains(asc))
                result.Add(asc);
        }

        return result;
    }

    /// <summary>预览 DirectedRect 内会被命中的敌人。</summary>
    public static List<AbilitySystemComponent> PreviewDirectedRectTargets(
        AbilitySystemComponent caster,
        GameplayAbility ability,
        Vector3 aimDirection)
    {
        if (caster == null || ability == null || ability.targetScope != TargetScope.DirectedRect)
            return new List<AbilitySystemComponent>();

        return FilterEnemiesInDirectedRect(
            caster,
            caster.transform.position,
            aimDirection,
            ability.GetAreaRadiusMeters(),
            ability.GetAreaWidthMeters());
    }

    public static List<AbilitySystemComponent> ResolveEffectTargets(
        AbilitySystemComponent caster,
        GameplayAbility ability,
        AbilityActivationContext context,
        EffectTargetSelection selection)
    {
        if (selection == EffectTargetSelection.ExplicitOnly || ability == null || caster == null)
            return context.GetExplicitTargets();

        return ability.ResolveEffectTargets(caster, context);
    }

    /// <summary>单体/指向技能的施法距离（米）。与 AOE 半径共用 GA 上的 range 字段。</summary>
    public static float GetCastRangeMeters(GameplayAbility ability)
    {
        return ability != null ? ability.GetAreaRadiusMeters() : 0f;
    }

    public static bool IsValidAbilityTarget(
        AbilitySystemComponent caster,
        AbilitySystemComponent target,
        GameplayAbility ability)
    {
        if (caster == null || target == null || ability == null) return false;
        if (!IsAlive(target)) return false;

        switch (ability.targetScope)
        {
            case TargetScope.Self:
                return target == caster;

            case TargetScope.SingleEnemy:
                if (!caster.IsEnemy(target)) return false;
                break;

            case TargetScope.SingleAlly:
                if (target != caster && !caster.IsAlly(target)) return false;
                break;

            case TargetScope.AllEnemies:
            case TargetScope.AllAllies:
            case TargetScope.Area:
            case TargetScope.AreaAroundSelf:
            case TargetScope.DirectedRect:
                return false;

            default:
                return false;
        }

        if (ability.targetScope == TargetScope.Self)
            return true;

        float range = GetCastRangeMeters(ability);
        return HorizontalDistance(caster.transform.position, target.transform.position) <= range;
    }

    public static List<AbilitySystemComponent> GetValidTargetsInRange(
        AbilitySystemComponent caster,
        GameplayAbility ability,
        IEnumerable<AbilitySystemComponent> candidates)
    {
        var result = new List<AbilitySystemComponent>();
        if (caster == null || ability == null || candidates == null) return result;

        if (ability.targetScope == TargetScope.Self)
        {
            if (IsAlive(caster))
                result.Add(caster);
            return result;
        }

        if (ability.targetScope == TargetScope.AreaAroundSelf
            || ability.targetScope == TargetScope.DirectedRect)
            return result;

        foreach (var candidate in candidates)
        {
            if (candidate == null) continue;
            if (IsValidAbilityTarget(caster, candidate, ability))
                result.Add(candidate);
        }

        return result;
    }

    public static List<AbilitySystemComponent> FindAllBattleActors()
    {
        var result = new List<AbilitySystemComponent>();
        var found = Object.FindObjectsOfType<AbilitySystemComponent>();

        foreach (var asc in found)
        {
            if (asc == null || !asc.gameObject.activeInHierarchy) continue;
            if (!IsAlive(asc)) continue;
            if (!result.Contains(asc))
                result.Add(asc);
        }

        return result;
    }

    public static AbilitySystemComponent RaycastUnit(Camera camera, LayerMask unitMask)
    {
        if (camera == null) return null;

        Ray ray = camera.ScreenPointToRay(Input.mousePosition);
        if (!Physics.Raycast(ray, out RaycastHit hit, 500f, unitMask, QueryTriggerInteraction.Ignore))
            return null;

        return hit.collider.GetComponentInParent<AbilitySystemComponent>();
    }
}
