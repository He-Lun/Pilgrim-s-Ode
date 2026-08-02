using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 战斗左侧角色信息面板 — 本地阵营头像、血条、激励进度。
/// </summary>
public class CharacterRosterPanel : MonoBehaviour
{
    [SerializeField] private RectTransform entryContainer;
    [SerializeField] private CharacterRosterEntryWidget entryPrefab;
    [SerializeField] private HealthBarUIConfig config;

    private readonly List<CharacterRosterEntryWidget> activeEntries = new List<CharacterRosterEntryWidget>();

    public void Configure(HealthBarUIConfig uiConfig) => config = uiConfig;

    public void SetRoster(IReadOnlyList<AbilitySystemComponent> actors)
    {
        ClearEntries();

        if (actors == null || entryContainer == null)
            return;

        int localTeam = config != null ? config.localTeamId : 0;

        foreach (var actor in actors)
        {
            if (actor == null || actor.Attributes == null || actor.TeamId != localTeam)
                continue;

            var entry = CreateEntry(actor);
            entry.Bind(actor);
            activeEntries.Add(entry);
        }
    }

    public void ClearEntries()
    {
        for (int i = 0; i < activeEntries.Count; i++)
        {
            if (activeEntries[i] != null)
            {
                activeEntries[i].Unbind();
                Destroy(activeEntries[i].gameObject);
            }
        }

        activeEntries.Clear();
    }

    public void RefreshEntries()
    {
        for (int i = 0; i < activeEntries.Count; i++)
            activeEntries[i]?.SyncDisplay();
    }

    private CharacterRosterEntryWidget CreateEntry(AbilitySystemComponent actor)
    {
        if (config != null)
        {
            var prefab = config.ResolveRosterEntryPrefab(actor);
            if (prefab != null)
                return Instantiate(prefab, entryContainer);
        }

        if (entryPrefab != null)
            return Instantiate(entryPrefab, entryContainer);

        if (config != null)
            return HealthBarFactory.CreateRosterEntry(entryContainer, config.ResolveOverlaySprites(actor), config);

        return null;
    }
}
