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
        if (AttackCooldownRemaining > 0f) AttackCooldownRemaining -= Time.deltaTime;
        if (dodgeCooldown > 0f) dodgeCooldown -= Time.deltaTime;
        if (dodgeInvulnerability > 0f) dodgeInvulnerability -= Time.deltaTime;
        if (comboResetTimer > 0f) comboResetTimer -= Time.deltaTime;
        else comboStep = 0;

        if (core == null || player == null || core.GameWon || core.Health.IsDead ||
            core.Shop.IsOpen || core.Progression.IsOpen || Cursor.visible)
        {
            IsBlocking = false;
            return;
        }

        bool wantsBlock = Input.GetMouseButton(1) && !IsDodging && AttackCooldownRemaining <= 0f;
        if (wantsBlock && !IsBlocking) blockStartTime = Time.time;
        IsBlocking = wantsBlock;

        if (Input.GetKeyDown(KeyCode.LeftControl))
            TryDodge();

        if (!IsBlocking && (Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Space)))
            TryLightAttack();

        if (!IsBlocking && Input.GetKeyDown(KeyCode.Q))
            TryHeavyAttack();
    }

    public void TryLightAttack()
    {
        if (AttackCooldownRemaining > 0f || IsDodging) return;
        if (!core.Stamina.TrySpend(LightAttackStamina))
        {
            core.ShowMessage("Not enough stamina for a light attack.");
            return;
        }

        comboStep = comboResetTimer > 0f ? (comboStep % 3) + 1 : 1;
        comboResetTimer = 0.85f;
        float[] comboMultipliers = { 1f, 1.15f, 1.38f };
        float damage = core.Shop.CurrentSwordDamage * comboMultipliers[comboStep - 1] * core.Progression.DamageMultiplier;
        bool critical = Random.value < core.Progression.CriticalChance;
        if (critical) damage *= core.Progression.CriticalDamageMultiplier;

        RanchEnemy enemy = core.Waves.GetNearestEnemy(3.6f);
        if (enemy == null)
        {
            core.ShowMessage("No Ranch Raider is within sword range.");
        }
        else
        {
            enemy.TakeDamage(damage, false, comboStep == 3 ? 1.4f : 0.55f);
            if (critical) core.ShowMessage($"CRITICAL RANCH HIT — {damage:F0} damage!");
        }

        AttackCooldownRemaining = 0.30f + comboStep * 0.025f;
        player.PlayAttackAnimation(false, comboStep);
    }

    public void TryHeavyAttack()
    {
        if (AttackCooldownRemaining > 0f || IsDodging) return;
        if (!core.Stamina.TrySpend(HeavyAttackStamina))
        {
            core.ShowMessage("Not enough stamina for a heavy attack.");
            return;
        }

        float damage = core.Shop.CurrentSwordDamage * 2.15f * core.Progression.DamageMultiplier;
        bool critical = Random.value < core.Progression.CriticalChance * 0.65f;
        if (critical) damage *= core.Progression.CriticalDamageMultiplier;

        RanchEnemy enemy = core.Waves.GetNearestEnemy(4.25f);
        if (enemy == null)
            core.ShowMessage("The heavy attack missed.");
        else
        {
            enemy.TakeDamage(damage, true, 3f);
            core.ShowMessage(critical ? $"CRITICAL HEAVY ATTACK — {damage:F0} damage!" : $"Heavy attack dealt {damage:F0} damage.");
        }

        comboStep = 0;
        comboResetTimer = 0f;
        AttackCooldownRemaining = 0.9f;
        player.PlayAttackAnimation(true, 0);
    }

    private void TryDodge()
    {
        if (dodgeCooldown > 0f || IsDodging || AttackCooldownRemaining > 0f) return;
        if (!core.Stamina.TrySpend(DodgeStamina))
        {
            core.ShowMessage("Not enough stamina to dodge.");
            return;
        }

        Vector3 direction = player.GetDesiredMoveDirection();
        if (direction.sqrMagnitude < 0.01f) direction = player.transform.forward;
        float distance = 3.1f + core.Progression.DodgeDistanceBonus;
        player.BeginDodge(direction.normalized, distance, 0.28f);
        dodgeInvulnerability = 0.36f;
        dodgeCooldown = 0.75f;
        IsBlocking = false;
    }

    public float ResolveIncomingDamage(float rawDamage, RanchEnemy attacker, out string result)
    {
        result = "";
        if (rawDamage <= 0f) return 0f;

        if (IsDodging)
        {
            result = "DODGED";
            return 0f;
        }

        if (!IsBlocking) return rawDamage;

        float timeBlocking = Time.time - blockStartTime;
        if (timeBlocking <= PerfectBlockWindow && core.Stamina.TrySpend(8f))
        {
            if (attacker != null) attacker.Stun(1.2f);
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
