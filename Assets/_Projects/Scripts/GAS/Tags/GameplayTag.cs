using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using Unity.VisualScripting;
using System.Runtime.CompilerServices;

[Serializable]
    public struct GameplayTag : IEquatable<GameplayTag>
    {
        [SerializeField] private string tagName;//只做名称显示
        private int cachedId;//参与运算

        public string TagName => tagName;

        public int Id
        {
            get
            {
                if (cachedId == 0 && !string.IsNullOrEmpty(tagName))
                {
                    cachedId = TagRegistry.Register(tagName);
                }
                return cachedId;
            }
        }

        public GameplayTag(string name)
        {
            tagName = name;
            cachedId = 0;
        }

        //比较标签是否相同
        public bool Matches(GameplayTag other)
        {
            // 优先用ID比较
            if (cachedId != 0 && other.cachedId != 0)
                return cachedId == other.cachedId;
            
            return tagName == other.tagName;
        }

        // 判断父子关系（例如 Damage.Fire 是否属于 Damage）
        public bool IsChildOf(GameplayTag parent)
        {
            if (string.IsNullOrEmpty(tagName)) return false;
            return TagRegistry.IsChildOf(this.Id, parent.Id);
        }

        public bool StartsWith(string prefix) => tagName?.StartsWith(prefix) ?? false;

        // ---------- 操作符 ----------
        public static bool operator ==(GameplayTag a, GameplayTag b) => a.Matches(b);
        public static bool operator !=(GameplayTag a, GameplayTag b) => !a.Matches(b);
        
        public bool Equals(GameplayTag other) => Matches(other);
        public override bool Equals(object obj) => obj is GameplayTag other && Matches(other);
        public override int GetHashCode() => Id;
        public override string ToString() => tagName ?? "Null";

        /// <summary>
        /// -------------此处添加具体标签类---------------
        /// </summary>
        public static class State
        {
            public static readonly GameplayTag Idle = new GameplayTag("State.Idle");
            public static readonly GameplayTag Moving = new GameplayTag("State.Moving");
            public static readonly GameplayTag Casting = new GameplayTag("State.Casting");
            public static readonly GameplayTag Hit = new GameplayTag("State.Hit");
            public static readonly GameplayTag Dead = new GameplayTag("State.Dead");
            [Header("引导/仪式中")]
            public static readonly GameplayTag Channeling = new GameplayTag("State.Channeling");
        }
        
        //职业
        public static class Job
        {
            [Header("圣骑士")]
            public static readonly GameplayTag Paladin = new GameplayTag("Job.Paladin");
            [Header("织法者")]
            public static readonly GameplayTag Wisdom = new GameplayTag("Job.Wisdom");
            [Header("刺客")]
            public static readonly GameplayTag Assassin = new GameplayTag("Job.Assassin");
            [Header("怒士")]
            public static readonly GameplayTag Berserker = new GameplayTag("Job.Berserker");
            [Header("回复术士")]
            public static readonly GameplayTag Healer = new GameplayTag("Job.Healer");
            [Header("追猎者")]
            public static readonly GameplayTag Hunter = new GameplayTag("Job.Hunter");
            [Header("神官")]
            public static readonly GameplayTag priest = new GameplayTag("Job.Priest");
        }

        //王国
        public static class Kingdom
        {
            [Header("斯非尼亚")]
            public static readonly GameplayTag Surfacia = new GameplayTag("Kingdom.Surfacia");
            [Header("图斯加德")]
            public static readonly GameplayTag Tuesgard = new GameplayTag("Kingdom.Tuesgard");
            [Header("欧森尼亚")]
            public static readonly GameplayTag Ossenia = new GameplayTag("Kingdom.Ossenia");
            [Header("伊门")]
            public static readonly GameplayTag YiMen = new GameplayTag("Kingdom.YiMen");
            
        }

        //角色
        public static class Character
        {
            [Header("罗兰")]
            public static readonly GameplayTag Roland = new GameplayTag("Character.Roland");
            [Header("椿")]
            public static readonly GameplayTag Haru = new GameplayTag("Character.Haru");
        }

        //伤害特性
        public static class DamageType
        {
            [Header("神圣")]
            public static readonly GameplayTag Divine = new GameplayTag("DamageType.Divine");
            [Header("物理")]
            public static readonly GameplayTag Physical = new GameplayTag("DamageType.Physical");
        }

        //Buff — 类别 tag（Buff.AttackUp 等）仅作默认/查询；技能实例用 Buff.<类别>.<技能名> 以支持多来源叠加。
        public static class Buff
        {
            [Header("攻击力提高（类别）")]
            public static readonly GameplayTag AttackUp = new GameplayTag("Buff.AttackUp");
            [Header("防御力提高（类别）")]
            public static readonly GameplayTag DefenseUp = new GameplayTag("Buff.DefenseUp");
            [Header("敏捷提高（类别）")]
            public static readonly GameplayTag AgilityUp = new GameplayTag("Buff.AgilityUp");
            [Header("速度提高（类别）")]
            public static readonly GameplayTag SpeedUp = new GameplayTag("Buff.SpeedUp");
            [Header("祈福护佑（增益回合冻结）")]
            public static readonly GameplayTag BlessingWard = new GameplayTag("Buff.BlessingWard");
            [Header("霸体（免疫受击/眩晕表现）")]
            public static readonly GameplayTag HyperArmor = new GameplayTag("Buff.HyperArmor");

            public static readonly GameplayTag HyperArmor_RingWave = new GameplayTag("Buff.HyperArmor.RingWave");

            [Header("攻升 — 技能实例")]
            public static readonly GameplayTag AttackUp_DogBlessing = new GameplayTag("Buff.AttackUp.DogBlessing");
            public static readonly GameplayTag AttackUp_HolyLightI = new GameplayTag("Buff.AttackUp.HolyLightI");
            public static readonly GameplayTag AttackUp_SurfaciaBlessing = new GameplayTag("Buff.AttackUp.SurfaciaBlessing");

            [Header("防升 — 技能实例")]
            public static readonly GameplayTag DefenseUp_CraneBlessing = new GameplayTag("Buff.DefenseUp.CraneBlessing");
            public static readonly GameplayTag DefenseUp_HolyLightII = new GameplayTag("Buff.DefenseUp.HolyLightII");
            public static readonly GameplayTag DefenseUp_SurfaciaBlessing = new GameplayTag("Buff.DefenseUp.SurfaciaBlessing");

            [Header("敏捷 — 技能实例")]
            public static readonly GameplayTag AgilityUp_SurfaciaBlessing = new GameplayTag("Buff.AgilityUp.SurfaciaBlessing");
        }

        //Debuff
        public static class Debuff
        {
            [Header("眩晕")]
            public static readonly GameplayTag Stun = new GameplayTag("Debuff.Stun");
        }

        //领域
        public static class Zone
        {
            [Header("神圣领域")]
            public static readonly GameplayTag HolyField = new GameplayTag("Zone.HolyField");
            [Header("平面屏障")]
            public static readonly GameplayTag Barrier = new GameplayTag("Zone.Barrier");
        }

        //伊门众神 — 椿「x神赐福」类技能 abilityTags
        public static class God
        {
            public static class YiMenGod
            {
                [Header("狐神")]
                public static readonly GameplayTag Fox = new GameplayTag("God.YiMenGod.Fox");
                [Header("犬神")]
                public static readonly GameplayTag Dog = new GameplayTag("God.YiMenGod.Dog");
                [Header("鹤神")]
                public static readonly GameplayTag Crane = new GameplayTag("God.YiMenGod.Crane");
                [Header("蝶神")]
                public static readonly GameplayTag Butterfly = new GameplayTag("God.YiMenGod.Butterfly");
                [Header("鱼神")]
                public static readonly GameplayTag Fish = new GameplayTag("God.YiMenGod.Fish");
            }
        }
    }

    /// <summary>
    /// 扩展方法，操作一个Tag链表
    /// </summary>
    public static class GameplayTagExtensions
    {
        public static bool HasTag(this List<GameplayTag> list, GameplayTag tag)
        {
            int id = tag.Id;
            foreach (var t in list)
            {
                if (t.Id == id) return true;
            }
            return false;
        }

        public static void AddTag(this List<GameplayTag> list, GameplayTag tag)
        {
            if (!list.HasTag(tag))
                list.Add(tag);
        }

        public static void RemoveTag(this System.Collections.Generic.List<GameplayTag> list, GameplayTag tag)
        {
            for (int i = list.Count - 1; i >= 0; i--)
            {
                if (list[i].Matches(tag))
                    list.RemoveAt(i);
            }
        }
    }
