using UnityEngine;

[DefaultExecutionOrder(-500)]
public class RanchSettingsSystem : MonoBehaviour
{
    public bool IsOpen { get; private set; }
    public bool HudVisible { get; private set; } = true;

    public bool ResourcesPanelVisible => resourcesPanelVisible;
    public bool PlayerStatusVisible => playerStatusVisible;
    public bool WavePanelVisible => wavePanelVisible;
    public bool CJPanelVisible => cjPanelVisible;
    public bool EquipmentSlotsVisible => equipmentSlotsVisible;
    public bool MessagesVisible => messagesVisible;
    public bool InteractionPromptVisible => interactionPromptVisible;

    private const string HudPreferenceKey = "RanchSimulatorHudVisible";
    private const string ResourcesPreferenceKey = "RanchSimulatorHudResources";
    private const string PlayerStatusPreferenceKey = "RanchSimulatorHudPlayerStatus";
    private const string WavePreferenceKey = "RanchSimulatorHudWaves";
    private const string CJPreferenceKey = "RanchSimulatorHudCJ";
    private const string EquipmentPreferenceKey = "RanchSimulatorHudEquipment";
    private const string MessagesPreferenceKey = "RanchSimulatorHudMessages";
    private const string PromptPreferenceKey = "RanchSimulatorHudPrompt";

    private const float VirtualWidth = 1600f;
    private const float VirtualHeight = 900f;

    private RanchGameCore core;
    private RanchPlayerController player;
    private RanchTitleScreen titleScreen;
    private float previousTimeScale = 1f;

    private bool resourcesPanelVisible = true;
    private bool playerStatusVisible = true;
    private bool wavePanelVisible = true;
    private bool cjPanelVisible = true;
    private bool equipmentSlotsVisible = true;
    private bool messagesVisible = true;
    private bool interactionPromptVisible = true;

    private Texture2D backgroundTexture;
    private Texture2D cardTexture;
    private Texture2D buttonTexture;
    private Texture2D toggleOnTexture;
    private Texture2D toggleOffTexture;
    private GUIStyle backgroundStyle;
    private GUIStyle cardStyle;
    private GUIStyle titleStyle;
    private GUIStyle sectionTitleStyle;
    private GUIStyle bodyStyle;
    private GUIStyle buttonStyle;
    private GUIStyle toggleOnStyle;
    private GUIStyle toggleOffStyle;
    private bool stylesReady;

    public void Initialize(RanchGameCore gameCore)
    {
        core = gameCore;
        titleScreen = GetComponent<RanchTitleScreen>();

        HudVisible = LoadPreference(HudPreferenceKey);
        resourcesPanelVisible = LoadPreference(ResourcesPreferenceKey);
        playerStatusVisible = LoadPreference(PlayerStatusPreferenceKey);
        wavePanelVisible = LoadPreference(WavePreferenceKey);
        cjPanelVisible = LoadPreference(CJPreferenceKey);
        equipmentSlotsVisible = LoadPreference(EquipmentPreferenceKey);
        messagesVisible = LoadPreference(MessagesPreferenceKey);
        interactionPromptVisible = LoadPreference(PromptPreferenceKey);
    }

    public void RegisterPlayer(RanchPlayerController playerController)
    {
        player = playerController;
    }

    private void Update()
    {
        if (core == null)
            return;

        // The title screen owns keyboard and mouse input until Single Player starts.
        if (titleScreen != null && titleScreen.IsOpen)
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
            (core.Classes == null || !core.Classes.IsOpen) &&
            (core.Laboratory == null || !core.Laboratory.IsOpen) &&
            !core.GameWon &&
            !core.Health.IsDead)
        {
            OpenMenu();
        }
    }

    public void ToggleHud()
    {
        HudVisible = !HudVisible;
        SavePreference(HudPreferenceKey, HudVisible);

        if (core != null)
        {
            core.ShowMessage(
                HudVisible
                    ? "HUD enabled. Individual HUD elements can be changed in Settings."
                    : "HUD hidden. Press H to show it again.",
                3f
            );
        }
    }

    public void OpenMenu()
    {
        if (IsOpen || core == null || core.Shop.IsOpen || core.Progression.IsOpen ||
            (core.Classes != null && core.Classes.IsOpen) ||
            (core.Laboratory != null && core.Laboratory.IsOpen) ||
            (titleScreen != null && titleScreen.IsOpen))
        {
            return;
        }

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

        if (player != null && !core.Health.IsDead && !core.GameWon &&
            (titleScreen == null || !titleScreen.IsOpen))
        {
            player.enabled = true;
        }

        if (titleScreen != null && titleScreen.IsOpen)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    private void OnGUI()
    {
        if (!IsOpen || core == null || (titleScreen != null && titleScreen.IsOpen))
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
        GUI.Label(new Rect(140f, 80f, 1320f, 58f), "SETTINGS & CONTROLS", titleStyle);

        // Controls card.
        GUI.Box(new Rect(140f, 160f, 570f, 575f), GUIContent.none, cardStyle);
        GUI.Label(new Rect(170f, 180f, 510f, 34f), "CONTROLS", sectionTitleStyle);
        GUI.Label(new Rect(170f, 225f, 510f, 485f), BuildControlsText(), bodyStyle);

        // Quick actions card.
        GUI.Box(new Rect(750f, 160f, 710f, 205f), GUIContent.none, cardStyle);
        GUI.Label(new Rect(780f, 180f, 650f, 34f), "QUICK ACTIONS", sectionTitleStyle);
        GUI.Label(
            new Rect(780f, 216f, 650f, 30f),
            "Save status: " + core.Save.LastSaveStatus,
            bodyStyle
        );

        if (GUI.Button(
            new Rect(780f, 270f, 145f, 58f),
            HudVisible ? "HUD: ON [H]" : "HUD: OFF [H]",
            HudVisible ? toggleOnStyle : toggleOffStyle))
        {
            ToggleHud();
        }

        if (GUI.Button(new Rect(945f, 270f, 145f, 58f), "QUICK SAVE", buttonStyle))
            core.Save.SaveGame(true);

        if (GUI.Button(new Rect(1110f, 270f, 145f, 58f), "QUICK LOAD", buttonStyle))
            core.Save.LoadGame(true);

        if (GUI.Button(new Rect(1275f, 270f, 145f, 58f), "UNSTUCK", buttonStyle))
        {
            player?.ReturnToSafePosition();
            core.ShowMessage("Returned to the last safe position.");
        }

        // Individual HUD element controls.
        GUI.Box(new Rect(750f, 390f, 710f, 345f), GUIContent.none, cardStyle);
        GUI.Label(new Rect(780f, 410f, 650f, 34f), "HUD ELEMENTS", sectionTitleStyle);
        GUI.Label(
            new Rect(780f, 445f, 650f, 28f),
            "These choices are remembered between game sessions.",
            bodyStyle
        );

        if (DrawToggleButton(new Rect(780f, 485f, 300f, 52f), "RESOURCES", resourcesPanelVisible))
            ToggleElement(ref resourcesPanelVisible, ResourcesPreferenceKey, "Resources panel");

        if (DrawToggleButton(new Rect(1120f, 485f, 300f, 52f), "HEALTH & STAMINA", playerStatusVisible))
            ToggleElement(ref playerStatusVisible, PlayerStatusPreferenceKey, "Health and stamina");

        if (DrawToggleButton(new Rect(780f, 550f, 300f, 52f), "WAVE STATUS", wavePanelVisible))
            ToggleElement(ref wavePanelVisible, WavePreferenceKey, "Wave status");

        if (DrawToggleButton(new Rect(1120f, 550f, 300f, 52f), "CJ PROGRESS", cjPanelVisible))
            ToggleElement(ref cjPanelVisible, CJPreferenceKey, "CJ progress");

        if (DrawToggleButton(new Rect(780f, 615f, 300f, 52f), "EQUIPMENT SLOTS", equipmentSlotsVisible))
            ToggleElement(ref equipmentSlotsVisible, EquipmentPreferenceKey, "Equipment slots");

        if (DrawToggleButton(new Rect(1120f, 615f, 300f, 52f), "MESSAGES", messagesVisible))
            ToggleElement(ref messagesVisible, MessagesPreferenceKey, "Messages");

        if (DrawToggleButton(new Rect(780f, 680f, 300f, 52f), "INTERACTION PROMPT", interactionPromptVisible))
            ToggleElement(ref interactionPromptVisible, PromptPreferenceKey, "Interaction prompt");

        if (GUI.Button(new Rect(1120f, 680f, 300f, 52f), "RESET HUD ELEMENTS", buttonStyle))
            ResetHudElements();

        if (GUI.Button(new Rect(600f, 765f, 400f, 58f), "RESUME [ESC]", buttonStyle))
        {
            CloseMenu();
            GUIUtility.ExitGUI();
        }

        GUI.matrix = oldMatrix;
    }

    private bool DrawToggleButton(Rect rect, string label, bool isVisible)
    {
        return GUI.Button(
            rect,
            label + (isVisible ? ": ON" : ": OFF"),
            isVisible ? toggleOnStyle : toggleOffStyle
        );
    }

    private void ToggleElement(ref bool field, string preferenceKey, string displayName)
    {
        field = !field;
        SavePreference(preferenceKey, field);

        if (core != null)
            core.ShowMessage(displayName + (field ? " shown." : " hidden."), 2.5f);
    }

    private void ResetHudElements()
    {
        resourcesPanelVisible = true;
        playerStatusVisible = true;
        wavePanelVisible = true;
        cjPanelVisible = true;
        equipmentSlotsVisible = true;
        messagesVisible = true;
        interactionPromptVisible = true;
        HudVisible = true;

        SavePreference(HudPreferenceKey, true, false);
        SavePreference(ResourcesPreferenceKey, true, false);
        SavePreference(PlayerStatusPreferenceKey, true, false);
        SavePreference(WavePreferenceKey, true, false);
        SavePreference(CJPreferenceKey, true, false);
        SavePreference(EquipmentPreferenceKey, true, false);
        SavePreference(MessagesPreferenceKey, true, false);
        SavePreference(PromptPreferenceKey, true, false);
        PlayerPrefs.Save();

        if (core != null)
            core.ShowMessage("All HUD elements restored.", 2.5f);
    }

    private string BuildControlsText()
    {
        return
            "MOVEMENT\n" +
            "WASD — Move\n" +
            "Mouse — Look\n" +
            "Left Control — Dodge\n\n" +
            "CLASS & EQUIPMENT\n" +
            "E at Dr. Oakberry — Change class\n" +
            "1 — Extractor\n" +
            "2 — Class weapon / Summoner wand\n" +
            "3 — Ranch Trap\n\n" +
            "COMBAT\n" +
            "Left Click / Space — Light attack\n" +
            "Q — Heavy attack\n" +
            "Right Click — Block / perfect block\n" +
            "Ranged attacks fire traveling projectiles\n" +
            "Left Click / F — Use selected trap or wand\n\n" +
            "RANCHING & RESEARCH\n" +
            "E — Interact / hold to extract\n" +
            "Shift + E — Instant bottle / sell all\n" +
            "[ and ] — Change bottle size\n" +
            "E at Ranch Laboratory — Production research\n\n" +
            "MENUS\n" +
            "P — Ranch shop\n" +
            "K — Current class weapon tree\n" +
            "Escape / F1 — Settings\n" +
            "H — Toggle entire HUD\n" +
            "Z — Quick save | X — Quick load";
    }

    private void EnsureStyles()
    {
        if (stylesReady)
            return;

        backgroundTexture = MakeTexture(new Color(0.015f, 0.02f, 0.03f, 0.99f));
        cardTexture = MakeTexture(new Color(0.06f, 0.09f, 0.13f, 0.99f));
        buttonTexture = MakeTexture(new Color(0.10f, 0.42f, 0.52f, 1f));
        toggleOnTexture = MakeTexture(new Color(0.10f, 0.58f, 0.43f, 1f));
        toggleOffTexture = MakeTexture(new Color(0.19f, 0.22f, 0.25f, 1f));

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

        sectionTitleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 22,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        sectionTitleStyle.normal.textColor = Color.white;

        bodyStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 17,
            wordWrap = true,
            alignment = TextAnchor.UpperLeft
        };
        bodyStyle.normal.textColor = Color.white;

        buttonStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 16,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        buttonStyle.normal.background = buttonTexture;
        buttonStyle.normal.textColor = Color.white;
        buttonStyle.hover.textColor = Color.white;
        buttonStyle.active.textColor = Color.white;

        toggleOnStyle = new GUIStyle(buttonStyle);
        toggleOnStyle.normal.background = toggleOnTexture;
        toggleOnStyle.hover.background = toggleOnTexture;
        toggleOnStyle.active.background = toggleOnTexture;

        toggleOffStyle = new GUIStyle(buttonStyle);
        toggleOffStyle.normal.background = toggleOffTexture;
        toggleOffStyle.hover.background = toggleOffTexture;
        toggleOffStyle.active.background = toggleOffTexture;
        toggleOffStyle.normal.textColor = new Color(0.75f, 0.78f, 0.80f, 1f);

        stylesReady = true;
    }

    private static bool LoadPreference(string key)
    {
        return PlayerPrefs.GetInt(key, 1) == 1;
    }

    private static void SavePreference(string key, bool value, bool saveImmediately = true)
    {
        PlayerPrefs.SetInt(key, value ? 1 : 0);
        if (saveImmediately)
            PlayerPrefs.Save();
    }

    private static Texture2D MakeTexture(Color color)
    {
        Texture2D texture = new Texture2D(1, 1);
        texture.SetPixel(0, 0, color);
        texture.Apply();
        return texture;
    }
}
