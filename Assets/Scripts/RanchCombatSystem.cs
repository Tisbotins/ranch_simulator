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
    private Material lightProjectileMaterial;
    private Material heavyProjectileMaterial;

    public void Initialize(RanchGameCore gameCore)
    {
        core = gameCore;
        lightProjectileMaterial = RanchWorldBuilder.CreateRuntimeMaterial(new Color(0.95f, 0.78f, 0.25f));
        heavyProjectileMaterial = RanchWorldBuilder.CreateRuntimeMaterial(new Color(1f, 0.35f, 0.12f));
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

        if (RanchGameModeState.IsLanClient)
        {
            IsBlocking = false;
            return;
        }

        if (core == null || player == null || core.GameWon || core.Health.IsDead ||
            core.Shop.IsOpen || core.Progression.IsOpen || core.Settings.IsOpen ||
            (core.Classes != null && core.Classes.IsOpen) ||
            (core.Laboratory != null && core.Laboratory.IsOpen) || Cursor.visible)
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

        if (core.Equipment.IsRanged)
        {
            FireRangedProjectile(false);
            AttackCooldownRemaining = 0.92f * core.Progression.RangedCooldownMultiplier;
            player.PlayAttackAnimation(false, 1);
            return;
        }

        comboStep = comboResetTimer > 0f ? (comboStep % 3) + 1 : 1;
        comboResetTimer = 0.85f;

        float[] comboMultipliers = { 1f, 1.15f, 1.38f };
        float damage =
            core.Shop.CurrentWeaponDamage *
            core.Equipment.WeaponDamageMultiplier *
            comboMultipliers[comboStep - 1] *
            core.Progression.DamageMultiplier;

        bool critical = Random.value < core.Progression.CriticalChance;
        if (critical)
            damage *= core.Progression.CriticalDamageMultiplier;

        RanchEnemy enemy = core.Waves.GetNearestEnemy(core.Equipment.LightAttackRange);
        if (enemy == null)
        {
            core.ShowMessage("No Ranch Raider is within weapon range.");
        }
        else
        {
            bool armorPiercing = core.Classes.CurrentClass == RanchClassType.Spear && comboStep == 3;
            float knockback = core.Classes.CurrentClass == RanchClassType.Spear
                ? 1.35f
                : (comboStep == 3 ? 1.4f : 0.55f);

            enemy.TakeDamage(damage, armorPiercing, knockback);
            if (critical)
                core.ShowMessage("CRITICAL " + core.Equipment.CurrentWeaponName.ToUpperInvariant() + " HIT — " + damage.ToString("F0") + " damage!");
        }

        AttackCooldownRemaining = 0.30f + comboStep * 0.025f;
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

        if (core.Equipment.IsRanged)
        {
            FireRangedProjectile(true);
            comboStep = 0;
            comboResetTimer = 0f;
            AttackCooldownRemaining = 1.50f * core.Progression.RangedCooldownMultiplier;
            player.PlayAttackAnimation(true, 0);
            return;
        }

        float weaponMultiplier;
        bool armorPiercing;
        float knockback;

        if (core.Classes.CurrentClass == RanchClassType.Spear)
        {
            weaponMultiplier = 2.30f;
            armorPiercing = true;
            knockback = 4.2f;
        }
        else
        {
            weaponMultiplier = 2.15f;
            armorPiercing = true;
            knockback = 3f;
        }

        float damage =
            core.Shop.CurrentWeaponDamage *
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
        AttackCooldownRemaining = 0.9f;
        player.PlayAttackAnimation(true, 0);
    }

    private void FireRangedProjectile(bool heavy)
    {
        float range = heavy ? core.Equipment.HeavyAttackRange : core.Equipment.LightAttackRange;
        RanchEnemy target = FindRangedTarget(range);
        Vector3 origin = player.GetProjectileOrigin();
        Vector3 direction = player.GetProjectileDirection(target);

        float multiplier = heavy ? 1.72f : 0.92f;
        float damage =
            core.Shop.CurrentWeaponDamage *
            core.Equipment.WeaponDamageMultiplier *
            multiplier *
            core.Progression.DamageMultiplier;

        bool critical = Random.value < core.Progression.CriticalChance * (heavy ? 0.75f : 1f);
        if (critical)
            damage *= core.Progression.CriticalDamageMultiplier;

        float speed = (heavy ? 24f : 29f) *
            core.Progression.ProjectileSpeedMultiplier *
            core.Shop.RangedProjectileSpeedMultiplier;

        GameObject projectileObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        projectileObject.name = heavy ? "Heavy Ranch Projectile" : "Ranch Projectile";
        projectileObject.transform.position = origin;
        projectileObject.transform.localScale = Vector3.one * (heavy ? 0.42f : 0.24f);

        Collider collider = projectileObject.GetComponent<Collider>();
        if (collider != null)
            Destroy(collider);

        Renderer renderer = projectileObject.GetComponent<Renderer>();
        if (renderer != null)
            renderer.sharedMaterial = heavy ? heavyProjectileMaterial : lightProjectileMaterial;

        RanchProjectile projectile = projectileObject.AddComponent<RanchProjectile>();
        projectile.Initialize(
            core,
            direction,
            speed,
            damage,
            range / Mathf.Max(1f, speed) + 0.65f,
            heavy,
            heavy ? 1.3f : 0.35f,
            heavy ? 0.28f : 0.16f
        );

        if (target == null)
            core.ShowMessage("Ranch projectile fired. Aim toward an enemy to land the shot.", 2.5f);
        else if (critical)
            core.ShowMessage("CRITICAL PROJECTILE — " + damage.ToString("F0") + " potential damage!", 2.5f);
    }


    private RanchEnemy FindRangedTarget(float range)
    {
        Vector3 origin = player.GetProjectileOrigin();
        Vector3 aimDirection = player.GetProjectileDirection(null).normalized;
        RanchEnemy best = null;
        float bestScore = float.MinValue;
        float minimumDot = Mathf.Cos(18f * Mathf.Deg2Rad);

        foreach (RanchEnemy enemy in core.Waves.ActiveEnemies)
        {
            if (enemy == null || enemy.Health <= 0f)
                continue;

            Vector3 toEnemy = enemy.transform.position + Vector3.up * 0.9f - origin;
            float distance = toEnemy.magnitude;
            if (distance <= 0.01f || distance > range)
                continue;

            float dot = Vector3.Dot(aimDirection, toEnemy / distance);
            if (dot < minimumDot)
                continue;

            float score = dot * 2f - distance / Mathf.Max(1f, range) * 0.35f;
            if (score > bestScore)
            {
                bestScore = score;
                best = enemy;
            }
        }

        return best;
    }

    private bool CanBeginWeaponAttack()
    {
        if (AttackCooldownRemaining > 0f || IsDodging)
            return false;

        if (core.Classes.CurrentClass == RanchClassType.Summoner)
        {
            core.ShowMessage("Summoners attack by selecting Slot 2 and summoning Delulus.");
            return false;
        }

        if (!core.Equipment.WeaponSlotActive)
        {
            core.ShowMessage("Select Slot 2 to use your class weapon.");
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
