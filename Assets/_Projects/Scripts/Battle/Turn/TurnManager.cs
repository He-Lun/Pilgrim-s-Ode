using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 回合阶段（星铁式：无全局回合，只有角色回合）。
/// </summary>
public enum TurnPhase
{
    BattleStart,
    TurnStart,    // 角色回合开始
    TurnAction,   // 回合行动中（等待玩家）
    TurnSettle,   // 回合结算
    BattleEnd
}

/// <summary>
/// 回合管理器 — 驱动整场战斗的状态机 / 编排者。
///   · 向行动条要“下一个”：插入栈优先（角色行动），否则时间轴（角色回合）；
///   · 回合流程：+1行动点 → 抽满 → 刷新移动 → 广播 TurnStarted → 等玩家 → TickModifiers → 广播 TurnEnded → 重排/移除；
///   · 插入行动（自爆/追击）深度优先排空，只跑技能、不触发回合事件；
///   · 激励完成入口：+3行动点、行动提前100%、授予激励卡（不进插入栈）。
/// 行动条只做排序增删，行动点/抽牌/事件广播都收归本类。
/// </summary>
public class TurnManager : MonoBehaviour
{
    public static TurnManager Instance { get; private set; }

    [Header("========== 引用 ==========")]
    [SerializeField] private ActionQueue actionQueue;

    [Header("========== 配置 ==========")]
    [Tooltip("手牌上限（抽满时用）")]
    [SerializeField] private int handLimit = 5;

    private readonly List<AbilitySystemComponent> allActors = new List<AbilitySystemComponent>();

    public TurnPhase Phase { get; private set; }
    public AbilitySystemComponent CurrentActor { get; private set; }

    // ---------- 事件 ----------
    public System.Action<TurnPhase> OnPhaseChanged;
    public System.Action<AbilitySystemComponent> OnTurnBegan;
    public System.Action<AbilitySystemComponent> OnTurnEnded;
    public System.Action<int> OnBattleEnded;   // 参数为获胜阵营 ID（-1 表示平局）

    private bool battleActive;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ==================================================================
    //  生命周期
    // ==================================================================

    /// <summary>
    /// 开始战斗。firstTeamId 为先手阵营（先手初始行动点较少）。
    /// </summary>
    public void StartBattle(
        List<AbilitySystemComponent> actors,
        int firstTeamId,
        int firstPlayerAP = 4,
        int secondPlayerAP = 5)
    {
        if (actors == null || actors.Count == 0) return;

        actionQueue = actionQueue != null ? actionQueue : ActionQueue.Instance;
        if (actionQueue == null)
        {
            Debug.LogError("[TurnManager] 缺少 ActionQueue，无法开始战斗。");
            return;
        }

        allActors.Clear();
        allActors.AddRange(actors);

        // 行动条入场（召唤物等 ParticipatesInActionQueue=false 会被跳过）
        actionQueue.Clear();
        foreach (var actor in allActors)
        {
            if (actor != null && actor.ParticipatesInActionQueue)
                actionQueue.Register(actor);
        }

        // 初始化各阵营行动点（先手 4 / 后手 5）
        var inited = new HashSet<TeamResourceManager>();
        foreach (var actor in allActors)
        {
            if (actor == null) continue;
            var tr = actor.TeamResource;
            if (tr == null || inited.Contains(tr)) continue;

            int start = actor.TeamId == firstTeamId ? firstPlayerAP : secondPlayerAP;
            tr.Initialize(tr.MaxActionPoints, start);
            inited.Add(tr);
        }

        // TODO: HandCardManager — 各角色开局首抽至手牌上限

        BattleZoneManager.Instance.ClearAll();
        BattleBarrierManager.Instance.ClearAll();
        BattleDestructiblePropManager.Instance.ClearAll();
        ElectricRingManager.Instance.ClearAll();

        battleActive = true;
        SetPhase(TurnPhase.BattleStart);
        AdvanceToNext();
    }

    /// <summary>玩家主动结束当前回合（可保留行动点、可只移动不出牌，都走这里）。</summary>
    public void EndCurrentTurn()
    {
        if (!battleActive || Phase != TurnPhase.TurnAction) return;
        EnterTurnSettle();
    }

    /// <summary>
    /// 出牌/移动等一次“动作”结算后由出牌流程回调，用于把期间产生的插入行动（如被击杀自爆）排空。
    /// </summary>
    public void NotifyActionResolved()
    {
        if (!battleActive) return;
        DrainInserts();
    }

    // ==================================================================
    //  激励完成入口（+AP / 行动提前 / 授予激励卡；不进插入栈）
    // ==================================================================
    public void OnInspirationCompleted(
        AbilitySystemComponent asc,
        GameplayAbility inspiration,
        InspirationTaskSO task = null)
    {
        if (asc == null) return;

        int apReward = task != null ? task.actionPointReward : 3;
        float priorityBoost = task != null ? task.actionPriorityBoost : 1f;

        asc.TeamResource?.AddActionPoints(apReward);

        if (actionQueue != null)
            actionQueue.AdvanceForward(asc, priorityBoost);

        // TODO: HandCardManager — 授予激励卡（计入上限，满手则失去；可叠加；打出即消耗移除）
        // asc.HandCards?.GrantInspirationCard(inspiration);
    }

    // ==================================================================
    //  插入行动对外入口（追击/反击等可用）
    // ==================================================================

    public void PushInsert(PendingAction action)
    {
        actionQueue?.PushInsert(action);
    }

    public void PushInsertBatch(List<PendingAction> batch)
    {
        actionQueue?.PushInsertBatch(batch);
    }

    // ==================================================================
    //  状态机核心
    // ==================================================================

    /// <summary>推进到下一个：先排空插入行动，再从时间轴取下一个角色回合。</summary>
    private void AdvanceToNext()
    {
        if (!battleActive) return;

        DrainInserts();

        if (CheckBattleEnd(out int winner))
        {
            EnterBattleEnd(winner);
            return;
        }

        AbilitySystemComponent actor;
        while (true)
        {
            actor = actionQueue.PopTimeline();
            if (actor == null) return; // 无人可行动（理论上已判胜负）

            if (actor.Attributes != null && actor.Attributes.IsDead())
            {
                actionQueue.Unregister(actor);
                continue;
            }
            break;
        }

        EnterTurnStart(actor);
    }

    private void EnterTurnStart(AbilitySystemComponent actor)
    {
        CurrentActor = actor;
        SetPhase(TurnPhase.TurnStart);

        // 眩晕：跳过本回合行动，直接结算（Tick 状态持续）
        if (actor != null && actor.HasTag(GameplayTag.Debuff.Stun))
        {
            RaiseTurnEvent(CombatEventType.TurnStarted, actor);
            OnTurnBegan?.Invoke(actor);
            EnterTurnSettle();
            return;
        }

        // 行动点 +1
        actor.TeamResource?.OnTurnStart(1);

        // 刷新移动力
        actor.GetComponent<CharacterMovementController>()?.RefreshMoveBudget();

        // TODO: HandCardManager — 抽牌至手牌上限 handLimit

        RaiseTurnEvent(CombatEventType.TurnStarted, actor);
        OnTurnBegan?.Invoke(actor);

        // 进入等待玩家操作
        SetPhase(TurnPhase.TurnAction);
    }

    private void EnterTurnSettle()
    {
        var actor = CurrentActor;
        SetPhase(TurnPhase.TurnSettle);

        // 结束前先把残留插入行动排空
        DrainInserts();

        // Buff 按“该角色回合”结算；有祈福护佑则冻结增益持续
        bool pauseBuffs = actor != null && actor.HasTag(GameplayTag.Buff.BlessingWard);
        actor?.Attributes?.TickModifiers(1, pauseBuffs);

        // 施法者回合结束：仪式持续倒数
        actor?.RitualTracker?.OnCasterTurnEnded();

        RaiseTurnEvent(CombatEventType.TurnEnded, actor);
        OnTurnEnded?.Invoke(actor);

        // 结算可能触发的插入行动
        DrainInserts();

        // 重排回条上 / 阵亡则移除
        if (actor != null)
        {
            if (actor.Attributes != null && actor.Attributes.IsDead())
                actionQueue.Unregister(actor);
            else
                actionQueue.Reinsert(actor);
        }

        CurrentActor = null;
        AdvanceToNext();
    }

    private void EnterBattleEnd(int winnerTeamId)
    {
        battleActive = false;
        CurrentActor = null;
        BattleZoneManager.Instance.ClearAll();
        BattleBarrierManager.Instance.ClearAll();
        BattleDestructiblePropManager.Instance.ClearAll();
        ElectricRingManager.Instance.ClearAll();
        SetPhase(TurnPhase.BattleEnd);

        OnBattleEnded?.Invoke(winnerTeamId);
        Debug.Log($"[TurnManager] 战斗结束，获胜阵营: {winnerTeamId}");
    }

    // ==================================================================
    //  插入行动排空（深度优先，只跑技能、不触发回合事件、不消耗行动点）
    // ==================================================================
    private void DrainInserts()
    {
        if (actionQueue == null) return;

        int guard = 0;
        const int maxIter = 256; // 防连锁死循环
        while (actionQueue.HasInsert && guard++ < maxIter)
        {
            var pending = actionQueue.PopInsert();
            if (pending.actor == null || pending.ability == null) continue;

            // 作为“行动”结算：绕过行动点，执行效果并广播 AbilityUsed；
            // 期间新触发的插入（如连锁死亡）会压到栈顶，天然深度优先。
            pending.ability.TryActivateAsInspiration(pending.actor, pending.context);
        }

        if (guard >= maxIter)
            Debug.LogWarning("[TurnManager] 插入行动排空达到上限，可能存在连锁死循环。");
    }

    /// <summary>取角色的死亡触发技能（自爆）。</summary>
    private GameplayAbility ResolveOnDeathAbility(AbilitySystemComponent asc)
    {
        // TODO: 自爆技能来源未定 —— 后续在 CharacterDataSO / ASC 增加 onDeathAbility 字段，
        //       或从 innateAbilities 中筛选带“OnDeath”触发标签的技能，这里返回它。
        return null;
    }

    // ==================================================================
    //  胜负判定
    // ==================================================================
    private bool CheckBattleEnd(out int winnerTeamId)
    {
        winnerTeamId = -1;

        var allTeams = new HashSet<int>();
        var teamsWithLiving = new HashSet<int>();

        foreach (var a in allActors)
        {
            if (a == null) continue;
            allTeams.Add(a.TeamId);

            if (a.Attributes == null || !a.Attributes.IsDead())
                teamsWithLiving.Add(a.TeamId);
        }

        // 全员阵亡 → 平局结束
        if (teamsWithLiving.Count == 0)
            return true;

        // 仅一个阵营参战（常见于本地测试）→ 不判胜负，战斗继续
        if (allTeams.Count <= 1)
            return false;

        // 多个阵营中只剩一方有人存活 → 该方获胜
        if (teamsWithLiving.Count == 1)
        {
            foreach (var t in teamsWithLiving)
                winnerTeamId = t;
            return true;
        }

        return false;
    }

    // ==================================================================
    //  辅助
    // ==================================================================
    private void SetPhase(TurnPhase phase)
    {
        Phase = phase;
        OnPhaseChanged?.Invoke(phase);
    }

    private void RaiseTurnEvent(CombatEventType type, AbilitySystemComponent actor)
    {
        CombatEventBus.Instance.Raise(new CombatEvent
        {
            type = type,
            instigator = actor,
            target = actor
        });
    }
}
