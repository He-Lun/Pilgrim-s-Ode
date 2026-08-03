/// <summary>
/// Buff 表现类别 — 实例 tag（Buff.AttackUp.*）映射到通用外观 key（Buff.AttackUp）。
/// </summary>
public static class BuffCategoryTag
{
    public static GameplayTag Resolve(GameplayTag tag)
    {
        string name = tag.TagName;
        if (string.IsNullOrEmpty(name)) return default;

        int last = name.LastIndexOf('.');
        int first = name.IndexOf('.');
        if (last <= first) return tag;

        return new GameplayTag(name.Substring(0, last));
    }

    public static bool BelongsToCategory(GameplayTag tag, GameplayTag category)
    {
        if (string.IsNullOrEmpty(category.TagName)) return false;
        if (tag.Matches(category)) return true;
        return tag.TagName.StartsWith(category.TagName + ".");
    }
}
