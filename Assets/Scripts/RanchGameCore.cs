using System;
using UnityEngine;
using UnityEngine.SceneManagement;

public class RanchGameCore : MonoBehaviour
{
    public static RanchGameCore Instance { get; private set; }

    public RanchInventory Inventory { get; private set; }
    public RanchBottleSystem Bottles { get; private set; }
    public RanchUpgradeSystem Upgrades { get; private set; }
    public RanchTreeSystem Tree { get; private set; }
    public RanchHealthSystem Health { get; private set; }
    public RanchStaminaSystem Stamina { get; private set; }
    public RanchClassSystem Classes { get; private set; }
    public RanchLaboratorySystem Laboratory { get; private set; }
    public RanchEquipmentSystem Equipment { get; private set; }
    public RanchDeployableSystem Deployables { get; private set; }
    public RanchCombatSystem Combat { get; private set; }
    public RanchProgressionSystem Progression { get; private set; }
    public RanchAreaSystem Areas { get; private set; }
    public RanchBossSystem Bosses { get; private set; }
    public RanchWaveSystem Waves { get; private set; }
    public RanchDrewSystem Drew { get; private set; }
    public RanchShopSystem Shop { get; private set; }
    public RanchCJSystem CJ { get; private set; }
    public RanchSaveSystem Save { get; private set; }
    public RanchSettingsSystem Settings { get; private set; }
    public RanchSpaceSystem Space { get; private set; }
    public RanchJuiceSystem Juice { get; private set; }
    public RanchMomentumSystem Momentum { get; private set; }
    public RanchDialogueSystem Dialogue { get; private set; }
    public RanchFacilitySystem Facility { get; private set; }

    public RanchPlayerController Player { get; private set; }
    public Transform RanchTreeTransform { get; private set; }
    public int BottlesSold { get; private set; }
    public int CJHeat { get; private set; }
    public bool GameWon { get; private set; }
    public string StatusMessage { get; private set; } = "";
    public float StatusMessageTime { get; private set; }

    public event Action ResourcesChanged;
    public event Action<string> MessageChanged;
    public event Action GameWonEvent;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    public void Initialize(
        RanchInventory inventory,
        RanchBottleSystem bottles,
        RanchUpgradeSystem upgrades,
        RanchTreeSystem tree,
        RanchHealthSystem health,
        RanchStaminaSystem stamina,
        RanchClassSystem classes,
        RanchLaboratorySystem laboratory,
        RanchEquipmentSystem equipment,
        RanchDeployableSystem deployables,
        RanchCombatSystem combat,
        RanchProgressionSystem progression,
        RanchAreaSystem areas,
        RanchBossSystem bosses,
        RanchWaveSystem waves,
        RanchDrewSystem drew,
        RanchShopSystem shop,
        RanchCJSystem cj,
        RanchSaveSystem save,
        RanchSettingsSystem settings,
        RanchSpaceSystem space,
        RanchJuiceSystem juice,
        RanchMomentumSystem momentum,
        RanchDialogueSystem dialogue,
        RanchFacilitySystem facility)
    {
        Inventory = inventory;
        Bottles = bottles;
        Upgrades = upgrades;
        Tree = tree;
        Health = health;
        Stamina = stamina;
        Classes = classes;
        Laboratory = laboratory;
        Equipment = equipment;
        Deployables = deployables;
        Combat = combat;
        Progression = progression;
        Areas = areas;
        Bosses = bosses;
        Waves = waves;
        Drew = drew;
        Shop = shop;
        CJ = cj;
        Save = save;
        Settings = settings;
        Space = space;
        Juice = juice;
        Momentum = momentum;
        Dialogue = dialogue;
        Facility = facility;
    }

    /// <summary>
    /// True when any full-screen menu owns the pause/cursor. Every menu must
    /// check this before opening, otherwise two menus can stack, each capture
    /// the other's zeroed timeScale, and leave the world paused or unpaused at
    /// the wrong moment. Keeping the list in one place stops new menus (like the
    /// Ranch Rocket console) from being forgotten by the older systems.
    /// </summary>
    public bool IsAnyMenuOpen =>
        (Shop != null && Shop.IsOpen) ||
        (Progression != null && Progression.IsOpen) ||
        (Settings != null && Settings.IsOpen) ||
        (Classes != null && Classes.IsOpen) ||
        (Laboratory != null && Laboratory.IsOpen) ||
        (Space != null && Space.IsOpen) ||
        (Dialogue != null && Dialogue.IsOpen);

    /// <summary>
    /// True whenever the player should not be receiving gameplay input at all:
    /// a menu is open, the run is over, or they are dead/downed.
    /// </summary>
    public bool IsPlayerBusy =>
        IsAnyMenuOpen ||
        GameWon ||
        (Health != null && (Health.IsDead || Health.IsDowned));

    public void RegisterWorld(RanchPlayerController player, Transform ranchTree)
    {
        Player = player;
        RanchTreeTransform = ranchTree;
        Combat.RegisterPlayer(player);
        Progression.RegisterPlayer(player);
        Classes.RegisterPlayer(player);
        Laboratory.RegisterPlayer(player);
        Settings.RegisterPlayer(player);
    }

    public void RegisterBottleSale(int bottleCount, int ranchUnitsSold)
    {
        BottlesSold += Mathf.Max(0, bottleCount);
        CJHeat += Mathf.Max(0, ranchUnitsSold);
        Progression.AddExperience(Mathf.Max(2f, ranchUnitsSold * 0.18f), "Bottle sale");
        CJ.CheckProgress();
        Save.RequestSave();
        NotifyResourcesChanged();
    }

    public void AddCJHeat(int amount)
    {
        CJHeat += Mathf.Max(0, amount);
        CJ.CheckProgress();
        Save.RequestSave();
        NotifyResourcesChanged();
    }

    public void RestoreProgress(int bottlesSold, int cjHeat, bool gameWon)
    {
        BottlesSold = Mathf.Max(0, bottlesSold);
        CJHeat = Mathf.Max(0, cjHeat);
        GameWon = gameWon;
        CJ.CheckProgress();

        // Never disable the player on load. A completed run is still playable,
        // and a frozen player with no restart key is unrecoverable.
    }

    public void NotifyResourcesChanged()
    {
        ResourcesChanged?.Invoke();
    }

    public void ShowMessage(string message, float seconds = 5f)
    {
        StatusMessage = message;
        StatusMessageTime = Mathf.Max(0f, seconds);
        MessageChanged?.Invoke(message);
        Debug.Log(message);
    }

    public void WinGame()
    {
        if (GameWon)
            return;

        // Defeating CJ is no longer the end — it opens the Cosmic Journey. Only
        // once that journey is finished (Cosmic CJ defeated) does the game end.
        if (Space != null && !Space.JourneyCompleted)
        {
            Progression.AddExperience(1000f, "Defeated CJ");
            ShowMessage("CJ falls — but a cosmic rift tears open above the ranch...", 8f);
            GameWonEvent?.Invoke();
            Space.BeginJourney();
            Save.SaveGame(false);
            return;
        }

        GameWon = true;
        Progression.AddExperience(1500f, "Defeated Cosmic CJ");
        ShowMessage(
            "You defeated Cosmic CJ. Every ranch in the galaxy is free.",
            999f
        );
        GameWonEvent?.Invoke();

        // The player is deliberately left in control. Freezing them here used to
        // be fine because R restarted the game; with that gone it would be a
        // soft lock. The ranch is theirs to keep playing.
        Save.SaveGame(false);
    }

    public void RestartScene()
    {
        Time.timeScale = 1f;
        Scene scene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(scene.name);
    }

    private void Update()
    {
        if (StatusMessageTime > 0f)
            StatusMessageTime -= Time.unscaledDeltaTime;

        // No R-to-erase. Wiping a completed run from the victory screen was the
        // single most destructive key in the game and repeatedly cost players
        // their save. Ctrl+Shift+Delete (RanchAdminSystem) is the deliberate,
        // confirmed way to clear save data.

        // Single-player only: in multiplayer death becomes a revivable "downed"
        // state, so reloading the scene here would needlessly break the session.
        if (Health != null && Health.IsDead && !RanchGameModeState.IsMultiplayer &&
            Input.GetKeyDown(KeyCode.R))
            RestartScene();
    }

}
