/// <summary>
/// 角色状态接口。
/// </summary>
public interface ICharacterState
{
    CharacterStateType Type { get; }
    int Priority { get; }

    void Enter(CharacterMotor ctx, CharacterStatePayload payload);
    void Tick(CharacterMotor ctx, float dt);
    void Exit(CharacterMotor ctx);
    bool CanBeInterruptedBy(CharacterStateType other);
}
