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
    Knockback = 6
}

public static class CharacterStatePriority
{
    public static int Get(CharacterStateType type)
    {
        return type switch
        {
            CharacterStateType.Death => 100,
            CharacterStateType.Knockback => 90,
            CharacterStateType.Hit => 80,
            CharacterStateType.Ability => 60,
            CharacterStateType.Move => 20,
            _ => 0
        };
    }
}
