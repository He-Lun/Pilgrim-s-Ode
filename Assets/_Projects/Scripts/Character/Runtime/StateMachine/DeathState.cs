using UnityEngine;

public class DeathState : ICharacterState
{
    public CharacterStateType Type => CharacterStateType.Death;
    public int Priority => CharacterStatePriority.Get(Type);

    public void Enter(CharacterMotor ctx, CharacterStatePayload payload)
    {
        ctx.AnimatorDriver?.TriggerDeath();
        ctx.IsDead = true;
    }

    public void Tick(CharacterMotor ctx, float dt) { }

    public void Exit(CharacterMotor ctx) { }

    public bool CanBeInterruptedBy(CharacterStateType other) => false;
}
