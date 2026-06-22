using UnityEngine;

/// <summary>
/// Runtime-generated title screen. RanchGameBootstrap adds this automatically,
/// so no extra scene objects or components are required.
/// </summary>
[DefaultExecutionOrder(9000)]
[DisallowMultipleComponent]
public class RanchTitleScreen : MonoBehaviour
{
    private const float VirtualWidth = 1600f;
    private const float VirtualHeight = 900f;

    public bool IsOpen { get; private set; }

    private RanchGameCore core;
    private float previousTimeScale = 1f;
    private bool startingGame;

    private Texture2D backgroundTexture;
    private Texture2D cardTexture;
    private Texture2D accentTexture;
    private Texture2D accentHoverTexture;
    private Texture2D accentActiveTexture;
    private Texture2D disabledTexture;
    private Texture2D lineTexture;

    private GUIStyle backgroundStyle;
    private GUIStyle cardStyle;
    private GUIStyle titleStyle;
    private GUIStyle subtitleStyle;
    private GUIStyle primaryButtonStyle;
    private GUIStyle disabledButtonStyle;
    private GUIStyle comingSoonStyle;
    private GUIStyle statusStyle;
    private GUIStyle footerStyle;
    private bool stylesReady;

    public void Initialize(RanchGameCore gameCore)
    {
        core = gameCore;
    }

    public void Open()
    {
        if (core == null || IsOpen)
            return;

        IsOpen = true;
        startingGame = false;
        previousTimeScale = Time.timeScale <= 0f ? 1f : Time.timeScale;
        HoldGameAtTitle();
    }

    private void Update()
    {
        if (!IsOpen)
            return;

        // Enter is a keyboard shortcut for the Single Player button.
        if (Input.GetKeyDown(KeyCode.Return) ||
            Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            BeginSinglePlayer();
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

        // Close any gameplay menus that may have received a key press
        // while the title screen was open.
        if (core.Settings != null && core.Settings.IsOpen)
            core.Settings.CloseMenu();

        if (core.Shop != null && core.Shop.IsOpen)
            core.Shop.CloseShop();

        if (core.Progression != null && core.Progression.IsOpen)
            core.Progression.CloseMenu();

        Time.timeScale = 0f;

        if (core.Player != null)
            core.Player.enabled = false;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void BeginSinglePlayer()
    {
        if (!IsOpen || startingGame || core == null)
            return;

        startingGame = true;

        // Loading happens only after the player chooses Single Player.
        bool loaded = core.Save != null && core.Save.LoadOnStartup();

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

        core.ShowMessage(
            loaded
                ? "Save loaded. Press Escape for settings, H for HUD, Z to save, and X to load."
                : "Welcome to Ranch Simulator. Build your empire, unlock new areas, and survive 30 waves.",
            9f
        );
    }

    private void OnGUI()
    {
        if (!IsOpen || core == null)
            return;

        EnsureStyles();

        int oldDepth = GUI.depth;
        Matrix4x4 oldMatrix = GUI.matrix;

        // Lower GUI depth draws this screen above the other runtime UI.
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
            new Rect(180f, 110f, 1240f, 110f),
            "RANCH SIMULATOR",
            titleStyle
        );

        GUI.Label(
            new Rect(250f, 215f, 1100f, 48f),
            "BUILD  •  BOTTLE  •  DEFEND",
            subtitleStyle
        );

        Rect card = new Rect(455f, 305f, 690f, 440f);
        GUI.Box(card, GUIContent.none, cardStyle);

        if (GUI.Button(
            new Rect(570f, 385f, 460f, 82f),
            "SINGLE PLAYER",
            primaryButtonStyle))
        {
            BeginSinglePlayer();
            GUIUtility.ExitGUI();
        }

        GUI.Box(
            new Rect(570f, 505f, 460f, 82f),
            "MULTIPLAYER",
            disabledButtonStyle
        );

        GUI.Label(
            new Rect(570f, 588f, 460f, 40f),
            "COMING SOON",
            comingSoonStyle
        );

        string saveText =
            core.Save != null && core.Save.HasSaveFile
                ? "Single Player will continue your saved ranch."
                : "Single Player will begin a new ranch.";

        GUI.Label(
            new Rect(535f, 660f, 530f, 42f),
            saveText,
            statusStyle
        );

        GUI.Label(
            new Rect(400f, 805f, 800f, 38f),
            "Press Enter to start Single Player",
            footerStyle
        );

        GUI.matrix = oldMatrix;
        GUI.depth = oldDepth;
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

        disabledTexture = MakeTexture(
            new Color(0.15f, 0.18f, 0.20f, 1f)
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
            fontSize = 29,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        primaryButtonStyle.normal.background = accentTexture;
        primaryButtonStyle.hover.background = accentHoverTexture;
        primaryButtonStyle.active.background = accentActiveTexture;
        primaryButtonStyle.normal.textColor = Color.white;
        primaryButtonStyle.hover.textColor = Color.white;
        primaryButtonStyle.active.textColor = Color.white;

        disabledButtonStyle = new GUIStyle(GUI.skin.box)
        {
            fontSize = 27,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        disabledButtonStyle.normal.background = disabledTexture;
        disabledButtonStyle.normal.textColor =
            new Color(0.56f, 0.60f, 0.62f, 1f);

        comingSoonStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 18,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        comingSoonStyle.normal.textColor =
            new Color(0.55f, 0.90f, 0.75f, 1f);

        statusStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 17,
            alignment = TextAnchor.MiddleCenter
        };
        statusStyle.normal.textColor =
            new Color(0.78f, 0.82f, 0.84f, 1f);

        footerStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 16,
            alignment = TextAnchor.MiddleCenter
        };
        footerStyle.normal.textColor =
            new Color(0.55f, 0.60f, 0.63f, 1f);

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
