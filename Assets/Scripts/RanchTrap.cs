using UnityEngine;

public class RanchTrap : MonoBehaviour
{
    private RanchDeployableSystem owner;
    private RanchGameCore core;
    private float damage;
    private float lifetime;
    private float age;
    private float scanTimer;
    private bool removed;

    public void Initialize(
        RanchDeployableSystem deployableOwner,
        RanchGameCore gameCore,
        float trapDamage,
        float trapLifetime)
    {
        owner = deployableOwner;
        core = gameCore;
        damage = Mathf.Max(1f, trapDamage);
        lifetime = Mathf.Max(1f, trapLifetime);
    }

    private void Update()
    {
        if (core == null)
            return;

        age += Time.deltaTime;
        if (age >= lifetime)
        {
            RemoveTrap(false);
            return;
        }

        scanTimer -= Time.deltaTime;
        if (scanTimer > 0f)
            return;

        scanTimer = 0.08f;
        RanchEnemy enemy = FindEnemyInRange(1.65f);
        if (enemy == null)
            return;

        enemy.Stun(2.25f);
        enemy.TakeDamage(damage, true, 5.5f);
        core.ShowMessage("Ranch Trap triggered on " + enemy.EnemyName + " for " + damage.ToString("F0") + " damage!", 4f);
        transform.localScale = new Vector3(1.35f, 0.25f, 1.35f);
        RemoveTrap(true);
    }

    private RanchEnemy FindEnemyInRange(float range)
    {
        if (core.Waves == null)
            return null;

        RanchEnemy nearest = null;
        float nearestDistance = range;

        foreach (RanchEnemy enemy in core.Waves.ActiveEnemies)
        {
            if (enemy == null || enemy.Health <= 0f)
                continue;

            float distance = Vector3.Distance(transform.position, enemy.transform.position);
            if (distance <= nearestDistance)
            {
                nearest = enemy;
                nearestDistance = distance;
            }
        }

        return nearest;
    }

    private void RemoveTrap(bool triggered)
    {
        if (removed)
            return;

        removed = true;
        owner?.NotifyTrapRemoved(this);

        if (!triggered)
            core?.ShowMessage("An unused Ranch Trap expired.", 3f);

        Destroy(gameObject, triggered ? 0.16f : 0f);
    }

    private void OnDestroy()
    {
        if (!removed)
            owner?.NotifyTrapRemoved(this);
    }
}
