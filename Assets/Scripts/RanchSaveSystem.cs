using System;
using System.IO;
using UnityEngine;

[Serializable]
public class RanchSaveData
{
    public int saveVersion = 8;
    public float rawRanch;
    public float totalRanchCollected;
    public float money;
    public int[] bottleCounts = new int[RanchBottleSystem.TierCount];
    public int unlockedBottleTier;
    public int selectedBottleTier;
    public int toolTier;
    public float lifetimeRanchExtracted;
    public float currentHealth;
    public int healthLevel;
    public int armorLevel;
    public int regenerationLevel;
    public int drewLevel;
    public int structureLevel;
    public int swordLevel;
    public int automationLevel;
    public int researchLevel;
    public int marketLevel;
    public int bottlesSold;
    public int cjHeat;
    public bool gameWon;
    public int highestWaveCleared;
    public int progressionLevel;
    public float progressionExperience;
    public int skillPoints;
    public int combatTraining;
    public int survivalTraining;
    public int engineeringTraining;
    public float currentStamina;
    public bool cjHasWarned;
    public bool cjBattleUnlocked;
    public int cjMilestoneIndex;
    public float playerX;
    public float playerY;
    public float playerZ;
    public float playerRotationY;
    public bool[] unlockedAreas = new bool[RanchAreaSystem.AreaCount];
    public int activeEquipmentSlot = 1;
    public int equippedWeapon;
    public bool spearUnlocked;
    public bool bowUnlocked;
    public int trapCount;
    public bool deluluWandUnlocked;
    public int currentClass;
    public bool oakberryIntroduced;
    public int lifetimeKnowledgePoints;
    public int[] classPowerLevels = new int[4];
    public int[] classTechniqueLevels = new int[4];
    public int[] classMasteryLevels = new int[4];
    public int spearWeaponLevel;
    public int bowWeaponLevel;
    public int summonerWeaponLevel;
    public int[] weaponModificationLevels = new int[4];

    // Cosmic Journey (post-CJ endgame) progress.
    public bool spaceJourneyUnlocked;
    public int spacePlanetIndex;
    public float spaceFuel;
    public bool cosmicCJDefeated;
}

public class RanchSaveSystem : MonoBehaviour
{
    public string SaveFileName = "RanchSimulatorSave.json";
    public float AutosaveInterval = 60f;
    public KeyCode SaveKey = KeyCode.Z;
    public KeyCode LoadKey = KeyCode.X;

    public bool HasSaveFile
    {
        get { return File.Exists(SavePath); }
    }

    public string LastSaveStatus { get; private set; } = "No save yet";

    public string SavePath
    {
        get
        {
            return Path.Combine(
                Application.persistentDataPath,
                SaveFileName
            );
        }
    }

    private RanchGameCore core;
    private float autosaveTimer;
    private bool dirty;
    private bool loading;

    public void Initialize(RanchGameCore gameCore)
    {
        core = gameCore;
        autosaveTimer = Mathf.Max(5f, AutosaveInterval);
        dirty = false;

        Debug.Log(
            "Ranch save file location:\n" +
            SavePath
        );
    }

    public bool LoadOnStartup()
    {
        if (!HasSaveFile)
        {
            LastSaveStatus = "No existing save";
            return false;
        }

        return LoadGame(false);
    }

    private void Update()
    {
        if (core == null || loading)
        {
            return;
        }

        if (Input.GetKeyDown(SaveKey))
        {
            Debug.Log(
                "Manual save key detected: " +
                SaveKey
            );

            SaveGame(true);
        }

        if (Input.GetKeyDown(LoadKey))
        {
            Debug.Log(
                "Manual load key detected: " +
                LoadKey
            );

            LoadGame(true);
        }

        autosaveTimer -= Time.unscaledDeltaTime;

        if (autosaveTimer <= 0f)
        {
            autosaveTimer =
                Mathf.Max(
                    5f,
                    AutosaveInterval
                );

            if (dirty)
            {
                SaveGame(false);
            }
        }
    }

    public void RequestSave()
    {
        if (!loading)
        {
            dirty = true;
        }
    }

    public void SaveGame(bool showMessage)
    {
        if (loading)
        {
            ReportSaveFailure(
                "Save blocked because a game is currently loading.",
                showMessage
            );

            return;
        }

        if (core == null)
        {
            ReportSaveFailure(
                "Save failed because RanchGameCore is missing.",
                showMessage
            );

            return;
        }

        if (core.Player == null)
        {
            ReportSaveFailure(
                "Save failed because the player has not been registered.",
                showMessage
            );

            return;
        }

        try
        {
            Directory.CreateDirectory(
                Application.persistentDataPath
            );

            RanchSaveData data =
                BuildSaveData();

            string json =
                JsonUtility.ToJson(
                    data,
                    true
                );

            File.WriteAllText(
                SavePath,
                json
            );

            if (!File.Exists(SavePath))
            {
                throw new IOException(
                    "Unity completed the write call, but the save file was not found afterward."
                );
            }

            dirty = false;

            autosaveTimer =
                Mathf.Max(
                    5f,
                    AutosaveInterval
                );

            LastSaveStatus =
                "Saved " +
                DateTime.Now.ToString(
                    "h:mm:ss tt"
                );

            Debug.Log(
                "Ranch Simulator saved successfully.\n" +
                "Path: " +
                SavePath +
                "\nBytes: " +
                new FileInfo(SavePath).Length
            );

            if (showMessage)
            {
                core.ShowMessage(
                    "Game saved successfully. Press X to load.",
                    6f
                );
            }
        }
        catch (Exception exception)
        {
            LastSaveStatus =
                "Save failed";

            Debug.LogError(
                "Ranch Simulator save failed.\n" +
                exception
            );

            if (showMessage)
            {
                core.ShowMessage(
                    "Save failed. Check the first red Console error.",
                    8f
                );
            }
        }
    }

    public bool LoadGame(bool showMessage)
    {

        if (core == null)
        {
            ReportLoadFailure(
                "Load failed because RanchGameCore is missing.",
                showMessage
            );

            return false;
        }

        if (!HasSaveFile)
        {
            LastSaveStatus =
                "No save file";

            if (showMessage)
            {
                core.ShowMessage(
                    "No Ranch Simulator save exists yet. Press Z to save.",
                    7f
                );
            }

            Debug.LogWarning(
                "No Ranch Simulator save exists at:\n" +
                SavePath
            );

            return false;
        }

        try
        {
            loading = true;

            string json =
                File.ReadAllText(
                    SavePath
                );

            if (string.IsNullOrWhiteSpace(json))
            {
                throw new Exception(
                    "The save file was empty."
                );
            }

            RanchSaveData data =
                JsonUtility.FromJson<RanchSaveData>(
                    json
                );

            if (data == null)
            {
                throw new Exception(
                    "Unity could not deserialize the save file."
                );
            }

            ApplySaveData(data);

            dirty = false;

            autosaveTimer =
                Mathf.Max(
                    5f,
                    AutosaveInterval
                );

            LastSaveStatus =
                "Loaded " +
                DateTime.Now.ToString(
                    "h:mm:ss tt"
                );

            Debug.Log(
                "Ranch Simulator loaded successfully from:\n" +
                SavePath
            );

            if (showMessage)
            {
                core.ShowMessage(
                    "Save loaded. Active waves restart from an intermission.",
                    7f
                );
            }

            return true;
        }
        catch (Exception exception)
        {
            LastSaveStatus =
                "Load failed";

            Debug.LogError(
                "Ranch Simulator load failed.\n" +
                exception
            );

            if (showMessage)
            {
                core.ShowMessage(
                    "Load failed. Check the first red Console error.",
                    8f
                );
            }

            return false;
        }
        finally
        {
            loading = false;
        }
    }

    /// <summary>
    /// Deletes every save slot, not just the active one. "Wipe save data" means
    /// both the host file and the multiplayer guest file — deleting only the
    /// slot currently selected would silently leave the other behind.
    /// </summary>
    public void DeleteAllSaves()
    {
        string original = SaveFileName;

        foreach (string slot in new[]
                 {
                     "RanchSimulatorSave.json",
                     "RanchSimulatorGuestSave.json"
                 })
        {
            SaveFileName = slot;
            DeleteSave();
        }

        SaveFileName = original;
    }

    public void DeleteSave()
    {
        try
        {
            if (HasSaveFile)
            {
                File.Delete(
                    SavePath
                );
            }

            dirty = false;
            LastSaveStatus =
                "Save deleted";

            Debug.Log(
                "Ranch Simulator save deleted:\n" +
                SavePath
            );
        }
        catch (Exception exception)
        {
            Debug.LogError(
                "Could not delete the Ranch Simulator save.\n" +
                exception
            );
        }
    }

    private RanchSaveData BuildSaveData()
    {
        RanchSaveData data = new RanchSaveData();

        data.rawRanch = core.Inventory.RawRanch;
        data.totalRanchCollected = core.Inventory.TotalRanchCollected;
        data.money = core.Inventory.Money;
        data.bottleCounts = core.Inventory.GetBottleCountsCopy();
        data.unlockedBottleTier = core.Bottles.UnlockedTier;
        data.selectedBottleTier = core.Bottles.SelectedTier;
        data.toolTier = core.Upgrades.ToolTier;
        data.lifetimeRanchExtracted = core.Tree.LifetimeRanchExtracted;
        data.currentHealth = core.Health.CurrentHealth;
        data.healthLevel = core.Health.HealthLevel;
        data.armorLevel = core.Health.ArmorLevel;
        data.regenerationLevel = core.Health.RegenerationLevel;
        data.drewLevel = core.Drew.Level;

        data.structureLevel = core.Shop.StructureLevel;
        data.swordLevel = core.Shop.SwordLevel;
        data.spearWeaponLevel = core.Shop.SpearLevel;
        data.bowWeaponLevel = core.Shop.BowLevel;
        data.summonerWeaponLevel = core.Shop.SummonerLevel;
        data.weaponModificationLevels = core.Shop.GetWeaponModificationLevelsCopy();
        data.automationLevel = core.Shop.AutomationLevel;
        data.researchLevel = core.Shop.ResearchLevel;
        data.marketLevel = core.Shop.MarketLevel;

        data.bottlesSold = core.BottlesSold;
        data.cjHeat = core.CJHeat;
        data.gameWon = core.GameWon;
        data.highestWaveCleared = core.Waves.HighestWaveCleared;

        data.progressionLevel = core.Progression.Level;
        data.progressionExperience = core.Progression.Experience;
        data.skillPoints = core.Progression.KnowledgePoints;
        data.lifetimeKnowledgePoints = core.Progression.LifetimeKnowledgePoints;
        data.classPowerLevels = core.Progression.GetPowerLevelsCopy();
        data.classTechniqueLevels = core.Progression.GetTechniqueLevelsCopy();
        data.classMasteryLevels = core.Progression.GetMasteryLevelsCopy();

        // Legacy fields remain populated for backwards-readable JSON.
        data.combatTraining = core.Progression.CombatTraining;
        data.survivalTraining = 0;
        data.engineeringTraining = 0;

        data.currentClass = (int)core.Classes.CurrentClass;
        data.oakberryIntroduced = core.Classes.OakberryIntroduced;
        data.currentStamina = core.Stamina.CurrentStamina;
        data.cjHasWarned = core.CJ.HasWarned;
        data.cjBattleUnlocked = core.CJ.BattleUnlocked;
        data.cjMilestoneIndex = core.CJ.MilestoneIndex;
        data.unlockedAreas = core.Areas.GetUnlockStateCopy();
        data.activeEquipmentSlot = core.Equipment.ActiveSlot;
        data.equippedWeapon = (int)core.Equipment.EquippedWeapon;
        data.spearUnlocked = core.Equipment.SpearUnlocked;
        data.bowUnlocked = core.Equipment.BowUnlocked;
        data.trapCount = core.Deployables.TrapCount;
        data.deluluWandUnlocked = core.Deployables.DeluluWandUnlocked;

        Vector3 position = core.Player.transform.position;
        data.playerX = position.x;
        data.playerY = position.y;
        data.playerZ = position.z;
        data.playerRotationY = core.Player.transform.eulerAngles.y;

        if (core.Space != null)
        {
            data.spaceJourneyUnlocked = core.Space.JourneyUnlocked;
            data.spacePlanetIndex = core.Space.PlanetIndex;
            data.spaceFuel = core.Space.Fuel;
            data.cosmicCJDefeated = core.Space.CosmicCJDefeated;
        }

        return data;
    }

    private void ApplySaveData(RanchSaveData data)
    {
        core.Waves.PrepareForLoad();
        core.Deployables.PrepareForLoad();
        core.Bosses.ResetBossState();

        float restoredTotalRanch = data.saveVersion >= 5
            ? data.totalRanchCollected
            : GetMigratedTotalRanch(data);

        core.Inventory.RestoreState(
            data.rawRanch,
            restoredTotalRanch,
            data.money,
            data.bottleCounts
        );

        core.Bottles.RestoreState(data.unlockedBottleTier, data.selectedBottleTier);
        core.Upgrades.RestoreState(data.toolTier);
        core.Tree.RestoreState(data.lifetimeRanchExtracted);

        if (data.saveVersion >= 7)
        {
            core.Shop.RestoreState(
                data.structureLevel,
                data.swordLevel,
                data.spearWeaponLevel,
                data.bowWeaponLevel,
                data.summonerWeaponLevel,
                data.automationLevel,
                data.researchLevel,
                data.marketLevel,
                data.weaponModificationLevels
            );
        }
        else
        {
            core.Shop.RestoreState(
                data.structureLevel,
                data.swordLevel,
                data.automationLevel,
                data.researchLevel,
                data.marketLevel
            );
        }

        int restoredClass;
        bool introduced;
        if (data.saveVersion >= 7)
        {
            restoredClass = Mathf.Clamp(data.currentClass, 0, 3);
            introduced = data.oakberryIntroduced || data.lifetimeKnowledgePoints > 0 ||
                data.skillPoints > 0 || data.progressionLevel > 1;
        }
        else
        {
            if (data.activeEquipmentSlot == 3 && data.deluluWandUnlocked)
                restoredClass = (int)RanchClassType.Summoner;
            else if (data.equippedWeapon == (int)RanchWeaponType.Spear)
                restoredClass = (int)RanchClassType.Spear;
            else if (data.equippedWeapon == (int)RanchWeaponType.Bow)
                restoredClass = (int)RanchClassType.Ranged;
            else
                restoredClass = (int)RanchClassType.Sword;

            introduced = data.progressionLevel > 1 || data.skillPoints > 0 ||
                data.combatTraining > 0 || data.survivalTraining > 0 || data.engineeringTraining > 0;
        }

        core.Classes.RestoreState(restoredClass, introduced);

        if (data.saveVersion >= 7)
        {
            core.Progression.RestoreState(
                data.progressionLevel,
                data.progressionExperience,
                data.skillPoints,
                data.lifetimeKnowledgePoints,
                data.classPowerLevels,
                data.classTechniqueLevels,
                data.classMasteryLevels
            );
        }
        else
        {
            core.Progression.RestoreLegacyState(
                data.progressionLevel,
                data.progressionExperience,
                data.skillPoints,
                data.combatTraining,
                data.survivalTraining,
                data.engineeringTraining
            );
        }

        core.Stamina.RestoreState(data.currentStamina);
        core.Health.RestoreState(
            data.currentHealth,
            data.healthLevel,
            data.armorLevel,
            data.regenerationLevel
        );
        core.Drew.RestoreState(data.drewLevel);

        if (data.saveVersion >= 4)
        {
            core.Areas.RestoreState(data.unlockedAreas);
            core.Equipment.RestoreState(
                data.activeEquipmentSlot,
                data.equippedWeapon,
                data.spearUnlocked,
                data.bowUnlocked
            );
        }
        else
        {
            bool[] migratedAreas = new bool[RanchAreaSystem.AreaCount];
            migratedAreas[0] = true;
            migratedAreas[1] = data.structureLevel >= 3;
            migratedAreas[2] = data.structureLevel >= 6;
            migratedAreas[3] = data.structureLevel >= 8;
            core.Areas.RestoreState(migratedAreas);
            core.Equipment.RestoreState(1, 0, false, false);
        }

        core.Deployables.RestoreState(
            data.saveVersion >= 6 ? data.trapCount : 0,
            data.saveVersion >= 6 && data.deluluWandUnlocked
        );
        core.Classes.EnsureCurrentClassEquipment(false);

        core.CJ.RestoreState(
            data.cjHasWarned,
            data.cjBattleUnlocked,
            data.cjMilestoneIndex
        );
        core.RestoreProgress(data.bottlesSold, data.cjHeat, data.gameWon);
        core.Waves.RestoreProgress(data.highestWaveCleared);

        core.Player.Teleport(
            new Vector3(data.playerX, data.playerY, data.playerZ),
            data.playerRotationY
        );

        if (core.Space != null && data.saveVersion >= 8)
        {
            core.Space.RestoreState(
                data.spaceJourneyUnlocked,
                data.spacePlanetIndex,
                data.spaceFuel,
                data.cosmicCJDefeated
            );
        }

        core.NotifyResourcesChanged();
    }

    private float GetMigratedTotalRanch(
        RanchSaveData data)
    {
        float migrated = Mathf.Max(
            data.rawRanch,
            data.lifetimeRanchExtracted
        );

        if (data.unlockedAreas != null)
        {
            for (int i = 1; i < RanchAreaSystem.AreaCount && i < data.unlockedAreas.Length; i++)
            {
                if (data.unlockedAreas[i])
                    migrated = Mathf.Max(migrated, core.Areas.GetUnlockRequirement(i));
            }
        }

        return migrated;
    }

    private void ReportSaveFailure(
        string message,
        bool showMessage)
    {
        LastSaveStatus =
            "Save failed";

        Debug.LogError(message);

        if (showMessage &&
            core != null)
        {
            core.ShowMessage(
                "Save failed. Check the Unity Console.",
                8f
            );
        }
    }

    private void ReportLoadFailure(
        string message,
        bool showMessage)
    {
        LastSaveStatus =
            "Load failed";

        Debug.LogError(message);

        if (showMessage &&
            core != null)
        {
            core.ShowMessage(
                "Load failed. Check the Unity Console.",
                8f
            );
        }
    }

    private void OnApplicationPause(
        bool paused)
    {
        if (paused && dirty)
        {
            SaveGame(false);
        }
    }

    private void OnApplicationQuit()
    {
        if (dirty)
        {
            SaveGame(false);
        }
    }
}
