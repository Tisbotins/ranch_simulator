using System.Text;
using UnityEngine;

public class RanchUI : MonoBehaviour
{
    private const float VirtualWidth = 1600f;
    private const float VirtualHeight = 900f;

    private RanchGameCore core;
    private RanchTitleScreen titleScreen;
    private Texture2D panelTexture;
    private Texture2D selectedTexture;
    private Texture2D healthFillTexture;
    private Texture2D healthLostTexture;
    private Texture2D staminaFillTexture;
    private Texture2D waitingTexture;
    private Texture2D activeTexture;
    private Texture2D damageTexture;
    private Texture2D bossFillTexture;
    private GUIStyle panelStyle;
    private GUIStyle selectedPanelStyle;
    private GUIStyle waitingStyle;
    private GUIStyle activeStyle;
    private GUIStyle titleStyle;
    private GUIStyle bodyStyle;
    private GUIStyle smallStyle;
    private GUIStyle centeredStyle;
    private GUIStyle healthStyle;
    private GUIStyle largeStyle;
    private bool stylesReady;
    private bool detailPanelOpen;

    public void Initialize(RanchGameCore gameCore)
    {
        core = gameCore;
        titleScreen = GetComponent<RanchTitleScreen>();
    }

    private void Update()
    {
        if (core == null || core.IsAnyMenuOpen)
            return;

        if (titleScreen != null && titleScreen.IsOpen)
            return;

        if (Input.GetKeyDown(KeyCode.Tab))
            detailPanelOpen = !detailPanelOpen;
    }

    private void OnGUI()
    {
        // Nothing from the gameplay HUD should render over the title screen.
        if (titleScreen != null && titleScreen.IsOpen)
            return;

        // The HUD must never draw over a full-screen menu, including the Ranch
        // Rocket console.
        if (core == null || core.Inventory == null || core.IsAnyMenuOpen)
        {
            return;
        }

        EnsureStyles();

        Matrix4x4 old = GUI.matrix;
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

        if (core.Settings.HudVisible)
        {
            if (core.Settings.ResourcesPanelVisible)
                DrawMainPanel();

            if (core.Settings.PlayerStatusVisible)
                DrawHealthAndStamina();

            if (core.Settings.WavePanelVisible)
                DrawWavePanel();

            if (core.Settings.CJPanelVisible)
                DrawCJGatePanel();

            if (core.Settings.EquipmentSlotsVisible)
                DrawEquipmentSlots();

            if (core.Settings.MessagesVisible)
                DrawMessage();

            if (core.Settings.InteractionPromptVisible)
                DrawPrompt();
        }
        else
        {
            GUI.Box(new Rect(20f, 20f, 240f, 48f), GUIContent.none, panelStyle);
            GUI.Label(new Rect(28f, 24f, 224f, 38f), "HUD OFF — PRESS H", centeredStyle);
        }

        // Damage, death, and victory feedback stay visible for gameplay safety.
        if (core.Health.DamageFlashTime > 0f)
            GUI.DrawTexture(new Rect(0f, 0f, VirtualWidth, VirtualHeight), damageTexture);

        if (core.Health.IsDead)
            DrawDeathScreen();

        if (core.Health.IsDowned)
            DrawDownedScreen();

        if (core.GameWon)
            DrawWinScreen();

        GUI.matrix = old;
    }

    /// <summary>
    /// The compact always-on panel. Only the numbers a player acts on moment to
    /// moment live here; everything else moved behind the Tab detail panel so
    /// the corner of the screen is no longer a wall of text.
    /// </summary>
    private void DrawMainPanel()
    {
        int tier = core.Bottles.SelectedTier;

        StringBuilder text = new StringBuilder();
        text.AppendLine("Raw Ranch   " + core.Inventory.RawRanch.ToString("F1"));
        text.AppendLine("Money       $" + core.Inventory.Money.ToString("F0"));
        text.AppendLine(
            "Bottle      " + core.Bottles.GetTierName(tier) +
            "  x" + core.Inventory.GetBottleCount(tier)
        );

        DrawPanel(new Rect(20f, 20f, 330f, 150f), "RANCH", text.ToString(), bodyStyle);

        GUI.Label(
            new Rect(20f, 174f, 330f, 22f),
            detailPanelOpen ? "TAB — hide details" : "TAB — details",
            smallStyle
        );

        if (detailPanelOpen)
            DrawDetailPanel();
    }

    /// <summary>Everything that used to clutter the permanent HUD.</summary>
    private void DrawDetailPanel()
    {
        StringBuilder text = new StringBuilder();
        text.AppendLine("Total Ranch   " + core.Inventory.TotalRanchCollected.ToString("F0"));
        text.AppendLine("Class         " + core.Classes.CurrentClassName);
        text.AppendLine("Weapon        " + core.Equipment.CurrentWeaponName);
        text.AppendLine(
            "Knowledge     Lv " + core.Progression.Level +
            " — " + core.Progression.CurrentPhaseName
        );
        text.AppendLine(
            "              " + core.Progression.KnowledgePoints + " pt | " +
            core.Progression.Experience.ToString("F0") + "/" +
            core.Progression.ExperienceToNextLevel.ToString("F0")
        );
        text.AppendLine("Tree          " + core.Tree.CurrentStageName);
        text.AppendLine("Empire        " + core.Shop.CurrentStructureName);
        text.AppendLine(
            "Traps         " + core.Deployables.TrapCount + " owned / " +
            core.Deployables.ActiveTrapCount + " placed"
        );
        text.AppendLine(
            "Delulus       " + core.Deployables.ActiveDeluluCount + "/" +
            core.Deployables.CurrentMaxActiveDelulus
        );
        text.AppendLine("CJ Heat       " + core.CJHeat + " — " + core.CJ.GetHeatStatus());
        text.AppendLine("Save          " + core.Save.LastSaveStatus);

        DrawPanel(new Rect(20f, 200f, 400f, 330f), "DETAILS", text.ToString(), smallStyle);
    }

    private void DrawHealthAndStamina()
    {
        Rect panel = new Rect(500f, 20f, 600f, 110f);
        GUI.Box(panel, GUIContent.none, panelStyle);
        GUI.Label(new Rect(515f, 22f, 570f, 25f), "PLAYER STATUS", titleStyle);

        Rect healthBar = new Rect(522f, 51f, 556f, 24f);
        GUI.DrawTexture(healthBar, healthLostTexture);
        float healthPercent = core.Health.MaxHealth <= 0f
            ? 0f
            : Mathf.Clamp01(core.Health.CurrentHealth / core.Health.MaxHealth);
        GUI.DrawTexture(
            new Rect(healthBar.x, healthBar.y, healthBar.width * healthPercent, healthBar.height),
            healthFillTexture
        );
        GUI.Label(
            healthBar,
            "HP " + core.Health.CurrentHealth.ToString("F0") + "/" + core.Health.MaxHealth.ToString("F0") +
            " | Armor " + core.Health.ArmorPercent.ToString("F0") + "%",
            healthStyle
        );

        Rect staminaBar = new Rect(522f, 81f, 556f, 20f);
        GUI.DrawTexture(staminaBar, healthLostTexture);
        float staminaPercent = core.Stamina.MaximumStamina <= 0f
            ? 0f
            : Mathf.Clamp01(core.Stamina.CurrentStamina / core.Stamina.MaximumStamina);
        GUI.DrawTexture(
            new Rect(staminaBar.x, staminaBar.y, staminaBar.width * staminaPercent, staminaBar.height),
            staminaFillTexture
        );
        GUI.Label(
            staminaBar,
            "STAMINA " + core.Stamina.CurrentStamina.ToString("F0") + "/" + core.Stamina.MaximumStamina.ToString("F0"),
            healthStyle
        );
    }

    private void DrawWavePanel()
    {
        DrawPanel(
            new Rect(1170f, 20f, 410f, 260f),
            "RANCH RAIDER WAVES",
            core.Waves.GetStatusText(),
            smallStyle
        );

        if (core.CJ.FinalBattleActive)
        {
            DrawCJFinalBossBar();
            return;
        }

        bool active =
            core.Waves.CurrentState == RanchWaveSystem.WaveState.Spawning ||
            core.Waves.CurrentState == RanchWaveSystem.WaveState.Fighting;

        Rect banner = new Rect(500f, 145f, 600f, 66f);
        GUI.Box(banner, GUIContent.none, active ? activeStyle : waitingStyle);
        GUI.Label(banner, core.Waves.GetBannerText(), centeredStyle);

        if (core.Waves.CurrentState == RanchWaveSystem.WaveState.Intermission &&
            core.Waves.SecondsUntilNextWave <= 5f)
        {
            GUI.Label(
                new Rect(690f, 215f, 220f, 100f),
                Mathf.CeilToInt(core.Waves.SecondsUntilNextWave).ToString(),
                largeStyle
            );
        }
    }

    private void DrawCJFinalBossBar()
    {
        Rect panel = new Rect(470f, 145f, 660f, 105f);
        GUI.Box(panel, GUIContent.none, activeStyle);
        GUI.Label(new Rect(490f, 151f, 620f, 30f), "CJ — ULTIMATE RANCHENATOR", titleStyle);

        Rect bar = new Rect(500f, 190f, 600f, 32f);
        GUI.DrawTexture(bar, healthLostTexture);
        GUI.DrawTexture(
            new Rect(bar.x, bar.y, bar.width * core.CJ.FinalBossHealthPercent, bar.height),
            bossFillTexture
        );

        string healthText = core.CJ.FinalBoss == null
            ? "ENTERING THE ARENA..."
            : "PHASE " + Mathf.Max(1, core.CJ.CurrentPhase) + "/3 — " +
              core.CJ.FinalBoss.Health.ToString("F0") + "/" + core.CJ.FinalBoss.MaxHealth.ToString("F0") + " HP";
        GUI.Label(bar, healthText, healthStyle);
    }

    private void DrawCJGatePanel()
    {
        DrawPanel(
            new Rect(1170f, 300f, 410f, 180f),
            "CJ FINAL BATTLE",
            core.CJ.GetGateStatusText(),
            smallStyle
        );
    }

    private void DrawEquipmentSlots()
    {
        float slotWidth = 215f;
        float gap = 16f;
        float totalWidth = slotWidth * RanchEquipmentSystem.SlotCount + gap * (RanchEquipmentSystem.SlotCount - 1);
        float startX = (VirtualWidth - totalWidth) * 0.5f;
        float y = 745f;

        for (int i = 0; i < RanchEquipmentSystem.SlotCount; i++)
        {
            Rect slot = new Rect(startX + i * (slotWidth + gap), y, slotWidth, 72f);
            GUI.Box(slot, GUIContent.none, core.Equipment.ActiveSlot == i ? selectedPanelStyle : panelStyle);
            GUI.Label(
                new Rect(slot.x + 8f, slot.y + 5f, slot.width - 16f, slot.height - 10f),
                (i + 1) + "\n" + core.Equipment.GetSlotName(i),
                centeredStyle
            );
        }
    }

    private void DrawMessage()
    {
        if (string.IsNullOrWhiteSpace(core.StatusMessage) || core.StatusMessageTime <= 0f)
            return;

        Rect rect = new Rect(20f, 630f, 560f, 100f);
        GUI.Box(rect, GUIContent.none, panelStyle);
        GUI.Label(
            new Rect(rect.x + 16f, rect.y + 12f, rect.width - 32f, rect.height - 24f),
            core.StatusMessage,
            smallStyle
        );
    }

    private void DrawPrompt()
    {
        if (core.Player == null || string.IsNullOrWhiteSpace(core.Player.CurrentPrompt) ||
            core.Health.IsDead || core.GameWon)
        {
            return;
        }

        Rect rect = new Rect(380f, 835f, 840f, 48f);
        GUI.Box(rect, GUIContent.none, panelStyle);
        GUI.Label(
            new Rect(rect.x + 12f, rect.y + 4f, rect.width - 24f, rect.height - 8f),
            core.Player.CurrentPrompt,
            centeredStyle
        );
    }

    private void DrawDeathScreen()
    {
        Rect rect = new Rect(390f, 260f, 820f, 360f);
        GUI.Box(rect, GUIContent.none, panelStyle);
        GUI.Label(new Rect(rect.x + 30f, rect.y + 45f, rect.width - 60f, 70f), "YOU WERE RANCHED", largeStyle);
        GUI.Label(
            new Rect(rect.x + 90f, rect.y + 145f, rect.width - 180f, 150f),
            "The Ranch Raiders overwhelmed you.\n\nUse weapons, blocking, dodges, health upgrades, and area progression.\n\nPress R to restart from your latest save.",
            centeredStyle
        );
    }

    private void DrawDownedScreen()
    {
        Rect rect = new Rect(390f, 280f, 820f, 320f);
        GUI.Box(rect, GUIContent.none, panelStyle);
        GUI.Label(new Rect(rect.x + 30f, rect.y + 40f, rect.width - 60f, 70f), "YOU ARE DOWNED", largeStyle);
        GUI.Label(
            new Rect(rect.x + 80f, rect.y + 135f, rect.width - 160f, 150f),
            "You were knocked out, but the ranch fights on.\n\nYour teammate can revive you — have them stand close and hold E.\n\nAuto-recovery in " +
            Mathf.CeilToInt(Mathf.Max(0f, core.Health.DownedAutoRecoverRemaining)) + " seconds.",
            centeredStyle
        );
    }

    private void DrawWinScreen()
    {
        Rect rect = new Rect(390f, 250f, 820f, 390f);
        GUI.Box(rect, GUIContent.none, panelStyle);
        GUI.Label(new Rect(rect.x + 30f, rect.y + 35f, rect.width - 60f, 75f), "CJ HAS BEEN OVERTHROWN", largeStyle);
        GUI.Label(
            new Rect(rect.x + 80f, rect.y + 140f, rect.width - 160f, 180f),
            "CJ: You have become... the Ranch Simulator.\n\nDrew: There is another.\n\nPress R to erase the completed save and start a new game.",
            centeredStyle
        );
    }

    private void DrawPanel(Rect rect, string heading, string body, GUIStyle textStyle)
    {
        GUI.Box(rect, GUIContent.none, panelStyle);
        GUI.Label(new Rect(rect.x + 12f, rect.y + 9f, rect.width - 24f, 38f), heading, titleStyle);
        GUI.Label(new Rect(rect.x + 18f, rect.y + 53f, rect.width - 36f, rect.height - 65f), body, textStyle);
    }

    private void EnsureStyles()
    {
        if (stylesReady)
            return;

        panelTexture = MakeTexture(new Color(0.025f, 0.025f, 0.025f, 0.94f));
        selectedTexture = MakeTexture(new Color(0.08f, 0.55f, 0.50f, 0.98f));
        healthLostTexture = MakeTexture(new Color(0.22f, 0.05f, 0.05f, 1f));
        healthFillTexture = MakeTexture(new Color(0.18f, 0.80f, 0.26f, 1f));
        staminaFillTexture = MakeTexture(new Color(0.15f, 0.55f, 0.95f, 1f));
        waitingTexture = MakeTexture(new Color(0.36f, 0.20f, 0.03f, 0.96f));
        activeTexture = MakeTexture(new Color(0.40f, 0.04f, 0.04f, 0.96f));
        damageTexture = MakeTexture(new Color(0.75f, 0.02f, 0.02f, 0.18f));
        bossFillTexture = MakeTexture(new Color(0.95f, 0.65f, 0.05f, 1f));

        panelStyle = new GUIStyle(GUI.skin.box);
        panelStyle.normal.background = panelTexture;

        selectedPanelStyle = new GUIStyle(panelStyle);
        selectedPanelStyle.normal.background = selectedTexture;

        waitingStyle = new GUIStyle(panelStyle);
        waitingStyle.normal.background = waitingTexture;

        activeStyle = new GUIStyle(panelStyle);
        activeStyle.normal.background = activeTexture;

        titleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 23,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        titleStyle.normal.textColor = Color.white;

        bodyStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 18,
            wordWrap = true,
            alignment = TextAnchor.UpperLeft
        };
        bodyStyle.normal.textColor = Color.white;

        smallStyle = new GUIStyle(bodyStyle)
        {
            fontSize = 16
        };

        centeredStyle = new GUIStyle(bodyStyle)
        {
            alignment = TextAnchor.MiddleCenter,
            fontStyle = FontStyle.Bold
        };

        healthStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 15,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        healthStyle.normal.textColor = Color.white;

        largeStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 42,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        largeStyle.normal.textColor = Color.white;

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
