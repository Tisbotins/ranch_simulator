using UnityEngine;

/// <summary>
/// Add only this component to one empty GameObject named RanchGame.
/// It creates, initializes, and connects every other game system.
/// </summary>
[DefaultExecutionOrder(-1000)]
[DisallowMultipleComponent]
public class RanchGameBootstrap : MonoBehaviour
{
    private bool hasBootstrapped;

    private void Awake()
    {
        if (hasBootstrapped)
            return;

        hasBootstrapped = true;

        RanchGameCore core = GetOrAdd<RanchGameCore>();
        RanchInventory inventory = GetOrAdd<RanchInventory>();
        RanchBottleSystem bottles = GetOrAdd<RanchBottleSystem>();
        RanchUpgradeSystem upgrades = GetOrAdd<RanchUpgradeSystem>();
        RanchTreeSystem tree = GetOrAdd<RanchTreeSystem>();
        RanchHealthSystem health = GetOrAdd<RanchHealthSystem>();
        RanchStaminaSystem stamina = GetOrAdd<RanchStaminaSystem>();
        RanchClassSystem classes = GetOrAdd<RanchClassSystem>();
        RanchLaboratorySystem laboratory = GetOrAdd<RanchLaboratorySystem>();
        RanchEquipmentSystem equipment = GetOrAdd<RanchEquipmentSystem>();
        RanchDeployableSystem deployables = GetOrAdd<RanchDeployableSystem>();
        RanchCombatSystem combat = GetOrAdd<RanchCombatSystem>();
        RanchProgressionSystem progression = GetOrAdd<RanchProgressionSystem>();
        RanchAreaSystem areas = GetOrAdd<RanchAreaSystem>();
        RanchBossSystem bosses = GetOrAdd<RanchBossSystem>();
        RanchWaveSystem waves = GetOrAdd<RanchWaveSystem>();
        RanchDrewSystem drew = GetOrAdd<RanchDrewSystem>();
        RanchShopSystem shop = GetOrAdd<RanchShopSystem>();
        RanchCJSystem cj = GetOrAdd<RanchCJSystem>();
        RanchSaveSystem save = GetOrAdd<RanchSaveSystem>();
        RanchSettingsSystem settings = GetOrAdd<RanchSettingsSystem>();
        // Sets the global IMGUI font before any other OnGUI runs.
        GetOrAdd<RanchFontHook>();

        RanchSpaceSystem space = GetOrAdd<RanchSpaceSystem>();
        RanchJuiceSystem juice = GetOrAdd<RanchJuiceSystem>();
        RanchMomentumSystem momentum = GetOrAdd<RanchMomentumSystem>();
        RanchDialogueSystem dialogue = GetOrAdd<RanchDialogueSystem>();
        RanchFacilitySystem facility = GetOrAdd<RanchFacilitySystem>();
        RanchAdminSystem admin = GetOrAdd<RanchAdminSystem>();
        RanchWorldBuilder world = GetOrAdd<RanchWorldBuilder>();
        RanchUI ui = GetOrAdd<RanchUI>();
        RanchLanMultiplayer multiplayer = GetOrAdd<RanchLanMultiplayer>();
        RanchTitleScreen titleScreen = GetOrAdd<RanchTitleScreen>();

        core.Initialize(
            inventory,
            bottles,
            upgrades,
            tree,
            health,
            stamina,
            classes,
            laboratory,
            equipment,
            deployables,
            combat,
            progression,
            areas,
            bosses,
            waves,
            drew,
            shop,
            cj,
            save,
            settings,
            space,
            juice,
            momentum,
            dialogue,
            facility
        );

        inventory.Initialize(core);
        bottles.Initialize(core);
        upgrades.Initialize(core);
        tree.Initialize(core);
        health.Initialize(core);
        stamina.Initialize(core);
        classes.Initialize(core);
        laboratory.Initialize(core);
        equipment.Initialize(core);
        deployables.Initialize(core);
        combat.Initialize(core);
        progression.Initialize(core);
        areas.Initialize(core);
        bosses.Initialize(core);
        waves.Initialize(core);
        drew.Initialize(core);
        shop.Initialize(core);
        cj.Initialize(core);
        save.Initialize(core);
        settings.Initialize(core);
        space.Initialize(core);
        juice.Initialize(core);
        momentum.Initialize(core);
        dialogue.Initialize(core);
        facility.Initialize(core);
        admin.Initialize(core);
        ui.Initialize(core);
        multiplayer.Initialize(core);
        titleScreen.Initialize(core, multiplayer);
        world.Initialize(core);
        world.BuildWorld();
        classes.BuildWorldObjects();
        // The laboratory no longer places a terminal in the open field; the
        // Research Facility below houses it, staffed by Giada Jade.
        facility.BuildWorldObjects();

        // The save is loaded only after the player selects Single Player.
        titleScreen.Open();
    }

    private T GetOrAdd<T>() where T : Component
    {
        T existing = GetComponent<T>();
        return existing != null ? existing : gameObject.AddComponent<T>();
    }
}
