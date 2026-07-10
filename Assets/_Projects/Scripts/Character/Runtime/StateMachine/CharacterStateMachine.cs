using System.Collections.Generic;

/// <summary>
/// 轻量角色状态机，支持优先级打断。
/// </summary>
public class CharacterStateMachine
{
    private readonly Dictionary<CharacterStateType, ICharacterState> states = new Dictionary<CharacterStateType, ICharacterState>();
    private CharacterMotor owner;
    private ICharacterState current;
    private CharacterStateType currentType = CharacterStateType.Idle;
    private CharacterStateType previousType = CharacterStateType.Idle;

    public CharacterStateType CurrentType => currentType;
    public CharacterStateType PreviousType => previousType;
    public ICharacterState Current => current;

    public void Initialize(CharacterMotor motor, IEnumerable<ICharacterState> stateList)
    {
        owner = motor;
        states.Clear();
        foreach (var state in stateList)
            states[state.Type] = state;
    }

    /// <summary>当前状态是否允许切到目标（不改变状态）。</summary>
    public bool CanTransitionTo(CharacterStateType type)
    {
        if (owner == null || !states.ContainsKey(type)) return false;
        if (current == null) return true;

        int nextPriority = CharacterStatePriority.Get(type);
        int currentPriority = CharacterStatePriority.Get(currentType);
        if (nextPriority < currentPriority && !current.CanBeInterruptedBy(type))
            return false;

        return true;
    }

    public bool TryTransition(CharacterStateType type, CharacterStatePayload payload, bool force = false)
    {
        if (owner == null || !states.TryGetValue(type, out var next)) return false;

        if (!force && !CanTransitionTo(type))
            return false;

        current?.Exit(owner);
        previousType = currentType;
        currentType = type;
        current = next;
        current.Enter(owner, payload);
        return true;
    }

    public void Tick(float dt)
    {
        current?.Tick(owner, dt);
    }
}
