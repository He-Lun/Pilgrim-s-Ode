using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 击退效果 — 沿远离施法者方向平滑位移，遇 NavMesh 障碍或其他单位停止。
/// </summary>
[System.Serializable]
public class KnockbackAbilityEffect : AbilityEffect
{
    [Tooltip("击退距离（米）")]
    public float distanceMeters = 2f;

    [Tooltip("击退持续时间（秒），决定初速与衰减")]
    public float durationSeconds = 0.35f;

    public override void Execute(AbilitySystemComponent caster, List<AbilitySystemComponent> targets)
    {
        if (caster == null || targets == null || distanceMeters <= 0f) return;

        Vector3 center = caster.transform.position;

        foreach (var target in targets)
        {
            if (target == null) continue;

            var movement = target.GetComponent<CharacterMovementController>();
            if (movement != null)
                movement.TryApplyKnockback(center, distanceMeters, durationSeconds);
        }
    }
}
