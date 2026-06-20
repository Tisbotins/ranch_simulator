using UnityEngine;

public class RanchProgressionSystem : MonoBehaviour
{
    public int Level { get; private set; } = 1;
    public float Experience { get; private set; }
    public int SkillPoints { get; private set; }
    public int CombatTraining { get; private set; }
    public int SurvivalTraining { get; private set; }
    public int EngineeringTraining { get; private set; }
    public bool IsOpen { get; private set; }

    public float DamageMultiplier => 1f + CombatTraining * 0.09f;
    public float CriticalChance => 0.05f + CombatTraining * 0.02f;
    public float CriticalDamageMultiplier => 1.5f + CombatTraining * 0.08f;
    public float MaximumStaminaBonus => SurvivalTraining * 12f;
    public float StaminaRegenerationBonus => SurvivalTraining * 1.75f;
    public float BlockReductionBonus => SurvivalTraining * 0.025f;
    public float DodgeDistanceBonus => SurvivalTraining * 0.25f;
    public float ProductionMultiplier => 1f + EngineeringTraining * 0.06f;
    public float SaleMultiplier => 1f + EngineeringTraining * 0.05f;
    public float ExperienceToNextLevel => 100f + Mathf.Pow(Level - 1, 1.35f) * 85f;

    public string CurrentPhaseName
    {
        get
        {
            if (core == null) return "Ranch Beginner";
            if (core.Shop.StructureLevel >= 8 || core.Waves.HighestWaveCleared >= 20) return "Ranchenator War";
            if (core.Shop.StructureLevel >= 5 || core.Waves.HighestWaveCleared >= 10) return "Ranch Empire";
            if (core.Shop.StructureLevel >= 3 || core.Waves.HighestWaveCleared >= 5) return "Ranch Industry";
            return "Ranch Homestead";
        }
    }

    private RanchGameCore core;
    private RanchPlayerController player;
    private float previousTimeScale = 1f;

    private const float VirtualWidth = 1600f;
    private const float VirtualHeight = 900f;
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
    }

    public void RegisterPlayer(RanchPlayerController playerController)
    {
        player = playerController;
    }

    private void Update()
    {
        if (core == null || core.GameWon || core.Health.IsDead) return;

        if (Input.GetKeyDown(KeyCode.K))
            ToggleMenu();

        if (IsOpen && Input.GetKeyDown(KeyCode.Escape))
            CloseMenu();
    }

    public void AddExperience(float amount, string reason = "")
    {
        if (amount <= 0f) return;

        Experience += amount;
        bool leveled = false;
        while (Experience >= ExperienceToNextLevel)
        {
            float required = ExperienceToNextLevel;
            Experience -= required;
            Level++;
            SkillPoints++;
            leveled = true;
        }

        if (leveled)
        {
            core.Stamina.RestoreFull();
            core.Health.Heal(core.Health.MaxHealth * 0.25f);
            core.ShowMessage($"RANCH LEVEL {Level}! You earned 1 skill point. Press K to spend it.", 7f);
            core.Save.RequestSave();
        }
    }

    public void ToggleMenu()
    {
        if (IsOpen) CloseMenu();
        else OpenMenu();
    }

    public void OpenMenu()
    {
        if (IsOpen || core.Shop.IsOpen || core.GameWon || core.Health.IsDead) return;
        IsOpen = true;
        previousTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        if (player != null) player.enabled = false;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void CloseMenu()
    {
        if (!IsOpen) return;
        IsOpen = false;
        Time.timeScale = previousTimeScale <= 0f ? 1f : previousTimeScale;
        if (player != null && !core.Health.IsDead && !core.GameWon) player.enabled = true;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    public void BuyCombatTraining()
    {
        SpendPoint(0, "Combat Training");
    }

    public void BuySurvivalTraining()
    {
        if (SpendPoint(1, "Survival Training"))
            core.Stamina.RestoreFull();
    }

    public void BuyEngineeringTraining()
    {
        SpendPoint(2, "Ranch Engineering");
    }

    private bool SpendPoint(int branch, string skillName)
    {
        int currentLevel = branch == 0 ? CombatTraining :
            (branch == 1 ? SurvivalTraining : EngineeringTraining);

        if (currentLevel >= 8)
        {
            core.ShowMessage(skillName + " is already maxed.");
            return false;
        }

        if (SkillPoints <= 0)
        {
            core.ShowMessage("You need a skill point. Earn XP from waves, sales, bosses, and construction.");
            return false;
        }

        SkillPoints--;
        if (branch == 0) CombatTraining++;
        else if (branch == 1) SurvivalTraining++;
        else EngineeringTraining++;

        int newLevel = branch == 0 ? CombatTraining :
            (branch == 1 ? SurvivalTraining : EngineeringTraining);
        core.ShowMessage(skillName + " upgraded to Level " + newLevel + ".");
        core.Save.RequestSave();
        return true;
    }

    public void RestoreState(int level, float experience, int skillPoints,
        int combatTraining, int survivalTraining, int engineeringTraining)
    {
        Level = Mathf.Max(1, level);
        Experience = Mathf.Max(0f, experience);
        SkillPoints = Mathf.Max(0, skillPoints);
        CombatTraining = Mathf.Clamp(combatTraining, 0, 8);
        SurvivalTraining = Mathf.Clamp(survivalTraining, 0, 8);
        EngineeringTraining = Mathf.Clamp(engineeringTraining, 0, 8);
    }

    private void OnGUI()
    {
        if (!IsOpen || core == null) return;
        EnsureStyles();

        Matrix4x4 old = GUI.matrix;
        float scale = Mathf.Min(Screen.width / VirtualWidth, Screen.height / VirtualHeight);
        GUI.matrix = Matrix4x4.TRS(
            new Vector3((Screen.width - VirtualWidth * scale) * 0.5f,
                (Screen.height - VirtualHeight * scale) * 0.5f, 0f),
            Quaternion.identity, new Vector3(scale, scale, 1f));

        GUI.Box(new Rect(100f, 70f, 1400f, 760f), GUIContent.none, backgroundStyle);
        GUI.Label(new Rect(150f, 100f, 900f, 60f), "RANCH PROGRESSION", titleStyle);
        GUI.Label(new Rect(980f, 110f, 430f, 45f),
            $"Level {Level} | Skill Points: {SkillPoints}", bodyStyle);
        GUI.Label(new Rect(150f, 165f, 1200f, 45f),
            $"Phase: {CurrentPhaseName} | XP: {Experience:F0} / {ExperienceToNextLevel:F0}", bodyStyle);

        DrawSkillCard(new Rect(150f, 245f, 390f, 440f), "COMBAT TRAINING",
            CombatTraining,
            $"+9% sword damage per level\n+2% critical chance per level\n+8% critical damage per level\n\nCurrent damage multiplier: {DamageMultiplier:0.00}x\nCurrent critical chance: {CriticalChance * 100f:F0}%",
            BuyCombatTraining);

        DrawSkillCard(new Rect(605f, 245f, 390f, 440f), "SURVIVAL TRAINING",
            SurvivalTraining,
            $"+12 maximum stamina per level\n+1.75 stamina regeneration per level\nStronger blocks and longer dodges\n\nMaximum stamina bonus: {MaximumStaminaBonus:F0}\nRegeneration bonus: {StaminaRegenerationBonus:0.00}/sec",
            BuySurvivalTraining);

        DrawSkillCard(new Rect(1060f, 245f, 390f, 440f), "RANCH ENGINEERING",
            EngineeringTraining,
            $"+6% Ranch production per level\n+5% bottle sale value per level\nImproves hand extraction and passive output\n\nProduction multiplier: {ProductionMultiplier:0.00}x\nSale multiplier: {SaleMultiplier:0.00}x",
            BuyEngineeringTraining);

        if (GUI.Button(new Rect(625f, 735f, 350f, 60f), "CLOSE [K]", buttonStyle))
        {
            CloseMenu();
            GUIUtility.ExitGUI();
        }

        GUI.matrix = old;
    }

    private void DrawSkillCard(Rect rect, string title, int level, string description, System.Action purchase)
    {
        GUI.Box(rect, GUIContent.none, cardStyle);
        GUI.Label(new Rect(rect.x + 20f, rect.y + 20f, rect.width - 40f, 45f), title, titleStyle);
        GUI.Label(new Rect(rect.x + 25f, rect.y + 78f, rect.width - 50f, 245f),
            $"Level {level}/8\n\n{description}", bodyStyle);

        bool old = GUI.enabled;
        GUI.enabled = SkillPoints > 0 && level < 8;
        if (GUI.Button(new Rect(rect.x + 35f, rect.y + 350f, rect.width - 70f, 58f),
            level >= 8 ? "MAXED" : "SPEND 1 SKILL POINT", buttonStyle))
            purchase?.Invoke();
        GUI.enabled = old;
    }

    private void EnsureStyles()
    {
        if (stylesReady) return;
        backgroundTexture = MakeTexture(new Color(0.02f, 0.025f, 0.04f, 0.98f));
        cardTexture = MakeTexture(new Color(0.07f, 0.10f, 0.16f, 0.98f));
        buttonTexture = MakeTexture(new Color(0.12f, 0.46f, 0.36f, 1f));
        backgroundStyle = new GUIStyle(GUI.skin.box); backgroundStyle.normal.background = backgroundTexture;
        cardStyle = new GUIStyle(GUI.skin.box); cardStyle.normal.background = cardTexture;
        titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 28, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        titleStyle.normal.textColor = Color.white;
        bodyStyle = new GUIStyle(GUI.skin.label) { fontSize = 20, wordWrap = true, alignment = TextAnchor.UpperLeft };
        bodyStyle.normal.textColor = Color.white;
        buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = 20, fontStyle = FontStyle.Bold };
        buttonStyle.normal.background = buttonTexture;
        buttonStyle.normal.textColor = Color.white;
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
