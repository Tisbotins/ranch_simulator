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
        if (core == null || controller == null || playerCamera == null || core.GameWon || core.Health.IsDead || core.Shop.IsOpen) return;
        HandleCursor();
        HandleMovement();
        UpdateCamera();
        HandleBottleSelection();
        HandleInteraction();
        HandleSword();
        UpdateTools();
    }

    private void HandleMovement()
    {
        transform.Rotate(Vector3.up * Input.GetAxis("Mouse X") * MouseSensitivity);
        cameraPitch = Mathf.Clamp(cameraPitch - Input.GetAxis("Mouse Y") * MouseSensitivity, -5f, 55f);

        Vector3 move = transform.right * Input.GetAxis("Horizontal") + transform.forward * Input.GetAxis("Vertical");
        if (move.magnitude > 1f) move.Normalize();
        if (controller.isGrounded && yVelocity < 0f) yVelocity = -2f;
        yVelocity += -18f * Time.deltaTime;
        Vector3 velocity = move * MoveSpeed;
        velocity.y = yVelocity;
        controller.Move(velocity * Time.deltaTime);
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

    private void HandleSword()
    {
        if (swordAnimation > 0f)
        {
            swordAnimation -= Time.deltaTime;
            if (sword != null)
            {
                float swing = Mathf.Sin((1f - swordAnimation / 0.25f) * Mathf.PI);
                sword.localRotation = Quaternion.Euler(20f + swing * 80f, 0f, -25f);
            }
        }
        else if (sword != null) sword.localRotation = Quaternion.Euler(20f, 0f, -25f);

        if (Input.GetKeyDown(KeyCode.Space) && !IsExtracting)
        {
            swordAnimation = 0.25f;
            core.Waves.AttackNearestEnemy(core.Shop.CurrentSwordDamage, 3.5f, true);
        }
    }

    private void UpdateTools()
    {
        if (sword != null) sword.gameObject.SetActive(!IsExtracting);
        if (extractor != null) extractor.gameObject.SetActive(IsExtracting);
    }

    private void HandleCursor()
    {
        if (Input.GetKeyDown(KeyCode.Escape)) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
        if (Input.GetMouseButtonDown(0)) LockCursor();
    }

    private static void LockCursor()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }
}
