using Mirror;
using UnityEngine;

/// <summary>
/// 客户端输入 → Server Command → 战斗逻辑。
/// </summary>
[DisallowMultipleComponent]
public class NetworkBattleController : NetworkBehaviour
{
    public static NetworkBattleController Instance { get; private set; }

    void Awake()
    {
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public override void OnStartServer()
    {
        Instance = this;
        AssignTeamsToClients();
    }

    public override void OnStopServer()
    {
        if (Instance == this) Instance = null;
    }

    public override void OnStartClient()
    {
        if (!isServer)
            Instance = this;

        if (!isServer)
            CmdRequestLocalTeam();
    }

    public override void OnStopClient()
    {
        if (Instance == this) Instance = null;
    }

    [Command(requiresAuthority = false)]
    void CmdRequestLocalTeam(NetworkConnectionToClient sender = null)
    {
        if (sender == null) return;
        int team = NetworkBattleTeamRegistry.GetTeam(sender.connectionId);
        if (team < 0) return;
        TargetSetLocalTeam(sender, team);
    }

    [Server]
    public void AssignTeamsToClients()
    {
        foreach (var kv in NetworkServer.connections)
        {
            var conn = kv.Value;
            if (conn == null) continue;
            int team = NetworkBattleTeamRegistry.GetTeam(conn.connectionId);
            if (team < 0) continue;
            TargetSetLocalTeam(conn, team);
        }
    }

    [TargetRpc]
    void TargetSetLocalTeam(NetworkConnectionToClient target, int teamId)
    {
        BattleNetworkGate.SetLocalTeamId(teamId);
        Debug.Log($"[NetworkBattleController] 本地 TeamId = {teamId}");
        if (!NetworkServer.active)
            BattleHealthBarBootstrap.EnsureAndSync();
    }

    public void RequestLocalTeamAssignment()
    {
        if (!NetworkClient.active || NetworkServer.active)
            return;
        if (BattleNetworkGate.LocalTeamId >= 0)
            return;
        CmdRequestLocalTeam();
    }

    public void RequestPresentationResync()
    {
        if (!NetworkClient.active || NetworkServer.active)
            return;

        CmdRequestPresentationResync();
    }

    [Command(requiresAuthority = false)]
    void CmdRequestPresentationResync(NetworkConnectionToClient sender = null)
    {
        if (sender == null)
            return;

        BattleNetworkRuntimeSpawner.ResolveState()?.ResyncForConnection(sender);
    }

    public static void RequestEndTurnFromInput()
    {
        var controller = Instance ?? BattleNetworkRuntimeSpawner.ResolveController();
        if (controller != null)
        {
            controller.RequestEndTurn();
            return;
        }

        if (NetworkServer.active)
        {
            TryEndTurnLocal();
            BattleNetworkRuntimeSpawner.ResolveState()?.RefreshFromSimulation();
            return;
        }

        if (BattleNetworkGate.IsNetworkBattleActive)
            return;

        TryEndTurnLocal();
    }

    public void RequestEndTurn()
    {
        if (!BattleNetworkGate.IsNetworkBattleActive)
        {
            TryEndTurnLocal();
            return;
        }

        if (NetworkServer.active)
        {
            TryExecuteEndTurn(ResolveSenderConnection());
            return;
        }

        if (!NetworkClient.active)
            return;

        CmdEndTurn();
    }

    public void RequestMove(int actorSlot, Vector3 targetWorldPoint)
    {
        if (!BattleNetworkGate.IsNetworkBattleActive)
        {
            TryMoveLocal(actorSlot, targetWorldPoint);
            return;
        }

        if (NetworkServer.active)
        {
            TryExecuteMove(actorSlot, targetWorldPoint, ResolveSenderConnection());
            return;
        }

        if (!NetworkClient.active)
            return;

        CmdMove(actorSlot, targetWorldPoint);
    }

    public void RequestPlayCard(int actorSlot, NetAbilityContext context)
    {
        if (!BattleNetworkGate.IsNetworkBattleActive)
        {
            TryPlayCardLocal(actorSlot, context);
            return;
        }

        if (NetworkServer.active)
        {
            TryExecutePlayCard(actorSlot, context, ResolveSenderConnection());
            return;
        }

        if (!NetworkClient.active)
            return;

        CmdPlayCard(actorSlot, context);
    }

    [Command(requiresAuthority = false)]
    void CmdEndTurn(NetworkConnectionToClient sender = null)
    {
        TryExecuteEndTurn(sender);
    }

    [Command(requiresAuthority = false)]
    void CmdMove(int actorSlot, Vector3 targetWorldPoint, NetworkConnectionToClient sender = null)
    {
        TryExecuteMove(actorSlot, targetWorldPoint, sender);
    }

    [Command(requiresAuthority = false)]
    void CmdPlayCard(int actorSlot, NetAbilityContext context, NetworkConnectionToClient sender = null)
    {
        TryExecutePlayCard(actorSlot, context, sender);
    }

    [Server]
    private bool ValidateSenderCanControlCurrentActor(NetworkConnectionToClient sender, out AbilitySystemComponent actor)
    {
        actor = null;
        var tm = TurnManager.Instance;
        if (tm == null || !BattleNetworkGate.IsBattleSimulationLive())
            return false;
        if (tm.Phase != TurnPhase.TurnAction)
            return false;

        actor = tm.CurrentActor;
        if (actor == null)
            return false;

        return ValidateSenderTeam(sender, actor.TeamId);
    }

    [Server]
    private bool ValidateSenderCanControlActor(NetworkConnectionToClient sender, int actorSlot, out AbilitySystemComponent actor)
    {
        actor = NetworkBattleActor.GetBySlot(actorSlot);
        var tm = TurnManager.Instance;
        if (actor == null || tm == null || !BattleNetworkGate.IsBattleSimulationLive())
            return false;
        if (tm.Phase != TurnPhase.TurnAction)
            return false;
        if (tm.CurrentActor != actor)
            return false;

        return ValidateSenderTeam(sender, actor.TeamId);
    }

    [Server]
    private static bool ValidateSenderTeam(NetworkConnectionToClient sender, int actorTeamId)
    {
        if (BattleNetworkGate.IsSoloHostTest)
            return true;

        if (sender == null) return false;
        int senderTeam = BattleNetworkGate.ResolveTeamForConnection(sender.connectionId);
        return senderTeam >= 0 && senderTeam == actorTeamId;
    }

    [Server]
    private void TryExecuteEndTurn(NetworkConnectionToClient sender)
    {
        if (!ValidateSenderCanControlCurrentActor(sender, out _))
            return;

        if (!TryEndTurnLocal())
            return;

        NetworkBattleState.Instance?.RefreshFromSimulation();
    }

    [Server]
    private void TryExecuteMove(int actorSlot, Vector3 targetWorldPoint, NetworkConnectionToClient sender)
    {
        if (!ValidateSenderCanControlActor(sender, actorSlot, out _))
            return;

        if (!TryMoveLocal(actorSlot, targetWorldPoint))
            return;

        NetworkBattleState.Instance?.RefreshFromSimulation();
    }

    [Server]
    private void TryExecutePlayCard(int actorSlot, NetAbilityContext context, NetworkConnectionToClient sender)
    {
        if (!ValidateSenderCanControlActor(sender, actorSlot, out _))
            return;

        if (!TryPlayCardLocal(actorSlot, context))
            return;

        NetworkBattleState.Instance?.RefreshFromSimulation();
    }

    private static NetworkConnectionToClient ResolveSenderConnection()
    {
        return NetworkServer.localConnection as NetworkConnectionToClient;
    }

    private static bool TryEndTurnLocal()
    {
        if (!BattleNetworkGate.IsSimulationServer)
            return false;

        var tm = TurnManager.Instance;
        if (tm == null || tm.Phase != TurnPhase.TurnAction)
            return false;

        var actor = tm.CurrentActor;
        if (actor != null)
        {
            var movement = actor.GetComponent<CharacterMovementController>();
            if (movement != null && movement.IsMoving)
                return false;
        }

        tm.EndCurrentTurn();
        return true;
    }

    private static bool TryMoveLocal(int actorSlot, Vector3 targetWorldPoint)
    {
        if (!BattleNetworkGate.IsSimulationServer)
            return false;

        var actor = NetworkBattleActor.GetBySlot(actorSlot);
        if (actor == null)
        {
            actor = ResolveActorWhenSlotMissing(actorSlot);
            if (actor == null) return false;
        }

        if (actor.IsChanneling)
            actor.InterruptRitualIfAny();

        var movement = actor.GetComponent<CharacterMovementController>();
        if (movement == null || movement.IsMoving)
            return false;

        var result = movement.TryMoveToWorldPoint(targetWorldPoint);
        return result == MoveResult.Success;
    }

    private static bool TryPlayCardLocal(int actorSlot, NetAbilityContext context)
    {
        if (!BattleNetworkGate.IsSimulationServer)
            return false;

        var actor = NetworkBattleActor.GetBySlot(actorSlot);
        if (actor == null)
        {
            actor = ResolveActorWhenSlotMissing(actorSlot);
            if (actor == null) return false;
        }

        if (actor.HandCards == null)
            return false;

        var activation = context.ToActivationContext();
        var play = actor.HandCards.PreparePlay(context.handIndex, activation);
        if (!play.isValid)
            return false;

        return BattleCardFacade.TryPlay(actor, play) == AbilityActivationResult.Success;
    }

    /// <summary>slot 未注册时回退到 CurrentActor（Host 刚开战时的兜底）。</summary>
    private static AbilitySystemComponent ResolveActorWhenSlotMissing(int actorSlot)
    {
        var tm = TurnManager.Instance;
        if (tm?.CurrentActor == null) return null;

        int currentSlot = NetworkBattleActor.GetSlotIndex(tm.CurrentActor);
        if (currentSlot >= 0 && currentSlot != actorSlot)
            return null;

        return tm.CurrentActor;
    }
}
