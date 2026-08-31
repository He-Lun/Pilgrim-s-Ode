using System.Collections;
using Mirror;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>准备场景匹配界面。</summary>
[DisallowMultipleComponent]
public class PreparationMatchmakingController : MonoBehaviour
{
    public const string SceneName = "Preparation";

    [Header("匹配")]
    [SerializeField] private bool autoStartWhenMatched = true;
    [SerializeField] private float autoStartDelay = 1.25f;

    [Header("背景图片（可选）")]
    [Tooltip("全屏背景 Sprite；留空则使用 screenBackgroundFallback 纯色。")]
    [SerializeField] private Sprite screenBackgroundSprite;
    [SerializeField] private Color screenBackgroundTint = Color.white;
    [SerializeField] private Color screenBackgroundFallback = new Color(0.04f, 0.06f, 0.1f, 0.96f);
    [Tooltip("全屏背景是否保持图片宽高比（不拉伸）。")]
    [SerializeField] private bool screenBackgroundPreserveAspect;

    [Tooltip("中央匹配面板背景 Sprite；留空则使用 panelBackgroundFallback 纯色。")]
    [SerializeField] private Sprite panelBackgroundSprite;
    [SerializeField] private Color panelBackgroundTint = Color.white;
    [SerializeField] private Color panelBackgroundFallback = new Color(0.1f, 0.12f, 0.18f, 0.94f);
    [Tooltip("面板背景是否保持图片宽高比。")]
    [SerializeField] private bool panelBackgroundPreserveAspect;

    [Tooltip("若已在场景中放好 Image，可直接拖入，会优先于上方 Sprite。")]
    [SerializeField] private Image screenBackgroundOverride;
    [SerializeField] private Image panelBackgroundOverride;

    private Canvas canvas;
    private Text statusText;
    private InputField addressInput;
    private Button hostButton;
    private Button joinButton;
    private Button disconnectButton;
    private Button startBattleButton;
    private Button soloPracticeButton;
    private GameObject idlePanel;
    private GameObject connectedPanel;

    private bool battleTransitionScheduled;
    private Coroutine autoStartRoutine;

    public static bool IsActiveScene()
    {
        return SceneManager.GetActiveScene().name == SceneName;
    }

    public static PreparationMatchmakingController FindInstance()
    {
        return FindObjectOfType<PreparationMatchmakingController>();
    }

    void Awake()
    {
        EnsureEventSystem();
        BuildUi();
    }

    void Start()
    {
        EnsureMenuBgm();
    }

    void OnDestroy()
    {
        if (autoStartRoutine != null)
            StopCoroutine(autoStartRoutine);
    }

    void Update()
    {
        RefreshUi();
        TryScheduleAutoBattleStart();
    }

    public void BeginBattleTransition()
    {
        if (battleTransitionScheduled)
            return;

        if (!NetworkServer.active)
        {
            SetStatus("只有房主可以开始对战。");
            return;
        }

        if (!NetworkServer.HasExternalConnections())
        {
            SetStatus("尚未匹配到对手，请等待另一名玩家加入。");
            return;
        }

        battleTransitionScheduled = true;
        SetStatus("匹配成功，正在进入战斗…");
        SetButtonsInteractable(false);

        var networkManager = PilgrimNetworkManager.Instance;
        if (networkManager == null)
        {
            Debug.LogError("[PreparationMatchmaking] 找不到 PilgrimNetworkManager。");
            battleTransitionScheduled = false;
            return;
        }

        networkManager.ServerChangeScene(NetworkBattleBootstrap.BattleSceneName);
    }

    private void TryScheduleAutoBattleStart()
    {
        if (!autoStartWhenMatched || battleTransitionScheduled || !NetworkServer.active)
            return;

        if (!NetworkServer.HasExternalConnections())
            return;

        if (autoStartRoutine != null)
            return;

        autoStartRoutine = StartCoroutine(AutoStartAfterDelay());
    }

    private IEnumerator AutoStartAfterDelay()
    {
        SetStatus("已匹配到对手，即将进入战斗…");
        yield return new WaitForSecondsRealtime(autoStartDelay);
        autoStartRoutine = null;
        BeginBattleTransition();
    }

    private void OnClickHost()
    {
        var networkManager = PilgrimNetworkManager.Instance;
        if (networkManager == null)
        {
            SetStatus("NetworkManager 未就绪。");
            return;
        }

        ApplyAddressFromInput(networkManager);
        SetStatus("正在创建房间…");
        networkManager.StartHost();
    }

    private void OnClickJoin()
    {
        var networkManager = PilgrimNetworkManager.Instance;
        if (networkManager == null)
        {
            SetStatus("NetworkManager 未就绪。");
            return;
        }

        ApplyAddressFromInput(networkManager);
        SetStatus($"正在连接 {networkManager.networkAddress}…");
        networkManager.StartClient();
    }

    private void OnClickDisconnect()
    {
        CancelAutoStart();

        var networkManager = PilgrimNetworkManager.Instance;
        if (networkManager == null)
            return;

        if (NetworkServer.active && NetworkClient.active)
            networkManager.StopHost();
        else if (NetworkServer.active)
            networkManager.StopServer();
        else if (NetworkClient.active)
            networkManager.StopClient();

        battleTransitionScheduled = false;
        SetStatus("已断开连接。");
    }

    private void OnClickSoloPractice()
    {
        CancelAutoStart();
        OnClickDisconnect();

        var networkManager = PilgrimNetworkManager.Instance;
        if (networkManager != null)
            Destroy(networkManager.gameObject);

        NetworkBattleBootstrap.RequestOfflineStartOnLoad = true;
        SceneManager.LoadScene(NetworkBattleBootstrap.BattleSceneName);
    }

    private void CancelAutoStart()
    {
        if (autoStartRoutine == null)
            return;

        StopCoroutine(autoStartRoutine);
        autoStartRoutine = null;
    }

    private void ApplyAddressFromInput(NetworkManager networkManager)
    {
        if (addressInput == null || networkManager == null)
            return;

        string address = addressInput.text?.Trim();
        if (!string.IsNullOrEmpty(address))
            networkManager.networkAddress = address;
    }

    private void RefreshUi()
    {
        bool networkActive = NetworkServer.active || NetworkClient.active;
        bool isHost = NetworkServer.active && NetworkClient.active;
        bool isClientOnly = NetworkClient.active && !NetworkServer.active;
        bool isServerOnly = NetworkServer.active && !NetworkClient.active;
        bool matched = NetworkServer.active && NetworkServer.HasExternalConnections();
        bool connecting = NetworkClient.active && !NetworkClient.isConnected;

        if (idlePanel != null)
            idlePanel.SetActive(!networkActive || connecting);
        if (connectedPanel != null)
            connectedPanel.SetActive(networkActive && !connecting);

        if (hostButton != null)
            hostButton.interactable = !networkActive;
        if (joinButton != null)
            joinButton.interactable = !networkActive;
        if (soloPracticeButton != null)
            soloPracticeButton.interactable = !networkActive;

        if (disconnectButton != null)
            disconnectButton.interactable = networkActive && !connecting && !battleTransitionScheduled;

        if (startBattleButton != null)
        {
            startBattleButton.gameObject.SetActive(isHost || isServerOnly);
            startBattleButton.interactable = matched && !battleTransitionScheduled;
        }

        if (statusText == null || battleTransitionScheduled)
            return;

        if (connecting)
        {
            SetStatus("正在连接服务器…");
            return;
        }

        if (isHost)
        {
            SetStatus(matched
                ? "匹配成功！等待进入战斗，或点击「开始对战」。"
                : "房间已创建，等待另一名玩家加入…");
            return;
        }

        if (isServerOnly)
        {
            SetStatus(matched
                ? "玩家已满，可以开始对战。"
                : "专用服务器运行中，等待玩家连接…");
            return;
        }

        if (isClientOnly)
        {
            SetStatus(NetworkClient.isConnected
                ? "已加入房间，等待房主开始对战…"
                : "连接已断开。");
        }
    }

    private void SetStatus(string message)
    {
        if (statusText != null)
            BattleUiFonts.ApplyToLabel(statusText, message);
    }

    private void SetButtonsInteractable(bool interactable)
    {
        if (hostButton != null)
            hostButton.interactable = interactable;
        if (joinButton != null)
            joinButton.interactable = interactable;
        if (disconnectButton != null)
            disconnectButton.interactable = interactable;
        if (startBattleButton != null)
            startBattleButton.interactable = interactable;
        if (soloPracticeButton != null)
            soloPracticeButton.interactable = interactable;
    }

    private static void EnsureMenuBgm()
    {
        AudioManager.Ensure().PlayBGM("Menu");
    }

    private static void EnsureEventSystem()
    {
        if (FindObjectOfType<EventSystem>() != null)
            return;

        var eventSystemGo = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        DontDestroyOnLoad(eventSystemGo);
    }

    private void BuildUi()
    {
        var canvasGo = new GameObject("PreparationCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);
        canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        var root = CreateRect("Root", canvasGo.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        Image backdropImage;
        if (screenBackgroundOverride != null)
        {
            backdropImage = screenBackgroundOverride;
            backdropImage.transform.SetParent(root, false);
            backdropImage.transform.SetAsFirstSibling();
            Stretch(backdropImage.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        }
        else
        {
            backdropImage = CreateImage("Backdrop", root, screenBackgroundFallback);
            Stretch(backdropImage.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        }

        ApplyBackgroundImage(
            backdropImage,
            screenBackgroundSprite,
            screenBackgroundTint,
            screenBackgroundFallback,
            screenBackgroundPreserveAspect);

        var panel = CreateRect("Panel", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-420f, -300f), new Vector2(420f, 300f));
        Image panelImage;
        if (panelBackgroundOverride != null)
        {
            panelImage = panelBackgroundOverride;
            panelImage.transform.SetParent(panel, false);
            panelImage.transform.SetAsFirstSibling();
            Stretch(panelImage.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        }
        else
        {
            panelImage = panel.gameObject.AddComponent<Image>();
        }

        ApplyBackgroundImage(
            panelImage,
            panelBackgroundSprite,
            panelBackgroundTint,
            panelBackgroundFallback,
            panelBackgroundPreserveAspect);

        CreateLabel("Title", panel, new Vector2(0f, 210f), new Vector2(760f, 72f), 42, FontStyle.Bold, "巡礼之诗 · 玩家匹配");
        CreateLabel("Hint", panel, new Vector2(0f, 150f), new Vector2(760f, 48f), 22, FontStyle.Normal, "创建房间或输入地址加入，匹配成功后进入 1v1 对战");

        idlePanel = CreateRect("IdlePanel", panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-360f, -180f), new Vector2(360f, 180f)).gameObject;

        CreateLabel("AddressLabel", idlePanel.transform, new Vector2(-250f, 95f), new Vector2(140f, 40f), 24, FontStyle.Normal, "服务器地址")
            .alignment = TextAnchor.MiddleLeft;
        addressInput = CreateInputField("AddressInput", idlePanel.transform, new Vector2(40f, 95f), new Vector2(430f, 52f), "127.0.0.1");

        hostButton = CreateButton("HostButton", idlePanel.transform, new Vector2(-130f, 10f), new Vector2(240f, 58f), "创建房间", new Color(0.18f, 0.42f, 0.72f, 1f), OnClickHost);
        joinButton = CreateButton("JoinButton", idlePanel.transform, new Vector2(130f, 10f), new Vector2(240f, 58f), "加入房间", new Color(0.16f, 0.55f, 0.38f, 1f), OnClickJoin);
        soloPracticeButton = CreateButton("SoloButton", idlePanel.transform, new Vector2(0f, -75f), new Vector2(320f, 52f), "单人练习（不进联机）", new Color(0.28f, 0.28f, 0.34f, 1f), OnClickSoloPractice);

        connectedPanel = CreateRect("ConnectedPanel", panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-360f, -180f), new Vector2(360f, 180f)).gameObject;
        connectedPanel.SetActive(false);

        startBattleButton = CreateButton("StartBattleButton", connectedPanel.transform, new Vector2(0f, 35f), new Vector2(320f, 58f), "开始对战", new Color(0.72f, 0.48f, 0.14f, 1f), BeginBattleTransition);
        disconnectButton = CreateButton("DisconnectButton", connectedPanel.transform, new Vector2(0f, -45f), new Vector2(320f, 52f), "断开连接", new Color(0.45f, 0.18f, 0.18f, 1f), OnClickDisconnect);

        statusText = CreateLabel("Status", panel, new Vector2(0f, -230f), new Vector2(760f, 110f), 22, FontStyle.Normal, "请选择创建房间或加入已有房间。");
        statusText.alignment = TextAnchor.UpperCenter;
        statusText.horizontalOverflow = HorizontalWrapMode.Wrap;
        statusText.verticalOverflow = VerticalWrapMode.Overflow;
    }

    private static RectTransform CreateRect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
        return rect;
    }

    private static void Stretch(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }

    private static Image CreateImage(string name, Transform parent, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        var image = go.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static void ApplyBackgroundImage(
        Image image,
        Sprite sprite,
        Color tint,
        Color fallbackColor,
        bool preserveAspect)
    {
        if (image == null)
            return;

        image.raycastTarget = false;
        image.preserveAspect = preserveAspect;

        if (sprite != null)
        {
            image.sprite = sprite;
            image.type = Image.Type.Simple;
            image.color = tint;
            return;
        }

        image.sprite = null;
        image.color = fallbackColor;
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (screenBackgroundOverride != null)
        {
            ApplyBackgroundImage(
                screenBackgroundOverride,
                screenBackgroundSprite,
                screenBackgroundTint,
                screenBackgroundFallback,
                screenBackgroundPreserveAspect);
        }

        if (panelBackgroundOverride != null)
        {
            ApplyBackgroundImage(
                panelBackgroundOverride,
                panelBackgroundSprite,
                panelBackgroundTint,
                panelBackgroundFallback,
                panelBackgroundPreserveAspect);
        }
    }
#endif

    private Text CreateLabel(string name, Transform parent, Vector2 anchoredPosition, Vector2 sizeDelta, int fontSize, FontStyle fontStyle, string text)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        go.transform.SetParent(parent, false);
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = sizeDelta;

        var label = go.GetComponent<Text>();
        label.fontSize = fontSize;
        label.fontStyle = fontStyle;
        label.color = new Color(0.92f, 0.94f, 0.98f, 1f);
        label.alignment = TextAnchor.MiddleCenter;
        label.supportRichText = true;
        BattleUiFonts.ApplyToLabel(label, text);
        return label;
    }

    private InputField CreateInputField(string name, Transform parent, Vector2 anchoredPosition, Vector2 sizeDelta, string defaultValue)
    {
        var root = CreateRect(name, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(anchoredPosition.x - sizeDelta.x * 0.5f, anchoredPosition.y - sizeDelta.y * 0.5f),
            new Vector2(anchoredPosition.x + sizeDelta.x * 0.5f, anchoredPosition.y + sizeDelta.y * 0.5f));

        var background = root.gameObject.AddComponent<Image>();
        background.color = new Color(0.07f, 0.09f, 0.13f, 1f);

        var textGo = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        textGo.transform.SetParent(root, false);
        var textRect = textGo.GetComponent<RectTransform>();
        Stretch(textRect, Vector2.zero, Vector2.one, new Vector2(14f, 8f), new Vector2(-14f, -8f));
        var text = textGo.GetComponent<Text>();
        text.fontSize = 24;
        text.color = Color.white;
        text.alignment = TextAnchor.MiddleLeft;
        BattleUiFonts.ApplyToLabel(text, defaultValue);

        var placeholderGo = new GameObject("Placeholder", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        placeholderGo.transform.SetParent(root, false);
        var placeholderRect = placeholderGo.GetComponent<RectTransform>();
        Stretch(placeholderRect, Vector2.zero, Vector2.one, new Vector2(14f, 8f), new Vector2(-14f, -8f));
        var placeholder = placeholderGo.GetComponent<Text>();
        placeholder.fontSize = 24;
        placeholder.color = new Color(1f, 1f, 1f, 0.35f);
        placeholder.text = "127.0.0.1";
        placeholder.font = BattleUiFonts.GetRosterFont();

        var input = root.gameObject.AddComponent<InputField>();
        input.textComponent = text;
        input.placeholder = placeholder;
        input.lineType = InputField.LineType.SingleLine;
        input.text = defaultValue;
        return input;
    }

    private Button CreateButton(string name, Transform parent, Vector2 anchoredPosition, Vector2 sizeDelta, string label, Color color, UnityEngine.Events.UnityAction onClick)
    {
        var rect = CreateRect(name, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(anchoredPosition.x - sizeDelta.x * 0.5f, anchoredPosition.y - sizeDelta.y * 0.5f),
            new Vector2(anchoredPosition.x + sizeDelta.x * 0.5f, anchoredPosition.y + sizeDelta.y * 0.5f));

        var image = rect.gameObject.AddComponent<Image>();
        image.color = color;

        var button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(onClick);

        CreateLabel("Label", rect, Vector2.zero, sizeDelta, 24, FontStyle.Bold, label);
        return button;
    }
}
