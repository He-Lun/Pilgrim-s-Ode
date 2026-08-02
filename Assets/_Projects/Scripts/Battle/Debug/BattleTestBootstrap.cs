using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 临时战斗测试入口 — 不依赖手牌，验证「回合开始 → 行动（移动）→ 回合结束」最小闭环。
/// 挂到场景中任意 GameObject，Play 后自动开战；空格结束当前回合。
/// </summary>
[DefaultExecutionOrder(-100)]
public class BattleTestBootstrap : MonoBehaviour
{
    [Header("参战角色（留空则自动收集场景中所有 AbilitySystemComponent）")]
    [SerializeField] private List<AbilitySystemComponent> actors = new List<AbilitySystemComponent>();

    [Header("开战配置")]
    [SerializeField] private int firstTeamId = 0;
    [SerializeField] private int firstPlayerAP = 4;
    [SerializeField] private int secondPlayerAP = 5;
    [SerializeField] private bool autoStartOnPlay = true;
    [SerializeField] private float startDelay = 0.5f;

    [Header("输入")]
    [SerializeField] private KeyCode endTurnKey = KeyCode.Space;
    [SerializeField] private KeyCode restartKey = KeyCode.R;

    [Header("调试")]
    [SerializeField] private bool snapActorsToNavMeshOnStart = true;
    [Header("表现")]
    [Tooltip("角色 Buff + 屏障/领域世界特效，共用同一 Catalog")]
    [SerializeField] private BuffPresentationCatalog buffPresentationCatalog;
    [Tooltip("所有角色 TeamId 相同时，自动拆成两个阵营以便测试回合流转")]
    [SerializeField] private bool autoSplitTeamsWhenSingleSide = true;

    private readonly Dictionary<int, TeamResourceManager> teamResources = new Dictionary<int, TeamResourceManager>();
    private bool battleStarted;

    void Awake()
    {
        // 必须在 CharacterMovementController.Start 之前，用编辑器里摆放的位置反算格子并贴地
        if (snapActorsToNavMeshOnStart)
            SnapActorsToNavMesh(CollectActors());
    }

    void Start()
    {
        if (autoStartOnPlay)
            Invoke(nameof(BeginTestBattle), startDelay);
    }

    void Update()
    {
        if (!battleStarted || TurnManager.Instance == null) return;

        if (Input.GetKeyDown(endTurnKey))
            TryEndTurn();

        if (Input.GetKeyDown(restartKey))
            BeginTestBattle();
    }

    [ContextMenu("Begin Test Battle")]
    public void BeginTestBattle()
    {
        CancelInvoke(nameof(BeginTestBattle));

        if (!EnsureBattleSystems())
            return;

        var roster = CollectActors();
        if (roster.Count == 0)
        {
            Debug.LogError("[BattleTestBootstrap] 场景中没有参战角色。请给角色挂 AbilitySystemComponent + CharacterMovementController + CharacterMotor。");
            return;
        }

        SetupTeamResources(roster);
        ApplyTestTeamSplit(roster);
        WireActors(roster);

        if (buffPresentationCatalog != null)
        {
            BattleBarrierManager.Instance.BindCatalog(buffPresentationCatalog);
            BattleZoneManager.Instance.BindCatalog(buffPresentationCatalog);
        }

        var turnManager = TurnManager.Instance;
        turnManager.OnTurnBegan -= HandleTurnBegan;
        turnManager.OnTurnEnded -= HandleTurnEnded;
        turnManager.OnBattleEnded -= HandleBattleEnded;
        turnManager.OnTurnBegan += HandleTurnBegan;
        turnManager.OnTurnEnded += HandleTurnEnded;
        turnManager.OnBattleEnded += HandleBattleEnded;

        turnManager.StartBattle(roster, firstTeamId, firstPlayerAP, secondPlayerAP);
        battleStarted = true;

        EnsureBattleCamera(roster);
        StartCoroutine(EnsureBattleUiNextFrames());

        Debug.Log($"[BattleTestBootstrap] 战斗开始，{roster.Count} 名角色。左键点地移动，{endTurnKey} 结束回合，{restartKey} 重开。滚轮缩放，右键平移，中键旋转视角。");
    }

    private System.Collections.IEnumerator EnsureBattleUiNextFrames()
    {
        yield return null;
        yield return null;
        BattleHealthBarBootstrap.EnsureAndSync();
    }

    private static void EnsureBattleCamera(List<AbilitySystemComponent> roster)
    {
        if (BattleCameraController.Instance == null)
        {
            var go = new GameObject("BattleCameraRig");
            go.AddComponent<BattleCameraController>();
            go.AddComponent<BattleCameraInput>();
            go.AddComponent<BattleCameraImpulsePlayer>();
        }
        else if (BattleCameraController.Instance.GetComponent<BattleCameraImpulsePlayer>() == null)
        {
            BattleCameraController.Instance.gameObject.AddComponent<BattleCameraImpulsePlayer>();
        }

        BattleCameraController.Instance?.FocusOnActors(roster);
    }

    private bool EnsureBattleSystems()
    {
        if (FindObjectOfType<BattleInputController>() == null)
            Debug.LogWarning("[BattleTestBootstrap] 场景中缺少 BattleInputController，将无法点击地面移动。");

        if (BattleSpaceSettings.Instance == null)
        {
            var host = GameObject.Find("BattleRuntime") ?? new GameObject("BattleRuntime");
            if (host.GetComponent<BattleSpaceSettings>() == null)
                host.AddComponent<BattleSpaceSettings>();
        }

        if (TurnManager.Instance == null || ActionQueue.Instance == null)
        {
            var existing = FindObjectOfType<TurnManager>();
            GameObject host = existing != null ? existing.gameObject : new GameObject("BattleRuntime");

            if (existing == null)
                host.AddComponent<TurnManager>();

            if (ActionQueue.Instance == null)
                host.AddComponent<ActionQueue>();
        }

        return TurnManager.Instance != null && ActionQueue.Instance != null;
    }

    private List<AbilitySystemComponent> CollectActors()
    {
        if (actors != null && actors.Count > 0)
            return actors.Where(a => a != null).Distinct().ToList();

        return FindObjectsOfType<AbilitySystemComponent>()
            .Where(a => a != null && a.gameObject.activeInHierarchy)
            .OrderBy(a => a.TeamId)
            .ThenBy(a => a.name)
            .ToList();
    }

    private void SetupTeamResources(List<AbilitySystemComponent> roster)
    {
        teamResources.Clear();

        var teamIds = roster.Select(a => a.TeamId).Distinct().OrderBy(id => id);
        foreach (int teamId in teamIds)
        {
            var existing = roster
                .Select(a => a.TeamResource)
                .FirstOrDefault(tr => tr != null && roster.Any(r => r.TeamId == teamId && r.TeamResource == tr));

            TeamResourceManager resource;
            if (existing != null)
            {
                resource = existing;
            }
            else
            {
                var go = new GameObject($"TeamResource_T{teamId}");
                go.transform.SetParent(transform);
                resource = go.AddComponent<TeamResourceManager>();
            }

            teamResources[teamId] = resource;
        }
    }

    private void ApplyTestTeamSplit(List<AbilitySystemComponent> roster)
    {
        if (!autoSplitTeamsWhenSingleSide || roster.Count < 2) return;

        var distinctTeams = roster.Select(a => a.TeamId).Distinct().ToList();
        if (distinctTeams.Count != 1) return;

        int teamA = distinctTeams[0];
        int teamB = teamA == 0 ? 1 : 0;

        if (!teamResources.ContainsKey(teamB))
        {
            var go = new GameObject($"TeamResource_T{teamB}");
            go.transform.SetParent(transform);
            teamResources[teamB] = go.AddComponent<TeamResourceManager>();
        }

        int split = roster.Count / 2;
        for (int i = split; i < roster.Count; i++)
        {
            var asc = roster[i];
            if (asc == null) continue;
            asc.Initialize(asc.Attributes ?? asc.GetComponent<AttributeSet>(), teamResources[teamB], teamB);
        }

        Debug.LogWarning($"[BattleTestBootstrap] 所有角色均为 Team {teamA}，已自动将后 {roster.Count - split} 名角色分配到 Team {teamB} 以便测试。");
    }

    private void WireActors(List<AbilitySystemComponent> roster)
    {
        foreach (var asc in roster)
        {
            if (asc == null) continue;

            if (!teamResources.TryGetValue(asc.TeamId, out var resource))
            {
                Debug.LogWarning($"[BattleTestBootstrap] 角色 {asc.name} 的 TeamId={asc.TeamId} 无对应 TeamResource，已跳过。");
                continue;
            }

            var attrs = asc.Attributes ?? asc.GetComponent<AttributeSet>();

            if (asc.CharacterData != null)
                asc.SetupFromCharacterData(asc.CharacterData, resource, asc.TeamId);
            else
                asc.Initialize(attrs, resource, asc.TeamId);

            if (asc.GetComponent<CharacterMovementController>() == null)
                Debug.LogWarning($"[BattleTestBootstrap] {asc.name} 缺少 CharacterMovementController，无法移动。");

            if (asc.GetComponent<CharacterMotor>() == null)
                Debug.LogWarning($"[BattleTestBootstrap] {asc.name} 缺少 CharacterMotor，无法播放移动。");

            if (asc.GetComponent<CharacterAnimationEvents>() == null)
            {
                var events = asc.gameObject.AddComponent<CharacterAnimationEvents>();
                Debug.LogWarning($"[BattleTestBootstrap] {asc.name} 缺少 CharacterAnimationEvents，已自动添加。");
            }

            if (asc.GetComponent<AbilityVfxPlayer>() == null)
            {
                var player = asc.gameObject.AddComponent<AbilityVfxPlayer>();
                if (buffPresentationCatalog != null)
                    player.BindCatalog(buffPresentationCatalog);
                Debug.LogWarning($"[BattleTestBootstrap] {asc.name} 缺少 AbilityVfxPlayer，已自动添加。");
            }
        }
    }

    private static void SnapActorsToNavMesh(List<AbilitySystemComponent> roster)
    {
        foreach (var asc in roster)
        {
            var movement = asc.GetComponent<CharacterMovementController>();
            if (movement == null) continue;

            movement.SnapToWorldPosition(asc.transform.position);
        }
    }

    private void TryEndTurn()
    {
        var tm = TurnManager.Instance;
        if (tm == null || tm.Phase != TurnPhase.TurnAction) return;

        var actor = tm.CurrentActor;
        if (actor != null)
        {
            var movement = actor.GetComponent<CharacterMovementController>();
            if (movement != null && movement.IsMoving)
            {
                Debug.Log("[BattleTestBootstrap] 角色仍在移动，请稍后再结束回合。");
                return;
            }
        }

        tm.EndCurrentTurn();
    }

    private void HandleTurnBegan(AbilitySystemComponent actor)
    {
        var movement = actor.GetComponent<CharacterMovementController>();
        Debug.Log($"[BattleTestBootstrap] 回合开始 → {actor.name} | AP={actor.TeamResource?.CurrentActionPoints} | 移动力={movement?.RemainingMoveMeters ?? 0f:F1}m");
    }

    private void HandleTurnEnded(AbilitySystemComponent actor)
    {
        Debug.Log($"[BattleTestBootstrap] 回合结束 → {actor.name}");
    }

    private void HandleBattleEnded(int winnerTeamId)
    {
        Debug.Log($"[BattleTestBootstrap] 战斗结束，获胜阵营: {winnerTeamId}");
    }

    void OnDestroy()
    {
        if (TurnManager.Instance == null) return;
        TurnManager.Instance.OnTurnBegan -= HandleTurnBegan;
        TurnManager.Instance.OnTurnEnded -= HandleTurnEnded;
        TurnManager.Instance.OnBattleEnded -= HandleBattleEnded;
    }
}
