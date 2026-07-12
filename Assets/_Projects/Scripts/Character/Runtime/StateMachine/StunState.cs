/// <summary>
/// 眩晕状态 — 持有 Debuff.Stun 期间锁定，不可被 Move/Ability 打断。
/// Hit / Knockback / Death 可短暂打断；收招后若标签仍在则回到 Stun。
/// </summary>
public class StunState : ICharacterState
{
    public CharacterStateType Type => CharacterStateType.Stun;
    public int Priority => CharacterStatePriority.Get(Type);

    public void Enter(CharacterMotor ctx, CharacterStatePayload payload)
    {
        ctx.NotifyMovementInterrupted();
        ctx.AnimatorDriver?.SetMoving(false);
        ctx.AnimatorDriver?.SetSpeed(0f);
        ctx.AnimatorDriver?.StopSkill();
        ctx.AnimatorDriver?.SetStunned(true);
    }

    public void Tick(CharacterMotor ctx, float dt) { }

    public void Exit(CharacterMotor ctx)
    {
        ctx.AnimatorDriver?.SetStunned(false);
    }

    public bool CanBeInterruptedBy(CharacterStateType other) =>
        other == CharacterStateType.Death
        || other == CharacterStateType.Knockback
        || other == CharacterStateType.Hit;
}
