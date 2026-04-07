using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public class GameSessionOverlay : MonoBehaviour
{
    public static bool RequiresManualStart => true;

    private static GameSessionOverlay instance;

    [Header("Window")]
    [SerializeField] private float windowWidth = 420f;
    [SerializeField] private float startWindowHeight = 210f;
    [SerializeField] private float resultWindowHeight = 250f;

    [Header("Health Bar")]
    [SerializeField] private float healthBarWidth = 260f;
    [SerializeField] private float healthBarHeight = 22f;
    [SerializeField] private float healthBarLeftOffset = 24f;
    [SerializeField] private float healthBarBottomOffset = 24f;
    [SerializeField] private Color healthBarBackgroundColor = new Color(0.12f, 0.12f, 0.12f, 0.9f);
    [SerializeField] private Color healthBarFillColor = new Color(0.85f, 0.16f, 0.16f, 1f);
    [SerializeField] private Color healthBarBorderColor = Color.white;

    [Header("Text")]
    [SerializeField] private string startTitle = "Failover";
    [SerializeField] private string startMessage = "Нажмите Start, чтобы начать прохождение.";
    [SerializeField] private string winTitle = "Победа";
    [SerializeField] private string loseTitle = "Поражение";
    [SerializeField] private string restartButtonText = "OK";
    [SerializeField] private string startButtonText = "Start";

    [Header("Style")]
    [SerializeField] private int titleFontSize = 28;
    [SerializeField] private int bodyFontSize = 20;
    [SerializeField] private int timeFontSize = 24;
    [SerializeField] private int buttonFontSize = 20;

    private WaveController waveController;
    private Fabricator fabricator;
    private LinkageNode linkageNode;
    private PlayerController playerController;
    private HealthComponent playerHealth;
    private WeaponController weaponController;
    private PlayerUpperBodyAim upperBodyAim;
    private PlayerInput playerInput;
    private bool initialized;
    private Texture2D solidTexture;
    private readonly GameSessionState sessionState = new();
    private readonly FabricatorInteractionInstaller fabricatorInteractionInstaller = new();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        instance = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (instance != null)
            return;

        if (FindFirstObjectByType<GameSessionOverlay>() != null)
            return;

        GameObject bootstrapObject = new GameObject(nameof(GameSessionOverlay));
        instance = bootstrapObject.AddComponent<GameSessionOverlay>();
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void Start()
    {
        BindSceneObjects();
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnDestroy()
    {
        UnbindSceneObjects();
        Time.timeScale = 1f;

        if (solidTexture != null)
            Destroy(solidTexture);

        if (instance == this)
            instance = null;
    }

    private void OnSceneLoaded(Scene _, LoadSceneMode __)
    {
        BindSceneObjects();
    }

    private void BindSceneObjects()
    {
        UnbindSceneObjects();

        waveController = FindFirstObjectByType<WaveController>();
        fabricator = FindFirstObjectByType<Fabricator>();
        linkageNode = FindFirstObjectByType<LinkageNode>();
        playerController = FindFirstObjectByType<PlayerController>();
        playerHealth = playerController != null ? playerController.GetComponent<HealthComponent>() : null;
        weaponController = playerController != null ? playerController.GetComponent<WeaponController>() : null;
        upperBodyAim = playerController != null ? playerController.GetComponent<PlayerUpperBodyAim>() : null;
        playerInput = playerController != null ? playerController.GetComponent<PlayerInput>() : null;

        if (waveController == null || playerHealth == null || fabricator == null)
        {
            initialized = false;
            sessionState.Reset();
            Time.timeScale = 1f;
            return;
        }

        fabricatorInteractionInstaller.EnsureInstalled(fabricator, linkageNode);
        fabricator.StateChanged += HandleFabricatorStateChanged;
        playerHealth.OnDeath += HandleLoss;

        initialized = true;
        sessionState.Reset();
        ApplyPausedState(true);
    }

    private void UnbindSceneObjects()
    {
        if (fabricator != null)
            fabricator.StateChanged -= HandleFabricatorStateChanged;

        if (playerHealth != null)
            playerHealth.OnDeath -= HandleLoss;

        waveController = null;
        fabricator = null;
        linkageNode = null;
        playerController = null;
        playerHealth = null;
        weaponController = null;
        upperBodyAim = null;
        playerInput = null;
    }

    private void OnGUI()
    {
        if (!initialized)
            return;

        DrawHealthBar();

        switch (sessionState.Current)
        {
            case GameSessionState.Status.WaitingToStart:
                DrawStartWindow();
                break;
            case GameSessionState.Status.Won:
                DrawResultWindow(winTitle);
                break;
            case GameSessionState.Status.Lost:
                DrawResultWindow(loseTitle);
                break;
        }
    }

    private void DrawStartWindow()
    {
        Rect windowRect = CreateCenteredRect(windowWidth, startWindowHeight);
        GUI.Box(windowRect, startTitle);

        GUIStyle labelStyle = CreateLabelStyle(bodyFontSize);
        GUIStyle titleStyle = CreateLabelStyle(titleFontSize);
        GUIStyle buttonStyle = CreateButtonStyle();

        GUI.Label(new Rect(windowRect.x + 20f, windowRect.y + 35f, windowRect.width - 40f, 40f), startTitle, titleStyle);
        GUI.Label(new Rect(windowRect.x + 20f, windowRect.y + 90f, windowRect.width - 40f, 45f), startMessage, labelStyle);

        if (GUI.Button(new Rect(windowRect.x + 110f, windowRect.y + 150f, windowRect.width - 220f, 38f), startButtonText, buttonStyle))
            StartRun();
    }

    private void DrawResultWindow(string resultTitle)
    {
        Rect windowRect = CreateCenteredRect(windowWidth, resultWindowHeight);
        GUI.Box(windowRect, resultTitle);

        GUIStyle titleStyle = CreateLabelStyle(titleFontSize);
        GUIStyle labelStyle = CreateLabelStyle(bodyFontSize);
        GUIStyle timeStyle = CreateLabelStyle(timeFontSize);
        GUIStyle buttonStyle = CreateButtonStyle();

        GUI.Label(new Rect(windowRect.x + 20f, windowRect.y + 35f, windowRect.width - 40f, 40f), resultTitle, titleStyle);
        GUI.Label(new Rect(windowRect.x + 20f, windowRect.y + 95f, windowRect.width - 40f, 35f), "Время прохождения", labelStyle);
        GUI.Label(new Rect(windowRect.x + 20f, windowRect.y + 130f, windowRect.width - 40f, 40f), FormatDuration(sessionState.RunDuration), timeStyle);

        if (GUI.Button(new Rect(windowRect.x + 110f, windowRect.y + 190f, windowRect.width - 220f, 38f), restartButtonText, buttonStyle))
            RestartScene();
    }

    private void StartRun()
    {
        if (sessionState.Current != GameSessionState.Status.WaitingToStart || waveController == null)
            return;

        sessionState.StartRun(Time.time);
        ApplyPausedState(false);
        waveController.StartWaves();
    }

    private void HandleFabricatorStateChanged(FabricatorState fabricatorState)
    {
        if (fabricatorState != FabricatorState.Captured || !sessionState.IsPlaying)
            return;

        CompleteRun(GameSessionState.Status.Won);
    }

    private void HandleLoss()
    {
        if (!sessionState.IsPlaying)
            return;

        CompleteRun(GameSessionState.Status.Lost);
    }

    private void CompleteRun(GameSessionState.Status resultState)
    {
        sessionState.Complete(resultState, Time.time);
        ApplyPausedState(true);
    }

    private static Rect CreateCenteredRect(float width, float height)
    {
        return new Rect(
            (Screen.width - width) * 0.5f,
            (Screen.height - height) * 0.5f,
            width,
            height);
    }

    private static string FormatDuration(float durationSeconds)
    {
        int totalSeconds = Mathf.Max(0, Mathf.RoundToInt(durationSeconds));
        int minutes = totalSeconds / 60;
        int seconds = totalSeconds % 60;
        return $"{minutes:00}:{seconds:00}";
    }

    private GUIStyle CreateLabelStyle(int fontSize)
    {
        GUIStyle style = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = fontSize,
            wordWrap = true
        };
        style.normal.textColor = Color.white;
        return style;
    }

    private GUIStyle CreateButtonStyle()
    {
        return new GUIStyle(GUI.skin.button)
        {
            fontSize = buttonFontSize
        };
    }

    private void DrawHealthBar()
    {
        if (playerHealth == null)
            return;

        Rect outerRect = new Rect(
            healthBarLeftOffset,
            Screen.height - healthBarBottomOffset - healthBarHeight,
            healthBarWidth,
            healthBarHeight);

        float fillPercent = playerHealth.Max > 0f ? Mathf.Clamp01(playerHealth.Current / playerHealth.Max) : 0f;
        Rect fillRect = new Rect(
            outerRect.x + 2f,
            outerRect.y + 2f,
            (outerRect.width - 4f) * fillPercent,
            outerRect.height - 4f);

        DrawFilledRect(outerRect, healthBarBorderColor);
        DrawFilledRect(
            new Rect(outerRect.x + 1f, outerRect.y + 1f, outerRect.width - 2f, outerRect.height - 2f),
            healthBarBackgroundColor);

        if (fillRect.width > 0f)
            DrawFilledRect(fillRect, healthBarFillColor);

        GUIStyle healthTextStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = bodyFontSize
        };
        healthTextStyle.normal.textColor = Color.white;

        GUI.Label(outerRect, $"{Mathf.CeilToInt(playerHealth.Current)} / {Mathf.CeilToInt(playerHealth.Max)}", healthTextStyle);
    }

    private void DrawFilledRect(Rect rect, Color color)
    {
        Texture2D texture = GetSolidTexture();
        Color previousColor = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(rect, texture);
        GUI.color = previousColor;
    }

    private Texture2D GetSolidTexture()
    {
        if (solidTexture != null)
            return solidTexture;

        solidTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
        {
            hideFlags = HideFlags.HideAndDontSave
        };
        solidTexture.SetPixel(0, 0, Color.white);
        solidTexture.Apply();
        return solidTexture;
    }

    private void RestartScene()
    {
        ApplyPausedState(false);
        Scene activeScene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(activeScene.path);
    }

    private void ApplyPausedState(bool paused)
    {
        Time.timeScale = paused ? 0f : 1f;

        if (playerController != null)
            playerController.SetMovementLocked(paused);

        if (weaponController != null)
            weaponController.SetGameplayInputEnabled(!paused);

        if (upperBodyAim != null)
            upperBodyAim.enabled = !paused;

        if (playerInput != null)
            playerInput.enabled = !paused;
    }
}
