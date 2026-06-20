using UnityEngine;

public class RanchBottleSystem : MonoBehaviour
{
    public const int TierCount = 8;

    private readonly string[] names =
    {
        "Cracked Bottle", "Tiny Bottle", "Standard Bottle", "Family Bottle",
        "Mega Jug", "Industrial Drum", "Ranch Tanker", "Ranchenator Vessel"
    };

    private readonly int[] capacities = { 1, 2, 5, 12, 30, 75, 200, 500 };

    public int UnlockedTier { get; private set; }
    public int SelectedTier { get; private set; }

    private RanchGameCore core;

    public void Initialize(RanchGameCore gameCore)
    {
        core = gameCore;
        UnlockedTier = 0;
        SelectedTier = 0;
    }

    public string GetTierName(int tier) => ValidTier(tier) ? names[tier] : "Unknown Bottle";
    public int GetCapacity(int tier) => ValidTier(tier) ? capacities[tier] : 0;
    public bool IsUnlocked(int tier) => ValidTier(tier) && tier <= UnlockedTier;

    public void UnlockThrough(int tier)
    {
        if (!ValidTier(tier)) return;
        UnlockedTier = Mathf.Max(UnlockedTier, tier);
        SelectedTier = Mathf.Min(SelectedTier, UnlockedTier);
        core.NotifyResourcesChanged();
    }

    public void SelectTier(int tier)
    {
        if (!IsUnlocked(tier))
        {
            core.ShowMessage("That bottle size is still locked.");
            return;
        }

        SelectedTier = tier;
        core.ShowMessage($"Selected {GetTierName(tier)} ({GetCapacity(tier)} Ranch).");
        core.NotifyResourcesChanged();
    }

    public void CycleSelection(int direction)
    {
        int count = UnlockedTier + 1;
        if (count <= 0) return;
        SelectedTier = (SelectedTier + direction) % count;
        if (SelectedTier < 0) SelectedTier += count;
        core.ShowMessage($"Selected {GetTierName(SelectedTier)} ({GetCapacity(SelectedTier)} Ranch).");
        core.NotifyResourcesChanged();
    }

    public bool TryBottleSelected(bool showMessage = true) => TryBottleTier(SelectedTier, showMessage);

    public bool TryBottleTier(int tier, bool showMessage = true)
    {
        if (!IsUnlocked(tier))
        {
            if (showMessage) core.ShowMessage("That bottle size is locked.");
            return false;
        }

        int capacity = GetCapacity(tier);
        if (!core.Inventory.TrySpendRawRanch(capacity))
        {
            if (showMessage) core.ShowMessage($"Need {capacity} raw Ranch to fill a {GetTierName(tier)}.");
            return false;
        }

        core.Inventory.AddBottle(tier);
        if (showMessage) core.ShowMessage($"Filled one {GetTierName(tier)} with {capacity} Ranch.");
        return true;
    }

    public void SellOneSelected() => SellTier(SelectedTier, 1);

    public void SellAllSelected()
    {
        int amount = core.Inventory.GetBottleCount(SelectedTier);
        if (amount <= 0)
        {
            core.ShowMessage($"You have no {GetTierName(SelectedTier)} bottles to sell.");
            return;
        }
        SellTier(SelectedTier, amount);
    }

    public void SellTier(int tier, int amount)
    {
        if (!ValidTier(tier) || amount <= 0) return;
        if (!core.Inventory.TryRemoveBottle(tier, amount))
        {
            core.ShowMessage("Not enough bottles of that size to sell.");
            return;
        }

        int capacity = GetCapacity(tier);
        float tierBonus = 1f + tier * 0.22f;
        float drewBonus = 1f + core.Drew.Level * 0.03f;
        float marketBonus = core.Shop.SaleMultiplier;
        float earned = amount * capacity * 5f * tierBonus * drewBonus * marketBonus;

        core.Inventory.AddMoney(earned);
        core.RegisterBottleSale(amount, amount * capacity);
        core.ShowMessage($"Sold {amount} {GetTierName(tier)}{(amount == 1 ? "" : "s")} for ${earned:F0}.");
    }

    private bool ValidTier(int tier) => tier >= 0 && tier < TierCount;

    public void BottleAllSelected()
    {
    int capacity = GetCapacity(SelectedTier);
    int made = 0;

    while (core.Inventory.RawRanch >= capacity)
    {
        core.Inventory.TrySpendRawRanch(capacity);
        core.Inventory.AddBottle(SelectedTier);
        made++;
    }

    if (made > 0)
    {
        core.ShowMessage("Instant bottled " + made + " " + GetTierName(SelectedTier) + "(s).");
    }
    else
    {
        core.ShowMessage("Not enough Ranch to bottle.");
    }
    }
}
