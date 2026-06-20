using System.Text;
using UnityEngine;

public class RanchUI : MonoBehaviour
{
    private const float VirtualWidth = 1600f;
    private const float VirtualHeight = 900f;
    private RanchGameCore core;
    private Texture2D panelTexture, healthFillTexture, healthLostTexture, staminaFillTexture,
        waitingTexture, activeTexture, damageTexture, bossFillTexture;
    private GUIStyle panelStyle, waitingStyle, activeStyle, titleStyle, bodyStyle,
        centeredStyle, healthStyle, largeStyle;
    private bool stylesReady;

    public void Initialize(RanchGameCore gameCore)
    {
        core = gameCore;
    }

    private void OnGUI()
    {
        if (core == null || core.Inventory == null || core.Shop.IsOpen || core.Progression.IsOpen)
            return;

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

        DrawMainPanel();
        DrawHealthAndStamina();
        DrawWavePanel();
        DrawCJGatePanel();
        DrawControlsPanel();
        DrawMessage();
        DrawPrompt();

        if (core.Health.DamageFlashTime > 0f)
            GUI.DrawTexture(new Rect(0f, 0f, VirtualWidth, VirtualHeight), damageTexture);

        if (core.Health.IsDead)
            DrawDeathScreen();

        if (core.GameWon)
            DrawWinScreen();

        GUI.matrix = old;
    }

    private void DrawMainPanel()
    {
        int tier = core.Bottles.SelectedTier;
        StringBuilder text = new StringBuilder();
        text.AppendLine($"Raw Ranch: {core.Inventory.RawRanch:F1}");
        text.AppendLine($"Money: ${core.Inventory.Money:F0}");
        text.AppendLine($"Bottles sold: {core.BottlesSold}");
        text.AppendLine();
        text.AppendLine($"Bottle: {core.Bottles.GetTierName(tier)} ({core.Bottles.GetCapacity(tier)} Ranch)");
        text.AppendLine($"Stored: {core.Inventory.GetBottleCount(tier)}");
        text.AppendLine();
        text.AppendLine($"Level {core.Progression.Level} — {core.Progression.CurrentPhaseName}");
        text.AppendLine($"XP: {core.Progression.Experience:F0}/{core.Progression.ExperienceToNextLevel:F0} | Points: {core.Progression.SkillPoints}");
        text.AppendLine($"Tree: {core.Tree.CurrentStageName}");
        text.AppendLine($"Tool: {core.Upgrades.CurrentToolName}");
        text.AppendLine($"Sword: {core.Shop.CurrentSwordName}");
        text.AppendLine($"Empire: {core.Shop.CurrentStructureName}");
        text.AppendLine($"CJ Heat: {core.CJHeat} — {core.CJ.GetHeatStatus()}");
        text.AppendLine($"CJ Gate: {core.CJ.GetGateStatusShort()}");
        text.AppendLine($"Save: {core.Save.LastSaveStatus}");
        DrawPanel(new Rect(20f, 20f, 430f, 535f), "RANCH SIMULATOR", text.ToString());
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
            $"HP {core.Health.CurrentHealth:F0}/{core.Health.MaxHealth:F0} | Armor {core.Health.ArmorPercent:F0}%",
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
            $"STAMINA {core.Stamina.CurrentStamina:F0}/{core.Stamina.MaximumStamina:F0}",
            healthStyle
        );
    }

    private void DrawWavePanel()
    {
        DrawPanel(
            new Rect(1150f, 20f, 430f, 270f),
            "RANCH RAIDER WAVES",
            core.Waves.GetStatusText()
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
            : $"PHASE {Mathf.Max(1, core.CJ.CurrentPhase)}/3 — {core.CJ.FinalBoss.Health:F0}/{core.CJ.FinalBoss.MaxHealth:F0} HP";
        GUI.Label(bar, healthText, healthStyle);
    }

    private void DrawCJGatePanel()
    {
        DrawPanel(
            new Rect(1150f, 315f, 430f, 165f),
            "CJ FINAL BATTLE",
            core.CJ.GetGateStatusText()
        );
    }

    private void DrawControlsPanel()
    {
        string controls =
            "WASD — Move\n" +
            "Left Click / Space — Light attack\n" +
            "Q — Heavy attack\n" +
            "Right Click — Block / perfect block\n" +
            "Left Control — Dodge\n" +
            "E — Interact / extract\n" +
            "Shift + E — Instant bottle / sell all\n" +
            "[ and ] — Change bottle\n" +
            "P — Shop | K — Progression\n" +
            "Z — Save | X — Load\n" +
            "R — Restart after defeat or victory";

        DrawPanel(new Rect(1150f, 500f, 430f, 340f), "CONTROLS", controls);
    }

    private void DrawMessage()
    {
        if (string.IsNullOrWhiteSpace(core.StatusMessage) || core.StatusMessageTime <= 0f)
            return;

        Rect rect = new Rect(20f, 745f, 740f, 120f);
        GUI.Box(rect, GUIContent.none, panelStyle);
        GUI.Label(
            new Rect(rect.x + 20f, rect.y + 16f, rect.width - 40f, rect.height - 32f),
            core.StatusMessage,
            bodyStyle
        );
    }

    private void DrawPrompt()
    {
        if (core.Player == null || string.IsNullOrWhiteSpace(core.Player.CurrentPrompt) ||
            core.Health.IsDead || core.GameWon)
            return;

        Rect rect = new Rect(380f, 825f, 840f, 58f);
        GUI.Box(rect, GUIContent.none, panelStyle);
        GUI.Label(
            new Rect(rect.x + 16f, rect.y + 8f, rect.width - 32f, rect.height - 16f),
            core.Player.CurrentPrompt,
            centeredStyle
        );
    }

    private void DrawDeathScreen()
    {
        Rect rect = new Rect(390f, 260f, 820f, 360f);
        GUI.Box(rect, GUIContent.none, panelStyle);
        GUI.Label(
            new Rect(rect.x + 30f, rect.y + 45f, rect.width - 60f, 70f),
            "YOU WERE RANCHED",
            largeStyle
        );
        GUI.Label(
            new Rect(rect.x + 90f, rect.y + 145f, rect.width - 180f, 150f),
            "The Ranch Raiders overwhelmed you.\n\nUse blocking, perfect blocks, dodges, skill points, health upgrades, and stronger swords.\n\nPress R to restart from your latest save.",
            centeredStyle
        );
    }

    private void DrawWinScreen()
    {
        Rect rect = new Rect(390f, 250f, 820f, 410f);
        GUI.Box(rect, GUIContent.none, panelStyle);
        GUI.Label(
            new Rect(rect.x + 30f, rect.y + 35f, rect.width - 60f, 75f),
            "CJ HAS BEEN OVERTHROWN",
            largeStyle
        );
        GUI.Label(
            new Rect(rect.x + 80f, rect.y + 135f, rect.width - 160f, 220f),
            "CJ: You have become... the Ranch Simulator.\n\nDrew: There is another.\n\nPress R to delete the completed save and begin a new game.",
            centeredStyle
        );
    }

    private void DrawPanel(Rect rect, string heading, string body)
    {
        GUI.Box(rect, GUIContent.none, panelStyle);
        GUI.Label(
            new Rect(rect.x + 16f, rect.y + 12f, rect.width - 32f, 42f),
            heading,
            titleStyle
        );
        GUI.Label(
            new Rect(rect.x + 22f, rect.y + 62f, rect.width - 44f, rect.height - 76f),
            body,
            bodyStyle
        );
    }

    private void EnsureStyles()
    {
        if (stylesReady)
            return;

        panelTexture = MakeTexture(new Color(0.025f, 0.025f, 0.025f, 0.93f));
        healthLostTexture = MakeTexture(new Color(0.22f, 0.05f, 0.05f, 1f));
        healthFillTexture = MakeTexture(new Color(0.18f, 0.80f, 0.26f, 1f));
        staminaFillTexture = MakeTexture(new Color(0.16f, 0.48f, 0.95f, 1f));
        waitingTexture = MakeTexture(new Color(0.36f, 0.20f, 0.03f, 0.96f));
        activeTexture = MakeTexture(new Color(0.40f, 0.04f, 0.04f, 0.96f));
        damageTexture = MakeTexture(new Color(0.75f, 0.02f, 0.02f, 0.18f));
        bossFillTexture = MakeTexture(new Color(0.95f, 0.63f, 0.08f, 1f));

        panelStyle = new GUIStyle(GUI.skin.box);
        panelStyle.normal.background = panelTexture;
        waitingStyle = new GUIStyle(panelStyle);
        waitingStyle.normal.background = waitingTexture;
        activeStyle = new GUIStyle(panelStyle);
        activeStyle.normal.background = activeTexture;

        titleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 25,
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
