using UnityEngine;

public class AbilityState : ICharacterState
{
    public CharacterStateType Type => CharacterStateType.Ability;
    public int Priority => CharacterStatePriority.Get(Type);

    public void Enter(CharacterMotor ctx, CharacterStatePayload payload)
    {
        if (payload.ability == null) return;

        var presentation = ctx.Asc != null
            ? ctx.Asc.GetPresentation(payload.ability)
            : AbilityPresentationEntry.FromAbilityDefaults(payload.ability);

        ctx.AnimatorDriver?.PlaySkill(presentation);
        ctx.Facing?.FaceAbilityContext(payload.abilityContext, ctx.Asc);
    }

    public void Tick(CharacterMotor ctx, float dt) { }

    public void Exit(CharacterMotor ctx)
    {
        ctx.AnimatorDriver?.StopSkill();
    }

    public bool CanBeInterruptedBy(CharacterStateType other)
    {
        return other == CharacterStateType.Death || other == CharacterStateType.Hit;
    }
}
