using UnityEngine;

public class RanchStaminaSystem : MonoBehaviour
{
    public float CurrentStamina { get; private set; }
    public float BaseMaximumStamina = 100f;
    public float BaseRegenerationPerSecond = 18f;
    public float RegenerationDelay = 0.75f;

    public float MaximumStamina => BaseMaximumStamina +
        (core != null && core.Progression != null ? core.Progression.MaximumStaminaBonus : 0f);

    public float RegenerationPerSecond => BaseRegenerationPerSecond +
        (core != null && core.Progression != null ? core.Progression.StaminaRegenerationBonus : 0f);

    private RanchGameCore core;
    private float regenerationDelayRemaining;

    public void Initialize(RanchGameCore gameCore)
    {
        core = gameCore;
        CurrentStamina = MaximumStamina;
    }

    private void Update()
    {
        if (core == null || core.GameWon || core.Health.IsDead || core.Shop.IsOpen || core.Progression.IsOpen)
            return;

        if (CurrentStamina > MaximumStamina)
            CurrentStamina = MaximumStamina;

        if (regenerationDelayRemaining > 0f)
        {
            regenerationDelayRemaining -= Time.deltaTime;
            return;
        }

        if (CurrentStamina < MaximumStamina)
            CurrentStamina = Mathf.Min(MaximumStamina, CurrentStamina + RegenerationPerSecond * Time.deltaTime);
    }

    public bool TrySpend(float amount)
    {
        if (amount <= 0f) return true;
        if (CurrentStamina + 0.001f < amount) return false;

        CurrentStamina = Mathf.Max(0f, CurrentStamina - amount);
        regenerationDelayRemaining = RegenerationDelay;
        return true;
    }

    public void Drain(float amount)
    {
        if (amount <= 0f) return;
        CurrentStamina = Mathf.Max(0f, CurrentStamina - amount);
        regenerationDelayRemaining = RegenerationDelay;
    }

    public void Restore(float amount)
    {
        if (amount <= 0f) return;
        CurrentStamina = Mathf.Min(MaximumStamina, CurrentStamina + amount);
    }

    public void RestoreFull()
    {
        CurrentStamina = MaximumStamina;
    }

    public void RestoreState(float savedStamina)
    {
        CurrentStamina = Mathf.Clamp(savedStamina, 0f, MaximumStamina);
        regenerationDelayRemaining = 0f;
    }
}
