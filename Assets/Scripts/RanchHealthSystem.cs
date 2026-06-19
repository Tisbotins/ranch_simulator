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
        if (IsDead || core == null || core.GameWon || core.Shop.IsOpen) return;

        if (RegenerationPerSecond > 0f && CurrentHealth < MaxHealth)
            CurrentHealth = Mathf.Min(MaxHealth, CurrentHealth + RegenerationPerSecond * Time.deltaTime);
    }

    public void TakeDamage(float rawDamage, string source)
    {
        if (IsDead || rawDamage <= 0f) return;
        float finalDamage = Mathf.Max(1f, rawDamage * (1f - armorValues[ArmorLevel]));
        CurrentHealth = Mathf.Max(0f, CurrentHealth - finalDamage);
        DamageFlashTime = 0.2f;
        core.ShowMessage($"{source} hit you for {finalDamage:F0} damage.");
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
        core.ShowMessage($"Maximum health upgraded to {MaxHealth:F0}.");
    }

    public void BuyArmorUpgrade()
    {
        float cost = GetNextArmorCost();
        if (cost < 0f) { core.ShowMessage("Maximum armor already reached."); return; }
        if (!core.Inventory.TrySpendMoney(cost)) { core.ShowMessage($"Need ${cost:F0} for armor."); return; }
        ArmorLevel++;
        core.ShowMessage($"Armor upgraded to {ArmorPercent:F0}% damage reduction.");
    }

    public void BuyRegenerationUpgrade()
    {
        float cost = GetNextRegenerationCost();
        if (cost < 0f) { core.ShowMessage("Maximum regeneration already reached."); return; }
        if (!core.Inventory.TrySpendMoney(cost)) { core.ShowMessage($"Need ${cost:F0} for regeneration."); return; }
        RegenerationLevel++;
        core.ShowMessage($"Regeneration upgraded to {RegenerationPerSecond:0.00} HP/sec.");
    }

    public void BuyFullHeal()
    {
        float cost = GetFullHealCost();
        if (cost <= 0f) { core.ShowMessage("You are already at full health."); return; }
        if (!core.Inventory.TrySpendMoney(cost)) { core.ShowMessage($"Need ${cost:F0} for a full heal."); return; }
        CurrentHealth = MaxHealth;
        core.ShowMessage("Health restored to full.");
    }

    private void Die()
    {
        IsDead = true;
        core.ShowMessage("You were defeated by the Ranch Raiders. Press R to restart.", 999f);
        if (core.Player != null) core.Player.enabled = false;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
}
