using UnityEngine;

public class RanchShopSystem : MonoBehaviour
{
    private readonly string[] structureNames =
    {
        "No Ranch Empire", "Roadside Ranch Stand", "Ranch Workshop", "Ranch Laboratory",
        "Advanced Ranch Laboratory", "Ranch Research Campus", "Ranch Industrial Complex",
        "Ranch Stronghold", "Ranch Citadel", "Golden Ranch Citadel"
    };
    private readonly float[] structureCosts = { 250f, 850f, 2400f, 6500f, 15000f, 34000f, 75000f, 160000f, 350000f };
    private readonly float[] passiveRanch = { 0f, 0.05f, 0.2f, 0.65f, 1.5f, 3.25f, 6.5f, 12f, 22f, 40f };
    private readonly float[] passiveMoney = { 0f, 0f, 0f, 0.5f, 1.5f, 4f, 10f, 24f, 55f, 125f };
    private readonly float[] defenseDamage = { 0f, 0f, 0f, 0f, 0f, 0f, 8f, 18f, 35f, 65f };

    private readonly string[] swordNames =
    {
        "Rusty Ranch Sword", "Tempered Ranch Blade", "Steel Ranch Saber", "Laboratory Edge",
        "Industrial Ranch Cleaver", "Citadel Defender", "Golden Ranchenator Sword"
    };
    private readonly float[] swordDamage = { 25f, 42f, 68f, 105f, 160f, 235f, 350f };
    private readonly float[] swordCosts = { 225f, 650f, 1600f, 3900f, 9000f, 22000f };

    private readonly float[] automationCosts = { 500f, 1300f, 3200f, 7500f, 17000f, 36000f };
    private readonly float[] automationIntervals = { 0f, 12f, 8f, 5f, 3f, 1.5f, 0.7f };
    private readonly float[] researchCosts = { 600f, 1500f, 3800f, 9000f, 21000f, 48000f };
    private readonly float[] marketCosts = { 700f, 1800f, 4500f, 11000f, 26000f, 60000f };

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
    public float CurrentPassiveRanchRate => passiveRanch[StructureLevel] * (1f + ResearchLevel * 0.20f) *
        (core != null && core.Progression != null ? core.Progression.ProductionMultiplier : 1f);
    public float CurrentPassiveMoneyRate => passiveMoney[StructureLevel] * (1f + MarketLevel * 0.25f) *
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
    private Texture2D screenTexture, cardTexture, buttonTexture, hoverTexture, selectedTexture;
    private GUIStyle screenStyle, cardStyle, titleStyle, subtitleStyle, bodyStyle, buttonStyle, tabStyle, selectedTabStyle;
    private bool stylesReady;

    public void Initialize(RanchGameCore gameCore) => core = gameCore;
    public void RegisterPlayer(RanchPlayerController playerController) => player = playerController;

    public void RegisterEmpireVisual(Transform root, GameObject[] groups, TextMesh label)
    {
        empireRoot = root;
        empireGroups = groups;
        empireLabel = label;
        RefreshEmpireVisual();
    }

    private void Update()
    {
        if (core == null || core.GameWon || core.Health.IsDead) return;
        if (Input.GetKeyDown(KeyCode.P)) ToggleShop();

        if (IsOpen)
        {
            if (Input.GetKeyDown(KeyCode.Escape)) CloseShop();
            return;
        }

        RunPassiveProduction();
        RunAutomation();
        RunDefense();
    }

    public void OpenShop()
    {
        if (IsOpen || core.Health.IsDead || core.GameWon) return;
        if (core.Progression.IsOpen) core.Progression.CloseMenu();
        IsOpen = true;
        previousTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        if (player != null) player.enabled = false;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void CloseShop()
    {
        if (!IsOpen) return;
        IsOpen = false;
        Time.timeScale = previousTimeScale <= 0f ? 1f : previousTimeScale;
        if (player != null && !core.Health.IsDead && !core.GameWon) player.enabled = true;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    public void ToggleShop() { if (IsOpen) CloseShop(); else OpenShop(); }

    public float GetNextStructureCost() => StructureLevel >= structureNames.Length - 1 ? -1f : structureCosts[StructureLevel];
    public float GetNextSwordCost() => SwordLevel >= swordNames.Length - 1 ? -1f : swordCosts[SwordLevel];
    public float GetNextAutomationCost() => AutomationLevel >= automationIntervals.Length - 1 ? -1f : automationCosts[AutomationLevel];
    public float GetNextResearchCost() => ResearchLevel >= researchCosts.Length ? -1f : researchCosts[ResearchLevel];
    public float GetNextMarketCost() => MarketLevel >= marketCosts.Length ? -1f : marketCosts[MarketLevel];

    public void BuyNextStructure()
    {
        float cost = GetNextStructureCost();
        if (cost < 0f) { core.ShowMessage("The Golden Ranch Citadel is already complete."); return; }
        if (!core.Inventory.TrySpendMoney(cost)) { core.ShowMessage($"Need ${cost:F0} for the next Ranch Empire structure."); return; }
        StructureLevel++;
        RefreshEmpireVisual();
        core.AddCJHeat(40 * StructureLevel);
        core.Progression.AddExperience(70f + StructureLevel * 30f, "Empire construction");
        core.Save.RequestSave();
        core.ShowMessage($"Built {CurrentStructureName}. Passive production increased.");
    }

    public void BuyNextSword()
    {
        float cost = GetNextSwordCost();
        if (cost < 0f) { core.ShowMessage("The Golden Ranchenator Sword is already maxed."); return; }
        if (StructureLevel < Mathf.Max(0, SwordLevel)) { core.ShowMessage("Build a stronger Ranch Empire structure first."); return; }
        if (!core.Inventory.TrySpendMoney(cost)) { core.ShowMessage($"Need ${cost:F0} for the next sword."); return; }
        SwordLevel++;
        core.Progression.AddExperience(35f + SwordLevel * 15f, "Sword upgrade");
        core.Save.RequestSave();
        core.ShowMessage($"Sword upgraded to {CurrentSwordName} with {CurrentSwordDamage:F0} damage.");
    }

    public void BuyNextAutomation()
    {
        float cost = GetNextAutomationCost();
        if (cost < 0f) { core.ShowMessage("Auto-bottling is already maxed."); return; }
        if (StructureLevel < 3) { core.ShowMessage("Build the Ranch Laboratory first."); return; }
        if (!core.Inventory.TrySpendMoney(cost)) { core.ShowMessage($"Need ${cost:F0} for the auto-bottler."); return; }
        AutomationLevel++;
        core.Progression.AddExperience(30f + AutomationLevel * 12f, "Automation upgrade");
        core.Save.RequestSave();
        core.ShowMessage($"Auto-bottler upgraded to Level {AutomationLevel}.");
    }

    public void BuyNextResearch()
    {
        float cost = GetNextResearchCost();
        if (cost < 0f) { core.ShowMessage("Ranch production research is maxed."); return; }
        if (StructureLevel < 3) { core.ShowMessage("Build the Ranch Laboratory first."); return; }
        if (!core.Inventory.TrySpendMoney(cost)) { core.ShowMessage($"Need ${cost:F0} for production research."); return; }
        ResearchLevel++;
        core.Progression.AddExperience(35f + ResearchLevel * 14f, "Production research");
        core.Save.RequestSave();
        core.ShowMessage($"Ranch production research upgraded to Level {ResearchLevel}.");
    }

    public void BuyNextMarketUpgrade()
    {
        float cost = GetNextMarketCost();
        if (cost < 0f) { core.ShowMessage("Ranch market research is maxed."); return; }
        if (StructureLevel < 4) { core.ShowMessage("Build the Advanced Ranch Laboratory first."); return; }
        if (!core.Inventory.TrySpendMoney(cost)) { core.ShowMessage($"Need ${cost:F0} for market research."); return; }
        MarketLevel++;
        core.Progression.AddExperience(35f + MarketLevel * 14f, "Market research");
        core.Save.RequestSave();
        core.ShowMessage($"Market research upgraded to Level {MarketLevel}.");
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
        if (AutomationLevel <= 0) return;
        automationTimer += Time.deltaTime;
        if (automationTimer < automationIntervals[AutomationLevel]) return;
        automationTimer = 0f;
        int attempts = 1 + Mathf.FloorToInt(AutomationLevel / 2f);
        for (int i = 0; i < attempts; i++) if (!core.Bottles.TryBottleSelected(false)) break;
    }

    private void RunDefense()
    {
        float damage = defenseDamage[StructureLevel];
        if (damage <= 0f) return;
        defenseTimer += Time.deltaTime;
        if (defenseTimer >= 2.5f)
        {
            defenseTimer = 0f;
            core.Waves.DamageNearestFromDefense(damage, 35f);
        }
    }

    public void RestoreState(int structureLevel, int swordLevel, int automationLevel,
        int researchLevel, int marketLevel)
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
            for (int i = 0; i < empireGroups.Length; i++)
                if (empireGroups[i] != null) empireGroups[i].SetActive(StructureLevel >= i + 1);

        if (empireRoot != null) empireRoot.localScale = Vector3.one * (0.9f + StructureLevel * 0.025f);
        if (empireLabel != null)
            empireLabel.text = $"{CurrentStructureName}\nRanch: {CurrentPassiveRanchRate:0.00}/sec | Money: ${CurrentPassiveMoneyRate:0.00}/sec";
    }

    private void OnGUI()
    {
        if (!IsOpen || core == null) return;
        EnsureStyles();
        Matrix4x4 old = GUI.matrix;
        float scale = Mathf.Min(Screen.width / VirtualWidth, Screen.height / VirtualHeight);
        GUI.matrix = Matrix4x4.TRS(new Vector3((Screen.width - VirtualWidth * scale) * 0.5f,
            (Screen.height - VirtualHeight * scale) * 0.5f, 0f), Quaternion.identity, new Vector3(scale, scale, 1f));
        DrawShop();
        GUI.matrix = old;
    }

    private void DrawShop()
    {
        GUI.Box(new Rect(20, 20, 1560, 860), GUIContent.none, screenStyle);
        GUI.Label(new Rect(60, 40, 900, 55), "RANCH EMPIRE SHOP", titleStyle);
        GUI.Label(new Rect(950, 48, 390, 42), $"Money: ${core.Inventory.Money:F0}", subtitleStyle);
        if (GUI.Button(new Rect(1380, 42, 150, 48), "CLOSE", buttonStyle)) { CloseShop(); GUIUtility.ExitGUI(); }
        GUI.Label(new Rect(60, 100, 1450, 34),
            $"{CurrentStructureName} | Ranch {CurrentPassiveRanchRate:0.00}/sec | Money ${CurrentPassiveMoneyRate:0.00}/sec | Sword {CurrentSwordName}", bodyStyle);

        string[] tabs = { "EMPIRE", "EQUIPMENT", "DEFENSE", "AUTOMATION" };
        for (int i = 0; i < tabs.Length; i++)
        {
            if (GUI.Button(new Rect(70 + i * 370, 150, 340, 55), tabs[i], selectedTab == i ? selectedTabStyle : tabStyle)) selectedTab = i;
        }

        if (selectedTab == 0) DrawEmpirePage();
        else if (selectedTab == 1) DrawEquipmentPage();
        else if (selectedTab == 2) DrawDefensePage();
        else DrawAutomationPage();
    }

    private void DrawEmpirePage()
    {
        float structureCost = GetNextStructureCost();
        DrawCard(new Rect(70, 235, 700, 560), "BUILD THE RANCH EMPIRE", GetStructureDescription());
        DrawButton(new Rect(105, 700, 630, 62), structureCost < 0 ? "GOLDEN RANCH CITADEL COMPLETE" : $"BUILD NEXT STRUCTURE — ${structureCost:F0}", structureCost >= 0, BuyNextStructure);

        float researchCost = GetNextResearchCost();
        DrawCard(new Rect(815, 235, 345, 270), "RANCH RESEARCH",
            $"Level: {ResearchLevel}/6\n\n+20% passive Ranch and +12% hand extraction per level.\n\n{(StructureLevel >= 3 ? "Laboratory online." : "Requires Ranch Laboratory.")}");
        DrawButton(new Rect(845, 425, 285, 55), researchCost < 0 ? "RESEARCH MAXED" : $"RESEARCH — ${researchCost:F0}", researchCost >= 0, BuyNextResearch);

        float marketCost = GetNextMarketCost();
        DrawCard(new Rect(1195, 235, 335, 270), "MARKET RESEARCH",
            $"Level: {MarketLevel}/6\n\n+25% passive money and +10% bottle sale value per level.\n\n{(StructureLevel >= 4 ? "Advanced Lab online." : "Requires Advanced Ranch Laboratory.")}");
        DrawButton(new Rect(1220, 425, 285, 55), marketCost < 0 ? "MARKET MAXED" : $"RESEARCH — ${marketCost:F0}", marketCost >= 0, BuyNextMarketUpgrade);

        DrawCard(new Rect(815, 540, 715, 255), "CURRENT EMPIRE OUTPUT",
            $"Structure: {CurrentStructureName}\n\nRanch: {CurrentPassiveRanchRate:0.00}/sec\nMoney: ${CurrentPassiveMoneyRate:0.00}/sec\nDefense: {defenseDamage[StructureLevel]:F0} damage every 2.5 seconds\n\nHigher structures also raise CJ Heat.");
    }

    private void DrawEquipmentPage()
    {
        float swordCost = GetNextSwordCost();
        DrawCard(new Rect(70, 235, 700, 560), "SWORD UPGRADES", GetSwordDescription());
        DrawButton(new Rect(105, 700, 630, 62), swordCost < 0 ? "SWORD MAXED" : $"UPGRADE SWORD — ${swordCost:F0}", swordCost >= 0, BuyNextSword);

        DrawCard(new Rect(815, 235, 715, 255), "RANCH COLLECTING TOOLS",
            $"Current: {core.Upgrades.CurrentToolName}\nMultiplier: {core.Upgrades.ExtractionMultiplier:0.00}x\n\nNext cost: {(core.Upgrades.ToolTier >= core.Upgrades.MaxToolTier ? "MAXED" : "$" + core.Upgrades.GetNextToolUpgradeCost().ToString("F0"))}");
        DrawButton(new Rect(850, 405, 645, 55), core.Upgrades.ToolTier >= core.Upgrades.MaxToolTier ? "EXTRACTOR MAXED" : "UPGRADE RANCH EXTRACTOR",
            core.Upgrades.ToolTier < core.Upgrades.MaxToolTier, core.Upgrades.BuyNextToolTier);

        DrawCard(new Rect(815, 530, 715, 265), "BOTTLE TECHNOLOGY",
            $"Highest unlocked: {core.Bottles.GetTierName(core.Bottles.UnlockedTier)}\nSelected: {core.Bottles.GetTierName(core.Bottles.SelectedTier)}\n\nNext cost: {(core.Bottles.UnlockedTier >= RanchBottleSystem.TierCount - 1 ? "MAXED" : "$" + core.Upgrades.GetNextBottleUpgradeCost().ToString("F0"))}");
        DrawButton(new Rect(850, 705, 645, 55), core.Bottles.UnlockedTier >= RanchBottleSystem.TierCount - 1 ? "BOTTLES MAXED" : "UNLOCK NEXT BOTTLE SIZE",
            core.Bottles.UnlockedTier < RanchBottleSystem.TierCount - 1, core.Upgrades.BuyNextBottleTier);
    }

    private void DrawDefensePage()
    {
        float healthCost = core.Health.GetNextHealthCost();
        float armorCost = core.Health.GetNextArmorCost();
        float regenCost = core.Health.GetNextRegenerationCost();

        DrawCard(new Rect(70, 235, 455, 330), "MAXIMUM HEALTH",
            $"Current: {core.Health.MaxHealth:F0} HP\nLevel: {core.Health.HealthLevel}/6\n\nSurvive stronger Ranch Raiders.");
        DrawButton(new Rect(100, 485, 395, 55), healthCost < 0 ? "HEALTH MAXED" : $"UPGRADE — ${healthCost:F0}", healthCost >= 0, core.Health.BuyHealthUpgrade);

        DrawCard(new Rect(570, 235, 455, 330), "ARMOR",
            $"Damage reduction: {core.Health.ArmorPercent:F0}%\nLevel: {core.Health.ArmorLevel}/6\n\nArmor reduces every hit.");
        DrawButton(new Rect(600, 485, 395, 55), armorCost < 0 ? "ARMOR MAXED" : $"UPGRADE — ${armorCost:F0}", armorCost >= 0, core.Health.BuyArmorUpgrade);

        DrawCard(new Rect(1070, 235, 460, 330), "REGENERATION",
            $"Current: {core.Health.RegenerationPerSecond:0.00} HP/sec\nLevel: {core.Health.RegenerationLevel}/6\n\nAutomatically restores health.");
        DrawButton(new Rect(1100, 485, 400, 55), regenCost < 0 ? "REGEN MAXED" : $"UPGRADE — ${regenCost:F0}", regenCost >= 0, core.Health.BuyRegenerationUpgrade);

        float healCost = core.Health.GetFullHealCost();
        DrawCard(new Rect(70, 610, 1460, 185), "RANCH MEDICAL BAY",
            $"Current health: {core.Health.CurrentHealth:F0} / {core.Health.MaxHealth:F0}\n\nBuy a full heal before a difficult wave.");
        DrawButton(new Rect(990, 680, 480, 62), healCost <= 0 ? "ALREADY FULL HEALTH" : $"FULL HEAL — ${healCost:F0}", healCost > 0, core.Health.BuyFullHeal);
    }

    private void DrawAutomationPage()
    {
        float automationCost = GetNextAutomationCost();
        string status = AutomationLevel <= 0 ? "Not installed." : $"Fills selected bottles every {automationIntervals[AutomationLevel]:0.0} seconds.";
        DrawCard(new Rect(70, 235, 700, 560), "AUTOMATIC BOTTLING",
            $"Level: {AutomationLevel}/6\n\n{status}\n\nHigher levels fill multiple bottles. Requires Ranch Laboratory.");
        DrawButton(new Rect(105, 700, 630, 62), automationCost < 0 ? "AUTO-BOTTLER MAXED" : $"UPGRADE AUTO-BOTTLER — ${automationCost:F0}", automationCost >= 0, BuyNextAutomation);

        DrawCard(new Rect(815, 235, 715, 255), "DREW",
            $"Current level: {core.Drew.Level}/10\n\nDrew automatically extracts Ranch and fills bottles.");
        DrawButton(new Rect(850, 405, 645, 55), core.Drew.Level >= 10 ? "DREW MAXED" : $"{(core.Drew.IsHired ? "UPGRADE" : "HIRE")} DREW — ${core.Drew.GetUpgradeCost():F0}",
            core.Drew.Level < 10, core.Drew.HireOrUpgrade);

        DrawCard(new Rect(815, 530, 715, 265), "AUTOMATION STATUS",
            $"Selected bottle: {core.Bottles.GetTierName(core.Bottles.SelectedTier)}\nRaw Ranch: {core.Inventory.RawRanch:F1}\nSelected bottles stored: {core.Inventory.GetBottleCount(core.Bottles.SelectedTier)}\n\nUse [ and ] outside the shop to change bottle size.");
    }

    private string GetStructureDescription()
    {
        if (StructureLevel >= structureNames.Length - 1)
            return $"Current: Golden Ranch Citadel\n\nThe ultimate structure is complete.\n\nPassive Ranch: {CurrentPassiveRanchRate:0.00}/sec\nPassive money: ${CurrentPassiveMoneyRate:0.00}/sec";
        int next = StructureLevel + 1;
        return $"Current: {CurrentStructureName}\n\nNext: {structureNames[next]}\nCost: ${structureCosts[StructureLevel]:F0}\n\nNext base output:\n{passiveRanch[next]:0.00} Ranch/sec\n${passiveMoney[next]:0.00}/sec\nDefense: {defenseDamage[next]:F0} damage\n\nProgress from a Ranch Stand through laboratories and finally the Ranch Citadel.";
    }

    private string GetSwordDescription()
    {
        if (SwordLevel >= swordNames.Length - 1)
            return $"Current: {CurrentSwordName}\nDamage: {CurrentSwordDamage:F0}\n\nThe ultimate sword is complete.";
        int next = SwordLevel + 1;
        return $"Current: {CurrentSwordName}\nDamage: {CurrentSwordDamage:F0}\n\nNext: {swordNames[next]}\nDamage: {swordDamage[next]:F0}\nCost: ${swordCosts[SwordLevel]:F0}\n\nPress Space outside the shop to attack.";
    }

    private void DrawCard(Rect rect, string heading, string body)
    {
        GUI.Box(rect, GUIContent.none, cardStyle);
        GUI.Label(new Rect(rect.x + 18, rect.y + 15, rect.width - 36, 40), heading, subtitleStyle);
        GUI.Label(new Rect(rect.x + 25, rect.y + 70, rect.width - 50, rect.height - 95), body, bodyStyle);
    }

    private void DrawButton(Rect rect, string label, bool enabled, System.Action action)
    {
        bool old = GUI.enabled;
        GUI.enabled = enabled;
        if (GUI.Button(rect, label, buttonStyle)) action?.Invoke();
        GUI.enabled = old;
    }

    private void EnsureStyles()
    {
        if (stylesReady) return;
        screenTexture = MakeTexture(new Color(0.015f, 0.025f, 0.035f, 0.98f));
        cardTexture = MakeTexture(new Color(0.07f, 0.11f, 0.15f, 0.98f));
        selectedTexture = MakeTexture(new Color(0.10f, 0.55f, 0.48f, 1f));
        buttonTexture = MakeTexture(new Color(0.12f, 0.35f, 0.45f, 1f));
        hoverTexture = MakeTexture(new Color(0.16f, 0.52f, 0.60f, 1f));

        screenStyle = new GUIStyle(GUI.skin.box); screenStyle.normal.background = screenTexture;
        cardStyle = new GUIStyle(GUI.skin.box); cardStyle.normal.background = cardTexture;
        titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 39, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
        titleStyle.normal.textColor = Color.white;
        subtitleStyle = new GUIStyle(GUI.skin.label) { fontSize = 25, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, wordWrap = true };
        subtitleStyle.normal.textColor = Color.white;
        bodyStyle = new GUIStyle(GUI.skin.label) { fontSize = 20, alignment = TextAnchor.UpperLeft, wordWrap = true };
        bodyStyle.normal.textColor = Color.white;
        buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = 20, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        buttonStyle.normal.background = buttonTexture; buttonStyle.hover.background = hoverTexture; buttonStyle.active.background = selectedTexture;
        buttonStyle.normal.textColor = Color.white; buttonStyle.hover.textColor = Color.white; buttonStyle.active.textColor = Color.white;
        tabStyle = new GUIStyle(buttonStyle) { fontSize = 22 };
        selectedTabStyle = new GUIStyle(tabStyle); selectedTabStyle.normal.background = selectedTexture;
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
