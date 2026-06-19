using UnityEngine;

public class RanchCJSystem : MonoBehaviour
{
    public bool HasWarned { get; private set; }
    public bool BattleUnlocked { get; private set; }
    public int WarningHeat = 250;
    public int UnlockHeat = 1500;
    public float CJPower = 10000f;

    private RanchGameCore core;

    public void Initialize(RanchGameCore gameCore) => core = gameCore;

    public void CheckProgress()
    {
        if (core == null) return;
        if (core.CJHeat >= WarningHeat && !HasWarned)
        {
            HasWarned = true;
            core.ShowMessage("CJ: Cute little Ranch operation you've got there.");
        }
        if (core.CJHeat >= UnlockHeat && !BattleUnlocked)
        {
            BattleUnlocked = true;
            core.ShowMessage("CJ's gate is unlocked. Build your empire before challenging him.");
        }
    }

    public float CalculatePlayerPower()
    {
        int selectedCapacity = core.Bottles.GetCapacity(core.Bottles.SelectedTier);
        return core.Inventory.Money + core.Inventory.RawRanch * 2f +
               core.Inventory.GetTotalBottleCount() * selectedCapacity * 8f +
               core.BottlesSold * 12f + core.Drew.Level * 500f +
               core.Bottles.UnlockedTier * 900f + core.Upgrades.ToolTier * 650f +
               core.Tree.Stage * 700f + core.Shop.StructureLevel * 1200f +
               core.Shop.SwordLevel * 450f + core.Health.HealthLevel * 250f +
               core.Waves.CurrentWave * 125f;
    }

    public void ChallengeCJ()
    {
        if (!BattleUnlocked) { core.ShowMessage("CJ has not acknowledged you yet. Sell more Ranch."); return; }
        float power = CalculatePlayerPower();
        if (power >= CJPower) core.WinGame();
        else
        {
            core.ShowMessage($"CJ defeats you economically. You need about {CJPower - power:F0} more Ranch Power.");
            core.AddCJHeat(100);
        }
    }
}
