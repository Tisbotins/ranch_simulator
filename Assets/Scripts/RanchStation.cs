using UnityEngine;

public enum RanchStationType { Bottle, Sell, BottleUpgrade, ToolUpgrade, Drew, CJGate, Shop }

public class RanchStation : RanchInteractable
{
    public RanchStationType StationType;
    private RanchGameCore core;

    public void Initialize(RanchGameCore gameCore, RanchStationType type)
    {
        core = gameCore;
        StationType = type;
    }

    public override string Prompt
    {
        get
        {
            if (core == null) return "Station unavailable";
            switch (StationType)
            {
                case RanchStationType.Bottle: return "Press E: Fill selected bottle | Shift + E: Instant bottle all | [ and ] change bottle";
                case RanchStationType.Sell: return "Press E: Sell one | Shift + E: Sell all selected";
                case RanchStationType.BottleUpgrade: return "Press E: Unlock next bottle size";
                case RanchStationType.ToolUpgrade: return "Press E: Upgrade Ranch extracting tool";
                case RanchStationType.Drew: return core.Drew.IsHired ? "Press E: Upgrade Drew" : "Press E: Hire Drew";
                case RanchStationType.CJGate: return core.CJ.GetGatePrompt();
                case RanchStationType.Shop: return "Press E: Open Ranch Empire Shop";
                default: return "Press E";
            }
        }
    }

    public override void Interact(RanchPlayerController player, bool held, float deltaTime)
    {
        if (held || core == null) return;
        switch (StationType)
        {
            case RanchStationType.Bottle:
                if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
                {
                core.Bottles.BottleAllSelected();
                }
                else
                {
                core.Bottles.TryBottleSelected();
                }
                break;
            case RanchStationType.Sell:
                if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) core.Bottles.SellAllSelected();
                else core.Bottles.SellOneSelected();
                break;
            case RanchStationType.BottleUpgrade: core.Upgrades.BuyNextBottleTier(); break;
            case RanchStationType.ToolUpgrade: core.Upgrades.BuyNextToolTier(); break;
            case RanchStationType.Drew: core.Drew.TalkToDrew(); break;
            case RanchStationType.CJGate: core.CJ.ChallengeCJ(); break;
            case RanchStationType.Shop: core.Shop.OpenShop(); break;
        }
    }
}
