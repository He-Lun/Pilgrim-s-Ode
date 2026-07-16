using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 可摧毁召唤物管理 — 生成登记、同 tag 替换、战斗结束清理、施法者阵亡时销毁。
/// </summary>
public sealed class BattleDestructiblePropManager
{
    private static BattleDestructiblePropManager instance;
    public static BattleDestructiblePropManager Instance => instance ??= new BattleDestructiblePropManager();

    private readonly List<DestructibleBattleProp> props = new List<DestructibleBattleProp>();
    private bool subscribed;

    public IReadOnlyList<DestructibleBattleProp> ActiveProps => props;

    public void EnsureSubscribed()
    {
        if (subscribed) return;
        CombatEventBus.Instance.OnEvent += HandleCombatEvent;
        subscribed = true;
    }

    public void ClearAll()
    {
        for (int i = props.Count - 1; i >= 0; i--)
        {
            var prop = props[i];
            if (prop != null)
                prop.Teardown(destroyGameObject: true, immediateVfx: true);
        }

        props.Clear();
    }

    public void Register(DestructibleBattleProp prop)
    {
        if (prop == null || props.Contains(prop)) return;
        EnsureSubscribed();
        props.Add(prop);
    }

    public void Unregister(DestructibleBattleProp prop)
    {
        if (prop == null) return;
        props.Remove(prop);
    }

    /// <summary>销毁同 tag 的旧召唤物（再召唤时替换）。</summary>
    public void DestroyByTag(GameplayTag propTag, AbilitySystemComponent owner)
    {
        if (string.IsNullOrEmpty(propTag.TagName)) return;

        for (int i = props.Count - 1; i >= 0; i--)
        {
            var prop = props[i];
            if (prop == null)
            {
                props.RemoveAt(i);
                continue;
            }

            if (!prop.PropTag.Matches(propTag)) continue;
            if (owner != null && prop.Owner != owner) continue;

            prop.Teardown(destroyGameObject: true, immediateVfx: true);
        }
    }

    private void HandleCombatEvent(CombatEvent evt)
    {
        if (evt.type != CombatEventType.CharacterKilled || evt.target == null)
            return;

        for (int i = props.Count - 1; i >= 0; i--)
        {
            var prop = props[i];
            if (prop == null)
            {
                props.RemoveAt(i);
                continue;
            }

            // 施法者阵亡：晶石播 Out；晶石自身阵亡由 DestructibleBattleProp.OnDeath 处理
            if (prop.Owner == evt.target)
                prop.Teardown(destroyGameObject: true, immediateVfx: false);
        }
    }
}
