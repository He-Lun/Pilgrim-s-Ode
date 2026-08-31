using System.Collections.Generic;
using Mirror;
using UnityEngine;

/// <summary>
/// 战斗权威状态：Server 写 SyncVar / ClientRpc，Client 读 hook 刷新表现。
/// </summary>
[DisallowMultipleComponent]
public class NetworkBattleState : NetworkBehaviour
{
    public static NetworkBattleState Instance { get; private set; }

    /// <summary>不依赖静态 Instance，避免 OnStartClient 未触发时误判未就绪。</summary>
    public static bool IsNetworkReady =>
        BattleNetworkRuntimeSpawner.IsNetworkSpawned(BattleNetworkRuntimeSpawner.ResolveState());

    [SyncVar(hook = nameof(OnBattleStartedChanged))]
    public bool battleStarted;

    [SyncVar(hook = nameof(OnPhaseChanged))]
    public NetTurnPhase phase;

    [SyncVar(hook = nameof(OnCurrentActorSlotChanged))]
    public int currentActorSlot = -1;

    [SyncVar(hook = nameof(OnTeamApChanged))]
    public int team0ActionPoints;

    [SyncVar(hook = nameof(OnTeamApChanged))]
    public int team1ActionPoints;

    [SyncVar]
    public int stateSequence;

    readonly List<NetCharacterSnapshot> characterSnapshots = new List<NetCharacterSnapshot>();

    public IReadOnlyList<NetCharacterSnapshot> Characters => characterSnapshots;

    public event System.Action StateRefreshed;

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public override void OnStartServer()
    {
        Instance = this;
        CombatEventBus.Instance.OnEvent += HandleServerCombatEvent;
        Debug.Log($"[NetworkBattleState] Server spawn 完成 sceneId={netIdentity.sceneId}, netId={netIdentity.netId}");
    }

    public override void OnStopServer()
    {
        CombatEventBus.Instance.OnEvent -= HandleServerCombatEvent;
        if (Instance == this) Instance = null;
    }

    public override void OnStartClient()
    {
        if (!isServer)
            Instance = this;

        BattlePresentationSync.EnsureSubscribed();

        if (!isServer)
        {
            Debug.Log($"[NetworkBattleState] Client spawn 完成 netId={netIdentity.netId}, battleStarted={battleStarted}");
            NetworkBattleBootstrap.FindInstance()?.EnsureClientPresentationReady();
            if (battleStarted)
                NetworkBattleBootstrap.FindInstance()?.NotifyClientBattleStarted();
        }

        StateRefreshed?.Invoke();
    }

    public override void OnStopClient()
    {
        if (Instance == this) Instance = null;
    }

    [Server]
    public void MarkBattleStarted()
    {
        battleStarted = true;
        lastHandSignatures.Clear();
        lastTagSignatures.Clear();
        lastActionBarSignature = null;
        RefreshFromSimulation();
        RpcBattleStarted();
    }

    [Server]
    public void ResyncForConnection(NetworkConnectionToClient conn)
    {
        if (conn == null || !battleStarted)
            return;

        // 重连 Client 手牌与状态为空，清去重缓存强制全量重发。
        lastHandSignatures.Clear();
        lastTagSignatures.Clear();
        lastActionBarSignature = null;
        RefreshFromSimulation();
        TargetBattleStarted(conn);
    }

    [ClientRpc]
    void RpcBattleStarted()
    {
        if (isServer)
            return;

        NetworkBattleBootstrap.FindInstance()?.NotifyClientBattleStarted();
        StateRefreshed?.Invoke();
    }

    [TargetRpc]
    void TargetBattleStarted(NetworkConnectionToClient conn)
    {
        NetworkBattleBootstrap.FindInstance()?.NotifyClientBattleStarted();
        StateRefreshed?.Invoke();
    }

    [Server]
    public void RefreshFromSimulation()
    {
        if (!BattleNetworkGate.IsSimulationServer)
            return;

        var tm = TurnManager.Instance;
        if (tm == null)
            return;

        phase = NetTurnPhaseUtility.ToNet(tm.Phase);
        currentActorSlot = tm.CurrentActor != null
            ? NetworkBattleActor.GetSlotIndex(tm.CurrentActor)
            : -1;

        team0ActionPoints = ResolveTeamAp(0);
        team1ActionPoints = ResolveTeamAp(1);

        RebuildCharacterSnapshots(tm.AllActors);
        stateSequence++;
        RpcSyncCharacterSnapshots(stateSequence, characterSnapshots.ToArray());
        SyncHandsToClients();
        SyncTagsToClients();
        SyncActionBarToClients();
    }

    const float ActionBarSyncInterval = 0.1f;

    string lastActionBarSignature;
    float nextActionBarCheckTime;

    void LateUpdate()
    {
        if (!isServer || !battleStarted || Time.unscaledTime < nextActionBarCheckTime)
            return;

        nextActionBarCheckTime = Time.unscaledTime + ActionBarSyncInterval;
        SyncActionBarToClients();
    }

    /// <summary>行动条改动路径太多，按签名轮询整条队列，比在每个改动点埋同步省事。</summary>
    [Server]
    void SyncActionBarToClients()
    {
        var queue = ActionQueue.Instance;
        if (queue == null)
            return;

        var rows = queue.GetTimelineSnapshot();
        var timelineSlots = new int[rows.Count];
        var timelineAvs = new float[rows.Count];
        var signature = new System.Text.StringBuilder(rows.Count * 12);

        for (int i = 0; i < rows.Count; i++)
        {
            timelineSlots[i] = NetworkBattleActor.GetSlotIndex(rows[i].unit);
            timelineAvs[i] = rows[i].av;
            signature.Append(timelineSlots[i]).Append(':')
                     .Append(Mathf.RoundToInt(timelineAvs[i])).Append('|');
        }

        var confirmSlots = new List<int>();
        signature.Append('#');
        foreach (var pending in queue.EnumerateConfirmRowInserts())
        {
            int slot = NetworkBattleActor.GetSlotIndex(pending.actor);
            if (slot < 0)
                continue;

            confirmSlots.Add(slot);
            signature.Append(slot).Append('|');
        }

        string current = signature.ToString();
        if (lastActionBarSignature == current)
            return;

        lastActionBarSignature = current;
        RpcSyncActionBar(timelineSlots, timelineAvs, confirmSlots.ToArray());
    }

    [ClientRpc]
    void RpcSyncActionBar(int[] timelineSlots, float[] timelineAvs, int[] confirmSlots)
    {
        if (isServer)
            return;

        var queue = ActionQueue.Instance;
        if (queue == null)
            return;

        int count = timelineSlots != null ? timelineSlots.Length : 0;
        var units = new List<AbilitySystemComponent>(count);
        var avs = new List<float>(count);

        for (int i = 0; i < count; i++)
        {
            var asc = NetworkBattleActor.GetBySlot(timelineSlots[i]);
            if (asc == null)
                continue;

            units.Add(asc);
            avs.Add(timelineAvs != null && i < timelineAvs.Length ? timelineAvs[i] : 0f);
        }

        int confirmCount = confirmSlots != null ? confirmSlots.Length : 0;
        var confirmActors = new List<AbilitySystemComponent>(confirmCount);

        for (int i = 0; i < confirmCount; i++)
        {
            var asc = NetworkBattleActor.GetBySlot(confirmSlots[i]);
            if (asc != null)
                confirmActors.Add(asc);
        }

        queue.ApplyNetworkSnapshot(units, avs, confirmActors);
    }

    readonly Dictionary<int, string> lastTagSignatures = new Dictionary<int, string>();

    /// <summary>眩晕等状态以 Server 为准，避免 Client 本地推演解不掉。</summary>
    [Server]
    void SyncTagsToClients()
    {
        foreach (var pair in NetworkBattleActor.AllSlots)
        {
            var asc = pair.Value != null ? pair.Value.Asc : null;
            if (asc == null)
                continue;

            var tags = asc.AppliedTags;
            int count = tags != null ? tags.Count : 0;
            var names = new string[count];
            var signature = new System.Text.StringBuilder(count * 12);

            for (int i = 0; i < count; i++)
            {
                names[i] = tags[i].TagName ?? string.Empty;
                signature.Append(names[i]).Append('|');
            }

            string current = signature.ToString();
            if (lastTagSignatures.TryGetValue(pair.Key, out string previous) && previous == current)
                continue;

            lastTagSignatures[pair.Key] = current;
            RpcSyncTags(pair.Key, names);
        }
    }

    [ClientRpc]
    void RpcSyncTags(int slot, string[] tagNames)
    {
        if (isServer)
            return;

        NetworkBattleActor.GetBySlot(slot)?.ApplyNetworkTags(tagNames);
    }

    readonly Dictionary<int, string> lastHandSignatures = new Dictionary<int, string>();

    /// <summary>只把本队手牌发给对应连接，对手看不到底牌。</summary>
    [Server]
    void SyncHandsToClients()
    {
        foreach (var conn in NetworkServer.connections.Values)
        {
            if (conn == null || !conn.isReady || conn is LocalConnectionToClient)
                continue;

            int team = BattleNetworkGate.ResolveTeamForConnection(conn.connectionId);

            foreach (var pair in NetworkBattleActor.AllSlots)
            {
                var asc = pair.Value != null ? pair.Value.Asc : null;
                if (asc == null || asc.TeamId != team)
                    continue;

                SyncHandForActor(conn, pair.Key, asc);
            }
        }
    }

    [Server]
    void SyncHandForActor(NetworkConnectionToClient conn, int slot, AbilitySystemComponent asc)
    {
        var hand = asc.HandCards;
        if (hand == null)
            return;

        int count = hand.HandCount;
        var abilityIds = new int[count];
        var inspirationFlags = new byte[count];
        var signature = new System.Text.StringBuilder(count * 8);

        for (int i = 0; i < count; i++)
        {
            var slotData = hand.Hand[i];
            abilityIds[i] = NetAbilityRegistry.GetId(slotData.ability);
            inspirationFlags[i] = (byte)(slotData.isInspiration ? 1 : 0);
            signature.Append(abilityIds[i]).Append(inspirationFlags[i]).Append('|');
        }

        int key = conn.connectionId * 1000 + slot;
        string current = signature.ToString();
        if (lastHandSignatures.TryGetValue(key, out string previous) && previous == current)
            return;

        lastHandSignatures[key] = current;
        TargetSyncHand(conn, slot, abilityIds, inspirationFlags);
    }

    [TargetRpc]
    void TargetSyncHand(NetworkConnectionToClient conn, int slot, int[] abilityIds, byte[] inspirationFlags)
    {
        var asc = NetworkBattleActor.GetBySlot(slot);
        var hand = asc != null ? asc.HandCards : null;
        if (hand == null)
            return;

        int count = abilityIds != null ? abilityIds.Length : 0;
        var abilities = new List<GameplayAbility>(count);
        var flags = new List<bool>(count);

        for (int i = 0; i < count; i++)
        {
            var ability = NetAbilityRegistry.GetById(abilityIds[i]);
            if (ability == null)
            {
                Debug.LogWarning($"[NetworkBattleState] 未能解析技能 ID {abilityIds[i]}，手牌槽 {i} 将被跳过。");
                continue;
            }

            abilities.Add(ability);
            flags.Add(inspirationFlags != null && i < inspirationFlags.Length && inspirationFlags[i] != 0);
        }

        hand.ApplyNetworkHand(abilities, flags);
        FindObjectOfType<BattleHandViewBridge>()?.Resync();
    }

    [ClientRpc]
    void RpcSyncCharacterSnapshots(int sequence, NetCharacterSnapshot[] snapshots)
    {
        if (isServer)
            return;

        characterSnapshots.Clear();
        if (snapshots != null && snapshots.Length > 0)
            characterSnapshots.AddRange(snapshots);

        StateRefreshed?.Invoke();
    }

    void HandleServerCombatEvent(CombatEvent evt)
    {
        if (!isServer)
            return;

        BattleNetworkPresentation.ServerBroadcastCombatEvent(evt);
    }

    public void ServerSendMovePresentation(int slot, Vector3[] waypoints, float costMeters)
    {
        if (!isServer)
            return;

        RpcPlayMovePresentation(slot, waypoints, costMeters);
    }

    public void ServerSendCombatEvent(NetCombatEvent evt)
    {
        if (!isServer)
            return;

        RpcCombatPresentation(evt);
    }

    [ClientRpc]
    void RpcPlayMovePresentation(int slot, Vector3[] waypoints, float costMeters)
    {
        if (isServer)
            return;

        BattleNetworkPresentation.ClientPlayMove(slot, waypoints, costMeters);
    }

    [ClientRpc]
    void RpcCombatPresentation(NetCombatEvent evt)
    {
        if (isServer)
            return;

        BattleNetworkPresentation.ClientReplayCombatEvent(evt);
    }

    [Server]
    private void RebuildCharacterSnapshots(IReadOnlyList<AbilitySystemComponent> actors)
    {
        characterSnapshots.Clear();

        for (int i = 0; i < actors.Count; i++)
        {
            var asc = actors[i];
            if (asc == null) continue;

            int slot = NetworkBattleActor.GetSlotIndex(asc);
            if (slot < 0) slot = i;

            characterSnapshots.Add(NetCharacterSnapshot.FromActor(asc, slot));
        }
    }

    private static int ResolveTeamAp(int teamId)
    {
        var tm = TurnManager.Instance;
        if (tm == null) return 0;

        foreach (var actor in tm.AllActors)
        {
            if (actor == null || actor.TeamId != teamId) continue;
            return actor.TeamResource != null ? actor.TeamResource.CurrentActionPoints : 0;
        }

        return 0;
    }

    void OnBattleStartedChanged(bool oldValue, bool newValue)
    {
        if (!isServer && newValue && !oldValue)
            NetworkBattleBootstrap.FindInstance()?.NotifyClientBattleStarted();
        StateRefreshed?.Invoke();
    }

    void OnPhaseChanged(NetTurnPhase _, NetTurnPhase __) => StateRefreshed?.Invoke();
    void OnCurrentActorSlotChanged(int _, int __) => StateRefreshed?.Invoke();
    void OnTeamApChanged(int _, int __) => StateRefreshed?.Invoke();
}
