using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>战斗 roster 初始化。</summary>
public static class BattleRosterSetup
{
    public struct Options
    {
        public List<AbilitySystemComponent> explicitActors;
        public Transform teamResourceParent;
        public BuffPresentationCatalog buffCatalog;
        public bool snapActorsToNavMesh;
        public bool autoSplitTeamsWhenSingleSide;
    }

    public struct Result
    {
        public List<AbilitySystemComponent> roster;
        public Dictionary<int, TeamResourceManager> teamResources;
    }

    public static Result Prepare(Options options)
    {
        var roster = CollectActors(options.explicitActors);
        if (options.snapActorsToNavMesh)
            SnapActorsToNavMesh(roster);

        var teamResources = SetupTeamResources(roster, options.teamResourceParent);
        if (options.autoSplitTeamsWhenSingleSide)
            ApplyTestTeamSplit(roster, teamResources, options.teamResourceParent);

        WireActors(roster, teamResources, options.buffCatalog);
        return new Result { roster = roster, teamResources = teamResources };
    }

    public static bool EnsureBattleSystems()
    {
        if (Object.FindObjectOfType<BattleInputController>() == null)
            Debug.LogWarning("[BattleRosterSetup] 场景中缺少 BattleInputController，将无法点击地面移动。");

        if (BattleSpaceSettings.Instance == null)
        {
            var host = GameObject.Find("BattleRuntime") ?? new GameObject("BattleRuntime");
            if (host.GetComponent<BattleSpaceSettings>() == null)
                host.AddComponent<BattleSpaceSettings>();
        }

        if (TurnManager.Instance == null || ActionQueue.Instance == null)
        {
            var existing = Object.FindObjectOfType<TurnManager>();
            var host = existing != null ? existing.gameObject : new GameObject("BattleRuntime");

            if (existing == null)
                host.AddComponent<TurnManager>();

            if (ActionQueue.Instance == null)
                host.AddComponent<ActionQueue>();
        }

        return TurnManager.Instance != null && ActionQueue.Instance != null;
    }

    public static List<AbilitySystemComponent> CollectActors(List<AbilitySystemComponent> explicitActors)
    {
        if (explicitActors != null && explicitActors.Count > 0)
            return explicitActors.Where(a => a != null).Distinct().ToList();

        return Object.FindObjectsOfType<AbilitySystemComponent>()
            .Where(a => a != null && a.gameObject.activeInHierarchy)
            .OrderBy(a => a.TeamId)
            .ThenBy(a => a.name)
            .ToList();
    }

    public static Dictionary<int, TeamResourceManager> SetupTeamResources(
        List<AbilitySystemComponent> roster,
        Transform parent)
    {
        var teamResources = new Dictionary<int, TeamResourceManager>();
        var teamIds = roster.Select(a => a.TeamId).Distinct().OrderBy(id => id);

        foreach (int teamId in teamIds)
        {
            var existing = roster
                .Select(a => a.TeamResource)
                .FirstOrDefault(tr => tr != null && roster.Any(r => r.TeamId == teamId && r.TeamResource == tr));

            TeamResourceManager resource;
            if (existing != null)
            {
                resource = existing;
            }
            else
            {
                var go = new GameObject($"TeamResource_T{teamId}");
                if (parent != null)
                    go.transform.SetParent(parent);
                resource = go.AddComponent<TeamResourceManager>();
            }

            teamResources[teamId] = resource;
        }

        return teamResources;
    }

    public static void ApplyTestTeamSplit(
        List<AbilitySystemComponent> roster,
        Dictionary<int, TeamResourceManager> teamResources,
        Transform parent)
    {
        if (roster.Count < 2) return;

        var distinctTeams = roster.Select(a => a.TeamId).Distinct().ToList();
        if (distinctTeams.Count != 1) return;

        int teamA = distinctTeams[0];
        int teamB = teamA == 0 ? 1 : 0;

        if (!teamResources.ContainsKey(teamB))
        {
            var go = new GameObject($"TeamResource_T{teamB}");
            if (parent != null)
                go.transform.SetParent(parent);
            teamResources[teamB] = go.AddComponent<TeamResourceManager>();
        }

        int split = roster.Count / 2;
        for (int i = split; i < roster.Count; i++)
        {
            var asc = roster[i];
            if (asc == null) continue;
            asc.Initialize(asc.Attributes ?? asc.GetComponent<AttributeSet>(), teamResources[teamB], teamB);
        }

        Debug.LogWarning($"[BattleRosterSetup] 所有角色均为 Team {teamA}，已自动将后 {roster.Count - split} 名角色分配到 Team {teamB} 以便测试。");
    }

    public static void WireActors(
        List<AbilitySystemComponent> roster,
        Dictionary<int, TeamResourceManager> teamResources,
        BuffPresentationCatalog buffCatalog)
    {
        foreach (var asc in roster)
        {
            if (asc == null) continue;

            if (!teamResources.TryGetValue(asc.TeamId, out var resource))
            {
                Debug.LogWarning($"[BattleRosterSetup] 角色 {asc.name} 的 TeamId={asc.TeamId} 无对应 TeamResource，已跳过。");
                continue;
            }

            var attrs = asc.Attributes ?? asc.GetComponent<AttributeSet>();

            if (asc.CharacterData != null)
                asc.SetupFromCharacterData(asc.CharacterData, resource, asc.TeamId);
            else
                asc.Initialize(attrs, resource, asc.TeamId);

            if (asc.GetComponent<CharacterMovementController>() == null)
                Debug.LogWarning($"[BattleRosterSetup] {asc.name} 缺少 CharacterMovementController。");

            if (asc.GetComponent<CharacterMotor>() == null)
                Debug.LogWarning($"[BattleRosterSetup] {asc.name} 缺少 CharacterMotor。");

            if (asc.GetComponent<CharacterAnimationEvents>() == null)
                asc.gameObject.AddComponent<CharacterAnimationEvents>();

            var vfxPlayer = asc.GetComponent<AbilityVfxPlayer>();
            if (vfxPlayer == null)
                vfxPlayer = asc.gameObject.AddComponent<AbilityVfxPlayer>();
            // prefab 上挂了但没配 catalog 的角色同样要补，否则查不到 Buff 类别特效。
            if (buffCatalog != null)
            {
                vfxPlayer.BindCatalog(buffCatalog);
                BattleBarrierManager.Instance.BindCatalog(buffCatalog);
                BattleZoneManager.Instance.BindCatalog(buffCatalog);
            }

            if (asc.GetComponent<AbilityAudioPlayer>() == null)
                asc.gameObject.AddComponent<AbilityAudioPlayer>();

            var stateAudio = asc.GetComponent<CharacterStateAudioPlayer>();
            if (stateAudio == null)
                stateAudio = asc.gameObject.AddComponent<CharacterStateAudioPlayer>();
            else
                stateAudio.RebuildStateAudioMap();

            if (asc.GetComponent<CharacterTurnVoicePlayer>() == null)
                asc.gameObject.AddComponent<CharacterTurnVoicePlayer>();

            _ = asc.HandCards;
        }
    }

    public static void EnsureBattleCamera(List<AbilitySystemComponent> roster)
    {
        if (BattleCameraController.Instance == null)
        {
            var go = new GameObject("BattleCameraRig");
            go.AddComponent<BattleCameraController>();
            go.AddComponent<BattleCameraInput>();
            go.AddComponent<BattleCameraImpulsePlayer>();
            go.AddComponent<BattleCameraTurnFocus>();
        }
        else
        {
            var rig = BattleCameraController.Instance.gameObject;
            if (rig.GetComponent<BattleCameraInput>() == null)
                rig.AddComponent<BattleCameraInput>();
            if (rig.GetComponent<BattleCameraImpulsePlayer>() == null)
                rig.AddComponent<BattleCameraImpulsePlayer>();
            if (rig.GetComponent<BattleCameraTurnFocus>() == null)
                rig.AddComponent<BattleCameraTurnFocus>();
        }

        BattleCameraController.Instance?.FocusOnActors(roster);
    }

    public static void EnsureBattleAudio()
    {
        var manager = AudioManager.Ensure();
        manager.GetComponent<CombatAudioPlayer>()?.Subscribe();
        manager.PlayBGM("Battle");
    }

    public static void SnapActorsToNavMesh(List<AbilitySystemComponent> roster)
    {
        foreach (var asc in roster)
        {
            var movement = asc.GetComponent<CharacterMovementController>();
            if (movement == null) continue;
            movement.SnapToWorldPosition(asc.transform.position);
        }
    }
}
