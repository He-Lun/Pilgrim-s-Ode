/// <summary>
/// 角色表现/逻辑状态类型。
/// </summary>
public enum CharacterStateType
{
    Idle = 0,
    Move = 1,
    Ability = 3,
    Hit = 4,
    Death = 5,
    Knockback = 6,
    Stun = 7,
    DashCharge = 8,
    /// <summary>重力拉取等 — 位移中保持眩晕表现。</summary>
    Pull = 9
}

public static class CharacterStatePriority
{
    public static int Get(CharacterStateType type)
    {
        return type switch
        {
            CharacterStateType.Death => 100,
            CharacterStateType.Knockback => 90,
            CharacterStateType.Pull => 90,
            CharacterStateType.Hit => 80,
            CharacterStateType.Stun => 70,
            CharacterStateType.DashCharge => 65,
            CharacterStateType.Ability => 60,
            CharacterStateType.Move => 20,
            _ => 0
        };
    }
}
