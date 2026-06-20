using UnityEngine;

public class RanchCJSystem : MonoBehaviour
{
    public bool HasWarned { get; private set; }
    public bool BattleUnlocked { get; private set; }
    public int MilestoneIndex { get; private set; }
    public int WarningHeat = 250;
    public int UnlockHeat = 1500;
    public float CJPower = 10000f;
    public float ThreatMultiplier => 1f + MilestoneIndex * 0.08f;

    private readonly int[] milestones = { 250, 500, 900, 1500, 2500, 4000 };
    private RanchGameCore core;

    public void Initialize(RanchGameCore gameCore) => core = gameCore;

    public void CheckProgress()
    {
        if (core == null) return;

        int reached = 0;
        for (int i = 0; i < milestones.Length; i++)
            if (core.CJHeat >= milestones[i]) reached = i + 1;

        while (MilestoneIndex < reached)
        {
            MilestoneIndex++;
            AnnounceMilestone(MilestoneIndex);
        }

        HasWarned = core.CJHeat >= WarningHeat || HasWarned;
        if (core.CJHeat >= UnlockHeat && !BattleUnlocked)
        {
            BattleUnlocked = true;
            core.ShowMessage("CJ's gate is unlocked. Build your empire before challenging him.", 8f);
        }
    }

    private void AnnounceMilestone(int milestone)
    {
        switch (milestone)
        {
            case 1: core.ShowMessage("CJ: Cute little Ranch operation you've got there.", 7f); break;
            case 2: core.ShowMessage("CJ has authorized stronger Raider equipment.", 7f); break;
            case 3: core.ShowMessage("CJ has placed your Ranch under corporate surveillance.", 7f); break;
            case 4: core.ShowMessage("CJ's gate is opening. Elite squads are mobilizing.", 7f); break;
            case 5: core.ShowMessage("CJ has declared your Ranch Empire a direct threat.", 7f); break;
            case 6: core.ShowMessage("MAXIMUM CJ HEAT: The Ranchenator war has begun.", 8f); break;
        }
    }

    public string GetHeatStatus()
    {
        if (MilestoneIndex <= 0) return "Ignored";
        if (MilestoneIndex == 1) return "Noticed";
        if (MilestoneIndex == 2) return "Monitored";
        if (MilestoneIndex == 3) return "Targeted";
        if (MilestoneIndex == 4) return "Hostile";
        if (MilestoneIndex == 5) return "Empire Threat";
        return "Ranchenator War";
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
               core.Waves.HighestWaveCleared * 150f + core.Progression.Level * 220f;
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

    public void RestoreState(bool hasWarned, bool battleUnlocked, int milestoneIndex)
    {
        HasWarned = hasWarned;
        BattleUnlocked = battleUnlocked;
        MilestoneIndex = Mathf.Clamp(milestoneIndex, 0, milestones.Length);
    }
}
