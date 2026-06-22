using UnityEngine;

public class RanchCombatSystem : MonoBehaviour
{
    public bool IsBlocking { get; private set; }
    public bool IsDodging => dodgeInvulnerability > 0f;
    public float AttackCooldownRemaining { get; private set; }

    public float LightAttackStamina = 10f;
    public float HeavyAttackStamina = 30f;
    public float DodgeStamina = 25f;
    public float BlockStaminaPerHit = 14f;
    public float PerfectBlockWindow = 0.20f;

    private RanchGameCore core;
    private RanchPlayerController player;
    private float blockStartTime;
    private float dodgeCooldown;
    private float dodgeInvulnerability;
    private float comboResetTimer;
    private int comboStep;

    public void Initialize(RanchGameCore gameCore)
    {
        core = gameCore;
    }

    public void RegisterPlayer(RanchPlayerController playerController)
    {
        player = playerController;
    }

    private void Update()
    {
        if (AttackCooldownRemaining > 0f)
            AttackCooldownRemaining -= Time.deltaTime;
        if (dodgeCooldown > 0f)
            dodgeCooldown -= Time.deltaTime;
        if (dodgeInvulnerability > 0f)
            dodgeInvulnerability -= Time.deltaTime;
        if (comboResetTimer > 0f)
            comboResetTimer -= Time.deltaTime;
        else
            comboStep = 0;

        if (core == null || player == null || core.GameWon || core.Health.IsDead ||
            core.Shop.IsOpen || core.Progression.IsOpen || core.Settings.IsOpen || Cursor.visible)
        {
            IsBlocking = false;
            return;
        }

        bool weaponReady = core.Equipment.WeaponSlotActive;
        bool wantsBlock =
            weaponReady &&
            core.Equipment.CanBlock &&
            Input.GetMouseButton(1) &&
            !IsDodging &&
            AttackCooldownRemaining <= 0f;

        if (wantsBlock && !IsBlocking)
            blockStartTime = Time.time;

        IsBlocking = wantsBlock;

        if (Input.GetKeyDown(KeyCode.LeftControl))
            TryDodge();

        if (weaponReady && !IsBlocking &&
            (Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Space)))
        {
            TryLightAttack();
        }

        if (weaponReady && !IsBlocking && Input.GetKeyDown(KeyCode.Q))
            TryHeavyAttack();
    }

    public void TryLightAttack()
    {
        if (!CanBeginWeaponAttack())
            return;

        float staminaCost = LightAttackStamina * core.Equipment.LightStaminaMultiplier;
        if (!core.Stamina.TrySpend(staminaCost))
        {
            core.ShowMessage("Not enough stamina for a light attack.");
            return;
        }

        comboStep = comboResetTimer > 0f ? (comboStep % 3) + 1 : 1;
        comboResetTimer = core.Equipment.IsRanged ? 0f : 0.85f;

        float[] comboMultipliers = { 1f, 1.15f, 1.38f };
        float comboMultiplier = core.Equipment.IsRanged ? 1f : comboMultipliers[comboStep - 1];
        float damage =
            core.Shop.CurrentSwordDamage *
            core.Equipment.WeaponDamageMultiplier *
            comboMultiplier *
            core.Progression.DamageMultiplier;

        bool critical = Random.value < core.Progression.CriticalChance;
        if (critical)
            damage *= core.Progression.CriticalDamageMultiplier;

        RanchEnemy enemy = core.Waves.GetNearestEnemy(core.Equipment.LightAttackRange);
        if (enemy == null)
        {
            core.ShowMessage(core.Equipment.IsRanged
                ? "No Ranch Raider is in bow range."
                : "No Ranch Raider is within weapon range.");
        }
        else
        {
            bool armorPiercing = core.Equipment.EquippedWeapon == RanchWeaponType.Spear && comboStep == 3;
            float knockback = core.Equipment.EquippedWeapon == RanchWeaponType.Spear
                ? 1.35f
                : (comboStep == 3 ? 1.4f : 0.55f);

            enemy.TakeDamage(damage, armorPiercing, knockback);
            if (critical)
                core.ShowMessage("CRITICAL " + core.Equipment.CurrentWeaponName.ToUpperInvariant() + " HIT — " + damage.ToString("F0") + " damage!");
        }

        AttackCooldownRemaining = core.Equipment.IsRanged ? 0.72f : 0.30f + comboStep * 0.025f;
        player.PlayAttackAnimation(false, comboStep);
    }

    public void TryHeavyAttack()
    {
        if (!CanBeginWeaponAttack())
            return;

        float staminaCost = HeavyAttackStamina * core.Equipment.HeavyStaminaMultiplier;
        if (!core.Stamina.TrySpend(staminaCost))
        {
            core.ShowMessage("Not enough stamina for a heavy attack.");
            return;
        }

        float weaponMultiplier;
        bool armorPiercing;
        float knockback;

        switch (core.Equipment.EquippedWeapon)
        {
            case RanchWeaponType.Spear:
                weaponMultiplier = 2.35f;
                armorPiercing = true;
                knockback = 4.2f;
                break;

            case RanchWeaponType.Bow:
                weaponMultiplier = 2.0f;
                armorPiercing = true;
                knockback = 0.4f;
                break;

            default:
                weaponMultiplier = 2.15f;
                armorPiercing = true;
                knockback = 3f;
                break;
        }

        float damage =
            core.Shop.CurrentSwordDamage *
            core.Equipment.WeaponDamageMultiplier *
            weaponMultiplier *
            core.Progression.DamageMultiplier;

        bool critical = Random.value < core.Progression.CriticalChance * 0.65f;
        if (critical)
            damage *= core.Progression.CriticalDamageMultiplier;

        RanchEnemy enemy = core.Waves.GetNearestEnemy(core.Equipment.HeavyAttackRange);
        if (enemy == null)
        {
            core.ShowMessage("The heavy " + core.Equipment.CurrentWeaponName + " attack missed.");
        }
        else
        {
            enemy.TakeDamage(damage, armorPiercing, knockback);
            core.ShowMessage(
                critical
                    ? "CRITICAL HEAVY ATTACK — " + damage.ToString("F0") + " damage!"
                    : "Heavy " + core.Equipment.CurrentWeaponName + " attack dealt " + damage.ToString("F0") + " damage."
            );
        }

        comboStep = 0;
        comboResetTimer = 0f;
        AttackCooldownRemaining = core.Equipment.IsRanged ? 1.25f : 0.9f;
        player.PlayAttackAnimation(true, 0);
    }

    private bool CanBeginWeaponAttack()
    {
        if (AttackCooldownRemaining > 0f || IsDodging)
            return false;

        if (!core.Equipment.WeaponSlotActive)
        {
            core.ShowMessage("Select Slot 2 to use your equipped weapon.");
            return false;
        }

        return true;
    }

    private void TryDodge()
    {
        if (dodgeCooldown > 0f || IsDodging || AttackCooldownRemaining > 0f)
            return;

        if (!core.Stamina.TrySpend(DodgeStamina))
        {
            core.ShowMessage("Not enough stamina to dodge.");
            return;
        }

        Vector3 direction = player.GetDesiredMoveDirection();
        if (direction.sqrMagnitude < 0.01f)
            direction = player.transform.forward;

        float distance = 3.1f + core.Progression.DodgeDistanceBonus;
        player.BeginDodge(direction.normalized, distance, 0.28f);
        dodgeInvulnerability = 0.36f;
        dodgeCooldown = 0.75f;
        IsBlocking = false;
    }

    public float ResolveIncomingDamage(float rawDamage, RanchEnemy attacker, out string result)
    {
        result = "";
        if (rawDamage <= 0f)
            return 0f;

        if (IsDodging)
        {
            result = "DODGED";
            return 0f;
        }

        if (!IsBlocking)
            return rawDamage;

        float timeBlocking = Time.time - blockStartTime;
        if (timeBlocking <= PerfectBlockWindow && core.Stamina.TrySpend(8f))
        {
            if (attacker != null)
                attacker.Stun(1.2f);

            core.Progression.AddExperience(5f, "Perfect block");
            result = "PERFECT BLOCK";
            return 0f;
        }

        if (core.Stamina.TrySpend(BlockStaminaPerHit))
        {
            float reduction = Mathf.Clamp(0.60f + core.Progression.BlockReductionBonus, 0.60f, 0.82f);
            result = "BLOCKED";
            return rawDamage * (1f - reduction);
        }

        IsBlocking = false;
        AttackCooldownRemaining = 0.65f;
        result = "GUARD BROKEN";
        return rawDamage * 1.15f;
    }

    public float MovementMultiplier => IsBlocking ? 0.45f : (AttackCooldownRemaining > 0.5f ? 0.7f : 1f);
}
