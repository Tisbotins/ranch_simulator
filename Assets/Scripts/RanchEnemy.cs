using UnityEngine;

public class RanchEnemy : MonoBehaviour
{
    public enum EnemyArchetype
    {
        Raider,
        RanchBat,
        RanchRotCrawler
    }

    public EnemyArchetype Archetype { get; private set; }
    public string EnemyName { get; private set; }
    public float Health { get; private set; }
    public float MaxHealth { get; private set; }
    public float Damage { get; private set; }
    public int Tier { get; private set; }
    public bool IsBoss { get; private set; }
    public bool IsFinalCJ { get; private set; }
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
    private RanchCJSystem finalCJOwner;
    private CharacterController characterController;
    private int enemySerial;
    private bool ranchJockey;
    private float batAltitude;
    private float movementSeed;
    private float jockeyChargeTimer;

    public void Initialize(
        RanchGameCore gameCore,
        RanchWaveSystem waveOwner,
        Transform playerTarget,
        int enemyTier,
        int serial,
        int wave,
        EnemyArchetype enemyArchetype = EnemyArchetype.Raider)
    {
        core = gameCore;
        owner = waveOwner;
        target = playerTarget;
        SpawnWave = wave;
        enemySerial = serial;
        Tier = Mathf.Clamp(enemyTier, 0, 4);
        Archetype = enemyArchetype;

        float heatThreat = core.CJ.ThreatMultiplier;
        MaxHealth = (30f + Tier * 32f + wave * 4f) * heatThreat;
        Health = MaxHealth;
        Damage = (6f + Tier * 4f + wave * 0.7f) * heatThreat;
        speed = 1.35f + Tier * 0.18f + Mathf.Min(wave * 0.015f, 0.35f);
        specialTimer = Random.Range(1f, 3f);
        movementSeed = Random.Range(0f, 100f);
        batAltitude = Random.Range(2.8f, 4.2f);

        SetupCollision();
        ApplyArchetype();
        CreateLabel();
    }

    public void MakeBoss(string bossName, float healthMultiplier, float damageMultiplier)
    {
        IsBoss = true;
        Archetype = EnemyArchetype.Raider;
        EnemyName = bossName;
        MaxHealth *= Mathf.Max(1f, healthMultiplier);
        Health = MaxHealth;
        Damage *= Mathf.Max(1f, damageMultiplier);
        speed *= 0.92f;
        transform.localScale = Vector3.one * 2.05f;

        Renderer rootRenderer = GetComponent<Renderer>();
        if (rootRenderer != null)
            rootRenderer.enabled = true;

        ranchJockey = bossName == "The Ranch Jockey";
        if (ranchJockey)
        {
            // The wave-15 Jockey is smaller and dramatically faster than
            // the other bosses, with a dedicated charge attack.
            transform.localScale = Vector3.one * 1.65f;
            speed *= 1.42f;
            Damage *= 0.92f;
            jockeyChargeTimer = 2.2f;
            CreateRanchJockeyVisual();

            if (rootRenderer != null)
            {
                rootRenderer.material =
                    RanchWorldBuilder.CreateRuntimeMaterial(
                        new Color(0.72f, 0.42f, 0.08f)
                    );
            }
        }

        UpdateLabel();
    }

    public void MakeFinalCJ(RanchCJSystem cjOwner)
    {
        finalCJOwner = cjOwner;
        IsBoss = true;
        IsFinalCJ = true;
        EnemyName = "CJ, the Ultimate Ranchenator";
        MaxHealth = Mathf.Max(5000f, MaxHealth * 10f);
        Health = MaxHealth;
        Damage = Mathf.Clamp(Damage * 0.72f, 28f, 55f);
        speed = Mathf.Max(speed, 2.15f);
        specialTimer = 2.5f;
        transform.localScale = Vector3.one * 2.6f;
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
            MoveWithCollision(knockbackVelocity * Time.deltaTime);
            knockbackVelocity = Vector3.Lerp(knockbackVelocity, Vector3.zero, 8f * Time.deltaTime);
        }

        if (IsFinalCJ) UpdateFinalCJ();
        else if (IsBoss) UpdateBoss();
        else if (Archetype == EnemyArchetype.RanchBat) UpdateRanchBat();
        else if (Archetype == EnemyArchetype.RanchRotCrawler) UpdateRanchRotCrawler();
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

        if (Archetype != EnemyArchetype.RanchBat)
            ApplyCrowdSeparation();

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
            MoveWithCollision(away * speed * 1.8f * Time.deltaTime);
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
            MoveWithCollision(dashDirection * 2.8f);
        }
        MoveToward(target.position, speed * 1.15f, 1.6f);
        if (distance <= 1.9f) TryAttack(Damage * 0.78f, 0.68f, "combo-struck");
    }

    private void UpdateRanchBat()
    {
        Vector3 playerCenter = target.position + Vector3.up * 1.15f;
        float distance = Vector3.Distance(transform.position, playerCenter);
        specialTimer -= Time.deltaTime;

        if (specialTimer <= 0f)
        {
            // Dive straight at the player, then return to circling.
            MoveToward3D(playerCenter, speed * 2.05f, 1.1f);

            if (distance <= 1.45f)
            {
                if (TryAttack(Damage * 0.72f, 0.7f, "dive-bit"))
                {
                    core.Player.ApplyKnockback(
                        target.position - transform.position,
                        2.8f
                    );
                    specialTimer = Random.Range(2.7f, 4.1f);
                }
            }
        }
        else
        {
            float orbitAngle = Time.time * (1.2f + speed * 0.12f) + movementSeed;
            Vector3 orbitPosition = target.position + new Vector3(
                Mathf.Cos(orbitAngle) * 4.2f,
                batAltitude + Mathf.Sin(Time.time * 2.1f + movementSeed) * 0.45f,
                Mathf.Sin(orbitAngle) * 4.2f
            );

            MoveToward3D(orbitPosition, speed * 1.45f, 0.25f);
        }
    }

    private void UpdateRanchRotCrawler()
    {
        Vector3 offset = new Vector3(
            Mathf.Sin(Time.time * 3.4f + movementSeed),
            0f,
            Mathf.Cos(Time.time * 3.4f + movementSeed)
        ) * 0.55f;

        MoveToward(target.position + offset, speed * 1.38f, 1.05f);

        float distance = Vector3.Distance(transform.position, target.position);
        if (distance <= 1.35f &&
            TryAttack(Damage * 0.68f, 0.62f, "bit"))
        {
            core.Player.ApplySlow(0.68f, 1.7f);
        }
    }

    private void UpdateRanchJockey()
    {
        float distance = Vector3.Distance(transform.position, target.position);
        jockeyChargeTimer -= Time.deltaTime;

        if (jockeyChargeTimer <= 0f && distance <= 12f)
        {
            Vector3 chargeDirection =
                (target.position - transform.position).normalized;
            chargeDirection.y = 0f;

            MoveWithCollision(chargeDirection * 4.8f);
            jockeyChargeTimer = bossPhaseThree ? 1.45f : 2.4f;

            if (Vector3.Distance(transform.position, target.position) <= 3.1f)
            {
                core.Health.TakeDamage(
                    Damage * 1.18f,
                    EnemyName + " mounted charge",
                    this
                );
                core.Player.ApplyKnockback(
                    target.position - transform.position,
                    7.2f
                );
            }
        }
        else
        {
            Vector3 flank = target.position +
                target.right *
                Mathf.Sin(Time.time * 1.5f + movementSeed) *
                2.4f;

            MoveToward(
                flank,
                speed * (bossPhaseThree ? 1.24f : 1.08f),
                1.9f
            );

            if (distance <= 2.25f)
                TryAttack(
                    Damage * 0.88f,
                    bossPhaseThree ? 0.62f : 0.88f,
                    "trampled"
                );
        }
    }

    private void UpdateBoss()
    {
        if (ranchJockey)
        {
            float jockeyHealthPercent =
                MaxHealth <= 0f ? 0f : Health / MaxHealth;

            if (!bossPhaseTwo && jockeyHealthPercent <= 0.66f)
            {
                bossPhaseTwo = true;
                speed *= 1.14f;
                Damage *= 1.10f;
                core.ShowMessage(
                    EnemyName + " kicked into a faster gear!",
                    5f
                );
            }

            if (!bossPhaseThree && jockeyHealthPercent <= 0.33f)
            {
                bossPhaseThree = true;
                speed *= 1.18f;
                Damage *= 1.12f;
                core.ShowMessage(
                    EnemyName + " began the final stampede!",
                    5f
                );
            }

            UpdateRanchJockey();
            return;
        }

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

    private void UpdateFinalCJ()
    {
        float healthPercent = MaxHealth <= 0f ? 0f : Health / MaxHealth;

        if (!bossPhaseTwo && healthPercent <= 0.66f)
        {
            bossPhaseTwo = true;
            speed *= 1.14f;
            Damage *= 1.08f;
            finalCJOwner?.NotifyBossPhaseChanged(2);
        }

        if (!bossPhaseThree && healthPercent <= 0.33f)
        {
            bossPhaseThree = true;
            speed *= 1.20f;
            Damage *= 1.14f;
            finalCJOwner?.NotifyBossPhaseChanged(3);
        }

        float distance = Vector3.Distance(transform.position, target.position);
        specialTimer -= Time.deltaTime;

        if (!bossPhaseTwo)
        {
            MoveToward(target.position, speed, 2.1f);

            if (specialTimer <= 0f && distance <= 9f)
            {
                specialTimer = 4.1f;
                core.Health.TakeDamage(Damage * 0.82f, EnemyName + " corporate shockwave", this);
                core.Player.ApplyKnockback(target.position - transform.position, 5.5f);
            }
            else if (distance <= 2.5f)
            {
                TryAttack(Damage, 1.05f, "slashed");
            }
        }
        else if (!bossPhaseThree)
        {
            MoveToward(target.position, speed * 1.08f, 1.9f);

            if (specialTimer <= 0f && distance <= 7f)
            {
                specialTimer = 3f;
                Vector3 charge = (target.position - transform.position).normalized;
                MoveWithCollision(charge * 2.8f);
                core.Player.ApplySlow(0.68f, 1.8f);
                core.Health.TakeDamage(Damage, EnemyName + " hostile takeover charge", this);
            }
            else if (distance <= 2.3f)
            {
                TryAttack(Damage * 1.06f, 0.82f, "combo-struck");
            }
        }
        else
        {
            MoveToward(target.position, speed * 1.15f, 1.75f);

            if (specialTimer <= 0f && distance <= 10f)
            {
                specialTimer = 2.15f;
                core.Health.TakeDamage(Damage * 1.18f, EnemyName + " Ultimate Ranchenator blast", this);
                core.Player.ApplyKnockback(target.position - transform.position, 7.5f);
            }
            else if (distance <= 2.2f)
            {
                TryAttack(Damage * 1.12f, 0.62f, "final-phase struck");
            }
        }
    }

    private void MoveToward(
        Vector3 destination,
        float moveSpeed,
        float stopDistance)
    {
        destination.y = transform.position.y;
        Vector3 difference = destination - transform.position;

        if (difference.magnitude <= stopDistance)
            return;

        Vector3 movement =
            difference.normalized * moveSpeed * Time.deltaTime;
        MoveWithCollision(movement);

        difference.y = 0f;
        if (difference.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.LookRotation(difference);
    }

    private void MoveToward3D(
        Vector3 destination,
        float moveSpeed,
        float stopDistance)
    {
        Vector3 difference = destination - transform.position;

        if (difference.magnitude <= stopDistance)
            return;

        MoveWithCollision(
            difference.normalized * moveSpeed * Time.deltaTime
        );

        Vector3 facing = difference;
        facing.y = 0f;
        if (facing.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.LookRotation(facing);
    }

    private void MoveWithCollision(Vector3 movement)
    {
        if (characterController != null &&
            characterController.enabled)
        {
            characterController.Move(movement);
        }
        else
        {
            transform.position += movement;
        }
    }

    private void ApplyCrowdSeparation()
    {
        if (owner == null)
            return;

        Vector3 separation = Vector3.zero;
        int nearby = 0;
        float preferredDistance = IsBoss ? 1.8f : 0.9f;

        foreach (RanchEnemy other in owner.ActiveEnemies)
        {
            if (other == null || other == this)
                continue;

            Vector3 difference = transform.position - other.transform.position;
            difference.y = 0f;
            float distance = difference.magnitude;

            if (distance > 0.001f && distance < preferredDistance)
            {
                separation +=
                    difference.normalized *
                    (preferredDistance - distance);
                nearby++;
            }
        }

        if (nearby > 0)
        {
            MoveWithCollision(
                separation / nearby * 2.4f * Time.deltaTime
            );
        }
    }

    private void SetupCollision()
    {
        CapsuleCollider primitiveCollider =
            GetComponent<CapsuleCollider>();

        if (primitiveCollider != null)
        {
            primitiveCollider.enabled = false;
            Destroy(primitiveCollider);
        }

        characterController =
            GetComponent<CharacterController>();

        if (characterController == null)
            characterController =
                gameObject.AddComponent<CharacterController>();

        characterController.radius = 0.48f;
        characterController.height = 1.9f;
        characterController.center = Vector3.zero;
        characterController.slopeLimit = 48f;
        characterController.stepOffset = 0.28f;
        characterController.skinWidth = 0.07f;
        characterController.minMoveDistance = 0f;
        characterController.detectCollisions = true;
    }

    private void ApplyArchetype()
    {
        EnemyName =
            GetArchetypeName(Archetype, Tier) +
            " #" +
            enemySerial;

        Renderer rootRenderer = GetComponent<Renderer>();

        switch (Archetype)
        {
            case EnemyArchetype.RanchBat:
                MaxHealth *= 0.68f;
                Health = MaxHealth;
                Damage *= 0.74f;
                speed *= 1.52f;

                if (characterController != null)
                {
                    characterController.radius = 0.52f;
                    characterController.height = 1.15f;
                }

                if (rootRenderer != null)
                    rootRenderer.enabled = false;

                CreateRanchBatVisual();
                break;

            case EnemyArchetype.RanchRotCrawler:
                MaxHealth *= 0.82f;
                Health = MaxHealth;
                Damage *= 0.78f;
                speed *= 1.36f;

                if (characterController != null)
                {
                    characterController.radius = 0.58f;
                    characterController.height = 0.85f;
                    characterController.center =
                        new Vector3(0f, -0.15f, 0f);
                    characterController.stepOffset = 0.18f;
                }

                if (rootRenderer != null)
                    rootRenderer.enabled = false;

                CreateRanchRotCrawlerVisual();
                break;
        }
    }

    private void CreateRanchBatVisual()
    {
        Color bodyColor = new Color(0.24f, 0.06f, 0.34f);
        Color wingColor = new Color(0.48f, 0.14f, 0.58f);

        AddVisualPrimitive(
            "Bat Body",
            PrimitiveType.Sphere,
            Vector3.zero,
            new Vector3(0.7f, 0.48f, 0.9f),
            bodyColor
        );

        AddVisualPrimitive(
            "Left Wing",
            PrimitiveType.Cube,
            new Vector3(-0.78f, 0f, 0f),
            new Vector3(1.05f, 0.08f, 0.52f),
            wingColor,
            new Vector3(0f, 0f, -18f)
        );

        AddVisualPrimitive(
            "Right Wing",
            PrimitiveType.Cube,
            new Vector3(0.78f, 0f, 0f),
            new Vector3(1.05f, 0.08f, 0.52f),
            wingColor,
            new Vector3(0f, 0f, 18f)
        );
    }

    private void CreateRanchRotCrawlerVisual()
    {
        Color shellColor = new Color(0.16f, 0.42f, 0.10f);
        Color eyeColor = new Color(0.82f, 0.96f, 0.18f);

        AddVisualPrimitive(
            "Crawler Body",
            PrimitiveType.Capsule,
            new Vector3(0f, -0.18f, 0f),
            new Vector3(0.82f, 0.42f, 1.15f),
            shellColor,
            new Vector3(90f, 0f, 0f)
        );

        AddVisualPrimitive(
            "Crawler Eye Left",
            PrimitiveType.Sphere,
            new Vector3(-0.24f, 0.10f, 0.60f),
            Vector3.one * 0.14f,
            eyeColor
        );

        AddVisualPrimitive(
            "Crawler Eye Right",
            PrimitiveType.Sphere,
            new Vector3(0.24f, 0.10f, 0.60f),
            Vector3.one * 0.14f,
            eyeColor
        );
    }

    private void CreateRanchJockeyVisual()
    {
        AddVisualPrimitive(
            "Jockey Mount",
            PrimitiveType.Cube,
            new Vector3(0f, -0.25f, 0.15f),
            new Vector3(1.15f, 0.48f, 1.75f),
            new Color(0.30f, 0.12f, 0.035f)
        );

        AddVisualPrimitive(
            "Jockey Mount Head",
            PrimitiveType.Cube,
            new Vector3(0f, 0.08f, 1.02f),
            new Vector3(0.62f, 0.58f, 0.62f),
            new Color(0.38f, 0.16f, 0.045f)
        );
    }

    private GameObject AddVisualPrimitive(
        string objectName,
        PrimitiveType primitiveType,
        Vector3 localPosition,
        Vector3 localScale,
        Color color,
        Vector3? localEulerAngles = null)
    {
        GameObject visual =
            GameObject.CreatePrimitive(primitiveType);
        visual.name = objectName;
        visual.transform.SetParent(transform, false);
        visual.transform.localPosition = localPosition;
        visual.transform.localScale = localScale;
        visual.transform.localEulerAngles =
            localEulerAngles ?? Vector3.zero;

        Collider visualCollider =
            visual.GetComponent<Collider>();
        if (visualCollider != null)
        {
            visualCollider.enabled = false;
            Destroy(visualCollider);
        }

        Renderer visualRenderer =
            visual.GetComponent<Renderer>();
        if (visualRenderer != null)
        {
            visualRenderer.material =
                RanchWorldBuilder.CreateRuntimeMaterial(color);
        }

        return visual;
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

        float armor = IsFinalCJ ? 0.18f : (IsBoss ? 0.22f : (Tier == 3 ? 0.34f : 0f));
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
        if (IsFinalCJ)
        {
            owner.NotifyEnemyDefeated(this);
            finalCJOwner?.NotifyCJDefeated(this);
            Destroy(gameObject);
            return;
        }

        Vector3 deathPosition = transform.position;

        // Ranch Fever rewards chained kills, so the payout scales with momentum.
        float momentumMultiplier = core.Momentum != null
            ? core.Momentum.RewardMultiplier
            : 1f;

        float reward = (12f + Tier * 18f) * (IsBoss ? 2f : 1f) * momentumMultiplier;
        core.Inventory.AddMoney(reward);
        core.Progression.AddExperience(IsBoss ? 0f : 18f + Tier * 12f + SpawnWave * 2f, "Enemy defeated");

        if (core.Momentum != null)
            core.Momentum.RegisterKill(deathPosition, IsBoss);

        RanchJuiceSystem.Popup(
            deathPosition,
            "+$" + reward.ToString("F0"),
            new Color(0.55f, 1f, 0.55f),
            IsBoss ? 30f : 22f
        );
        RanchJuiceSystem.Sparkle(deathPosition, new Color(1f, 0.85f, 0.35f), IsBoss ? 12 : 5);
        if (IsBoss)
            RanchJuiceSystem.Shake(0.28f, 0.45f);

        owner.NotifyEnemyDefeated(this);
        if (IsBoss) core.Bosses.NotifyBossDefeated(this, SpawnWave);
        Destroy(gameObject);
    }

    private void CreateLabel()
    {
        GameObject labelObject = new GameObject("Enemy Health Label");
        labelObject.transform.SetParent(transform, false);
        labelObject.transform.localPosition =
            Archetype == EnemyArchetype.RanchBat
                ? new Vector3(0f, 1.15f, 0f)
                : Archetype == EnemyArchetype.RanchRotCrawler
                    ? new Vector3(0f, 0.95f, 0f)
                    : new Vector3(0f, 1.7f, 0f);
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
            healthLabel.text = $"{(IsFinalCJ ? "FINAL BOSS: " : (IsBoss ? "BOSS: " : ""))}{EnemyName}\n{Mathf.CeilToInt(Mathf.Max(0f, Health))}/{Mathf.CeilToInt(MaxHealth)} HP";
    }

    public static string GetArchetypeName(
        EnemyArchetype archetype,
        int tier)
    {
        switch (archetype)
        {
            case EnemyArchetype.RanchBat:
                return "Ranch Bat";

            case EnemyArchetype.RanchRotCrawler:
                return "Ranch Rot Crawler";

            default:
                return GetEnemyName(tier);
        }
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
