using UnityEngine;

public enum RanchWeaponType
{
    Sword = 0,
    Spear = 1,
    Bow = 2
}

public class RanchEquipmentSystem : MonoBehaviour
{
    public const int SlotCount = 3;

    public int ActiveSlot { get; private set; } = 1;
    public RanchWeaponType EquippedWeapon { get; private set; } = RanchWeaponType.Sword;
    public bool SpearUnlocked { get; private set; }
    public bool BowUnlocked { get; private set; }

    public RanchClassType CurrentClass =>
        core != null && core.Classes != null
            ? core.Classes.CurrentClass
            : RanchClassType.Sword;

    public bool ExtractorSlotActive => ActiveSlot == 0;
    public bool WeaponSlotActive => ActiveSlot == 1 && CurrentClass != RanchClassType.Summoner;
    public bool TrapSlotActive => ActiveSlot == 2;
    public bool WandSlotActive => ActiveSlot == 1 && CurrentClass == RanchClassType.Summoner;
    public bool ExtraSlotActive => TrapSlotActive;

    public string CurrentWeaponName
    {
        get
        {
            return core == null || core.Shop == null
                ? GetBaseWeaponName(CurrentClass)
                : core.Shop.GetCurrentClassWeaponName();
        }
    }

    public float WeaponDamageMultiplier
    {
        get
        {
            switch (CurrentClass)
            {
                case RanchClassType.Spear: return 0.96f;
                case RanchClassType.Ranged: return 0.80f;
                case RanchClassType.Summoner: return 0f;
                default: return 1f;
            }
        }
    }

    public float LightAttackRange
    {
        get
        {
            switch (CurrentClass)
            {
                case RanchClassType.Spear:
                    return 5.7f * (core == null ? 1f : core.Progression.SpearRangeMultiplier * core.Shop.SpearRangeMultiplier);
                case RanchClassType.Ranged:
                    return 34f;
                default:
                    return 3.6f;
            }
        }
    }

    public float HeavyAttackRange
    {
        get
        {
            switch (CurrentClass)
            {
                case RanchClassType.Spear:
                    return 6.8f * (core == null ? 1f : core.Progression.SpearRangeMultiplier * core.Shop.SpearRangeMultiplier);
                case RanchClassType.Ranged:
                    return 42f;
                default:
                    return 4.25f;
            }
        }
    }

    public float LightStaminaMultiplier => CurrentClass == RanchClassType.Ranged ? 1.3f : 1f;
    public float HeavyStaminaMultiplier => CurrentClass == RanchClassType.Spear ? 1.12f : 1f;
    public bool CanBlock => CurrentClass == RanchClassType.Sword || CurrentClass == RanchClassType.Spear;
    public bool IsRanged => CurrentClass == RanchClassType.Ranged;

    private RanchGameCore core;
    private Transform extractorVisual;
    private Transform swordVisual;
    private Transform spearVisual;
    private Transform bowVisual;
    private Transform trapVisual;
    private Transform wandVisual;
    private bool extractionOverride;

    public void Initialize(RanchGameCore gameCore)
    {
        core = gameCore;
    }

    public void RegisterVisuals(
        Transform extractor,
        Transform sword,
        Transform spear,
        Transform bow,
        Transform trap = null,
        Transform wand = null)
    {
        extractorVisual = extractor;
        swordVisual = sword;
        spearVisual = spear;
        bowVisual = bow;
        trapVisual = trap;
        wandVisual = wand;
        RefreshVisuals();
    }

    public string GetSlotName(int slot)
    {
        switch (slot)
        {
            case 0:
                return core == null ? "Extractor" : core.Upgrades.CurrentToolName;
            case 1:
                if (CurrentClass != RanchClassType.Summoner)
                    return CurrentWeaponName;

                if (core == null || core.Deployables == null)
                    return "Delulu Wand";

                return core.Deployables.DeluluWandUnlocked
                    ? CurrentWeaponName + " [" + core.Deployables.ActiveDeluluCount + " active]"
                    : "Delulu Wand [LOCKED]";
            case 2:
                return core == null || core.Deployables == null
                    ? "Ranch Trap"
                    : "Ranch Trap x" + core.Deployables.TrapCount;
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

    public void ApplyClass(RanchClassType classType, bool selectClassSlot)
    {
        switch (classType)
        {
            case RanchClassType.Spear:
                SpearUnlocked = true;
                EquippedWeapon = RanchWeaponType.Spear;
                if (selectClassSlot) ActiveSlot = 1;
                break;
            case RanchClassType.Ranged:
                BowUnlocked = true;
                EquippedWeapon = RanchWeaponType.Bow;
                if (selectClassSlot) ActiveSlot = 1;
                break;
            case RanchClassType.Summoner:
                if (selectClassSlot) ActiveSlot = 1;
                break;
            default:
                EquippedWeapon = RanchWeaponType.Sword;
                if (selectClassSlot) ActiveSlot = 1;
                break;
        }

        extractionOverride = false;
        RefreshVisuals();
    }

    public bool IsWeaponUnlocked(RanchWeaponType weapon)
    {
        switch (weapon)
        {
            case RanchWeaponType.Spear: return SpearUnlocked;
            case RanchWeaponType.Bow: return BowUnlocked;
            default: return true;
        }
    }

    public float GetWeaponUnlockCost(RanchWeaponType weapon) => 0f;

    public string GetWeaponDescription(RanchWeaponType weapon)
    {
        switch (weapon)
        {
            case RanchWeaponType.Spear:
                return "Long melee reach, armor-piercing heavy thrusts, and strong knockback.";
            case RanchWeaponType.Bow:
                return "Fires actual projectiles at long range. Lower damage and no blocking.";
            default:
                return "Balanced close-range weapon with fast three-hit combos and reliable blocking.";
        }
    }

    public void BuyOrEquipWeapon(RanchWeaponType weapon)
    {
        if (core == null || core.Classes == null)
            return;

        RanchClassType classType = RanchClassType.Sword;
        if (weapon == RanchWeaponType.Spear) classType = RanchClassType.Spear;
        if (weapon == RanchWeaponType.Bow) classType = RanchClassType.Ranged;
        core.Classes.ChangeClass(classType);
    }

    public void RestoreState(int activeSlot, int equippedWeapon, bool spearUnlocked, bool bowUnlocked)
    {
        SpearUnlocked = spearUnlocked;
        BowUnlocked = bowUnlocked;

        // Version-7 saves may still contain the removed Slot 4 index.
        // Move those saves to Slot 2, which now holds the Summoner wand.
        if (activeSlot == 3)
            activeSlot = 1;

        ActiveSlot = Mathf.Clamp(activeSlot, 0, SlotCount - 1);

        if (core != null && core.Classes != null)
        {
            ApplyClass(core.Classes.CurrentClass, false);
        }
        else
        {
            EquippedWeapon = (RanchWeaponType)Mathf.Clamp(equippedWeapon, 0, 2);
        }

        extractionOverride = false;
        RefreshVisuals();
    }

    public Transform GetActiveWeaponVisual()
    {
        switch (EquippedWeapon)
        {
            case RanchWeaponType.Spear: return spearVisual;
            case RanchWeaponType.Bow: return bowVisual;
            default: return swordVisual;
        }
    }

    private static string GetBaseWeaponName(RanchClassType classType)
    {
        switch (classType)
        {
            case RanchClassType.Spear: return "Ranch Spear";
            case RanchClassType.Ranged: return "Ranch Bow";
            case RanchClassType.Summoner: return "Delulu Wand";
            default: return "Ranch Sword";
        }
    }

    private void RefreshVisuals()
    {
        bool showExtractor = extractionOverride || ActiveSlot == 0;
        bool showWeapon = !extractionOverride && ActiveSlot == 1 && CurrentClass != RanchClassType.Summoner;
        bool showTrap = !extractionOverride && ActiveSlot == 2;
        bool showWand = !extractionOverride && ActiveSlot == 1 && CurrentClass == RanchClassType.Summoner;

        SetVisible(extractorVisual, showExtractor);
        SetVisible(swordVisual, showWeapon && CurrentClass == RanchClassType.Sword);
        SetVisible(spearVisual, showWeapon && CurrentClass == RanchClassType.Spear);
        SetVisible(bowVisual, showWeapon && CurrentClass == RanchClassType.Ranged);
        SetVisible(trapVisual, showTrap);
        SetVisible(wandVisual, showWand);
    }

    private static void SetVisible(Transform target, bool visible)
    {
        if (target != null && target.gameObject.activeSelf != visible)
            target.gameObject.SetActive(visible);
    }
}
