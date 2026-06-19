using System.Text;
using UnityEngine;

public class RanchUI : MonoBehaviour
{
    private const float VirtualWidth = 1600f;
    private const float VirtualHeight = 900f;
    private RanchGameCore core;
    private Texture2D panelTexture, healthFillTexture, healthLostTexture, waitingTexture, activeTexture, damageTexture;
    private GUIStyle panelStyle, waitingStyle, activeStyle, titleStyle, bodyStyle, centeredStyle, healthStyle, largeStyle;
    private bool stylesReady;

    public void Initialize(RanchGameCore gameCore) => core = gameCore;

    private void OnGUI()
    {
        if (core == null || core.Inventory == null || core.Shop.IsOpen) return;
        EnsureStyles();

        Matrix4x4 old = GUI.matrix;
        float scale = Mathf.Min(Screen.width / VirtualWidth, Screen.height / VirtualHeight);
        GUI.matrix = Matrix4x4.TRS(
            new Vector3((Screen.width - VirtualWidth * scale) * 0.5f, (Screen.height - VirtualHeight * scale) * 0.5f, 0f),
            Quaternion.identity,
            new Vector3(scale, scale, 1f));

        DrawMainPanel();
        DrawHealthBar();
        DrawWavePanel();
        DrawControlsPanel();
        DrawMessage();
        DrawPrompt();

        if (core.Health.DamageFlashTime > 0f)
            GUI.DrawTexture(new Rect(0f, 0f, VirtualWidth, VirtualHeight), damageTexture);
        if (core.Health.IsDead) DrawDeathScreen();
        if (core.GameWon) DrawWinScreen();

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
        text.AppendLine($"Selected bottle: {core.Bottles.GetTierName(tier)}");
        text.AppendLine($"Capacity: {core.Bottles.GetCapacity(tier)} Ranch");
        text.AppendLine($"In inventory: {core.Inventory.GetBottleCount(tier)}");
        text.AppendLine();
        text.AppendLine($"Tree: {core.Tree.CurrentStageName}");
        text.AppendLine($"Tool: {core.Upgrades.CurrentToolName}");
        text.AppendLine($"Sword: {core.Shop.CurrentSwordName}");
        text.AppendLine($"Drew: Level {core.Drew.Level}");
        text.AppendLine($"Empire: {core.Shop.CurrentStructureName}");
        text.AppendLine($"CJ Heat: {core.CJHeat}");
        DrawPanel(new Rect(20f, 20f, 430f, 455f), "RANCH SIMULATOR", text.ToString());
    }

    private void DrawHealthBar()
    {
        Rect panel = new Rect(500f, 20f, 600f, 78f);
        GUI.Box(panel, GUIContent.none, panelStyle);
        GUI.Label(new Rect(515f, 23f, 570f, 28f), "PLAYER HEALTH", titleStyle);
        Rect bar = new Rect(522f, 57f, 556f, 26f);
        GUI.DrawTexture(bar, healthLostTexture);
        float percent = core.Health.MaxHealth <= 0f ? 0f : Mathf.Clamp01(core.Health.CurrentHealth / core.Health.MaxHealth);
        GUI.DrawTexture(new Rect(bar.x, bar.y, bar.width * percent, bar.height), healthFillTexture);
        GUI.Label(bar, $"{core.Health.CurrentHealth:F0} / {core.Health.MaxHealth:F0} HP | Armor {core.Health.ArmorPercent:F0}% | Regen {core.Health.RegenerationPerSecond:0.00}/sec", healthStyle);
    }

    private void DrawWavePanel()
    {
        DrawPanel(new Rect(1150f, 20f, 430f, 260f), "RANCH RAIDER WAVES", core.Waves.GetStatusText());
        bool active = core.Waves.CurrentState == RanchWaveSystem.WaveState.Spawning || core.Waves.CurrentState == RanchWaveSystem.WaveState.Fighting;
        Rect banner = new Rect(500f, 110f, 600f, 66f);
        GUI.Box(banner, GUIContent.none, active ? activeStyle : waitingStyle);
        GUI.Label(banner, core.Waves.GetBannerText(), centeredStyle);
        if (core.Waves.CurrentState == RanchWaveSystem.WaveState.Intermission && core.Waves.SecondsUntilNextWave <= 5f)
            GUI.Label(new Rect(690f, 180f, 220f, 100f), Mathf.CeilToInt(core.Waves.SecondsUntilNextWave).ToString(), largeStyle);
    }

    private void DrawControlsPanel()
    {
        string controls = "WASD — Move\nE — Interact / extract\nShift + E — Sell all selected\n[ and ] — Change bottle\n1–8 — Select bottle\nSpace — Swing sword\nP — Open Ranch Empire Shop";
        DrawPanel(new Rect(1150f, 300f, 430f, 255f), "CONTROLS", controls);
    }

    private void DrawMessage()
    {
        if (string.IsNullOrWhiteSpace(core.StatusMessage) || core.StatusMessageTime <= 0f) return;
        Rect rect = new Rect(20f, 745f, 740f, 120f);
        GUI.Box(rect, GUIContent.none, panelStyle);
        GUI.Label(new Rect(rect.x + 20f, rect.y + 16f, rect.width - 40f, rect.height - 32f), core.StatusMessage, bodyStyle);
    }

    private void DrawPrompt()
    {
        if (core.Player == null || string.IsNullOrWhiteSpace(core.Player.CurrentPrompt) || core.Health.IsDead || core.GameWon) return;
        Rect rect = new Rect(380f, 825f, 840f, 58f);
        GUI.Box(rect, GUIContent.none, panelStyle);
        GUI.Label(new Rect(rect.x + 16f, rect.y + 8f, rect.width - 32f, rect.height - 16f), core.Player.CurrentPrompt, centeredStyle);
    }

    private void DrawDeathScreen()
    {
        Rect rect = new Rect(390f, 260f, 820f, 360f);
        GUI.Box(rect, GUIContent.none, panelStyle);
        GUI.Label(new Rect(rect.x + 30f, rect.y + 45f, rect.width - 60f, 70f), "YOU WERE RANCHED", largeStyle);
        GUI.Label(new Rect(rect.x + 90f, rect.y + 145f, rect.width - 180f, 150f), "The Ranch Raiders overwhelmed you.\n\nUpgrade health, armor, regeneration, and your sword in the Ranch Empire Shop.\n\nPress R to restart.", centeredStyle);
    }

    private void DrawWinScreen()
    {
        Rect rect = new Rect(390f, 250f, 820f, 390f);
        GUI.Box(rect, GUIContent.none, panelStyle);
        GUI.Label(new Rect(rect.x + 30f, rect.y + 35f, rect.width - 60f, 75f), "CJ HAS BEEN OVERTHROWN", largeStyle);
        GUI.Label(new Rect(rect.x + 80f, rect.y + 140f, rect.width - 160f, 180f), "CJ: You have become... the Ranch Simulator.\n\nDrew: There is another.\n\nPress R to restart.", centeredStyle);
    }

    private void DrawPanel(Rect rect, string heading, string body)
    {
        GUI.Box(rect, GUIContent.none, panelStyle);
        GUI.Label(new Rect(rect.x + 16f, rect.y + 12f, rect.width - 32f, 42f), heading, titleStyle);
        GUI.Label(new Rect(rect.x + 22f, rect.y + 62f, rect.width - 44f, rect.height - 76f), body, bodyStyle);
    }

    private void EnsureStyles()
    {
        if (stylesReady) return;
        panelTexture = MakeTexture(new Color(0.025f, 0.025f, 0.025f, 0.93f));
        healthLostTexture = MakeTexture(new Color(0.22f, 0.05f, 0.05f, 1f));
        healthFillTexture = MakeTexture(new Color(0.18f, 0.80f, 0.26f, 1f));
        waitingTexture = MakeTexture(new Color(0.36f, 0.20f, 0.03f, 0.96f));
        activeTexture = MakeTexture(new Color(0.40f, 0.04f, 0.04f, 0.96f));
        damageTexture = MakeTexture(new Color(0.75f, 0.02f, 0.02f, 0.18f));

        panelStyle = new GUIStyle(GUI.skin.box); panelStyle.normal.background = panelTexture;
        waitingStyle = new GUIStyle(panelStyle); waitingStyle.normal.background = waitingTexture;
        activeStyle = new GUIStyle(panelStyle); activeStyle.normal.background = activeTexture;
        titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 26, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        titleStyle.normal.textColor = Color.white;
        bodyStyle = new GUIStyle(GUI.skin.label) { fontSize = 19, wordWrap = true, alignment = TextAnchor.UpperLeft };
        bodyStyle.normal.textColor = Color.white;
        centeredStyle = new GUIStyle(bodyStyle) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
        healthStyle = new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        healthStyle.normal.textColor = Color.white;
        largeStyle = new GUIStyle(GUI.skin.label) { fontSize = 42, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
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
