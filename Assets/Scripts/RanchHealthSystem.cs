using UnityEngine;

public class RanchHealthSystem : MonoBehaviour
{
    private readonly float[] healthValues = { 100f, 130f, 170f, 220f, 290f, 380f, 500f };
    private readonly float[] armorValues = { 0f, 0.05f, 0.10f, 0.16f, 0.23f, 0.31f, 0.40f };
    private readonly float[] regenValues = { 0f, 0.15f, 0.35f, 0.70f, 1.20f, 2.00f, 3.25f };
    private readonly float[] healthCosts = { 180f, 450f, 1000f, 2200f, 4800f, 9500f };
    private readonly float[] armorCosts = { 250f, 650f, 1500f, 3400f, 7200f, 14500f };
    private readonly float[] regenCosts = { 300f, 800f, 1800f, 4000f, 8500f, 17000f };

    public float CurrentHealth { get; private set; }
    public float MaxHealth => healthValues[HealthLevel];
    public float ArmorPercent => armorValues[ArmorLevel] * 100f;
    public float RegenerationPerSecond => regenValues[RegenerationLevel];
    public int HealthLevel { get; private set; }
    public int ArmorLevel { get; private set; }
    public int RegenerationLevel { get; private set; }
    public bool IsDead { get; private set; }

    // Co-op downed state: in multiplayer a knocked-out player is "downed" rather
    // than permanently dead, so the session never breaks. A teammate can revive
    // them, and a safety timer auto-recovers them if nobody does.
    public bool IsDowned { get; private set; }
    public float DownedAutoRecoverRemaining { get; private set; }
    public const float DownedAutoRecoverSeconds = 30f;

    public float DamageFlashTime { get; private set; }

    private RanchGameCore core;

    public void Initialize(RanchGameCore gameCore)
    {
        core = gameCore;
        CurrentHealth = MaxHealth;
    }

    private void Update()
    {
        if (DamageFlashTime > 0f) DamageFlashTime -= Time.unscaledDeltaTime;

        if (IsDowned)
        {
            DownedAutoRecoverRemaining -= Time.unscaledDeltaTime;
            if (DownedAutoRecoverRemaining <= 0f)
                AutoRecover();
            return;
        }

        if (IsDead || core == null || core.GameWon || core.Shop.IsOpen || core.Progression.IsOpen) return;
        if (RegenerationPerSecond > 0f && CurrentHealth < MaxHealth)
            CurrentHealth = Mathf.Min(MaxHealth, CurrentHealth + RegenerationPerSecond * Time.deltaTime);
    }

    public void TakeDamage(float rawDamage, string source, RanchEnemy attacker = null)
    {
        if (IsDead || IsDowned || rawDamage <= 0f) return;

        string combatResult;
        float combatAdjusted = core.Combat.ResolveIncomingDamage(rawDamage, attacker, out combatResult);
        if (combatAdjusted <= 0f)
        {
            if (!string.IsNullOrEmpty(combatResult)) core.ShowMessage(combatResult + "!");
            return;
        }

        float finalDamage = Mathf.Max(1f, combatAdjusted * (1f - armorValues[ArmorLevel]));
        CurrentHealth = Mathf.Max(0f, CurrentHealth - finalDamage);
        DamageFlashTime = 0.2f;
        string prefix = string.IsNullOrEmpty(combatResult) ? "" : combatResult + " — ";
        core.ShowMessage($"{prefix}{source} hit you for {finalDamage:F0} damage.");
        if (CurrentHealth <= 0f) Die();
    }

    public void Heal(float amount)
    {
        if (!IsDead && amount > 0f) CurrentHealth = Mathf.Min(MaxHealth, CurrentHealth + amount);
    }

    public float GetNextHealthCost() => HealthLevel >= healthValues.Length - 1 ? -1f : healthCosts[HealthLevel];
    public float GetNextArmorCost() => ArmorLevel >= armorValues.Length - 1 ? -1f : armorCosts[ArmorLevel];
    public float GetNextRegenerationCost() => RegenerationLevel >= regenValues.Length - 1 ? -1f : regenCosts[RegenerationLevel];
    public float GetFullHealCost() => CurrentHealth >= MaxHealth ? 0f : Mathf.Max(25f, Mathf.Ceil((MaxHealth - CurrentHealth) * 1.25f));

    public void BuyHealthUpgrade()
    {
        float cost = GetNextHealthCost();
        if (cost < 0f) { core.ShowMessage("Maximum health already reached."); return; }
        if (!core.Inventory.TrySpendMoney(cost)) { core.ShowMessage($"Need ${cost:F0} for the health upgrade."); return; }
        float oldMax = MaxHealth;
        HealthLevel++;
        CurrentHealth = Mathf.Min(MaxHealth, CurrentHealth + MaxHealth - oldMax);
        core.Progression.AddExperience(25f + HealthLevel * 8f, "Health upgrade");
        core.Save.RequestSave();
        core.ShowMessage($"Maximum health upgraded to {MaxHealth:F0}.");
    }

    public void BuyArmorUpgrade()
    {
        float cost = GetNextArmorCost();
        if (cost < 0f) { core.ShowMessage("Maximum armor already reached."); return; }
        if (!core.Inventory.TrySpendMoney(cost)) { core.ShowMessage($"Need ${cost:F0} for armor."); return; }
        ArmorLevel++;
        core.Progression.AddExperience(25f + ArmorLevel * 8f, "Armor upgrade");
        core.Save.RequestSave();
        core.ShowMessage($"Armor upgraded to {ArmorPercent:F0}% damage reduction.");
    }

    public void BuyRegenerationUpgrade()
    {
        float cost = GetNextRegenerationCost();
        if (cost < 0f) { core.ShowMessage("Maximum regeneration already reached."); return; }
        if (!core.Inventory.TrySpendMoney(cost)) { core.ShowMessage($"Need ${cost:F0} for regeneration."); return; }
        RegenerationLevel++;
        core.Progression.AddExperience(25f + RegenerationLevel * 8f, "Regeneration upgrade");
        core.Save.RequestSave();
        core.ShowMessage($"Regeneration upgraded to {RegenerationPerSecond:0.00} HP/sec.");
    }

    public void BuyFullHeal()
    {
        float cost = GetFullHealCost();
        if (cost <= 0f) { core.ShowMessage("You are already at full health."); return; }
        if (!core.Inventory.TrySpendMoney(cost)) { core.ShowMessage($"Need ${cost:F0} for a full heal."); return; }
        CurrentHealth = MaxHealth;
        core.Save.RequestSave();
        core.ShowMessage("Health restored to full.");
    }

    public void RestoreState(float currentHealth, int healthLevel, int armorLevel, int regenerationLevel)
    {
        HealthLevel = Mathf.Clamp(healthLevel, 0, healthValues.Length - 1);
        ArmorLevel = Mathf.Clamp(armorLevel, 0, armorValues.Length - 1);
        RegenerationLevel = Mathf.Clamp(regenerationLevel, 0, regenValues.Length - 1);
        CurrentHealth = Mathf.Clamp(currentHealth, 1f, MaxHealth);
        IsDead = false;
        IsDowned = false;
        DownedAutoRecoverRemaining = 0f;
        DamageFlashTime = 0f;
    }

    private void Die()
    {
        // In co-op, going down must never break the session. Enter the downed
        // state and let a teammate revive instead of ending the game.
        if (RanchGameModeState.IsMultiplayer)
        {
            EnterDowned();
            return;
        }

        IsDead = true;
        core.Save.SaveGame(false);
        core.ShowMessage("You were defeated by the Ranch Raiders. Press R to restart.", 999f);
        if (core.Player != null) core.Player.enabled = false;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void EnterDowned()
    {
        if (IsDowned) return;

        IsDowned = true;
        IsDead = false;
        CurrentHealth = 0f;
        DamageFlashTime = 0.2f;
        DownedAutoRecoverRemaining = DownedAutoRecoverSeconds;

        // Freeze the downed player but keep the world running for everyone.
        if (core.Player != null) core.Player.enabled = false;

        core.ShowMessage(
            "You are DOWNED. Your teammate can revive you — auto-recovery in " +
            Mathf.CeilToInt(DownedAutoRecoverSeconds) + "s.",
            DownedAutoRecoverSeconds
        );
    }

    /// <summary>Revive a downed player with a fraction of their max health.</summary>
    public void Revive(float healthFraction)
    {
        if (!IsDowned) return;

        IsDowned = false;
        IsDead = false;
        DownedAutoRecoverRemaining = 0f;
        CurrentHealth = Mathf.Clamp(MaxHealth * healthFraction, 1f, MaxHealth);
        DamageFlashTime = 0f;

        if (core.Player != null && !core.GameWon)
            core.Player.enabled = true;

        core.ShowMessage("You were revived! Get back in the fight.", 4f);
    }

    private void AutoRecover()
    {
        Revive(0.35f);
        core.ShowMessage("No teammate reached you in time — you recovered on your own.", 4f);
    }
}
