using UnityEngine;

public class RanchDelulu : MonoBehaviour
{
    private RanchDeployableSystem owner;
    private RanchGameCore core;
    private CharacterController controller;
    private Renderer[] renderers;
    private Color[] originalColors;
    private RanchEnemy target;
    private float damage;
    private float lifetime;
    private float age;
    private float targetRefreshTimer;
    private float attackTimer;
    private float verticalVelocity;
    private bool removed;
    private Vector3 originalScale;

    public void Initialize(
        RanchDeployableSystem deployableOwner,
        RanchGameCore gameCore,
        CharacterController characterController,
        Renderer[] visualRenderers,
        float attackDamage,
        float activeLifetime)
    {
        owner = deployableOwner;
        core = gameCore;
        controller = characterController;
        renderers = visualRenderers ?? new Renderer[0];
        damage = Mathf.Max(1f, attackDamage);
        lifetime = Mathf.Max(3f, activeLifetime);
        originalScale = transform.localScale;

        originalColors = new Color[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
                originalColors[i] = renderers[i].material.color;
        }
    }

    private void Update()
    {
        if (core == null)
            return;

        age += Time.deltaTime;
        float remaining = lifetime - age;

        if (remaining <= 0f)
        {
            RemoveDelulu();
            return;
        }

        if (remaining <= 4f)
            ApplyFade(remaining / 4f);

        attackTimer -= Time.deltaTime;
        targetRefreshTimer -= Time.deltaTime;

        if (target == null || target.Health <= 0f ||
            Vector3.Distance(transform.position, target.transform.position) > 32f ||
            targetRefreshTimer <= 0f)
        {
            target = FindNearestEnemy(28f);
            targetRefreshTimer = 0.22f;
        }

        if (target != null)
            ChaseAndAttack(target);
        else
            FollowPlayer();
    }

    private RanchEnemy FindNearestEnemy(float range)
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

    private void ChaseAndAttack(RanchEnemy enemy)
    {
        Vector3 difference = enemy.transform.position - transform.position;
        difference.y = 0f;
        float distance = difference.magnitude;

        if (distance > 1.35f)
        {
            Move(difference.normalized * 5.6f);
            FaceDirection(difference);
            return;
        }

        Move(Vector3.zero);
        FaceDirection(difference);

        if (attackTimer <= 0f)
        {
            attackTimer = 0.65f;
            enemy.TakeDamage(damage, false, 0.9f);
        }
    }

    private void FollowPlayer()
    {
        if (core.Player == null)
        {
            Move(Vector3.zero);
            return;
        }

        Vector3 desired = core.Player.transform.position - core.Player.transform.right * 1.5f;
        Vector3 difference = desired - transform.position;
        difference.y = 0f;

        if (difference.magnitude > 4f)
        {
            Move(difference.normalized * 4.5f);
            FaceDirection(difference);
        }
        else
        {
            Move(Vector3.zero);
        }
    }

    private void Move(Vector3 horizontalVelocity)
    {
        if (controller == null || !controller.enabled)
        {
            transform.position += horizontalVelocity * Time.deltaTime;
            return;
        }

        if (controller.isGrounded && verticalVelocity < 0f)
            verticalVelocity = -2f;
        else
            verticalVelocity += -18f * Time.deltaTime;

        Vector3 velocity = horizontalVelocity;
        velocity.y = verticalVelocity;
        controller.Move(velocity * Time.deltaTime);
    }

    private void FaceDirection(Vector3 direction)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(direction), 12f * Time.deltaTime);
    }

    private void ApplyFade(float percent)
    {
        percent = Mathf.Clamp01(percent);
        transform.localScale = originalScale * Mathf.Lerp(0.18f, 1f, percent);

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null)
                continue;

            Color color = originalColors[i];
            color.a *= percent;
            renderers[i].material.color = color;
        }
    }

    private void RemoveDelulu()
    {
        if (removed)
            return;

        removed = true;
        owner?.NotifyDeluluRemoved(this);
        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        if (!removed)
            owner?.NotifyDeluluRemoved(this);
    }
}
