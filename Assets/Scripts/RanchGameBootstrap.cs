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
        RanchEquipmentSystem equipment = GetOrAdd<RanchEquipmentSystem>();
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
        RanchWorldBuilder world = GetOrAdd<RanchWorldBuilder>();
        RanchUI ui = GetOrAdd<RanchUI>();

        core.Initialize(
            inventory,
            bottles,
            upgrades,
            tree,
            health,
            stamina,
            equipment,
            combat,
            progression,
            areas,
            bosses,
            waves,
            drew,
            shop,
            cj,
            save,
            settings
        );

        inventory.Initialize(core);
        bottles.Initialize(core);
        upgrades.Initialize(core);
        tree.Initialize(core);
        health.Initialize(core);
        stamina.Initialize(core);
        equipment.Initialize(core);
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
        ui.Initialize(core);
        world.Initialize(core);
        world.BuildWorld();

        bool loaded = save.LoadOnStartup();
        core.ShowMessage(
            loaded
                ? "Save loaded. Press Escape for settings, H for HUD, Z to save, and X to load."
                : "Welcome to Ranch Simulator. Explore the expanded areas, build your empire, and survive 30 waves.",
            9f
        );
    }

    private T GetOrAdd<T>() where T : Component
    {
        T existing = GetComponent<T>();
        return existing != null ? existing : gameObject.AddComponent<T>();
    }
}
