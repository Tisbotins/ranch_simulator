using UnityEngine;

public class RanchUpgradeSystem : MonoBehaviour
{
    private readonly string[] toolNames =
    {
        "Bare Hands", "Wooden Ranch Tap", "Copper Extractor", "Pressure Ranch Pump",
        "Powered Ranch Drill", "Quantum Sap Siphon", "Ultimate Ranchenator Extractor"
    };

    private readonly float[] multipliers = { 1f, 1.5f, 2.25f, 3.5f, 5.5f, 8.5f, 13f };

    public int ToolTier { get; private set; }
    public int MaxToolTier => toolNames.Length - 1;
    public float ExtractionMultiplier => multipliers[ToolTier];
    public string CurrentToolName => toolNames[ToolTier];

    private RanchGameCore core;

    public void Initialize(RanchGameCore gameCore) => core = gameCore;

    public float GetNextBottleUpgradeCost()
    {
        int next = core.Bottles.UnlockedTier + 1;
        return 80f * next * next;
    }

    public float GetNextToolUpgradeCost()
    {
        int next = ToolTier + 1;
        return 120f * next * next;
    }

    public void BuyNextBottleTier()
    {
        int next = core.Bottles.UnlockedTier + 1;
        if (next >= RanchBottleSystem.TierCount)
        {
            core.ShowMessage("Every bottle size is already unlocked.");
            return;
        }

        float cost = GetNextBottleUpgradeCost();
        if (!core.Inventory.TrySpendMoney(cost))
        {
            core.ShowMessage($"Need ${cost:F0} for the next bottle size.");
            return;
        }

        core.Bottles.UnlockThrough(next);
        core.Bottles.SelectTier(next);
        core.AddCJHeat(next * 8);
        core.Progression.AddExperience(20f + next * 8f, "Bottle upgrade");
        core.Save.RequestSave();
    }

    public void BuyNextToolTier()
    {
        int next = ToolTier + 1;
        if (next >= toolNames.Length)
        {
            core.ShowMessage("Your Ranch extracting tool is already maxed.");
            return;
        }

        float cost = GetNextToolUpgradeCost();
        if (!core.Inventory.TrySpendMoney(cost))
        {
            core.ShowMessage($"Need ${cost:F0} for the next extractor.");
            return;
        }

        ToolTier = next;
        core.AddCJHeat(next * 5);
        core.ShowMessage($"Extractor upgraded to {CurrentToolName} ({ExtractionMultiplier:0.##}x extraction).");
        core.Progression.AddExperience(25f + next * 10f, "Extractor upgrade");
        core.Save.RequestSave();
        core.NotifyResourcesChanged();
    }

    public void RestoreState(int toolTier)
    {
        ToolTier = Mathf.Clamp(toolTier, 0, MaxToolTier);
        core.NotifyResourcesChanged();
    }
}
