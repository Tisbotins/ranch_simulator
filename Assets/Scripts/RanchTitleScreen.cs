using UnityEngine;

/// <summary>
/// Runtime-generated title screen with Single Player, LAN, direct-online, and
/// relay-online multiplayer modes. RanchGameBootstrap adds it automatically.
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
    private const string DirectPlaceholder = "public-ip-or-dns:7777";
    private const string RelayPlaceholder = "relay-host-or-ip:7778";

    public bool IsOpen { get; private set; }

    private RanchGameCore core;
    private RanchLanMultiplayer multiplayer;
    private float previousTimeScale = 1f;
    private bool startingGame;
    private MenuPage page;
    private string joinAddress = "127.0.0.1";
    private string onlineJoinAddress = DirectPlaceholder;
    private string relayAddress = RelayPlaceholder;
    private string relayRoomCode = "RANCH";
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
        BeginMultiplayerHost(false);
    }

    private void BeginOnlineHost()
    {
        BeginMultiplayerHost(true);
    }

    private void BeginRelayHost()
    {
        if (!CanStart())
            return;

        if (IsPlaceholder(relayAddress, RelayPlaceholder))
        {
            menuStatus = "Enter the relay server address first.";
            return;
        }

        if (multiplayer == null ||
            !multiplayer.StartRelayHost(relayAddress, relayRoomCode))
        {
            menuStatus = multiplayer != null
                ? multiplayer.StatusText
                : "Multiplayer system is missing.";
            return;
        }

        startingGame = true;
        bool loaded = core.Save != null && core.Save.LoadOnStartup();

        EnterGame(
            (loaded ? "Host save loaded. " : "New host ranch started. ") +
            "Relay host started. Give your friend the relay address and room code."
        );
    }

    private void BeginMultiplayerHost(bool onlineDirect)
    {
        if (!CanStart())
            return;

        if (multiplayer == null || !multiplayer.StartHost(onlineDirect))
        {
            menuStatus = onlineDirect
                ? "Could not start the online host."
                : "Could not start the LAN host.";
            return;
        }

        startingGame = true;
        bool loaded = core.Save != null && core.Save.LoadOnStartup();

        EnterGame(
            (loaded ? "Host save loaded. " : "New host ranch started. ") +
            (onlineDirect
                ? "Forward TCP port 7777, then give your friend your public IP."
                : "Give your friend the LAN address shown in the top-right corner.")
        );
    }

    private void BeginLanClient()
    {
        BeginMultiplayerClient(false);
    }

    private void BeginOnlineClient()
    {
        BeginMultiplayerClient(true);
    }

    private void BeginRelayClient()
    {
        if (!CanStart())
            return;

        if (IsPlaceholder(relayAddress, RelayPlaceholder))
        {
            menuStatus = "Enter the relay server address first.";
            return;
        }

        if (multiplayer == null ||
            !multiplayer.StartRelayClient(relayAddress, relayRoomCode))
        {
            menuStatus = multiplayer != null
                ? multiplayer.StatusText
                : "Multiplayer system is missing.";
            return;
        }

        startingGame = true;
        EnterGame(
            "Connecting as a relay guest. No router port forwarding is needed."
        );
    }

    private void BeginMultiplayerClient(bool onlineDirect)
    {
        if (!CanStart())
            return;

        string address = onlineDirect ? onlineJoinAddress : joinAddress;
        if (onlineDirect && IsPlaceholder(address, DirectPlaceholder))
        {
            menuStatus = "Replace the placeholder with the host's public IP or DNS name.";
            return;
        }

        if (multiplayer == null ||
            !multiplayer.StartClient(address, onlineDirect))
        {
            menuStatus = multiplayer != null
                ? multiplayer.StatusText
                : "Multiplayer system is missing.";
            return;
        }

        startingGame = true;
        EnterGame(
            onlineDirect
                ? "Connecting as a direct-online guest. The host controls the ranch and save file."
                : "Connecting as a LAN guest. The host controls the ranch and save file."
        );
    }

    private bool CanStart()
    {
        return IsOpen && !startingGame && core != null;
    }

    private static bool IsPlaceholder(string value, string placeholder)
    {
        return string.IsNullOrWhiteSpace(value) ||
            value.Trim() == placeholder;
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
            "MULTIPLAYER",
            secondaryButtonStyle))
        {
            page = MenuPage.Multiplayer;
            menuStatus = "";
            GUIUtility.ExitGUI();
        }

        string saveText =
            core.Save != null && core.Save.HasSaveFile
                ? "Single Player or Host can continue your saved ranch."
                : "Single Player or Host will begin a new ranch.";

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
        Rect card = new Rect(220f, 240f, 1160f, 565f);
        GUI.Box(card, GUIContent.none, cardStyle);

        GUI.Label(
            new Rect(365f, 270f, 870f, 44f),
            "TWO-PLAYER DIRECT MULTIPLAYER",
            subtitleStyle
        );

        if (GUI.Button(
            new Rect(285f, 345f, 300f, 64f),
            "HOST LAN RANCH",
            primaryButtonStyle))
        {
            BeginLanHost();
            GUIUtility.ExitGUI();
        }

        if (GUI.Button(
            new Rect(650f, 345f, 300f, 64f),
            "HOST DIRECT",
            primaryButtonStyle))
        {
            BeginOnlineHost();
            GUIUtility.ExitGUI();
        }

        if (GUI.Button(
            new Rect(1015f, 345f, 300f, 64f),
            "HOST RELAY",
            primaryButtonStyle))
        {
            BeginRelayHost();
            GUIUtility.ExitGUI();
        }

        string hostAddress = multiplayer != null
            ? multiplayer.LocalAddress + ":" + multiplayer.Port
            : "Unavailable";

        GUI.Label(
            new Rect(285f, 415f, 300f, 48f),
            "LAN host address: " + hostAddress,
            statusStyle
        );

        GUI.Label(
            new Rect(650f, 415f, 300f, 48f),
            "Direct online still needs TCP 7777 port forwarding.",
            statusStyle
        );

        GUI.Label(
            new Rect(1015f, 415f, 300f, 48f),
            "Relay online needs no router changes.",
            statusStyle
        );

        GUI.Label(
            new Rect(285f, 485f, 110f, 35f),
            "LAN HOST",
            fieldLabelStyle
        );

        joinAddress = GUI.TextField(
            new Rect(395f, 478f, 190f, 46f),
            joinAddress,
            45,
            fieldStyle
        );

        if (GUI.Button(
            new Rect(285f, 535f, 300f, 64f),
            "JOIN LAN RANCH",
            primaryButtonStyle))
        {
            BeginLanClient();
            GUIUtility.ExitGUI();
        }

        GUI.Label(
            new Rect(650f, 485f, 105f, 35f),
            "PUBLIC",
            fieldLabelStyle
        );

        onlineJoinAddress = GUI.TextField(
            new Rect(755f, 478f, 195f, 46f),
            onlineJoinAddress,
            64,
            fieldStyle
        );

        if (GUI.Button(
            new Rect(650f, 535f, 300f, 64f),
            "JOIN DIRECT",
            primaryButtonStyle))
        {
            BeginOnlineClient();
            GUIUtility.ExitGUI();
        }

        GUI.Label(
            new Rect(1015f, 475f, 105f, 28f),
            "RELAY",
            fieldLabelStyle
        );

        relayAddress = GUI.TextField(
            new Rect(1120f, 470f, 195f, 42f),
            relayAddress,
            64,
            fieldStyle
        );

        GUI.Label(
            new Rect(1015f, 520f, 105f, 28f),
            "ROOM",
            fieldLabelStyle
        );

        relayRoomCode = GUI.TextField(
            new Rect(1120f, 515f, 195f, 42f),
            relayRoomCode,
            24,
            fieldStyle
        );

        if (GUI.Button(
            new Rect(1015f, 575f, 300f, 64f),
            "JOIN RELAY",
            primaryButtonStyle))
        {
            BeginRelayClient();
            GUIUtility.ExitGUI();
        }

        if (GUI.Button(
            new Rect(285f, 680f, 260f, 58f),
            "BACK",
            secondaryButtonStyle))
        {
            page = MenuPage.Main;
            menuStatus = "";
            GUIUtility.ExitGUI();
        }

        GUI.Label(
            new Rect(570f, 660f, 745f, 90f),
            string.IsNullOrEmpty(menuStatus)
                ? "Use Relay Online when you do not want to port forward. Both players connect outward to the same relay room."
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
