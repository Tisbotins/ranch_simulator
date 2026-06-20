using UnityEngine;

public class RanchEnemy : MonoBehaviour
{
    public string EnemyName { get; private set; }
    public float Health { get; private set; }
    public float MaxHealth { get; private set; }
    public float Damage { get; private set; }
    public int Tier { get; private set; }
    public bool IsBoss { get; private set; }
    public int SpawnWave { get; private set; }

    public float DistanceToPlayer => core == null || core.Player == null
        ? float.MaxValue
        : Vector3.Distance(transform.position, core.Player.transform.position);

    private RanchGameCore core;
    private RanchWaveSystem owner;
    private Transform target;
    private TextMesh healthLabel;
    private float speed;
    private float attackTimer;
    private float specialTimer;
    private float stunnedTimer;
    private float fleeTimer;
    private Vector3 knockbackVelocity;
    private bool bossPhaseTwo;
    private bool bossPhaseThree;

    public void Initialize(RanchGameCore gameCore, RanchWaveSystem waveOwner,
        Transform playerTarget, int enemyTier, int serial, int wave)
    {
        core = gameCore;
        owner = waveOwner;
        target = playerTarget;
        SpawnWave = wave;
        Tier = Mathf.Clamp(enemyTier, 0, 4);
        EnemyName = $"{GetEnemyName(Tier)} #{serial}";
        float heatThreat = core.CJ.ThreatMultiplier;
        MaxHealth = (30f + Tier * 32f + wave * 4f) * heatThreat;
        Health = MaxHealth;
        Damage = (6f + Tier * 4f + wave * 0.7f) * heatThreat;
        speed = 1.35f + Tier * 0.18f + Mathf.Min(wave * 0.015f, 0.35f);
        specialTimer = Random.Range(1f, 3f);
        CreateLabel();
    }

    public void MakeBoss(string bossName, float healthMultiplier, float damageMultiplier)
    {
        IsBoss = true;
        EnemyName = bossName;
        MaxHealth *= Mathf.Max(1f, healthMultiplier);
        Health = MaxHealth;
        Damage *= Mathf.Max(1f, damageMultiplier);
        speed *= 0.92f;
        transform.localScale = Vector3.one * 2.05f;
        UpdateLabel();
    }

    private void Update()
    {
        if (core == null || target == null || core.GameWon || core.Health.IsDead ||
            core.Shop.IsOpen || core.Progression.IsOpen) return;

        if (stunnedTimer > 0f)
        {
            stunnedTimer -= Time.deltaTime;
            return;
        }

        if (knockbackVelocity.sqrMagnitude > 0.01f)
        {
            transform.position += knockbackVelocity * Time.deltaTime;
            knockbackVelocity = Vector3.Lerp(knockbackVelocity, Vector3.zero, 8f * Time.deltaTime);
        }

        if (IsBoss) UpdateBoss();
        else
        {
            switch (Tier)
            {
                case 0: UpdateSneak(); break;
                case 1: UpdateBandit(); break;
                case 2: UpdateCreamRaider(); break;
                case 3: UpdateMarauder(); break;
                default: UpdateElite(); break;
            }
        }
        UpdateLabel();
    }

    private void UpdateSneak()
    {
        Vector3 behind = target.position - target.forward * 1.35f;
        MoveToward(behind, speed * 1.28f, 1.4f);
        if (Vector3.Distance(transform.position, target.position) <= 1.65f)
            TryAttack(Damage * 0.82f, 0.78f, "ambushed");
    }

    private void UpdateBandit()
    {
        if (fleeTimer > 0f)
        {
            fleeTimer -= Time.deltaTime;
            Vector3 away = (transform.position - target.position).normalized;
            transform.position += away * speed * 1.8f * Time.deltaTime;
            return;
        }

        MoveToward(target.position, speed * 1.08f, 1.65f);
        float distance = Vector3.Distance(transform.position, target.position);
        if (distance <= 1.8f)
        {
            specialTimer -= Time.deltaTime;
            if (specialTimer <= 0f && TryStealBottle())
            {
                specialTimer = 5.5f;
                fleeTimer = 2.2f;
            }
            else TryAttack(Damage * 0.9f, 1.1f, "slashed");
        }
    }

    private void UpdateCreamRaider()
    {
        float distance = Vector3.Distance(transform.position, target.position);
        if (distance > 6.5f) MoveToward(target.position, speed, 5.6f);
        else if (distance < 3.5f)
        {
            Vector3 away = transform.position + (transform.position - target.position).normalized * 3f;
            MoveToward(away, speed * 0.9f, 0.2f);
        }

        specialTimer -= Time.deltaTime;
        if (distance <= 7f && specialTimer <= 0f)
        {
            specialTimer = 2.65f;
            core.Player.ApplySlow(0.58f, 2.2f);
            core.Health.TakeDamage(Damage * 0.72f, EnemyName + " cream blast", this);
        }
    }

    private void UpdateMarauder()
    {
        MoveToward(target.position, speed * 0.72f, 1.9f);
        if (Vector3.Distance(transform.position, target.position) <= 2.1f)
        {
            if (TryAttack(Damage * 1.45f, 1.85f, "smashed"))
                core.Player.ApplyKnockback(target.position - transform.position, 4.5f);
        }
    }

    private void UpdateElite()
    {
        float distance = Vector3.Distance(transform.position, target.position);
        specialTimer -= Time.deltaTime;
        if (distance > 3.5f && specialTimer <= 0f)
        {
            specialTimer = 3f;
            Vector3 dashDirection = (target.position - transform.position).normalized;
            transform.position += dashDirection * 2.8f;
        }
        MoveToward(target.position, speed * 1.15f, 1.6f);
        if (distance <= 1.9f) TryAttack(Damage * 0.78f, 0.68f, "combo-struck");
    }

    private void UpdateBoss()
    {
        float healthPercent = MaxHealth <= 0f ? 0f : Health / MaxHealth;
        if (!bossPhaseTwo && healthPercent <= 0.66f)
        {
            bossPhaseTwo = true;
            speed *= 1.18f;
            Damage *= 1.12f;
            core.ShowMessage(EnemyName + " entered Phase 2!", 5f);
        }
        if (!bossPhaseThree && healthPercent <= 0.33f)
        {
            bossPhaseThree = true;
            speed *= 1.25f;
            Damage *= 1.18f;
            core.ShowMessage(EnemyName + " entered FINAL PHASE!", 5f);
        }

        MoveToward(target.position, speed, 2.2f);
        specialTimer -= Time.deltaTime;
        float distance = Vector3.Distance(transform.position, target.position);
        if (specialTimer <= 0f && distance <= 8f)
        {
            specialTimer = bossPhaseThree ? 2.5f : 4f;
            core.Health.TakeDamage(Damage * 1.25f, EnemyName + " shockwave", this);
            core.Player.ApplyKnockback(target.position - transform.position, 6f);
        }
        else if (distance <= 2.5f)
        {
            TryAttack(Damage, bossPhaseThree ? 0.75f : 1.1f, "crushed");
        }
    }

    private void MoveToward(Vector3 destination, float moveSpeed, float stopDistance)
    {
        destination.y = transform.position.y;
        if (Vector3.Distance(transform.position, destination) <= stopDistance) return;
        transform.position = Vector3.MoveTowards(transform.position, destination, moveSpeed * Time.deltaTime);
        Vector3 direction = destination - transform.position;
        if (direction.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(direction);
    }

    private bool TryAttack(float damage, float interval, string verb)
    {
        attackTimer += Time.deltaTime;
        if (attackTimer < interval) return false;
        attackTimer = 0f;
        core.Health.TakeDamage(damage, EnemyName + " " + verb, this);
        return true;
    }

    private bool TryStealBottle()
    {
        int selected = core.Bottles.SelectedTier;
        if (!core.Inventory.TryRemoveBottle(selected, 1)) return false;
        core.AddCJHeat(core.Bottles.GetCapacity(selected));
        core.ShowMessage(EnemyName + " stole one " + core.Bottles.GetTierName(selected) + "!");
        return true;
    }

    public void TakeDamage(float amount, bool armorPiercing = false, float knockback = 0f)
    {
        if (amount <= 0f || Health <= 0f) return;

        if (!IsBoss && Tier == 4 && !armorPiercing && Random.value < 0.14f)
        {
            core.ShowMessage(EnemyName + " dodged your attack!");
            return;
        }

        float armor = IsBoss ? 0.22f : (Tier == 3 ? 0.34f : 0f);
        float finalDamage = armorPiercing ? amount : amount * (1f - armor);
        Health -= Mathf.Max(1f, finalDamage);

        if (knockback > 0f && core.Player != null)
        {
            Vector3 away = transform.position - core.Player.transform.position;
            away.y = 0f;
            if (away.sqrMagnitude > 0.01f) knockbackVelocity += away.normalized * knockback;
        }

        UpdateLabel();
        if (Health <= 0f) Die();
    }

    public void Stun(float duration)
    {
        stunnedTimer = Mathf.Max(stunnedTimer, duration);
    }

    private void Die()
    {
        float reward = (12f + Tier * 18f) * (IsBoss ? 2f : 1f);
        core.Inventory.AddMoney(reward);
        core.Progression.AddExperience(IsBoss ? 0f : 18f + Tier * 12f + SpawnWave * 2f, "Enemy defeated");
        owner.NotifyEnemyDefeated(this);
        if (IsBoss) core.Bosses.NotifyBossDefeated(this, SpawnWave);
        else core.ShowMessage($"{EnemyName} defeated. Earned ${reward:F0}.");
        Destroy(gameObject);
    }

    private void CreateLabel()
    {
        GameObject labelObject = new GameObject("Enemy Health Label");
        labelObject.transform.SetParent(transform, false);
        labelObject.transform.localPosition = new Vector3(0f, 1.7f, 0f);
        healthLabel = labelObject.AddComponent<TextMesh>();
        healthLabel.anchor = TextAnchor.MiddleCenter;
        healthLabel.alignment = TextAlignment.Center;
        healthLabel.characterSize = 0.08f;
        healthLabel.fontSize = 48;
        healthLabel.color = Color.white;
        labelObject.AddComponent<RanchBillboard>();
        UpdateLabel();
    }

    private void UpdateLabel()
    {
        if (healthLabel != null)
            healthLabel.text = $"{(IsBoss ? "BOSS: " : "")}{EnemyName}\n{Mathf.CeilToInt(Mathf.Max(0f, Health))}/{Mathf.CeilToInt(MaxHealth)} HP";
    }

    public static string GetEnemyName(int tier)
    {
        switch (tier)
        {
            case 0: return "Ranch Sneak";
            case 1: return "Bottle Bandit";
            case 2: return "Cream Raider";
            case 3: return "Ancient Ranch Marauder";
            default: return "CJ Elite Ranchenator";
        }
    }
}
