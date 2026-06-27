using UnityEngine;

public class RanchPlayerController : MonoBehaviour
{
    public string CurrentPrompt { get; private set; } = "";
    public bool IsExtracting { get; private set; }
    public float MoveSpeed = 6f;
    public float MouseSensitivity = 2.2f;
    public float InteractionRange = 3.2f;
    public float FallRecoveryHeight = -8f;

    private RanchGameCore core;
    private CharacterController controller;
    private Camera playerCamera;
    private float cameraPitch = 15f;
    private float yVelocity;
    private float weaponAnimation;
    private bool heavyAnimation;
    private int animationCombo;
    private float slowMultiplier = 1f;
    private float slowTimer;
    private Vector3 knockbackVelocity;
    private Vector3 dodgeDirection;
    private float dodgeTime;
    private float dodgeDuration;
    private float dodgeDistance;
    private Vector3 lastSafePosition;
    private float lastSafeRotation;
    private float safeSampleTimer;
    private Vector3 animatedBasePosition;
    private Quaternion animatedBaseRotation;
    private Transform lastAnimatedWeapon;

    public void Initialize(
        RanchGameCore gameCore,
        CharacterController characterController,
        Camera camera)
    {
        core = gameCore;
        controller = characterController;
        playerCamera = camera;
        lastSafePosition = transform.position;
        lastSafeRotation = transform.eulerAngles.y;
        LockCursor();
        core.Equipment.SetExtractionOverride(false);
    }

    private void Update()
    {
        if (core == null || controller == null || playerCamera == null)
            return;

        if (transform.position.y < FallRecoveryHeight)
        {
            ReturnToSafePosition();
            core.ShowMessage("You fell off the map and were returned to safety.");
        }

        if (core.GameWon || core.Health.IsDead || core.Health.IsDowned || core.Shop.IsOpen ||
            core.Progression.IsOpen || core.Settings.IsOpen ||
            (core.Classes != null && core.Classes.IsOpen) ||
            (core.Laboratory != null && core.Laboratory.IsOpen))
        {
            core.Equipment.SetExtractionOverride(false);
            return;
        }

        // Both the host and the LAN guest run the full local game loop. Each
        // player owns their own ranch, shop, upgrades, and save; only the shared
        // enemy threat and downed/revive flow are networked (RanchLanMultiplayer).
        HandleCursor();
        HandleEquipmentSlots();
        HandleMovement();
        UpdateCamera();
        HandleBottleSelection();
        HandleDeployables();
        HandleInteraction();
        UpdateWeaponAnimation();
        UpdateSafetyPosition();
    }

    private void HandleMovement()
    {
        transform.Rotate(Vector3.up * Input.GetAxis("Mouse X") * MouseSensitivity);
        cameraPitch = Mathf.Clamp(
            cameraPitch - Input.GetAxis("Mouse Y") * MouseSensitivity,
            -5f,
            55f
        );

        if (slowTimer > 0f)
        {
            slowTimer -= Time.deltaTime;
            if (slowTimer <= 0f)
                slowMultiplier = 1f;
        }

        if (dodgeTime > 0f)
        {
            dodgeTime -= Time.deltaTime;
            float speed = dodgeDuration <= 0f ? 0f : dodgeDistance / dodgeDuration;
            controller.Move(dodgeDirection * speed * Time.deltaTime);
            return;
        }

        Vector3 move = GetDesiredMoveDirection();
        if (move.magnitude > 1f)
            move.Normalize();

        if (controller.isGrounded && yVelocity < 0f)
            yVelocity = -2f;

        yVelocity += -18f * Time.deltaTime;

        float movementMultiplier = core.Combat.MovementMultiplier * slowMultiplier;
        Vector3 velocity = move * MoveSpeed * movementMultiplier + knockbackVelocity;
        velocity.y = yVelocity;
        controller.Move(velocity * Time.deltaTime);
        knockbackVelocity = Vector3.Lerp(knockbackVelocity, Vector3.zero, 7f * Time.deltaTime);
    }

    public Vector3 GetDesiredMoveDirection()
    {
        Vector3 move =
            transform.right * Input.GetAxisRaw("Horizontal") +
            transform.forward * Input.GetAxisRaw("Vertical");

        return move.magnitude > 1f ? move.normalized : move;
    }

    public Vector3 GetProjectileOrigin()
    {
        return transform.position + Vector3.up * 1.55f + transform.forward * 0.9f;
    }

    public Vector3 GetProjectileDirection(RanchEnemy target)
    {
        Vector3 origin = GetProjectileOrigin();

        if (target != null && target.Health > 0f)
        {
            Vector3 targetPoint = target.transform.position + Vector3.up * 0.9f;
            Vector3 assistedDirection = targetPoint - origin;
            if (assistedDirection.sqrMagnitude > 0.01f)
                return assistedDirection.normalized;
        }

        if (playerCamera != null)
            return playerCamera.transform.forward.normalized;

        return transform.forward;
    }

    public void BeginDodge(Vector3 direction, float distance, float duration)
    {
        dodgeDirection = direction.normalized;
        dodgeDistance = Mathf.Max(0f, distance);
        dodgeDuration = Mathf.Max(0.05f, duration);
        dodgeTime = dodgeDuration;
    }

    public void ApplySlow(float multiplier, float duration)
    {
        slowMultiplier = Mathf.Clamp(multiplier, 0.25f, 1f);
        slowTimer = Mathf.Max(slowTimer, duration);
    }

    public void ApplyKnockback(Vector3 direction, float strength)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude > 0.01f)
            knockbackVelocity += direction.normalized * Mathf.Max(0f, strength);
    }

    public void PlayAttackAnimation(bool heavy, int combo)
    {
        Transform weapon = core.Equipment.GetActiveWeaponVisual();
        if (weapon == null)
            return;

        if (lastAnimatedWeapon != weapon)
        {
            ResetLastAnimatedWeapon();
            lastAnimatedWeapon = weapon;
            animatedBasePosition = weapon.localPosition;
            animatedBaseRotation = weapon.localRotation;
        }

        heavyAnimation = heavy;
        animationCombo = combo;
        weaponAnimation = heavy ? 0.75f : 0.32f;
        RanchLanMultiplayer.NotifyLocalAttackAnimation(heavy, combo);
    }

    private void UpdateWeaponAnimation()
    {
        Transform weapon = core.Equipment.GetActiveWeaponVisual();

        if (weapon == null || !weapon.gameObject.activeInHierarchy)
        {
            ResetLastAnimatedWeapon();
            return;
        }

        if (lastAnimatedWeapon != weapon)
        {
            ResetLastAnimatedWeapon();
            lastAnimatedWeapon = weapon;
            animatedBasePosition = weapon.localPosition;
            animatedBaseRotation = weapon.localRotation;
        }

        if (weaponAnimation <= 0f)
        {
            weapon.localPosition = animatedBasePosition;
            weapon.localRotation = animatedBaseRotation;
            return;
        }

        float total = heavyAnimation ? 0.75f : 0.32f;
        weaponAnimation -= Time.deltaTime;
        float progress = 1f - Mathf.Clamp01(weaponAnimation / total);
        float pulse = Mathf.Sin(progress * Mathf.PI);

        switch (core.Equipment.EquippedWeapon)
        {
            case RanchWeaponType.Spear:
                weapon.localPosition = animatedBasePosition + Vector3.forward * pulse * (heavyAnimation ? 1.2f : 0.7f);
                weapon.localRotation = animatedBaseRotation * Quaternion.Euler(-pulse * 12f, 0f, 0f);
                break;

            case RanchWeaponType.Bow:
                weapon.localPosition = animatedBasePosition + Vector3.back * pulse * 0.18f;
                weapon.localRotation = animatedBaseRotation * Quaternion.Euler(0f, pulse * 8f, 0f);
                break;

            default:
                float angle = heavyAnimation ? 145f : 75f + animationCombo * 10f;
                weapon.localRotation = animatedBaseRotation * Quaternion.Euler(pulse * angle, 0f, 0f);
                break;
        }
    }

    private void ResetLastAnimatedWeapon()
    {
        if (lastAnimatedWeapon != null)
        {
            lastAnimatedWeapon.localPosition = animatedBasePosition;
            lastAnimatedWeapon.localRotation = animatedBaseRotation;
        }

        lastAnimatedWeapon = null;
        weaponAnimation = 0f;
    }

    private void UpdateCamera()
    {
        Quaternion rotation = Quaternion.Euler(cameraPitch, transform.eulerAngles.y, 0f);
        playerCamera.transform.position = transform.position + rotation * new Vector3(0f, 3.8f, -7f);
        playerCamera.transform.LookAt(transform.position + new Vector3(0f, 1.4f, 0f));
    }

    private void HandleEquipmentSlots()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1))
            core.Equipment.SelectSlot(0);
        if (Input.GetKeyDown(KeyCode.Alpha2))
            core.Equipment.SelectSlot(1);
        if (Input.GetKeyDown(KeyCode.Alpha3))
            core.Equipment.SelectSlot(2);
    }

    private void HandleBottleSelection()
    {
        if (Input.GetKeyDown(KeyCode.LeftBracket))
            core.Bottles.CycleSelection(-1);
        if (Input.GetKeyDown(KeyCode.RightBracket))
            core.Bottles.CycleSelection(1);
    }


    private void HandleDeployables()
    {
        if (core.Deployables == null)
            return;

        bool deployPressed =
            Input.GetMouseButtonDown(0) ||
            Input.GetKeyDown(KeyCode.F);

        if (!deployPressed)
            return;

        if (core.Equipment.TrapSlotActive ||
            core.Equipment.WandSlotActive)
        {
            core.Deployables.UseSelectedDeployable(transform);
        }
    }

    private void HandleInteraction()
    {
        RanchInteractable nearest = FindNearestInteractable();
        IsExtracting = false;

        if (nearest == null)
        {
            CurrentPrompt = "";
            core.Equipment.SetExtractionOverride(false);
            return;
        }

        CurrentPrompt = nearest.Prompt;

        if (nearest.UsesHeldInteraction)
        {
            IsExtracting = Input.GetKey(KeyCode.E);
            core.Equipment.SetExtractionOverride(IsExtracting);

            if (IsExtracting)
                nearest.Interact(this, true, Time.deltaTime);
        }
        else
        {
            core.Equipment.SetExtractionOverride(false);
            if (Input.GetKeyDown(KeyCode.E))
                nearest.Interact(this, false, 0f);
        }
    }

    private RanchInteractable FindNearestInteractable()
    {
        Collider[] colliders = Physics.OverlapSphere(transform.position, InteractionRange);
        RanchInteractable nearest = null;
        float nearestDistance = float.MaxValue;

        foreach (Collider collider in colliders)
        {
            RanchInteractable interactable = collider.GetComponentInParent<RanchInteractable>();
            if (interactable == null)
                continue;

            float distance = Vector3.Distance(transform.position, interactable.transform.position);
            if (distance < nearestDistance)
            {
                nearest = interactable;
                nearestDistance = distance;
            }
        }

        return nearest;
    }

    private void UpdateSafetyPosition()
    {
        if (!controller.isGrounded || dodgeTime > 0f)
            return;

        safeSampleTimer -= Time.deltaTime;
        if (safeSampleTimer > 0f)
            return;

        safeSampleTimer = 0.35f;
        lastSafePosition = transform.position;
        lastSafeRotation = transform.eulerAngles.y;
    }

    public void ReturnToSafePosition()
    {
        Teleport(lastSafePosition, lastSafeRotation);
        yVelocity = 0f;
        knockbackVelocity = Vector3.zero;
    }

    public void Teleport(Vector3 position, float rotationY)
    {
        if (controller != null)
            controller.enabled = false;

        transform.position = position;
        transform.rotation = Quaternion.Euler(0f, rotationY, 0f);

        if (controller != null)
            controller.enabled = true;

        lastSafePosition = position;
        lastSafeRotation = rotationY;
        yVelocity = 0f;
    }

    private void HandleCursor()
    {
        if (Input.GetMouseButtonDown(0) && Cursor.visible)
            LockCursor();
    }

    private static void LockCursor()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }
}
