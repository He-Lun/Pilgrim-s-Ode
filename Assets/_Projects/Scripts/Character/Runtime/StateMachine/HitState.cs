using UnityEngine;

public class HitState : ICharacterState
{
    public CharacterStateType Type => CharacterStateType.Hit;
    public int Priority => CharacterStatePriority.Get(Type);

    public void Enter(CharacterMotor ctx, CharacterStatePayload payload)
    {
        ctx.AnimatorDriver?.TriggerHit();
    }

    public void Tick(CharacterMotor ctx, float dt) { }

    public void Exit(CharacterMotor ctx) { }

    public bool CanBeInterruptedBy(CharacterStateType other) => other == CharacterStateType.Death;
}
