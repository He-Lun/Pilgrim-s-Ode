using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 战斗 Overlay 根节点 — 左侧角色血条 + 右侧行动条。
/// </summary>
public class BattleHealthBarUiRoot : MonoBehaviour
{
    public static BattleHealthBarUiRoot Instance { get; private set; }

    [SerializeField] private BattleHealthBarController healthBarController;
    [SerializeField] private CharacterRosterPanel rosterPanel;
    [SerializeField] private ActionOrderBarPanel actionOrderBarPanel;
    [SerializeField] private HealthBarUIConfig config;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        healthBarController ??= GetComponent<BattleHealthBarController>();
        rosterPanel ??= GetComponentInChildren<CharacterRosterPanel>(true);
        actionOrderBarPanel ??= GetComponentInChildren<ActionOrderBarPanel>(true);
        config ??= HealthBarUIConfig.LoadDefault();

        if (healthBarController != null)
            healthBarController.Configure(rosterPanel, config);

        actionOrderBarPanel?.Configure(config);
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void SyncFromBattle()
    {
        healthBarController?.SyncFromBattle();
        actionOrderBarPanel?.Refresh();
    }
}
