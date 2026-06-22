using UnityEngine;

public class RanchShopSystem : MonoBehaviour
{
    private readonly string[] structureNames =
    {
        "No Ranch Empire",
        "Roadside Ranch Stand",
        "Ranch Workshop",
        "Ranch Laboratory",
        "Advanced Ranch Laboratory",
        "Ranch Research Campus",
        "Ranch Industrial Complex",
        "Ranch Stronghold",
        "Ranch Citadel",
        "Golden Ranch Citadel"
    };

    private readonly float[] structureCosts =
    {
        250f, 850f, 2400f, 6500f, 15000f, 34000f, 75000f, 160000f, 350000f
    };

    private readonly float[] passiveRanch =
    {
        0f, 0.05f, 0.2f, 0.65f, 1.5f, 3.25f, 6.5f, 12f, 22f, 40f
    };

    private readonly float[] passiveMoney =
    {
        0f, 0f, 0f, 0.5f, 1.5f, 4f, 10f, 24f, 55f, 125f
    };

    private readonly float[] defenseDamage =
    {
        0f, 0f, 0f, 0f, 0f, 0f, 8f, 18f, 35f, 65f
    };

    private readonly string[] swordNames =
    {
        "Rusty Ranch Sword",
        "Tempered Ranch Blade",
        "Steel Ranch Saber",
        "Laboratory Edge",
        "Industrial Ranch Cleaver",
        "Citadel Defender",
        "Golden Ranchenator Sword"
    };

    private readonly float[] swordDamage =
    {
        25f, 42f, 68f, 105f, 160f, 235f, 350f
    };

    private readonly float[] swordCosts =
    {
        225f, 650f, 1600f, 3900f, 9000f, 22000f
    };

    private readonly float[] automationCosts =
    {
        500f, 1300f, 3200f, 7500f, 17000f, 36000f
    };

    private readonly float[] automationIntervals =
    {
        0f, 12f, 8f, 5f, 3f, 1.5f, 0.7f
    };

    private readonly float[] researchCosts =
    {
        600f, 1500f, 3800f, 9000f, 21000f, 48000f
    };

    private readonly float[] marketCosts =
    {
        700f, 1800f, 4500f, 11000f, 26000f, 60000f
    };

    public bool IsOpen { get; private set; }
    public int StructureLevel { get; private set; }
    public int SwordLevel { get; private set; }
    public int AutomationLevel { get; private set; }
    public int ResearchLevel { get; private set; }
    public int MarketLevel { get; private set; }

    public string CurrentStructureName => structureNames[StructureLevel];
    public string CurrentSwordName => swordNames[SwordLevel];
    public float CurrentSwordDamage => swordDamage[SwordLevel];
    public float ExtractionResearchMultiplier => 1f + ResearchLevel * 0.12f;
    public float SaleMultiplier => 1f + MarketLevel * 0.10f;

    public float CurrentPassiveRanchRate =>
        passiveRanch[StructureLevel] *
        (1f + ResearchLevel * 0.20f) *
        (core != null && core.Progression != null ? core.Progression.ProductionMultiplier : 1f);

    public float CurrentPassiveMoneyRate =>
        passiveMoney[StructureLevel] *
        (1f + MarketLevel * 0.25f) *
        (core != null && core.Progression != null ? core.Progression.SaleMultiplier : 1f);

    private RanchGameCore core;
    private RanchPlayerController player;
    private Transform empireRoot;
    private GameObject[] empireGroups;
    private TextMesh empireLabel;
    private float ranchAccumulator;
    private float moneyAccumulator;
    private float automationTimer;
    private float defenseTimer;
    private float previousTimeScale = 1f;
    private int selectedTab;

    private const float VirtualWidth = 1600f;
    private const float VirtualHeight = 900f;

    private Texture2D screenTexture;
    private Texture2D cardTexture;
    private Texture2D buttonTexture;
    private Texture2D hoverTexture;
    private Texture2D selectedTexture;
    private GUIStyle screenStyle;
    private GUIStyle cardStyle;
    private GUIStyle titleStyle;
    private GUIStyle subtitleStyle;
    private GUIStyle bodyStyle;
    private GUIStyle smallBodyStyle;
    private GUIStyle buttonStyle;
    private GUIStyle tabStyle;
    private GUIStyle selectedTabStyle;
    private bool stylesReady;

    public void Initialize(RanchGameCore gameCore)
    {
        core = gameCore;
    }

    public void RegisterPlayer(RanchPlayerController playerController)
    {
        player = playerController;
    }

    public void RegisterEmpireVisual(Transform root, GameObject[] groups, TextMesh label)
    {
        empireRoot = root;
        empireGroups = groups;
        empireLabel = label;
        RefreshEmpireVisual();
    }

    private void Update()
    {
        if (core == null || core.GameWon || core.Health.IsDead)
            return;

        if (Input.GetKeyDown(KeyCode.P) && !core.Settings.IsOpen)
            ToggleShop();

        if (IsOpen)
        {
            if (Input.GetKeyDown(KeyCode.Escape))
                CloseShop();
            return;
        }

        RunPassiveProduction();
        RunAutomation();
        RunDefense();
    }

    public void OpenShop()
    {
        if (IsOpen || core.Health.IsDead || core.GameWon || core.Settings.IsOpen)
            return;

        if (core.Progression.IsOpen)
            core.Progression.CloseMenu();

        IsOpen = true;
        previousTimeScale = Time.timeScale;
        Time.timeScale = 0f;

        if (player != null)
            player.enabled = false;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void CloseShop()
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

    public void ToggleShop()
    {
        if (IsOpen)
            CloseShop();
        else
            OpenShop();
    }

    public float GetNextStructureCost()
    {
        return StructureLevel >= structureNames.Length - 1 ? -1f : structureCosts[StructureLevel];
    }

    public float GetNextSwordCost()
    {
        return SwordLevel >= swordNames.Length - 1 ? -1f : swordCosts[SwordLevel];
    }

    public float GetNextAutomationCost()
    {
        return AutomationLevel >= automationIntervals.Length - 1 ? -1f : automationCosts[AutomationLevel];
    }

    public float GetNextResearchCost()
    {
        return ResearchLevel >= researchCosts.Length ? -1f : researchCosts[ResearchLevel];
    }

    public float GetNextMarketCost()
    {
        return MarketLevel >= marketCosts.Length ? -1f : marketCosts[MarketLevel];
    }

    public void BuyNextStructure()
    {
        float cost = GetNextStructureCost();
        if (cost < 0f)
        {
            core.ShowMessage("The Golden Ranch Citadel is already complete.");
            return;
        }

        int nextLevel = StructureLevel + 1;
        string areaReason;
        if (!core.Areas.CanBuildStructureLevel(nextLevel, out areaReason))
        {
            core.ShowMessage(areaReason, 7f);
            return;
        }

        if (!core.Inventory.TrySpendMoney(cost))
        {
            core.ShowMessage("Need $" + cost.ToString("F0") + " for the next Ranch Empire structure.");
            return;
        }

        StructureLevel++;
        RefreshEmpireVisual();
        core.AddCJHeat(40 * StructureLevel);
        core.Progression.AddExperience(70f + StructureLevel * 30f, "Empire construction");
        core.Save.RequestSave();
        core.ShowMessage("Built " + CurrentStructureName + ". It has appeared in its assigned area.");
    }

    public void BuyNextSword()
    {
        float cost = GetNextSwordCost();
        if (cost < 0f)
        {
            core.ShowMessage("The shared weapon damage tier is already maxed.");
            return;
        }

        if (StructureLevel < Mathf.Max(0, SwordLevel))
        {
            core.ShowMessage("Build a stronger Ranch Empire structure first.");
            return;
        }

        if (!core.Inventory.TrySpendMoney(cost))
        {
            core.ShowMessage("Need $" + cost.ToString("F0") + " for the next weapon damage tier.");
            return;
        }

        SwordLevel++;
        core.Progression.AddExperience(35f + SwordLevel * 15f, "Weapon upgrade");
        core.Save.RequestSave();
        core.ShowMessage("Weapon damage upgraded to Tier " + (SwordLevel + 1) + " with " + CurrentSwordDamage.ToString("F0") + " base damage.");
    }

    public void BuyNextAutomation()
    {
        float cost = GetNextAutomationCost();
        if (cost < 0f)
        {
            core.ShowMessage("Auto-bottling is already maxed.");
            return;
        }

        if (StructureLevel < 3)
        {
            core.ShowMessage("Build the Ranch Laboratory first.");
            return;
        }

        if (!core.Inventory.TrySpendMoney(cost))
        {
            core.ShowMessage("Need $" + cost.ToString("F0") + " for the auto-bottler.");
            return;
        }

        AutomationLevel++;
        core.Progression.AddExperience(30f + AutomationLevel * 12f, "Automation upgrade");
        core.Save.RequestSave();
        core.ShowMessage("Auto-bottler upgraded to Level " + AutomationLevel + ".");
    }

    public void BuyNextResearch()
    {
        float cost = GetNextResearchCost();
        if (cost < 0f)
        {
            core.ShowMessage("Ranch production research is maxed.");
            return;
        }

        if (StructureLevel < 3)
        {
            core.ShowMessage("Build the Ranch Laboratory first.");
            return;
        }

        if (!core.Inventory.TrySpendMoney(cost))
        {
            core.ShowMessage("Need $" + cost.ToString("F0") + " for production research.");
            return;
        }

        ResearchLevel++;
        core.Progression.AddExperience(35f + ResearchLevel * 14f, "Production research");
        core.Save.RequestSave();
        core.ShowMessage("Ranch production research upgraded to Level " + ResearchLevel + ".");
    }

    public void BuyNextMarketUpgrade()
    {
        float cost = GetNextMarketCost();
        if (cost < 0f)
        {
            core.ShowMessage("Ranch market research is maxed.");
            return;
        }

        if (StructureLevel < 4)
        {
            core.ShowMessage("Build the Advanced Ranch Laboratory first.");
            return;
        }

        if (!core.Inventory.TrySpendMoney(cost))
        {
            core.ShowMessage("Need $" + cost.ToString("F0") + " for market research.");
            return;
        }

        MarketLevel++;
        core.Progression.AddExperience(35f + MarketLevel * 14f, "Market research");
        core.Save.RequestSave();
        core.ShowMessage("Market research upgraded to Level " + MarketLevel + ".");
    }

    private void RunPassiveProduction()
    {
        ranchAccumulator += CurrentPassiveRanchRate * Time.deltaTime;
        moneyAccumulator += CurrentPassiveMoneyRate * Time.deltaTime;

        if (ranchAccumulator >= 0.25f)
        {
            float payout = Mathf.Floor(ranchAccumulator * 4f) / 4f;
            ranchAccumulator -= payout;
            core.Inventory.AddRawRanch(payout);
        }

        if (moneyAccumulator >= 1f)
        {
            float payout = Mathf.Floor(moneyAccumulator);
            moneyAccumulator -= payout;
            core.Inventory.AddMoney(payout);
        }
    }

    private void RunAutomation()
    {
        if (AutomationLevel <= 0)
            return;

        automationTimer += Time.deltaTime;
        if (automationTimer < automationIntervals[AutomationLevel])
            return;

        automationTimer = 0f;
        int attempts = 1 + Mathf.FloorToInt(AutomationLevel / 2f);

        for (int i = 0; i < attempts; i++)
        {
            if (!core.Bottles.TryBottleSelected(false))
                break;
        }
    }

    private void RunDefense()
    {
        float damage = defenseDamage[StructureLevel];
        if (damage <= 0f)
            return;

        defenseTimer += Time.deltaTime;
        if (defenseTimer >= 2.5f)
        {
            defenseTimer = 0f;
            core.Waves.DamageNearestFromDefense(damage, 35f);
        }
    }

    public void RestoreState(
        int structureLevel,
        int swordLevel,
        int automationLevel,
        int researchLevel,
        int marketLevel)
    {
        StructureLevel = Mathf.Clamp(structureLevel, 0, structureNames.Length - 1);
        SwordLevel = Mathf.Clamp(swordLevel, 0, swordNames.Length - 1);
        AutomationLevel = Mathf.Clamp(automationLevel, 0, automationIntervals.Length - 1);
        ResearchLevel = Mathf.Clamp(researchLevel, 0, researchCosts.Length);
        MarketLevel = Mathf.Clamp(marketLevel, 0, marketCosts.Length);
        RefreshEmpireVisual();
        core.NotifyResourcesChanged();
    }

    private void RefreshEmpireVisual()
    {
        if (empireGroups != null)
        {
            for (int i = 0; i < empireGroups.Length; i++)
            {
                if (empireGroups[i] != null)
                    empireGroups[i].SetActive(StructureLevel >= i + 1);
            }
        }

        if (empireRoot != null)
            empireRoot.localScale = Vector3.one;

        if (empireLabel != null)
        {
            empireLabel.text =
                CurrentStructureName +
                "\nRanch: " + CurrentPassiveRanchRate.ToString("0.00") +
                "/sec | Money: $" + CurrentPassiveMoneyRate.ToString("0.00") + "/sec";
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
            new Vector3(
                (Screen.width - VirtualWidth * scale) * 0.5f,
                (Screen.height - VirtualHeight * scale) * 0.5f,
                0f
            ),
            Quaternion.identity,
            new Vector3(scale, scale, 1f)
        );

        DrawShop();
        GUI.matrix = old;
    }

    private void DrawShop()
    {
        GUI.Box(new Rect(20f, 20f, 1560f, 860f), GUIContent.none, screenStyle);
        GUI.Label(new Rect(55f, 38f, 820f, 55f), "RANCH EMPIRE SHOP", titleStyle);
        GUI.Label(new Rect(930f, 45f, 390f, 45f), "Money: $" + core.Inventory.Money.ToString("F0"), subtitleStyle);

        if (GUI.Button(new Rect(1370f, 42f, 160f, 48f), "CLOSE [P]", buttonStyle))
        {
            CloseShop();
            GUIUtility.ExitGUI();
        }

        GUI.Label(
            new Rect(55f, 95f, 1490f, 38f),
            CurrentStructureName +
            " | " + core.Equipment.CurrentWeaponName +
            " | Ranch " + CurrentPassiveRanchRate.ToString("0.00") +
            "/sec | Money $" + CurrentPassiveMoneyRate.ToString("0.00") + "/sec",
            smallBodyStyle
        );

        string[] tabs = { "EMPIRE", "EQUIPMENT", "WEAPONS", "DEFENSE", "AUTOMATION" };
        float tabWidth = 286f;

        for (int i = 0; i < tabs.Length; i++)
        {
            if (GUI.Button(
                new Rect(55f + i * 300f, 145f, tabWidth, 54f),
                tabs[i],
                selectedTab == i ? selectedTabStyle : tabStyle))
            {
                selectedTab = i;
            }
        }

        switch (selectedTab)
        {
            case 0:
                DrawEmpirePage();
                break;
            case 1:
                DrawEquipmentPage();
                break;
            case 2:
                DrawWeaponsPage();
                break;
            case 3:
                DrawDefensePage();
                break;
            default:
                DrawAutomationPage();
                break;
        }
    }

    private void DrawEmpirePage()
    {
        float structureCost = GetNextStructureCost();
        string nextArea = StructureLevel >= structureNames.Length - 1
            ? "All areas complete"
            : core.Areas.GetAreaName(core.Areas.GetRequiredAreaForStructure(StructureLevel + 1));

        DrawCard(
            new Rect(55f, 220f, 650f, 570f),
            "BUILD THE RANCH EMPIRE",
            GetStructureDescription() + "\n\nRequired area: " + nextArea,
            bodyStyle
        );

        DrawButton(
            new Rect(90f, 710f, 580f, 58f),
            structureCost < 0f
                ? "GOLDEN RANCH CITADEL COMPLETE"
                : "BUILD NEXT STRUCTURE — $" + structureCost.ToString("F0"),
            structureCost >= 0f,
            BuyNextStructure
        );

        float researchCost = GetNextResearchCost();
        DrawCard(
            new Rect(745f, 220f, 390f, 300f),
            "RANCH RESEARCH",
            "Level: " + ResearchLevel + "/6\n\nEach level adds 20% passive Ranch output and 12% hand extraction.\n\n" +
            (StructureLevel >= 3 ? "Laboratory online." : "Requires the Ranch Laboratory."),
            smallBodyStyle
        );
        DrawButton(
            new Rect(780f, 445f, 320f, 52f),
            researchCost < 0f ? "RESEARCH MAXED" : "RESEARCH — $" + researchCost.ToString("F0"),
            researchCost >= 0f,
            BuyNextResearch
        );

        float marketCost = GetNextMarketCost();
        DrawCard(
            new Rect(1170f, 220f, 375f, 300f),
            "MARKET RESEARCH",
            "Level: " + MarketLevel + "/6\n\nEach level adds 25% passive money and 10% bottle sale value.\n\n" +
            (StructureLevel >= 4 ? "Advanced Laboratory online." : "Requires the Advanced Ranch Laboratory."),
            smallBodyStyle
        );
        DrawButton(
            new Rect(1200f, 445f, 315f, 52f),
            marketCost < 0f ? "MARKET MAXED" : "RESEARCH — $" + marketCost.ToString("F0"),
            marketCost >= 0f,
            BuyNextMarketUpgrade
        );

        DrawCard(
            new Rect(745f, 550f, 800f, 240f),
            "AREA ACCESS & EMPIRE OUTPUT",
            core.Areas.GetStatusSummary() +
            "\n\nCurrent output: " + CurrentPassiveRanchRate.ToString("0.00") +
            " Ranch/sec and $" + CurrentPassiveMoneyRate.ToString("0.00") +
            "/sec. Defense deals " + defenseDamage[StructureLevel].ToString("F0") + " damage every 2.5 seconds.",
            smallBodyStyle
        );
    }

    private void DrawEquipmentPage()
    {
        DrawCard(
            new Rect(55f, 220f, 720f, 570f),
            "RANCH EXTRACTOR",
            "Current tool: " + core.Upgrades.CurrentToolName +
            "\nExtraction multiplier: " + core.Upgrades.ExtractionMultiplier.ToString("0.00") +
            "x\n\nUse Inventory Slot 1 to visibly hold the extractor. Holding E at the Ranch Tree also temporarily equips it.\n\nNext upgrade: " +
            (core.Upgrades.ToolTier >= core.Upgrades.MaxToolTier
                ? "MAXED"
                : "$" + core.Upgrades.GetNextToolUpgradeCost().ToString("F0")),
            bodyStyle
        );

        DrawButton(
            new Rect(95f, 705f, 640f, 58f),
            core.Upgrades.ToolTier >= core.Upgrades.MaxToolTier ? "EXTRACTOR MAXED" : "UPGRADE RANCH EXTRACTOR",
            core.Upgrades.ToolTier < core.Upgrades.MaxToolTier,
            core.Upgrades.BuyNextToolTier
        );

        DrawCard(
            new Rect(815f, 220f, 730f, 570f),
            "BOTTLE TECHNOLOGY",
            "Highest unlocked: " + core.Bottles.GetTierName(core.Bottles.UnlockedTier) +
            "\nSelected bottle: " + core.Bottles.GetTierName(core.Bottles.SelectedTier) +
            "\nCapacity: " + core.Bottles.GetCapacity(core.Bottles.SelectedTier) +
            " raw Ranch\nStored: " + core.Inventory.GetBottleCount(core.Bottles.SelectedTier) +
            "\n\nUse [ and ] while playing to change bottle size. Shift + E at the Bottle Station instantly fills as many selected bottles as possible.\n\nNext upgrade: " +
            (core.Bottles.UnlockedTier >= RanchBottleSystem.TierCount - 1
                ? "MAXED"
                : "$" + core.Upgrades.GetNextBottleUpgradeCost().ToString("F0")),
            bodyStyle
        );

        DrawButton(
            new Rect(855f, 705f, 650f, 58f),
            core.Bottles.UnlockedTier >= RanchBottleSystem.TierCount - 1 ? "BOTTLES MAXED" : "UNLOCK NEXT BOTTLE SIZE",
            core.Bottles.UnlockedTier < RanchBottleSystem.TierCount - 1,
            core.Upgrades.BuyNextBottleTier
        );
    }

    private void DrawWeaponsPage()
    {
        DrawWeaponCard(
            new Rect(55f, 220f, 470f, 390f),
            RanchWeaponType.Sword,
            "BALANCED"
        );

        DrawWeaponCard(
            new Rect(565f, 220f, 470f, 390f),
            RanchWeaponType.Spear,
            "LONG REACH"
        );

        DrawWeaponCard(
            new Rect(1075f, 220f, 470f, 390f),
            RanchWeaponType.Bow,
            "RANGED"
        );

        float weaponCost = GetNextSwordCost();
        DrawCard(
            new Rect(55f, 640f, 1490f, 150f),
            "SHARED WEAPON DAMAGE TIER",
            "Current tier: " + (SwordLevel + 1) + "/" + swordNames.Length +
            " | Base damage: " + CurrentSwordDamage.ToString("F0") +
            "\nThis upgrade improves Sword, Spear, and Bow damage together.",
            smallBodyStyle
        );

        DrawButton(
            new Rect(1030f, 690f, 470f, 58f),
            weaponCost < 0f ? "WEAPON DAMAGE MAXED" : "UPGRADE DAMAGE — $" + weaponCost.ToString("F0"),
            weaponCost >= 0f,
            BuyNextSword
        );
    }

    private void DrawWeaponCard(Rect rect, RanchWeaponType weapon, string role)
    {
        bool unlocked = core.Equipment.IsWeaponUnlocked(weapon);
        bool equipped = core.Equipment.EquippedWeapon == weapon;
        float cost = core.Equipment.GetWeaponUnlockCost(weapon);
        string weaponName = weapon == RanchWeaponType.Sword
            ? "RANCH SWORD"
            : weapon == RanchWeaponType.Spear ? "RANCH SPEAR" : "RANCH BOW";

        DrawCard(
            rect,
            weaponName,
            role + "\n\n" + core.Equipment.GetWeaponDescription(weapon) +
            "\n\nStatus: " + (equipped ? "EQUIPPED IN SLOT 2" : unlocked ? "UNLOCKED" : "LOCKED"),
            smallBodyStyle
        );

        string buttonText;
        if (equipped)
            buttonText = "EQUIPPED";
        else if (unlocked)
            buttonText = "EQUIP WEAPON";
        else
            buttonText = "UNLOCK & EQUIP — $" + cost.ToString("F0");

        bool old = GUI.enabled;
        GUI.enabled = !equipped;
        if (GUI.Button(
            new Rect(rect.x + 35f, rect.y + rect.height - 75f, rect.width - 70f, 52f),
            buttonText,
            buttonStyle))
        {
            core.Equipment.BuyOrEquipWeapon(weapon);
        }
        GUI.enabled = old;
    }

    private void DrawDefensePage()
    {
        float healthCost = core.Health.GetNextHealthCost();
        float armorCost = core.Health.GetNextArmorCost();
        float regenCost = core.Health.GetNextRegenerationCost();

        DrawCard(
            new Rect(55f, 220f, 470f, 260f),
            "MAXIMUM HEALTH",
            "Current: " + core.Health.MaxHealth.ToString("F0") + " HP\nLevel: " + core.Health.HealthLevel + "/6\n\nMore health helps you survive later waves and bosses.",
            smallBodyStyle
        );
        DrawButton(
            new Rect(90f, 405f, 400f, 52f),
            healthCost < 0f ? "HEALTH MAXED" : "UPGRADE — $" + healthCost.ToString("F0"),
            healthCost >= 0f,
            core.Health.BuyHealthUpgrade
        );

        DrawCard(
            new Rect(565f, 220f, 470f, 260f),
            "ARMOR",
            "Damage reduction: " + core.Health.ArmorPercent.ToString("F0") + "%\nLevel: " + core.Health.ArmorLevel + "/6\n\nArmor reduces every hit that gets through blocking or dodging.",
            smallBodyStyle
        );
        DrawButton(
            new Rect(600f, 405f, 400f, 52f),
            armorCost < 0f ? "ARMOR MAXED" : "UPGRADE — $" + armorCost.ToString("F0"),
            armorCost >= 0f,
            core.Health.BuyArmorUpgrade
        );

        DrawCard(
            new Rect(1075f, 220f, 470f, 260f),
            "REGENERATION",
            "Current: " + core.Health.RegenerationPerSecond.ToString("0.00") + " HP/sec\nLevel: " + core.Health.RegenerationLevel + "/6\n\nRegeneration restores health between enemy attacks.",
            smallBodyStyle
        );
        DrawButton(
            new Rect(1110f, 405f, 400f, 52f),
            regenCost < 0f ? "REGEN MAXED" : "UPGRADE — $" + regenCost.ToString("F0"),
            regenCost >= 0f,
            core.Health.BuyRegenerationUpgrade
        );

        float healCost = core.Health.GetFullHealCost();
        DrawCard(
            new Rect(55f, 520f, 470f, 270f),
            "RANCH MEDICAL BAY",
            "Current health: " + core.Health.CurrentHealth.ToString("F0") + " / " + core.Health.MaxHealth.ToString("F0") +
            "\n\nBuy a full heal before a difficult wave or the CJ final battle.",
            smallBodyStyle
        );
        DrawButton(
            new Rect(90f, 710f, 400f, 52f),
            healCost <= 0f ? "ALREADY FULL HEALTH" : "FULL HEAL — $" + healCost.ToString("F0"),
            healCost > 0f,
            core.Health.BuyFullHeal
        );

        DrawCard(
            new Rect(565f, 520f, 470f, 270f),
            "RANCH TRAPS — SLOT 3",
            "Owned: " + core.Deployables.TrapCount +
            "\nPlaced: " + core.Deployables.ActiveTrapCount +
            "\n\nPlace with Left Click or F. Traps damage, stun, and knock enemies back. Unused traps expire after 120 seconds.",
            smallBodyStyle
        );
        DrawButton(
            new Rect(600f, 710f, 400f, 52f),
            "BUY " + RanchDeployableSystem.TrapPackSize + " TRAPS — $" + RanchDeployableSystem.TrapPackCost.ToString("F0"),
            true,
            core.Deployables.BuyTrapPack
        );

        string wandStatus = core.Deployables.DeluluWandUnlocked
            ? "UNLOCKED | Active Delulus: " + core.Deployables.ActiveDeluluCount + "/" + RanchDeployableSystem.MaxActiveDelulus
            : "LOCKED";

        DrawCard(
            new Rect(1075f, 520f, 470f, 270f),
            "DELULU WAND — SLOT 4",
            "Status: " + wandStatus +
            "\n\nPress 4, then Left Click or F to summon a little Delulu protector. Delulus chase and attack nearby enemies before fading away.",
            smallBodyStyle
        );
        DrawButton(
            new Rect(1110f, 710f, 400f, 52f),
            core.Deployables.DeluluWandUnlocked
                ? "SELECT DELULU WAND"
                : "UNLOCK WAND — $" + RanchDeployableSystem.DeluluWandCost.ToString("F0"),
            true,
            core.Deployables.BuyDeluluWand
        );
    }

    private void DrawAutomationPage()
    {
        float automationCost = GetNextAutomationCost();
        string automationStatus = AutomationLevel <= 0
            ? "Not installed."
            : "Fills selected bottles every " + automationIntervals[AutomationLevel].ToString("0.0") + " seconds.";

        DrawCard(
            new Rect(55f, 220f, 720f, 570f),
            "AUTOMATIC BOTTLING",
            "Level: " + AutomationLevel + "/6\n\n" + automationStatus +
            "\n\nHigher levels fill multiple bottles each cycle. The Ranch Laboratory is required before the first purchase.",
            bodyStyle
        );
        DrawButton(new Rect(95f, 705f, 640f, 58f), automationCost < 0f ? "AUTO-BOTTLER MAXED" : "UPGRADE AUTO-BOTTLER — $" + automationCost.ToString("F0"), automationCost >= 0f, BuyNextAutomation);

        DrawCard(
            new Rect(815f, 220f, 730f, 270f),
            "DREW",
            "Current level: " + core.Drew.Level + "/10\n\nDrew automatically extracts Ranch and fills bottles. Higher levels increase his speed and output.",
            smallBodyStyle
        );
        DrawButton(new Rect(855f, 410f, 650f, 55f), core.Drew.Level >= 10 ? "DREW MAXED" : (core.Drew.IsHired ? "UPGRADE" : "HIRE") + " DREW — $" + core.Drew.GetUpgradeCost().ToString("F0"), core.Drew.Level < 10, core.Drew.HireOrUpgrade);

        DrawCard(
            new Rect(815f, 525f, 730f, 265f),
            "AUTOMATION STATUS",
            "Selected bottle: " + core.Bottles.GetTierName(core.Bottles.SelectedTier) +
            "\nRaw Ranch: " + core.Inventory.RawRanch.ToString("F1") +
            "\nSelected bottles stored: " + core.Inventory.GetBottleCount(core.Bottles.SelectedTier) +
            "\n\nUse [ and ] outside the shop to change bottle size.",
            smallBodyStyle
        );
    }

    private string GetStructureDescription()
    {
        if (StructureLevel >= structureNames.Length - 1)
        {
            return
                "Current: Golden Ranch Citadel\n\nThe ultimate structure is complete.\n\nPassive Ranch: " +
                CurrentPassiveRanchRate.ToString("0.00") +
                "/sec\nPassive money: $" +
                CurrentPassiveMoneyRate.ToString("0.00") + "/sec";
        }

        int next = StructureLevel + 1;
        return
            "Current: " + CurrentStructureName +
            "\n\nNext: " + structureNames[next] +
            "\nCost: $" + structureCosts[StructureLevel].ToString("F0") +
            "\n\nNext base output:\n" + passiveRanch[next].ToString("0.00") +
            " Ranch/sec\n$" + passiveMoney[next].ToString("0.00") +
            "/sec\nDefense: " + defenseDamage[next].ToString("F0") +
            " damage\n\nStructures now appear across the Homestead, Laboratory, Industrial, and Citadel areas.";
    }

    private void DrawCard(Rect rect, string heading, string body, GUIStyle textStyle)
    {
        GUI.Box(rect, GUIContent.none, cardStyle);
        GUI.Label(new Rect(rect.x + 18f, rect.y + 12f, rect.width - 36f, 42f), heading, subtitleStyle);
        GUI.Label(new Rect(rect.x + 24f, rect.y + 62f, rect.width - 48f, rect.height - 86f), body, textStyle);
    }

    private void DrawButton(Rect rect, string label, bool enabled, System.Action action)
    {
        bool old = GUI.enabled;
        GUI.enabled = enabled;
        if (GUI.Button(rect, label, buttonStyle))
            action?.Invoke();
        GUI.enabled = old;
    }

    private void EnsureStyles()
    {
        if (stylesReady)
            return;

        screenTexture = MakeTexture(new Color(0.015f, 0.025f, 0.035f, 0.99f));
        cardTexture = MakeTexture(new Color(0.07f, 0.11f, 0.15f, 0.99f));
        selectedTexture = MakeTexture(new Color(0.10f, 0.55f, 0.48f, 1f));
        buttonTexture = MakeTexture(new Color(0.12f, 0.35f, 0.45f, 1f));
        hoverTexture = MakeTexture(new Color(0.16f, 0.52f, 0.60f, 1f));

        screenStyle = new GUIStyle(GUI.skin.box);
        screenStyle.normal.background = screenTexture;

        cardStyle = new GUIStyle(GUI.skin.box);
        cardStyle.normal.background = cardTexture;

        titleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 38,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleLeft
        };
        titleStyle.normal.textColor = Color.white;

        subtitleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 23,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            wordWrap = true
        };
        subtitleStyle.normal.textColor = Color.white;

        bodyStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 19,
            alignment = TextAnchor.UpperLeft,
            wordWrap = true
        };
        bodyStyle.normal.textColor = Color.white;

        smallBodyStyle = new GUIStyle(bodyStyle)
        {
            fontSize = 17
        };

        buttonStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 18,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            wordWrap = true
        };
        buttonStyle.normal.background = buttonTexture;
        buttonStyle.hover.background = hoverTexture;
        buttonStyle.active.background = selectedTexture;
        buttonStyle.normal.textColor = Color.white;
        buttonStyle.hover.textColor = Color.white;
        buttonStyle.active.textColor = Color.white;

        tabStyle = new GUIStyle(buttonStyle)
        {
            fontSize = 19
        };

        selectedTabStyle = new GUIStyle(tabStyle);
        selectedTabStyle.normal.background = selectedTexture;
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
