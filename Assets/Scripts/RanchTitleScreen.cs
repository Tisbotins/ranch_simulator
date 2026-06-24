using UnityEngine;

/// <summary>
/// Runtime-generated title screen with separate Single Player and LAN
/// Multiplayer modes. RanchGameBootstrap adds it automatically.
/// </summary>
[DefaultExecutionOrder(9000)]
[DisallowMultipleComponent]
public class RanchTitleScreen : MonoBehaviour
{
    private enum MenuPage
    {
        Main,
        Multiplayer
    }

    private const float VirtualWidth = 1600f;
    private const float VirtualHeight = 900f;

    public bool IsOpen { get; private set; }

    private RanchGameCore core;
    private RanchLanMultiplayer multiplayer;
    private float previousTimeScale = 1f;
    private bool startingGame;
    private MenuPage page;
    private string joinAddress = "127.0.0.1";
    private string menuStatus = "";

    private Texture2D backgroundTexture;
    private Texture2D cardTexture;
    private Texture2D accentTexture;
    private Texture2D accentHoverTexture;
    private Texture2D accentActiveTexture;
    private Texture2D secondaryTexture;
    private Texture2D secondaryHoverTexture;
    private Texture2D lineTexture;

    private GUIStyle backgroundStyle;
    private GUIStyle cardStyle;
    private GUIStyle titleStyle;
    private GUIStyle subtitleStyle;
    private GUIStyle primaryButtonStyle;
    private GUIStyle secondaryButtonStyle;
    private GUIStyle statusStyle;
    private GUIStyle footerStyle;
    private GUIStyle fieldStyle;
    private GUIStyle fieldLabelStyle;
    private bool stylesReady;

    public void Initialize(
        RanchGameCore gameCore,
        RanchLanMultiplayer lanMultiplayer)
    {
        core = gameCore;
        multiplayer = lanMultiplayer;
    }

    public void Open()
    {
        if (core == null || IsOpen)
            return;

        RanchGameModeState.ResetToTitle();
        IsOpen = true;
        startingGame = false;
        page = MenuPage.Main;
        menuStatus = "";
        previousTimeScale = Time.timeScale <= 0f ? 1f : Time.timeScale;
        HoldGameAtTitle();
    }

    private void Update()
    {
        if (!IsOpen)
            return;

        if (page == MenuPage.Main &&
            (Input.GetKeyDown(KeyCode.Return) ||
             Input.GetKeyDown(KeyCode.KeypadEnter)))
        {
            BeginSinglePlayer();
        }

        if (page == MenuPage.Multiplayer &&
            Input.GetKeyDown(KeyCode.Escape))
        {
            page = MenuPage.Main;
            menuStatus = "";
        }
    }

    private void LateUpdate()
    {
        if (IsOpen)
            HoldGameAtTitle();
    }

    private void HoldGameAtTitle()
    {
        if (core == null)
            return;

        if (core.Settings != null && core.Settings.IsOpen)
            core.Settings.CloseMenu();

        if (core.Shop != null && core.Shop.IsOpen)
            core.Shop.CloseShop();

        if (core.Progression != null && core.Progression.IsOpen)
            core.Progression.CloseMenu();

        if (core.Classes != null && core.Classes.IsOpen)
            core.Classes.CloseMenu();

        if (core.Laboratory != null && core.Laboratory.IsOpen)
            core.Laboratory.CloseMenu();

        Time.timeScale = 0f;

        if (core.Player != null)
            core.Player.enabled = false;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void BeginSinglePlayer()
    {
        if (!CanStart())
            return;

        startingGame = true;
        RanchGameModeState.SetMode(RanchGameMode.SinglePlayer);

        bool loaded = core.Save != null && core.Save.LoadOnStartup();
        EnterGame(
            loaded
                ? "Save loaded. Press Escape for settings, H for HUD, Z to save, and X to load."
                : "Welcome to Ranch Simulator. Build your empire, unlock new areas, and survive 30 waves."
        );
    }

    private void BeginLanHost()
    {
        if (!CanStart())
            return;

        if (multiplayer == null || !multiplayer.StartHost())
        {
            menuStatus = "Could not start the LAN host.";
            return;
        }

        startingGame = true;
        bool loaded = core.Save != null && core.Save.LoadOnStartup();

        EnterGame(
            (loaded ? "Host save loaded. " : "New host ranch started. ") +
            "Give your friend the LAN address shown in the top-right corner."
        );
    }

    private void BeginLanClient()
    {
        if (!CanStart())
            return;

        if (multiplayer == null || !multiplayer.StartClient(joinAddress))
        {
            menuStatus = multiplayer != null
                ? multiplayer.StatusText
                : "LAN multiplayer system is missing.";
            return;
        }

        startingGame = true;
        EnterGame(
            "Connecting as a LAN guest. The host controls the ranch and save file."
        );
    }

    private bool CanStart()
    {
        return IsOpen && !startingGame && core != null;
    }

    private void EnterGame(string message)
    {
        IsOpen = false;
        Time.timeScale = previousTimeScale <= 0f ? 1f : previousTimeScale;

        if (core.Player != null &&
            core.Health != null &&
            !core.Health.IsDead &&
            !core.GameWon)
        {
            core.Player.enabled = true;
        }

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        core.ShowMessage(message, 10f);
    }

    private void OnGUI()
    {
        if (!IsOpen || core == null)
            return;

        EnsureStyles();

        int oldDepth = GUI.depth;
        Matrix4x4 oldMatrix = GUI.matrix;
        GUI.depth = -1000;

        float scale = Mathf.Min(
            Screen.width / VirtualWidth,
            Screen.height / VirtualHeight
        );

        GUI.matrix = Matrix4x4.TRS(
            new Vector3(
                (Screen.width - VirtualWidth * scale) * 0.5f,
                (Screen.height - VirtualHeight * scale) * 0.5f,
                0f
            ),
            Quaternion.identity,
            new Vector3(scale, scale, 1f)
        );

        GUI.Box(
            new Rect(0f, 0f, VirtualWidth, VirtualHeight),
            GUIContent.none,
            backgroundStyle
        );

        GUI.DrawTexture(
            new Rect(0f, 0f, VirtualWidth, 12f),
            lineTexture
        );
        GUI.DrawTexture(
            new Rect(0f, VirtualHeight - 12f, VirtualWidth, 12f),
            lineTexture
        );

        GUI.Label(
            new Rect(180f, 90f, 1240f, 110f),
            "RANCH SIMULATOR",
            titleStyle
        );

        GUI.Label(
            new Rect(250f, 195f, 1100f, 48f),
            "BUILD  •  BOTTLE  •  DEFEND",
            subtitleStyle
        );

        if (page == MenuPage.Main)
            DrawMainMenu();
        else
            DrawMultiplayerMenu();

        GUI.matrix = oldMatrix;
        GUI.depth = oldDepth;
    }

    private void DrawMainMenu()
    {
        Rect card = new Rect(455f, 285f, 690f, 455f);
        GUI.Box(card, GUIContent.none, cardStyle);

        if (GUI.Button(
            new Rect(570f, 365f, 460f, 82f),
            "SINGLE PLAYER",
            primaryButtonStyle))
        {
            BeginSinglePlayer();
            GUIUtility.ExitGUI();
        }

        if (GUI.Button(
            new Rect(570f, 485f, 460f, 82f),
            "MULTIPLAYER — LAN",
            secondaryButtonStyle))
        {
            page = MenuPage.Multiplayer;
            menuStatus = "";
            GUIUtility.ExitGUI();
        }

        string saveText =
            core.Save != null && core.Save.HasSaveFile
                ? "Single Player or Host LAN can continue your saved ranch."
                : "Single Player or Host LAN will begin a new ranch.";

        GUI.Label(
            new Rect(535f, 625f, 530f, 45f),
            saveText,
            statusStyle
        );

        GUI.Label(
            new Rect(400f, 805f, 800f, 38f),
            "Press Enter to start Single Player",
            footerStyle
        );
    }

    private void DrawMultiplayerMenu()
    {
        Rect card = new Rect(390f, 270f, 820f, 500f);
        GUI.Box(card, GUIContent.none, cardStyle);

        GUI.Label(
            new Rect(485f, 300f, 630f, 44f),
            "TWO-PLAYER LAN EXPERIMENT",
            subtitleStyle
        );

        if (GUI.Button(
            new Rect(520f, 370f, 560f, 72f),
            "HOST LAN RANCH",
            primaryButtonStyle))
        {
            BeginLanHost();
            GUIUtility.ExitGUI();
        }

        string hostAddress = multiplayer != null
            ? multiplayer.LocalAddress + ":" + multiplayer.Port
            : "Unavailable";

        GUI.Label(
            new Rect(520f, 448f, 560f, 34f),
            "Your host address: " + hostAddress,
            statusStyle
        );

        GUI.Label(
            new Rect(520f, 505f, 200f, 35f),
            "HOST IPv4 ADDRESS",
            fieldLabelStyle
        );

        joinAddress = GUI.TextField(
            new Rect(720f, 498f, 360f, 46f),
            joinAddress,
            45,
            fieldStyle
        );

        if (GUI.Button(
            new Rect(520f, 565f, 560f, 72f),
            "JOIN LAN RANCH",
            primaryButtonStyle))
        {
            BeginLanClient();
            GUIUtility.ExitGUI();
        }

        if (GUI.Button(
            new Rect(520f, 660f, 270f, 58f),
            "BACK",
            secondaryButtonStyle))
        {
            page = MenuPage.Main;
            menuStatus = "";
            GUIUtility.ExitGUI();
        }

        GUI.Label(
            new Rect(800f, 655f, 280f, 68f),
            string.IsNullOrEmpty(menuStatus)
                ? "Both laptops must be on the same LAN."
                : menuStatus,
            statusStyle
        );

        GUI.Label(
            new Rect(330f, 805f, 940f, 42f),
            "Host runs the ranch and save. Guest movement, enemy views, and basic attacks are synchronized.",
            footerStyle
        );
    }

    private void EnsureStyles()
    {
        if (stylesReady)
            return;

        backgroundTexture = MakeTexture(
            new Color(0.012f, 0.018f, 0.025f, 1f)
        );
        cardTexture = MakeTexture(
            new Color(0.035f, 0.065f, 0.075f, 0.98f)
        );
        accentTexture = MakeTexture(
            new Color(0.10f, 0.58f, 0.43f, 1f)
        );
        accentHoverTexture = MakeTexture(
            new Color(0.14f, 0.72f, 0.52f, 1f)
        );
        accentActiveTexture = MakeTexture(
            new Color(0.07f, 0.44f, 0.33f, 1f)
        );
        secondaryTexture = MakeTexture(
            new Color(0.13f, 0.20f, 0.23f, 1f)
        );
        secondaryHoverTexture = MakeTexture(
            new Color(0.18f, 0.29f, 0.32f, 1f)
        );
        lineTexture = MakeTexture(
            new Color(0.10f, 0.58f, 0.43f, 1f)
        );

        backgroundStyle = new GUIStyle(GUI.skin.box);
        backgroundStyle.normal.background = backgroundTexture;

        cardStyle = new GUIStyle(GUI.skin.box);
        cardStyle.normal.background = cardTexture;

        titleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 68,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        titleStyle.normal.textColor = Color.white;

        subtitleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 24,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        subtitleStyle.normal.textColor =
            new Color(0.55f, 0.90f, 0.75f, 1f);

        primaryButtonStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 27,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        primaryButtonStyle.normal.background = accentTexture;
        primaryButtonStyle.hover.background = accentHoverTexture;
        primaryButtonStyle.active.background = accentActiveTexture;
        primaryButtonStyle.normal.textColor = Color.white;
        primaryButtonStyle.hover.textColor = Color.white;
        primaryButtonStyle.active.textColor = Color.white;

        secondaryButtonStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 24,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        secondaryButtonStyle.normal.background = secondaryTexture;
        secondaryButtonStyle.hover.background = secondaryHoverTexture;
        secondaryButtonStyle.active.background = secondaryTexture;
        secondaryButtonStyle.normal.textColor = Color.white;
        secondaryButtonStyle.hover.textColor = Color.white;
        secondaryButtonStyle.active.textColor = Color.white;

        statusStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 17,
            alignment = TextAnchor.MiddleCenter,
            wordWrap = true
        };
        statusStyle.normal.textColor =
            new Color(0.78f, 0.82f, 0.84f, 1f);

        footerStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 16,
            alignment = TextAnchor.MiddleCenter,
            wordWrap = true
        };
        footerStyle.normal.textColor =
            new Color(0.55f, 0.60f, 0.63f, 1f);

        fieldStyle = new GUIStyle(GUI.skin.textField)
        {
            fontSize = 21,
            alignment = TextAnchor.MiddleLeft
        };

        fieldLabelStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 15,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleLeft
        };
        fieldLabelStyle.normal.textColor = Color.white;

        stylesReady = true;
    }

    private static Texture2D MakeTexture(Color color)
    {
        Texture2D texture = new Texture2D(1, 1);
        texture.SetPixel(0, 0, color);
        texture.Apply();
        return texture;
    }
}
