using Mirror;
using UnityEngine;

/// <summary>
/// Client 读 NetworkBattleState，刷新 TurnManager 镜像与角色表现。
/// </summary>
[DisallowMultipleComponent]
public class BattlePresentationSync : MonoBehaviour
{
    [SerializeField] private bool snapPositionOnSync = true;

    private static BattlePresentationSync activeInstance;
    private static bool subscribed;

    void Awake()
    {
        activeInstance = this;
    }

    void OnDestroy()
    {
        if (activeInstance == this)
            activeInstance = null;
    }

    void Update()
    {
        EnsureSubscribed();
    }

    void OnDisable()
    {
        Unsubscribe();
    }

    public static void EnsureSubscribed()
    {
        if (activeInstance == null)
            activeInstance = Object.FindObjectOfType<BattlePresentationSync>(true);

        var state = BattleNetworkRuntimeSpawner.ResolveState();
        if (state == null || subscribed)
            return;

        state.StateRefreshed += HandleStateRefreshed;
        subscribed = true;
        HandleStateRefreshed();
    }

    public static void ResetSubscription()
    {
        Unsubscribe();
        activeInstance = null;
    }

    private static void Unsubscribe()
    {
        if (!subscribed)
            return;

        var state = BattleNetworkRuntimeSpawner.ResolveState() ?? NetworkBattleState.Instance;
        if (state != null)
            state.StateRefreshed -= HandleStateRefreshed;

        subscribed = false;
    }

    private static void HandleStateRefreshed()
    {
        if (activeInstance == null)
            activeInstance = Object.FindObjectOfType<BattlePresentationSync>(true);

        activeInstance?.ApplyLatestState();
    }

    private void ApplyLatestState()
    {
        var state = BattleNetworkRuntimeSpawner.ResolveState();
        if (state == null)
            return;

        if (BattleNetworkGate.IsSimulationServer && NetworkClient.active)
        {
            RefreshTeamStatusUi();
            return;
        }

        if (BattleNetworkGate.IsSimulationServer && !NetworkClient.active)
            return;

        if (!state.battleStarted && !BattleNetworkGate.IsBattleSimulationLive())
            return;

        var tm = TurnManager.Instance;
        if (tm != null)
        {
            var actor = NetworkBattleActor.GetBySlot(state.currentActorSlot);
            var localPhase = NetTurnPhaseUtility.ToLocal(state.phase);
            tm.ApplyNetworkPresentationState(localPhase, actor);
        }

        ApplyCharacterSnapshots(state);
        ApplyTeamActionPoints(state);
        RefreshClientBattleUi();
        Object.FindObjectOfType<BattleHandViewBridge>()?.Resync();
    }

    private static void RefreshClientBattleUi()
    {
        if (!NetworkClient.active || NetworkServer.active)
            return;

        BattleHealthBarBootstrap.EnsureAndSync();
        RefreshTeamStatusUi();
    }

    private static void ApplyTeamActionPoints(NetworkBattleState state)
    {
        ApplyTeamApForTeam(0, state.team0ActionPoints);
        ApplyTeamApForTeam(1, state.team1ActionPoints);
    }

    private static void ApplyTeamApForTeam(int teamId, int actionPoints)
    {
        foreach (var marker in NetworkBattleActor.AllSlots.Values)
        {
            var asc = marker?.Asc;
            if (asc == null || asc.TeamId != teamId) continue;
            asc.TeamResource?.ApplyNetworkPresentation(actionPoints);
            break;
        }
    }

    private void ApplyCharacterSnapshots(NetworkBattleState state)
    {
        var characters = state.Characters;
        for (int i = 0; i < characters.Count; i++)
        {
            var snap = characters[i];
            var asc = NetworkBattleActor.GetBySlot(snap.slotIndex);
            if (asc == null) continue;

            // 位移动画进行中不要硬设 position，否则角色会变成瞬间平移。
            if (snapPositionOnSync && !IsPlayingLocomotion(asc))
                asc.transform.position = snap.WorldPosition;

            var attrs = asc.Attributes;
            if (attrs != null)
                attrs.ApplyNetworkHealthPresentation(snap.currentHealth);

            // 位移过程中别覆盖：PlayNetworkMove 已经本地扣过，
            // 此时快照带的还是扣减前的值，回写会让预览多出一段。
            if (!IsPlayingLocomotion(asc))
            {
                asc.GetComponent<CharacterMovementController>()
                   ?.ApplyNetworkMoveBudget(snap.remainingMoveMeters);
            }
        }
    }

    private static bool IsPlayingLocomotion(AbilitySystemComponent asc)
    {
        var motor = asc.GetComponent<CharacterMotor>();
        if (motor != null && motor.IsMoving)
            return true;

        var movement = asc.GetComponent<CharacterMovementController>();
        return movement != null && movement.IsMoving;
    }

    private static void RefreshTeamStatusUi()
    {
        var panel = Object.FindObjectOfType<BattleTeamStatusPanel>();
        panel?.SyncFromBattle();
    }
}
