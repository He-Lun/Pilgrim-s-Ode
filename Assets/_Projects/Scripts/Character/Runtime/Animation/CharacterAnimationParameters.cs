/// <summary>
/// Animator 参数名约定 — 与 CharacterAnimatorDriver 保持一致。
/// 将 BattleCharacter.controller 挂到角色 Animator 上即可对接 FSM。
/// </summary>
public static class CharacterAnimationParameters
{
    public const string Speed = "Speed";
    public const string IsMoving = "IsMoving";
    public const string Hit = "Hit";
    public const string HitRecover = "HitRecover";
    public const string Death = "Death";
    public const string SkillIndex = "SkillIndex";
}
