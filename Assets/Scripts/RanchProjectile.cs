using UnityEngine;

public class RanchProjectile : MonoBehaviour
{
    private RanchGameCore core;
    private Vector3 velocity;
    private float damage;
    private float remainingLifetime;
    private bool armorPiercing;
    private float knockback;
    private float radius;
    private bool resolved;

    public void Initialize(
        RanchGameCore gameCore,
        Vector3 direction,
        float speed,
        float attackDamage,
        float lifetime,
        bool piercesArmor,
        float knockbackStrength,
        float projectileRadius)
    {
        core = gameCore;
        velocity = direction.normalized * Mathf.Max(1f, speed);
        damage = Mathf.Max(1f, attackDamage);
        remainingLifetime = Mathf.Max(0.2f, lifetime);
        armorPiercing = piercesArmor;
        knockback = Mathf.Max(0f, knockbackStrength);
        radius = Mathf.Clamp(projectileRadius, 0.08f, 0.7f);
        transform.forward = direction.normalized;
    }

    private void Update()
    {
        if (resolved || core == null)
        {
            Destroy(gameObject);
            return;
        }

        float delta = Time.deltaTime;
        remainingLifetime -= delta;
        if (remainingLifetime <= 0f)
        {
            Destroy(gameObject);
            return;
        }

        Vector3 distance = velocity * delta;
        RaycastHit hit;
        if (Physics.SphereCast(
            transform.position,
            radius,
            velocity.normalized,
            out hit,
            distance.magnitude,
            Physics.DefaultRaycastLayers,
            QueryTriggerInteraction.Ignore))
        {
            RanchEnemy enemy = hit.collider.GetComponentInParent<RanchEnemy>();
            if (enemy != null && enemy.Health > 0f)
            {
                enemy.TakeDamage(damage, armorPiercing, knockback);
                resolved = true;
                Destroy(gameObject);
                return;
            }

            // Ignore friendly/player colliders. World geometry still stops the shot.
            RanchPlayerController player = hit.collider.GetComponentInParent<RanchPlayerController>();
            RanchDelulu delulu = hit.collider.GetComponentInParent<RanchDelulu>();
            RanchTrap trap = hit.collider.GetComponentInParent<RanchTrap>();
            if (player == null && delulu == null && trap == null)
            {
                resolved = true;
                Destroy(gameObject);
                return;
            }
        }

        transform.position += distance;
        transform.Rotate(0f, 0f, 720f * delta, Space.Self);
    }
}
