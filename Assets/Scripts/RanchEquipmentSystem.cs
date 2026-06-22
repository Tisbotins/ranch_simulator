using UnityEngine;

public enum RanchWeaponType
{
    Sword = 0,
    Spear = 1,
    Bow = 2
}

public class RanchEquipmentSystem : MonoBehaviour
{
    public const int SlotCount = 4;

    public int ActiveSlot { get; private set; } = 1;
    public RanchWeaponType EquippedWeapon { get; private set; } = RanchWeaponType.Sword;
    public bool SpearUnlocked { get; private set; }
    public bool BowUnlocked { get; private set; }

    public bool ExtractorSlotActive => ActiveSlot == 0;
    public bool WeaponSlotActive => ActiveSlot == 1;
    public bool TrapSlotActive => ActiveSlot == 2;
    public bool WandSlotActive => ActiveSlot == 3;
    public bool ExtraSlotActive => TrapSlotActive;

    public string CurrentWeaponName
    {
        get
        {
            switch (EquippedWeapon)
            {
                case RanchWeaponType.Spear:
                    return "Ranch Spear";
                case RanchWeaponType.Bow:
                    return "Ranch Bow";
                default:
                    return core == null ? "Ranch Sword" : core.Shop.CurrentSwordName;
            }
        }
    }

    public float WeaponDamageMultiplier
    {
        get
        {
            switch (EquippedWeapon)
            {
                case RanchWeaponType.Spear:
                    return 0.92f;
                case RanchWeaponType.Bow:
                    return 0.82f;
                default:
                    return 1f;
            }
        }
    }

    public float LightAttackRange
    {
        get
        {
            switch (EquippedWeapon)
            {
                case RanchWeaponType.Spear:
                    return 5.7f;
                case RanchWeaponType.Bow:
                    return 28f;
                default:
                    return 3.6f;
            }
        }
    }

    public float HeavyAttackRange
    {
        get
        {
            switch (EquippedWeapon)
            {
                case RanchWeaponType.Spear:
                    return 6.8f;
                case RanchWeaponType.Bow:
                    return 36f;
                default:
                    return 4.25f;
            }
        }
    }

    public float LightStaminaMultiplier => EquippedWeapon == RanchWeaponType.Bow ? 1.25f : 1f;
    public float HeavyStaminaMultiplier => EquippedWeapon == RanchWeaponType.Spear ? 1.12f : 1f;
    public bool CanBlock => EquippedWeapon != RanchWeaponType.Bow;
    public bool IsRanged => EquippedWeapon == RanchWeaponType.Bow;

    private RanchGameCore core;
    private Transform extractorVisual;
    private Transform swordVisual;
    private Transform spearVisual;
    private Transform bowVisual;
    private bool extractionOverride;

    public void Initialize(RanchGameCore gameCore)
    {
        core = gameCore;
    }

    public void RegisterVisuals(
        Transform extractor,
        Transform sword,
        Transform spear,
        Transform bow)
    {
        extractorVisual = extractor;
        swordVisual = sword;
        spearVisual = spear;
        bowVisual = bow;
        RefreshVisuals();
    }

    public string GetSlotName(int slot)
    {
        switch (slot)
        {
            case 0:
                return core == null ? "Extractor" : core.Upgrades.CurrentToolName;
            case 1:
                return CurrentWeaponName;
            case 2:
                return core == null || core.Deployables == null
                    ? "Ranch Trap"
                    : "Ranch Trap x" + core.Deployables.TrapCount;
            case 3:
                if (core == null || core.Deployables == null)
                    return "Delulu Wand";

                return core.Deployables.DeluluWandUnlocked
                    ? "Delulu Wand [" + core.Deployables.ActiveDeluluCount + " active]"
                    : "Delulu Wand [LOCKED]";
            default:
                return "Unknown Slot";
        }
    }

    public void SelectSlot(int slot)
    {
        slot = Mathf.Clamp(slot, 0, SlotCount - 1);
        ActiveSlot = slot;
        extractionOverride = false;
        RefreshVisuals();

        if (core != null)
        {
            core.ShowMessage("Selected Slot " + (slot + 1) + ": " + GetSlotName(slot) + ".");
            core.Save?.RequestSave();
        }
    }

    public void SetExtractionOverride(bool active)
    {
        if (extractionOverride == active)
            return;

        extractionOverride = active;
        RefreshVisuals();
    }

    public bool IsWeaponUnlocked(RanchWeaponType weapon)
    {
        switch (weapon)
        {
            case RanchWeaponType.Spear:
                return SpearUnlocked;
            case RanchWeaponType.Bow:
                return BowUnlocked;
            default:
                return true;
        }
    }

    public float GetWeaponUnlockCost(RanchWeaponType weapon)
    {
        switch (weapon)
        {
            case RanchWeaponType.Spear:
                return 3000f;
            case RanchWeaponType.Bow:
                return 6500f;
            default:
                return 0f;
        }
    }

    public string GetWeaponDescription(RanchWeaponType weapon)
    {
        switch (weapon)
        {
            case RanchWeaponType.Spear:
                return "Long melee reach, armor-piercing heavy thrusts, and strong knockback.";
            case RanchWeaponType.Bow:
                return "Long-range attacks and powerful charged shots. The bow cannot block.";
            default:
                return "Balanced close-range weapon with fast three-hit combos and reliable blocking.";
        }
    }

    public void BuyOrEquipWeapon(RanchWeaponType weapon)
    {
        if (!IsWeaponUnlocked(weapon))
        {
            float cost = GetWeaponUnlockCost(weapon);

            if (!core.Inventory.TrySpendMoney(cost))
            {
                core.ShowMessage("Need $" + cost.ToString("F0") + " to unlock the " + GetDisplayName(weapon) + ".");
                return;
            }

            if (weapon == RanchWeaponType.Spear)
                SpearUnlocked = true;
            else if (weapon == RanchWeaponType.Bow)
                BowUnlocked = true;

            core.Progression.AddExperience(100f, "Weapon unlocked");
            core.ShowMessage(GetDisplayName(weapon) + " unlocked and equipped.", 6f);
        }
        else
        {
            core.ShowMessage(GetDisplayName(weapon) + " equipped in Slot 2.");
        }

        EquippedWeapon = weapon;
        ActiveSlot = 1;
        extractionOverride = false;
        RefreshVisuals();
        core.Save.RequestSave();
    }

    public void RestoreState(
        int activeSlot,
        int equippedWeapon,
        bool spearUnlocked,
        bool bowUnlocked)
    {
        SpearUnlocked = spearUnlocked;
        BowUnlocked = bowUnlocked;

        RanchWeaponType restoredWeapon = (RanchWeaponType)Mathf.Clamp(equippedWeapon, 0, 2);
        if (!IsWeaponUnlocked(restoredWeapon))
            restoredWeapon = RanchWeaponType.Sword;

        EquippedWeapon = restoredWeapon;
        ActiveSlot = Mathf.Clamp(activeSlot, 0, SlotCount - 1);
        extractionOverride = false;
        RefreshVisuals();
    }

    public Transform GetActiveWeaponVisual()
    {
        switch (EquippedWeapon)
        {
            case RanchWeaponType.Spear:
                return spearVisual;
            case RanchWeaponType.Bow:
                return bowVisual;
            default:
                return swordVisual;
        }
    }

    private string GetDisplayName(RanchWeaponType weapon)
    {
        switch (weapon)
        {
            case RanchWeaponType.Spear:
                return "Ranch Spear";
            case RanchWeaponType.Bow:
                return "Ranch Bow";
            default:
                return "Ranch Sword";
        }
    }

    private void RefreshVisuals()
    {
        bool showExtractor = extractionOverride || ActiveSlot == 0;
        bool showWeapon = !extractionOverride && ActiveSlot == 1;

        SetVisible(extractorVisual, showExtractor);
        SetVisible(swordVisual, showWeapon && EquippedWeapon == RanchWeaponType.Sword);
        SetVisible(spearVisual, showWeapon && EquippedWeapon == RanchWeaponType.Spear);
        SetVisible(bowVisual, showWeapon && EquippedWeapon == RanchWeaponType.Bow);
    }

    private static void SetVisible(Transform target, bool visible)
    {
        if (target != null && target.gameObject.activeSelf != visible)
            target.gameObject.SetActive(visible);
    }
}
