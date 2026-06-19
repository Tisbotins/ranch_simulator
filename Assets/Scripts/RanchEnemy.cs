using UnityEngine;

public class RanchEnemy : MonoBehaviour
{
    public string EnemyName { get; private set; }
    public float Health { get; private set; }
    public float MaxHealth { get; private set; }
    public float Damage { get; private set; }
    public int Tier { get; private set; }

    public float DistanceToPlayer => core == null || core.Player == null
        ? float.MaxValue
        : Vector3.Distance(transform.position, core.Player.transform.position);

    private RanchGameCore core;
    private RanchWaveSystem owner;
    private Transform target;
    private TextMesh healthLabel;
    private float speed;
    private float attackTimer;

    public void Initialize(RanchGameCore gameCore, RanchWaveSystem waveOwner,
        Transform playerTarget, int enemyTier, int serial, int wave)
    {
        core = gameCore;
        owner = waveOwner;
        target = playerTarget;
        Tier = Mathf.Clamp(enemyTier, 0, 4);
        EnemyName = $"{GetEnemyName(Tier)} #{serial}";
        MaxHealth = 30f + Tier * 32f + wave * 4f;
        Health = MaxHealth;
        Damage = 6f + Tier * 4f + wave * 0.7f;
        speed = 1.35f + Tier * 0.18f + Mathf.Min(wave * 0.015f, 0.35f);
        CreateLabel();
    }

    private void Update()
    {
        if (core == null || target == null || core.GameWon || core.Health.IsDead || core.Shop.IsOpen) return;
        Vector3 destination = target.position;
        destination.y = transform.position.y;
        float distance = Vector3.Distance(transform.position, destination);

        if (distance > 1.75f)
        {
            transform.position = Vector3.MoveTowards(transform.position, destination, speed * Time.deltaTime);
            Vector3 direction = destination - transform.position;
            if (direction.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(direction);
        }
        else
        {
            attackTimer += Time.deltaTime;
            if (attackTimer >= 1.15f)
            {
                attackTimer = 0f;
                core.Health.TakeDamage(Damage, EnemyName);
            }
        }
        UpdateLabel();
    }

    public void TakeDamage(float amount)
    {
        if (amount <= 0f || Health <= 0f) return;
        Health -= amount;
        UpdateLabel();
        if (Health <= 0f) Die();
    }

    private void Die()
    {
        float reward = 12f + Tier * 18f;
        core.Inventory.AddMoney(reward);
        core.ShowMessage($"{EnemyName} defeated. Earned ${reward:F0}.");
        owner.NotifyEnemyDefeated(this);
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
            healthLabel.text = $"{EnemyName}\n{Mathf.CeilToInt(Mathf.Max(0f, Health))}/{Mathf.CeilToInt(MaxHealth)} HP";
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
