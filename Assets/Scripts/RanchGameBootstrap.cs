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
        if (hasBootstrapped) return;
        hasBootstrapped = true;

        RanchGameCore core = GetOrAdd<RanchGameCore>();
        RanchInventory inventory = GetOrAdd<RanchInventory>();
        RanchBottleSystem bottles = GetOrAdd<RanchBottleSystem>();
        RanchUpgradeSystem upgrades = GetOrAdd<RanchUpgradeSystem>();
        RanchTreeSystem tree = GetOrAdd<RanchTreeSystem>();
        RanchHealthSystem health = GetOrAdd<RanchHealthSystem>();
        RanchWaveSystem waves = GetOrAdd<RanchWaveSystem>();
        RanchDrewSystem drew = GetOrAdd<RanchDrewSystem>();
        RanchShopSystem shop = GetOrAdd<RanchShopSystem>();
        RanchCJSystem cj = GetOrAdd<RanchCJSystem>();
        RanchWorldBuilder world = GetOrAdd<RanchWorldBuilder>();
        RanchUI ui = GetOrAdd<RanchUI>();

        core.Initialize(inventory, bottles, upgrades, tree, health, waves, drew, shop, cj);
        inventory.Initialize(core);
        bottles.Initialize(core);
        upgrades.Initialize(core);
        tree.Initialize(core);
        health.Initialize(core);
        waves.Initialize(core);
        drew.Initialize(core);
        shop.Initialize(core);
        cj.Initialize(core);
        ui.Initialize(core);
        world.Initialize(core);
        world.BuildWorld();

        core.ShowMessage("Welcome to Ranch Simulator. Extract Ranch, build laboratories, survive waves, and overthrow CJ.");
    }

    private T GetOrAdd<T>() where T : Component
    {
        T existing = GetComponent<T>();
        return existing != null ? existing : gameObject.AddComponent<T>();
    }
}
