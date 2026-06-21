using UnityEngine;

[DefaultExecutionOrder(-500)]
public class RanchSettingsSystem : MonoBehaviour
{
    public bool IsOpen { get; private set; }
    public bool HudVisible { get; private set; } = true;

    private const string HudPreferenceKey = "RanchSimulatorHudVisible";
    private const float VirtualWidth = 1600f;
    private const float VirtualHeight = 900f;

    private RanchGameCore core;
    private RanchPlayerController player;
    private float previousTimeScale = 1f;

    private Texture2D backgroundTexture;
    private Texture2D cardTexture;
    private Texture2D buttonTexture;
    private GUIStyle backgroundStyle;
    private GUIStyle cardStyle;
    private GUIStyle titleStyle;
    private GUIStyle bodyStyle;
    private GUIStyle buttonStyle;
    private bool stylesReady;

    public void Initialize(RanchGameCore gameCore)
    {
        core = gameCore;
        HudVisible = PlayerPrefs.GetInt(HudPreferenceKey, 1) == 1;
    }

    public void RegisterPlayer(RanchPlayerController playerController)
    {
        player = playerController;
    }

    private void Update()
    {
        if (core == null)
            return;

        if (Input.GetKeyDown(KeyCode.H))
            ToggleHud();

        if (IsOpen)
        {
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.F1))
                CloseMenu();
            return;
        }

        if ((Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.F1)) &&
            !core.Shop.IsOpen &&
            !core.Progression.IsOpen &&
            !core.GameWon &&
            !core.Health.IsDead)
        {
            OpenMenu();
        }
    }

    public void ToggleHud()
    {
        HudVisible = !HudVisible;
        PlayerPrefs.SetInt(HudPreferenceKey, HudVisible ? 1 : 0);
        PlayerPrefs.Save();

        if (core != null)
            core.ShowMessage(HudVisible ? "HUD enabled." : "HUD hidden. Press H to show it again.");
    }

    public void OpenMenu()
    {
        if (IsOpen || core == null || core.Shop.IsOpen || core.Progression.IsOpen)
            return;

        IsOpen = true;
        previousTimeScale = Time.timeScale;
        Time.timeScale = 0f;

        if (player != null)
            player.enabled = false;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void CloseMenu()
    {
        if (!IsOpen)
            return;

        IsOpen = false;
        Time.timeScale = previousTimeScale <= 0f ? 1f : previousTimeScale;

        if (player != null && !core.Health.IsDead && !core.GameWon)
            player.enabled = true;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void OnGUI()
    {
        if (!IsOpen || core == null)
            return;

        EnsureStyles();

        Matrix4x4 oldMatrix = GUI.matrix;
        float scale = Mathf.Min(Screen.width / VirtualWidth, Screen.height / VirtualHeight);
        GUI.matrix = Matrix4x4.TRS(
            new Vector3(
                (Screen.width - VirtualWidth * scale) * 0.5f,
                (Screen.height - VirtualHeight * scale) * 0.5f,
                0f
            ),
            Quaternion.identity,
            new Vector3(scale, scale, 1f)
        );

        GUI.Box(new Rect(90f, 55f, 1420f, 790f), GUIContent.none, backgroundStyle);
        GUI.Label(new Rect(140f, 85f, 1320f, 60f), "SETTINGS & CONTROLS", titleStyle);

        GUI.Box(new Rect(140f, 175f, 640f, 560f), GUIContent.none, cardStyle);
        GUI.Label(new Rect(175f, 200f, 570f, 485f), BuildControlsText(), bodyStyle);

        GUI.Box(new Rect(825f, 175f, 635f, 560f), GUIContent.none, cardStyle);
        GUI.Label(
            new Rect(860f, 200f, 565f, 110f),
            "QUICK ACTIONS\n\nSave status: " + core.Save.LastSaveStatus,
            bodyStyle
        );

        if (GUI.Button(new Rect(870f, 325f, 250f, 62f), HudVisible ? "HIDE HUD [H]" : "SHOW HUD [H]", buttonStyle))
            ToggleHud();

        if (GUI.Button(new Rect(1160f, 325f, 250f, 62f), "QUICK SAVE", buttonStyle))
            core.Save.SaveGame(true);

        if (GUI.Button(new Rect(870f, 415f, 250f, 62f), "QUICK LOAD", buttonStyle))
            core.Save.LoadGame(true);

        if (GUI.Button(new Rect(1160f, 415f, 250f, 62f), "UNSTUCK", buttonStyle))
        {
            player?.ReturnToSafePosition();
            core.ShowMessage("Returned to the last safe position.");
        }

        GUI.Label(
            new Rect(875f, 510f, 530f, 150f),
            "The game autosaves after progression changes and inventory changes.\n\nThe Unstuck button returns you to your most recent safe grounded position.",
            bodyStyle
        );

        if (GUI.Button(new Rect(600f, 760f, 400f, 58f), "RESUME [ESC]", buttonStyle))
        {
            CloseMenu();
            GUIUtility.ExitGUI();
        }

        GUI.matrix = oldMatrix;
    }

    private string BuildControlsText()
    {
        return
            "MOVEMENT\n" +
            "WASD — Move\n" +
            "Mouse — Look\n" +
            "Left Control — Dodge\n\n" +
            "THREE-SLOT INVENTORY\n" +
            "1 — Extractor\n" +
            "2 — Equipped weapon\n" +
            "3 — Extra slot\n\n" +
            "COMBAT\n" +
            "Left Click / Space — Light attack\n" +
            "Q — Heavy attack\n" +
            "Right Click — Block / perfect block\n\n" +
            "RANCHING\n" +
            "E — Interact / hold to extract\n" +
            "Shift + E — Instant bottle / sell all\n" +
            "[ and ] — Change bottle size\n\n" +
            "MENUS\n" +
            "P — Ranch shop\n" +
            "K — Progression\n" +
            "Escape / F1 — Settings\n" +
            "H — Toggle HUD\n" +
            "Z — Quick save | X — Quick load";
    }

    private void EnsureStyles()
    {
        if (stylesReady)
            return;

        backgroundTexture = MakeTexture(new Color(0.015f, 0.02f, 0.03f, 0.99f));
        cardTexture = MakeTexture(new Color(0.06f, 0.09f, 0.13f, 0.99f));
        buttonTexture = MakeTexture(new Color(0.10f, 0.42f, 0.52f, 1f));

        backgroundStyle = new GUIStyle(GUI.skin.box);
        backgroundStyle.normal.background = backgroundTexture;

        cardStyle = new GUIStyle(GUI.skin.box);
        cardStyle.normal.background = cardTexture;

        titleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 36,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        titleStyle.normal.textColor = Color.white;

        bodyStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 19,
            wordWrap = true,
            alignment = TextAnchor.UpperLeft
        };
        bodyStyle.normal.textColor = Color.white;

        buttonStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 19,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        buttonStyle.normal.background = buttonTexture;
        buttonStyle.normal.textColor = Color.white;
        buttonStyle.hover.textColor = Color.white;
        buttonStyle.active.textColor = Color.white;

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
