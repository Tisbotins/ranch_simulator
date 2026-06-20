using UnityEngine;

public class RanchPlayerController : MonoBehaviour
{
    public string CurrentPrompt { get; private set; } = "";
    public bool IsExtracting { get; private set; }
    public float MoveSpeed = 6f;
    public float MouseSensitivity = 2.2f;
    public float InteractionRange = 3.2f;

    private RanchGameCore core;
    private CharacterController controller;
    private Camera playerCamera;
    private Transform sword;
    private Transform extractor;
    private float cameraPitch = 15f;
    private float yVelocity;
    private float swordAnimation;
    private bool heavyAnimation;
    private int animationCombo;
    private float slowMultiplier = 1f;
    private float slowTimer;
    private Vector3 knockbackVelocity;
    private Vector3 dodgeDirection;
    private float dodgeTime;
    private float dodgeDuration;
    private float dodgeDistance;

    public void Initialize(RanchGameCore gameCore, CharacterController characterController,
        Camera camera, Transform swordVisual, Transform extractorVisual)
    {
        core = gameCore;
        controller = characterController;
        playerCamera = camera;
        sword = swordVisual;
        extractor = extractorVisual;
        LockCursor();
        UpdateTools();
    }

    private void Update()
    {
        if (core == null || controller == null || playerCamera == null || core.GameWon ||
            core.Health.IsDead || core.Shop.IsOpen || core.Progression.IsOpen) return;

        HandleCursor();
        HandleMovement();
        UpdateCamera();
        HandleBottleSelection();
        HandleInteraction();
        UpdateSwordAnimation();
        UpdateTools();
    }

    private void HandleMovement()
    {
        transform.Rotate(Vector3.up * Input.GetAxis("Mouse X") * MouseSensitivity);
        cameraPitch = Mathf.Clamp(cameraPitch - Input.GetAxis("Mouse Y") * MouseSensitivity, -5f, 55f);

        if (slowTimer > 0f)
        {
            slowTimer -= Time.deltaTime;
            if (slowTimer <= 0f) slowMultiplier = 1f;
        }

        if (dodgeTime > 0f)
        {
            dodgeTime -= Time.deltaTime;
            float speed = dodgeDuration <= 0f ? 0f : dodgeDistance / dodgeDuration;
            controller.Move(dodgeDirection * speed * Time.deltaTime);
            return;
        }

        Vector3 move = GetDesiredMoveDirection();
        if (move.magnitude > 1f) move.Normalize();
        if (controller.isGrounded && yVelocity < 0f) yVelocity = -2f;
        yVelocity += -18f * Time.deltaTime;

        float movementMultiplier = core.Combat.MovementMultiplier * slowMultiplier;
        Vector3 velocity = move * MoveSpeed * movementMultiplier + knockbackVelocity;
        velocity.y = yVelocity;
        controller.Move(velocity * Time.deltaTime);
        knockbackVelocity = Vector3.Lerp(knockbackVelocity, Vector3.zero, 7f * Time.deltaTime);
    }

    public Vector3 GetDesiredMoveDirection()
    {
        Vector3 move = transform.right * Input.GetAxisRaw("Horizontal") +
                       transform.forward * Input.GetAxisRaw("Vertical");
        return move.magnitude > 1f ? move.normalized : move;
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
        heavyAnimation = heavy;
        animationCombo = combo;
        swordAnimation = heavy ? 0.75f : 0.30f;
    }

    private void UpdateSwordAnimation()
    {
        if (sword == null) return;

        if (swordAnimation > 0f)
        {
            float total = heavyAnimation ? 0.75f : 0.30f;
            swordAnimation -= Time.deltaTime;
            float progress = 1f - Mathf.Clamp01(swordAnimation / total);
            float swing = Mathf.Sin(progress * Mathf.PI);
            float angle = heavyAnimation ? 145f : 75f + animationCombo * 10f;
            sword.localRotation = Quaternion.Euler(20f + swing * angle, 0f, -25f);
        }
        else
        {
            sword.localRotation = Quaternion.Euler(20f, 0f, -25f);
        }
    }

    private void UpdateCamera()
    {
        Quaternion rotation = Quaternion.Euler(cameraPitch, transform.eulerAngles.y, 0f);
        playerCamera.transform.position = transform.position + rotation * new Vector3(0f, 3.8f, -7f);
        playerCamera.transform.LookAt(transform.position + new Vector3(0f, 1.4f, 0f));
    }

    private void HandleBottleSelection()
    {
        if (Input.GetKeyDown(KeyCode.LeftBracket)) core.Bottles.CycleSelection(-1);
        if (Input.GetKeyDown(KeyCode.RightBracket)) core.Bottles.CycleSelection(1);
        for (int i = 0; i < RanchBottleSystem.TierCount; i++)
        {
            KeyCode key = (KeyCode)((int)KeyCode.Alpha1 + i);
            if (Input.GetKeyDown(key)) core.Bottles.SelectTier(i);
        }
    }

    private void HandleInteraction()
    {
        RanchInteractable nearest = FindNearestInteractable();
        IsExtracting = false;
        if (nearest == null) { CurrentPrompt = ""; return; }
        CurrentPrompt = nearest.Prompt;
        if (nearest.UsesHeldInteraction)
        {
            IsExtracting = Input.GetKey(KeyCode.E);
            if (IsExtracting) nearest.Interact(this, true, Time.deltaTime);
        }
        else if (Input.GetKeyDown(KeyCode.E)) nearest.Interact(this, false, 0f);
    }

    private RanchInteractable FindNearestInteractable()
    {
        Collider[] colliders = Physics.OverlapSphere(transform.position, InteractionRange);
        RanchInteractable nearest = null;
        float nearestDistance = float.MaxValue;
        foreach (Collider collider in colliders)
        {
            RanchInteractable interactable = collider.GetComponentInParent<RanchInteractable>();
            if (interactable == null) continue;
            float distance = Vector3.Distance(transform.position, interactable.transform.position);
            if (distance < nearestDistance) { nearest = interactable; nearestDistance = distance; }
        }
        return nearest;
    }

    private void UpdateTools()
    {
        if (sword != null) sword.gameObject.SetActive(!IsExtracting);
        if (extractor != null) extractor.gameObject.SetActive(IsExtracting);
    }

    public void Teleport(Vector3 position, float rotationY)
    {
        if (controller != null) controller.enabled = false;
        transform.position = position;
        transform.rotation = Quaternion.Euler(0f, rotationY, 0f);
        if (controller != null) controller.enabled = true;
    }

    private void HandleCursor()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        if (Input.GetMouseButtonDown(0) && Cursor.visible) LockCursor();
    }

    private static void LockCursor()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }
}
