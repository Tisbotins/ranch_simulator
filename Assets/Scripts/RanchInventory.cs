using System;
using UnityEngine;

public class RanchInventory : MonoBehaviour
{
    public float RawRanch { get; private set; }
    public float Money { get; private set; }

    private readonly int[] bottleCounts = new int[RanchBottleSystem.TierCount];
    private RanchGameCore core;

    public event Action InventoryChanged;

    public void Initialize(RanchGameCore gameCore) => core = gameCore;

    public void AddRawRanch(float amount)
    {
        if (amount <= 0f) return;
        RawRanch += amount;
        Changed();
    }

    public bool TrySpendRawRanch(float amount)
    {
        if (amount < 0f || RawRanch + 0.0001f < amount) return false;
        RawRanch = Mathf.Max(0f, RawRanch - amount);
        Changed();
        return true;
    }

    public void AddMoney(float amount)
    {
        if (amount <= 0f) return;
        Money += amount;
        Changed();
    }

    public bool TrySpendMoney(float amount)
    {
        if (amount < 0f || Money + 0.0001f < amount) return false;
        Money = Mathf.Max(0f, Money - amount);
        Changed();
        return true;
    }

    public void AddBottle(int tier, int amount = 1)
    {
        if (!ValidTier(tier) || amount <= 0) return;
        bottleCounts[tier] += amount;
        Changed();
    }

    public bool TryRemoveBottle(int tier, int amount = 1)
    {
        if (!ValidTier(tier) || amount <= 0 || bottleCounts[tier] < amount) return false;
        bottleCounts[tier] -= amount;
        Changed();
        return true;
    }

    public int GetBottleCount(int tier) => ValidTier(tier) ? bottleCounts[tier] : 0;

    public int GetTotalBottleCount()
    {
        int total = 0;
        for (int i = 0; i < bottleCounts.Length; i++) total += bottleCounts[i];
        return total;
    }

    private bool ValidTier(int tier) => tier >= 0 && tier < bottleCounts.Length;

    private void Changed()
    {
        InventoryChanged?.Invoke();
        core?.NotifyResourcesChanged();
    }
}
