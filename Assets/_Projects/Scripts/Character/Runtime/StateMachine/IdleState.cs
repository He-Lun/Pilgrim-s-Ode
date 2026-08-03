using UnityEngine;

public class IdleState : ICharacterState
{
    public CharacterStateType Type => CharacterStateType.Idle;
    public int Priority => CharacterStatePriority.Get(Type);

    public void Enter(CharacterMotor ctx, CharacterStatePayload payload)
    {
        ctx.AnimatorDriver?.SetMoving(false);
        ctx.AnimatorDriver?.SetSpeed(0f);
        ctx.AnimatorDriver?.StopSkill();
    }

    public void Tick(CharacterMotor ctx, float dt) { }

    public void Exit(CharacterMotor ctx) { }

    public bool CanBeInterruptedBy(CharacterStateType other) => true;
}
