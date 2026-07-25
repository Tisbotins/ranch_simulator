using UnityEngine;

public class RanchProgressionSystem : MonoBehaviour
{
    private const int ClassCount = 4;
    private const int MaxNodeLevel = 6;

    public int Level { get; private set; } = 1;
    public float Experience { get; private set; }
    public int KnowledgePoints { get; private set; }
    public int LifetimeKnowledgePoints { get; private set; }
    public bool IsOpen { get; private set; }

    // Compatibility aliases used by older HUD/save integrations.
    public int SkillPoints => KnowledgePoints;
    public int CombatTraining => GetPowerLevel(CurrentClass);
    public int SurvivalTraining => 0;
    public int EngineeringTraining => 0;

    public float ExperienceToNextLevel => 100f + Mathf.Pow(Level - 1, 1.35f) * 85f;

    public RanchClassType CurrentClass =>
        core != null && core.Classes != null
            ? core.Classes.CurrentClass
            : RanchClassType.Sword;

    public float DamageMultiplier
    {
        get
        {
            int power = GetPowerLevel(CurrentClass);
            int mastery = GetMasteryLevel(CurrentClass);
            return 1f + power * 0.10f + mastery * 0.025f;
        }
    }

    public float CriticalChance
    {
        get
        {
            int technique = GetTechniqueLevel(CurrentClass);
            switch (CurrentClass)
            {
                case RanchClassType.Spear:
                    return 0.05f + technique * 0.015f;
                case RanchClassType.Ranged:
                    return 0.05f + technique * 0.018f;
                case RanchClassType.Summoner:
                    return 0f;
                default:
                    return 0.05f + technique * 0.025f;
            }
        }
    }

    public float CriticalDamageMultiplier =>
        1.5f + GetMasteryLevel(CurrentClass) * 0.08f;

    // Survival and production were removed from the Ranch Knowledge menu.
    public float MaximumStaminaBonus => 0f;
    public float StaminaRegenerationBonus => 0f;
    public float ProductionMultiplier => 1f;
    public float SaleMultiplier => 1f;

    public float BlockReductionBonus
    {
        get
        {
            if (CurrentClass == RanchClassType.Sword)
                return GetMasteryLevel(CurrentClass) * 0.025f;
            if (CurrentClass == RanchClassType.Spear)
                return GetMasteryLevel(CurrentClass) * 0.015f;
            return 0f;
        }
    }

    public float DodgeDistanceBonus => 0f;
    public float SpearRangeMultiplier => 1f + GetTechniqueLevel(RanchClassType.Spear) * 0.055f;
    public float ProjectileSpeedMultiplier => 1f + GetTechniqueLevel(RanchClassType.Ranged) * 0.10f;
    public float RangedCooldownMultiplier => Mathf.Max(
        0.62f,
        1f - GetTechniqueLevel(RanchClassType.Ranged) * 0.045f -
        GetMasteryLevel(RanchClassType.Ranged) * 0.025f
    );
    public float SummonDamageMultiplier =>
        1f + GetPowerLevel(RanchClassType.Summoner) * 0.14f +
        GetMasteryLevel(RanchClassType.Summoner) * 0.04f;
    public float SummonLifetimeBonus =>
        GetTechniqueLevel(RanchClassType.Summoner) * 3f;
    public float SummonCooldownMultiplier => Mathf.Max(
        0.55f,
        1f - GetTechniqueLevel(RanchClassType.Summoner) * 0.06f
    );
    public int MaxActiveDeluluBonus =>
        GetMasteryLevel(RanchClassType.Summoner) / 2;

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

    private readonly int[] powerLevels = new int[ClassCount];
    private readonly int[] techniqueLevels = new int[ClassCount];
    private readonly int[] masteryLevels = new int[ClassCount];

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
        if (core == null || core.GameWon || core.Health.IsDead || core.Health.IsDowned || core.Settings.IsOpen ||
            core.Shop.IsOpen || (core.Classes != null && core.Classes.IsOpen) ||
            (core.Laboratory != null && core.Laboratory.IsOpen))
        {
            return;
        }

        if (Input.GetKeyDown(KeyCode.K))
            ToggleMenu();

        if (IsOpen && Input.GetKeyDown(KeyCode.Escape))
            CloseMenu();
    }

    public void AddExperience(float amount, string reason = "")
    {
        if (amount <= 0f)
            return;

        Experience += amount;
        int earned = 0;
        while (Experience >= ExperienceToNextLevel)
        {
            float required = ExperienceToNextLevel;
            Experience -= required;
            Level++;
            KnowledgePoints++;
            LifetimeKnowledgePoints++;
            earned++;
        }

        if (earned > 0)
        {
            core.Stamina.RestoreFull();
            core.Health.Heal(core.Health.MaxHealth * 0.25f);
            core.ShowMessage(
                "RANCH KNOWLEDGE LEVEL " + Level + "! You earned " + earned +
                " Ranch Knowledge Point" + (earned == 1 ? "." : "s.") +
                " Press K to upgrade your " + core.Classes.CurrentClassName + " tree.",
                8f
            );
            core.Classes.NotifyKnowledgePointEarned();
            core.Save.RequestSave();
            core.NotifyResourcesChanged();
        }
    }

    public int GetPowerLevel(RanchClassType classType)
    {
        return powerLevels[Mathf.Clamp((int)classType, 0, ClassCount - 1)];
    }

    public int GetTechniqueLevel(RanchClassType classType)
    {
        return techniqueLevels[Mathf.Clamp((int)classType, 0, ClassCount - 1)];
    }

    public int GetMasteryLevel(RanchClassType classType)
    {
        return masteryLevels[Mathf.Clamp((int)classType, 0, ClassCount - 1)];
    }

    public int[] GetPowerLevelsCopy() => (int[])powerLevels.Clone();
    public int[] GetTechniqueLevelsCopy() => (int[])techniqueLevels.Clone();
    public int[] GetMasteryLevelsCopy() => (int[])masteryLevels.Clone();

    public void ToggleMenu()
    {
        if (IsOpen) CloseMenu();
        else OpenMenu();
    }

    public void OpenMenu()
    {
        if (IsOpen || core.Shop.IsOpen || core.Settings.IsOpen || core.GameWon ||
            core.Health.IsDead || core.Health.IsDowned ||
            (core.Classes != null && core.Classes.IsOpen) ||
            (core.Laboratory != null && core.Laboratory.IsOpen) ||
            (core.Space != null && core.Space.IsOpen))
        {
            return;
        }

        IsOpen = true;
        previousTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        if (player != null) player.enabled = false;
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

    public void BuyPowerUpgrade()
    {
        int index = (int)CurrentClass;
        powerLevels[index] = SpendPoint(powerLevels[index], GetPowerName(CurrentClass));
    }

    public void BuyTechniqueUpgrade()
    {
        int index = (int)CurrentClass;
        techniqueLevels[index] = SpendPoint(techniqueLevels[index], GetTechniqueName(CurrentClass));
    }

    public void BuyMasteryUpgrade()
    {
        int index = (int)CurrentClass;
        masteryLevels[index] = SpendPoint(masteryLevels[index], GetMasteryName(CurrentClass));
    }

    // Legacy button methods now feed the current class tree.
    public void BuyCombatTraining() => BuyPowerUpgrade();
    public void BuySurvivalTraining() => BuyTechniqueUpgrade();
    public void BuyEngineeringTraining() => BuyMasteryUpgrade();

    private int SpendPoint(int nodeLevel, string nodeName)
    {
        if (nodeLevel >= MaxNodeLevel)
        {
            core.ShowMessage(nodeName + " is already maxed.");
            return nodeLevel;
        }

        if (KnowledgePoints <= 0)
        {
            core.ShowMessage("You need a Ranch Knowledge Point. Earn knowledge from waves, sales, bosses, upgrades, and construction.");
            return nodeLevel;
        }

        KnowledgePoints--;
        nodeLevel++;
        core.ShowMessage(nodeName + " upgraded to Level " + nodeLevel + ".");
        core.Save.RequestSave();
        core.NotifyResourcesChanged();
        return nodeLevel;
    }

    public void RestoreState(
        int level,
        float experience,
        int knowledgePoints,
        int lifetimeKnowledgePoints,
        int[] restoredPower,
        int[] restoredTechnique,
        int[] restoredMastery)
    {
        Level = Mathf.Max(1, level);
        Experience = Mathf.Max(0f, experience);
        KnowledgePoints = Mathf.Max(0, knowledgePoints);
        LifetimeKnowledgePoints = Mathf.Max(Mathf.Max(0, lifetimeKnowledgePoints), Level - 1);
        CopyLevels(restoredPower, powerLevels);
        CopyLevels(restoredTechnique, techniqueLevels);
        CopyLevels(restoredMastery, masteryLevels);
    }

    public void RestoreLegacyState(
        int level,
        float experience,
        int oldSkillPoints,
        int combatTraining,
        int survivalTraining,
        int engineeringTraining)
    {
        Level = Mathf.Max(1, level);
        Experience = Mathf.Max(0f, experience);

        int refunded = Mathf.Max(0, oldSkillPoints) +
            Mathf.Max(0, combatTraining) +
            Mathf.Max(0, survivalTraining) +
            Mathf.Max(0, engineeringTraining);

        KnowledgePoints = refunded;
        LifetimeKnowledgePoints = Mathf.Max(Level - 1, refunded);

        for (int i = 0; i < ClassCount; i++)
        {
            powerLevels[i] = 0;
            techniqueLevels[i] = 0;
            masteryLevels[i] = 0;
        }
    }

    private static void CopyLevels(int[] source, int[] destination)
    {
        for (int i = 0; i < destination.Length; i++)
        {
            int value = source != null && i < source.Length ? source[i] : 0;
            destination[i] = Mathf.Clamp(value, 0, MaxNodeLevel);
        }
    }

    private void OnGUI()
    {
        if (!IsOpen || core == null)
            return;

        EnsureStyles();
        Matrix4x4 old = GUI.matrix;
        float scale = Mathf.Min(Screen.width / VirtualWidth, Screen.height / VirtualHeight);
        GUI.matrix = Matrix4x4.TRS(
            new Vector3((Screen.width - VirtualWidth * scale) * 0.5f,
                (Screen.height - VirtualHeight * scale) * 0.5f, 0f),
            Quaternion.identity,
            new Vector3(scale, scale, 1f)
        );

        GUI.Box(new Rect(100f, 70f, 1400f, 760f), GUIContent.none, backgroundStyle);
        GUI.Label(new Rect(150f, 95f, 900f, 60f), "RANCH KNOWLEDGE — " + core.Classes.CurrentClassName.ToUpperInvariant(), titleStyle);
        GUI.Label(new Rect(960f, 105f, 450f, 45f),
            "Knowledge Level " + Level + " | Points: " + KnowledgePoints, bodyStyle);
        GUI.Label(new Rect(150f, 165f, 1200f, 45f),
            "Progress: " + Experience.ToString("F0") + " / " + ExperienceToNextLevel.ToString("F0") +
            " | Change class by speaking with Dr. Oakberry.", bodyStyle);

        DrawSkillCard(new Rect(150f, 245f, 390f, 440f),
            GetPowerName(CurrentClass),
            GetPowerLevel(CurrentClass),
            GetPowerDescription(CurrentClass),
            BuyPowerUpgrade);

        DrawSkillCard(new Rect(605f, 245f, 390f, 440f),
            GetTechniqueName(CurrentClass),
            GetTechniqueLevel(CurrentClass),
            GetTechniqueDescription(CurrentClass),
            BuyTechniqueUpgrade);

        DrawSkillCard(new Rect(1060f, 245f, 390f, 440f),
            GetMasteryName(CurrentClass),
            GetMasteryLevel(CurrentClass),
            GetMasteryDescription(CurrentClass),
            BuyMasteryUpgrade);

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
        GUI.Label(new Rect(rect.x + 20f, rect.y + 20f, rect.width - 40f, 60f), title, titleStyle);
        GUI.Label(new Rect(rect.x + 25f, rect.y + 90f, rect.width - 50f, 235f),
            "Level " + level + "/" + MaxNodeLevel + "\n\n" + description, bodyStyle);

        bool old = GUI.enabled;
        GUI.enabled = KnowledgePoints > 0 && level < MaxNodeLevel;
        if (GUI.Button(new Rect(rect.x + 35f, rect.y + 350f, rect.width - 70f, 58f),
            level >= MaxNodeLevel ? "MAXED" : "SPEND 1 KNOWLEDGE POINT", buttonStyle))
        {
            purchase?.Invoke();
        }
        GUI.enabled = old;
    }

    private static string GetPowerName(RanchClassType classType)
    {
        switch (classType)
        {
            case RanchClassType.Spear: return "THRUST POWER";
            case RanchClassType.Ranged: return "PROJECTILE POWER";
            case RanchClassType.Summoner: return "DELULU POWER";
            default: return "BLADE POWER";
        }
    }

    private static string GetTechniqueName(RanchClassType classType)
    {
        switch (classType)
        {
            case RanchClassType.Spear: return "SPEAR REACH";
            case RanchClassType.Ranged: return "DRAW SPEED";
            case RanchClassType.Summoner: return "LASTING DELULU";
            default: return "BLADE PRECISION";
        }
    }

    private static string GetMasteryName(RanchClassType classType)
    {
        switch (classType)
        {
            case RanchClassType.Spear: return "PIERCING MASTERY";
            case RanchClassType.Ranged: return "RANGED MASTERY";
            case RanchClassType.Summoner: return "SWARM MASTERY";
            default: return "GUARD MASTERY";
        }
    }

    private static string GetPowerDescription(RanchClassType classType)
    {
        return classType == RanchClassType.Summoner
            ? "+14% Delulu attack damage per level."
            : "+10% class weapon damage per level.";
    }

    private static string GetTechniqueDescription(RanchClassType classType)
    {
        switch (classType)
        {
            case RanchClassType.Spear:
                return "+5.5% spear range and +1.5% critical chance per level.";
            case RanchClassType.Ranged:
                return "+10% projectile speed, faster recovery, and +1.8% critical chance per level.";
            case RanchClassType.Summoner:
                return "+3 seconds of Delulu lifetime and faster wand recharge per level.";
            default:
                return "+2.5% critical-hit chance per level.";
        }
    }

    private static string GetMasteryDescription(RanchClassType classType)
    {
        switch (classType)
        {
            case RanchClassType.Spear:
                return "Improves critical damage, thrust damage, and blocking efficiency.";
            case RanchClassType.Ranged:
                return "Improves critical damage and reduces projectile attack cooldowns.";
            case RanchClassType.Summoner:
                return "Improves Delulu damage and grants one extra active Delulu every two levels.";
            default:
                return "Improves critical damage and damage reduction while blocking.";
        }
    }

    private void EnsureStyles()
    {
        if (stylesReady)
            return;

        backgroundTexture = MakeTexture(new Color(0.02f, 0.025f, 0.04f, 0.98f));
        cardTexture = MakeTexture(new Color(0.07f, 0.10f, 0.16f, 0.98f));
        buttonTexture = MakeTexture(new Color(0.12f, 0.46f, 0.36f, 1f));
        backgroundStyle = new GUIStyle(GUI.skin.box);
        backgroundStyle.normal.background = backgroundTexture;
        cardStyle = new GUIStyle(GUI.skin.box);
        cardStyle.normal.background = cardTexture;
        titleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 27,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            wordWrap = true
        };
        titleStyle.normal.textColor = Color.white;
        bodyStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 20,
            wordWrap = true,
            alignment = TextAnchor.UpperLeft
        };
        bodyStyle.normal.textColor = Color.white;
        buttonStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 19,
            fontStyle = FontStyle.Bold,
            wordWrap = true
        };
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
