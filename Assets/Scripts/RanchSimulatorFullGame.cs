#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// RANCH SIMULATOR: ULTIMATE UPDATE
/// Drop this file into Assets/Scripts in a Unity 3D project and press Play.
/// The bootstrap below creates the game automatically, so no scene setup is required.
///
/// Works with Unity's old Input Manager, new Input System, or Both.
/// </summary>
public class RanchSimulatorFullGame : MonoBehaviour
{
    public static RanchSimulatorFullGame Instance { get; private set; }

    // ============================================================
    // RESOURCES AND PROGRESSION
    // ============================================================

    private double ranch;
    private double money;
    private long bottles;
    private long bottlesSold;

    private readonly string[] bottleNames =
    {
        "Cracked Bottle", "Tiny Bottle", "Standard Bottle", "Family Bottle",
        "Mega Jug", "Industrial Drum", "Ranch Tanker", "Ranchenator Vessel"
    };

    private readonly int[] bottleCaps = { 1, 2, 5, 12, 30, 75, 200, 500 };
    private int bottleTier;

    private readonly string[] ranchTypeNames =
    {
        "Classic Ranch", "Premium Ranch", "Sparkling Ranch",
        "Dark Ranch", "Quantum Ranch", "Divine Ranch"
    };

    private readonly double[] ranchValueMultipliers = { 1d, 2d, 5d, 20d, 100d, 1000d };
    private readonly double[] laboratoryResearchCosts = { 0d, 0d, 500000d, 5000000d, 50000000d, 500000000d };
    private int ranchTypeTier;

    private double ranchPerSecond = 1.5d;
    private double extractionMultiplier = 1d;

    // ============================================================
    // PLAYER, CAMERA, HEALTH, AND SWORD
    // ============================================================

    private GameObject player;
    private CharacterController controller;
    private Camera mainCamera;

    private float moveSpeed = 7f;
    private float mouseSensitivity = 2.1f;
    private float cameraPitch = 17f;
    private float verticalVelocity;
    private const float Gravity = -20f;

    private float playerMaxHealth = 150f;
    private float playerHealth = 150f;
    private float invulnerabilityTimer;

    private readonly string[] swordNames =
    {
        "Wooden Ranch Sword", "Iron Ranch Sword", "Steel Ranch Sword",
        "Diamond Ranch Sword", "Quantum Ranch Sword", "CJ Slayer"
    };

    private readonly float[] swordDamages = { 10f, 25f, 60f, 150f, 500f, 2500f };
    private readonly double[] swordUpgradeCosts = { 0d, 250d, 1250d, 6000d, 30000d, 150000d };
    private int swordTier;

    private GameObject ranchSwordPivot;
    private Renderer swordBladeRenderer;
    private Quaternion swordRestRotation;
    private Quaternion swordAttackRotation;
    private float swordCooldownTimer;
    private float swordAnimationTimer;
    private const float SwordCooldown = 0.42f;
    private const float SwordAnimationLength = 0.27f;
    private const float SwordRange = 3.6f;

    // ============================================================
    // DREW
    // ============================================================

    private GameObject drew;
    private bool drewUnlocked;
    private int drewLevel;
    private double drewUpgradeBaseCost = 100d;
    private float drewTimer;
    private float drewWorkInterval = 6f;
    private bool drewMovingToTree = true;
    private readonly List<GameObject> miniDrews = new List<GameObject>();
    private float miniDrewAttackTimer;

    // ============================================================
    // BUILDINGS
    // ============================================================

    private bool factoryBuilt;
    private bool warehouseBuilt;
    private bool laboratoryBuilt;
    private bool fortressBuilt;
    private bool citadelBuilt;
    private int nextBuildingIndex;

    private readonly string[] buildingNames =
    {
        "Ranch Factory", "Ranch Warehouse", "Ranch Laboratory",
        "Ranch Fortress", "Ranch Citadel"
    };

    private readonly double[] buildingCosts =
    {
        2500d, 15000d, 100000d, 1000000d, 25000000d
    };

    private float factoryTimer;
    private float fortressTimer;

    // ============================================================
    // WAVES AND ENEMIES
    // ============================================================

    private readonly List<RanchEnemy> activeEnemies = new List<RanchEnemy>();
    private float waveTimer;
    private const float WaveInterval = 180f; // Every three minutes.
    private int waveNumber;
    private int enemiesDefeated;
    private int baseEnemyCount = 3;

    // Exact requested wave scaling.
    private const float HealthScalePerWave = 1.5f;
    private const float DamageScalePerWave = 1.4f;
    private const float SpeedScalePerWave = 1.1f;

    // ============================================================
    // CJ FINAL BATTLE
    // ============================================================

    private bool finalBattleActive;
    private int finalBattlePhase;
    private bool cjDefeated;

    // ============================================================
    // WORLD OBJECTS
    // ============================================================

    private GameObject ranchTree;
    private GameObject bottleStation;
    private GameObject sellStation;
    private GameObject bottleUpgradeStation;
    private GameObject drewStation;
    private GameObject swordStation;
    private GameObject buildYard;
    private GameObject laboratoryStation;
    private GameObject cjGate;

    private readonly List<GameObject> spawnedObjects = new List<GameObject>();

    private Material greenMaterial;
    private Material brownMaterial;
    private Material blueMaterial;
    private Material yellowMaterial;
    private Material redMaterial;
    private Material whiteMaterial;
    private Material blackMaterial;
    private Material ranchMaterial;
    private Material purpleMaterial;
    private Material cyanMaterial;
    private Material orangeMaterial;
    private Material grayMaterial;
    private Material darkRedMaterial;

    // ============================================================
    // UI AND INTERACTION
    // ============================================================

    private const float InteractionRange = 3.5f;
    private string currentPrompt = string.Empty;
    private string lastEvent = "Extract Ranch. Build an empire. Overthrow CJ.";
    private float eventTimer;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void Start()
    {
        Application.targetFrameRate = 60;
        QualitySettings.vSyncCount = 1;

        CreateMaterials();
        BuildWorld();
        CreatePlayer();
        CreateRanchSword();
        CreateDrew();
        LockCursor();

        waveTimer = WaveInterval;
        LogEvent("Welcome to McMinnville Ranch Country. CJ controls the Ranch market.");
    }

    private void Update()
    {
        if (cjDefeated)
        {
            if (RanchInput.Down(RanchAction.Restart))
            {
                SceneManager.LoadScene(SceneManager.GetActiveScene().name);
            }
            return;
        }

        HandleCursor();
        HandleMovement();
        UpdateCamera();
        HandleCombat();
        UpdateInteractions();
        UpdateDrew();
        UpdateBuildings();
        UpdateEnemyWaves();
        UpdateFinalBattle();
        UpdateTimers();
    }

    // ============================================================
    // WORLD CREATION
    // ============================================================

    private void CreateMaterials()
    {
        greenMaterial = MakeMaterial(new Color(0.22f, 0.62f, 0.22f));
        brownMaterial = MakeMaterial(new Color(0.33f, 0.18f, 0.07f));
        blueMaterial = MakeMaterial(new Color(0.18f, 0.43f, 0.92f));
        yellowMaterial = MakeMaterial(new Color(0.95f, 0.74f, 0.18f));
        redMaterial = MakeMaterial(new Color(0.85f, 0.18f, 0.12f));
        whiteMaterial = MakeMaterial(new Color(0.92f, 0.92f, 0.88f));
        blackMaterial = MakeMaterial(new Color(0.04f, 0.04f, 0.04f));
        ranchMaterial = MakeMaterial(new Color(1f, 0.91f, 0.65f));
        purpleMaterial = MakeMaterial(new Color(0.48f, 0.10f, 0.68f));
        cyanMaterial = MakeMaterial(new Color(0.10f, 0.86f, 0.95f));
        orangeMaterial = MakeMaterial(new Color(1f, 0.36f, 0.05f));
        grayMaterial = MakeMaterial(new Color(0.35f, 0.38f, 0.42f));
        darkRedMaterial = MakeMaterial(new Color(0.35f, 0.02f, 0.02f));
    }

    private Material MakeMaterial(Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        Material material = new Material(shader);
        material.color = color;
        return material;
    }

    private void BuildWorld()
    {
        GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "McMinnville Oregon Ranch Grounds";
        ground.transform.localScale = new Vector3(10f, 1f, 10f);
        ground.GetComponent<Renderer>().material = greenMaterial;
        spawnedObjects.Add(ground);

        ranchTree = new GameObject("Ranch Tree");
        ranchTree.transform.position = new Vector3(0f, 0f, 8f);

        GameObject trunk = CreatePrimitive(PrimitiveType.Cylinder, "Ranch Tree Trunk",
            ranchTree.transform.position + new Vector3(0f, 1.7f, 0f),
            new Vector3(0.9f, 2f, 0.9f), brownMaterial, ranchTree.transform);

        GameObject crown = CreatePrimitive(PrimitiveType.Sphere, "Ranch Tree Crown",
            ranchTree.transform.position + new Vector3(0f, 4.4f, 0f),
            new Vector3(3.4f, 2.5f, 3.4f), ranchMaterial, ranchTree.transform);

        GameObject tap = CreatePrimitive(PrimitiveType.Cube, "Ranch Extraction Tap",
            ranchTree.transform.position + new Vector3(0f, 2.1f, -0.9f),
            new Vector3(0.35f, 0.25f, 1.2f), blueMaterial, ranchTree.transform);

        RemoveCollider(tap);
        CreateLabel("RANCH TREE\nHold E to extract", ranchTree.transform.position + new Vector3(0f, 6.2f, 0f));

        bottleStation = CreateStation("Bottle Station", new Vector3(-8f, 0.5f, 0f), blueMaterial,
            "BOTTLE STATION\nB = one | V = all");
        sellStation = CreateStation("Sell Station", new Vector3(8f, 0.5f, 0f), yellowMaterial,
            "SELL STATION\nF = sell | Shift+F = all");
        bottleUpgradeStation = CreateStation("Bottle Upgrade Station", new Vector3(0f, 0.5f, -8f), redMaterial,
            "BOTTLE UPGRADES\nPress U");
        drewStation = CreateStation("Drew Station", new Vector3(-8f, 0.5f, -8f), whiteMaterial,
            "DREW STATION\nPress G");
        swordStation = CreateStation("Sword Upgrade Station", new Vector3(8f, 0.5f, -8f), grayMaterial,
            "SWORD UPGRADES\nPress K");
        buildYard = CreateStation("Building Yard", new Vector3(-14f, 0.5f, 8f), orangeMaterial,
            "BUILDING YARD\nPress H");
        laboratoryStation = CreateStation("Laboratory Research Pad", new Vector3(14f, 0.5f, 8f), purpleMaterial,
            "RANCH RESEARCH\nPress J");
        cjGate = CreateStation("CJ Gate", new Vector3(0f, 0.5f, 18f), blackMaterial,
            "CJ GATE\nCitadel required | C");

        for (int i = 0; i < 18; i++)
        {
            float angle = i * Mathf.PI * 2f / 18f;
            float radius = 34f + (i % 3) * 3f;
            CreateDecorativeTree(new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius));
        }

        CreateBarrel(new Vector3(-3f, 0.7f, 2f));
        CreateBarrel(new Vector3(-4.3f, 0.7f, 2.8f));
        CreateBarrel(new Vector3(3.7f, 0.7f, -2.7f));
    }

    private GameObject CreatePrimitive(PrimitiveType type, string objectName, Vector3 position,
        Vector3 scale, Material material, Transform parent = null)
    {
        GameObject obj = GameObject.CreatePrimitive(type);
        obj.name = objectName;
        obj.transform.position = position;
        obj.transform.localScale = scale;
        if (parent != null) obj.transform.SetParent(parent, true);
        Renderer renderer = obj.GetComponent<Renderer>();
        if (renderer != null) renderer.material = material;
        spawnedObjects.Add(obj);
        return obj;
    }

    private GameObject CreateStation(string stationName, Vector3 position, Material material, string label)
    {
        GameObject station = CreatePrimitive(PrimitiveType.Cube, stationName, position,
            new Vector3(2.5f, 1f, 2.5f), material);
        CreateLabel(label, position + new Vector3(0f, 2.5f, 0f));
        return station;
    }

    private void CreateDecorativeTree(Vector3 position)
    {
        GameObject root = new GameObject("Background Tree");
        root.transform.position = position;
        spawnedObjects.Add(root);

        CreatePrimitive(PrimitiveType.Cylinder, "Trunk", position + new Vector3(0f, 1.2f, 0f),
            new Vector3(0.5f, 1.4f, 0.5f), brownMaterial, root.transform);
        CreatePrimitive(PrimitiveType.Sphere, "Leaves", position + new Vector3(0f, 3.2f, 0f),
            new Vector3(2.4f, 2.4f, 2.4f), greenMaterial, root.transform);
    }

    private void CreateBarrel(Vector3 position)
    {
        CreatePrimitive(PrimitiveType.Cylinder, "Ranch Barrel", position,
            new Vector3(0.8f, 0.7f, 0.8f), ranchMaterial);
    }

    private void CreateLabel(string text, Vector3 position)
    {
        GameObject labelObject = new GameObject("World Label");
        labelObject.transform.position = position;
        TextMesh textMesh = labelObject.AddComponent<TextMesh>();
        textMesh.text = text;
        textMesh.fontSize = 42;
        textMesh.characterSize = 0.115f;
        textMesh.anchor = TextAnchor.MiddleCenter;
        textMesh.alignment = TextAlignment.Center;
        textMesh.color = Color.black;
        labelObject.AddComponent<LabelBillboard>();
        spawnedObjects.Add(labelObject);
    }

    private void CreatePlayer()
    {
        player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        player.name = "Player";
        player.transform.position = new Vector3(0f, 1.2f, -3f);
        player.GetComponent<Renderer>().material = blueMaterial;

        CapsuleCollider capsuleCollider = player.GetComponent<CapsuleCollider>();
        if (capsuleCollider != null) Destroy(capsuleCollider);

        controller = player.AddComponent<CharacterController>();
        controller.height = 2f;
        controller.radius = 0.45f;
        controller.center = Vector3.zero;

        mainCamera = Camera.main;
        if (mainCamera == null)
        {
            GameObject cameraObject = new GameObject("Main Camera");
            mainCamera = cameraObject.AddComponent<Camera>();
            cameraObject.tag = "MainCamera";
        }
    }

    private void CreateRanchSword()
    {
        ranchSwordPivot = new GameObject("Ranch Sword Pivot");
        ranchSwordPivot.transform.SetParent(player.transform);
        ranchSwordPivot.transform.localPosition = new Vector3(0.72f, 0.45f, 0.55f);

        swordRestRotation = Quaternion.Euler(12f, 0f, -35f);
        swordAttackRotation = Quaternion.Euler(12f, 0f, 78f);
        ranchSwordPivot.transform.localRotation = swordRestRotation;

        GameObject handle = CreatePrimitive(PrimitiveType.Cylinder, "Sword Handle", Vector3.zero,
            new Vector3(0.12f, 0.35f, 0.12f), brownMaterial, ranchSwordPivot.transform);
        handle.transform.localPosition = new Vector3(0f, 0.25f, 0f);
        RemoveCollider(handle);

        GameObject guard = CreatePrimitive(PrimitiveType.Cube, "Sword Guard", Vector3.zero,
            new Vector3(0.75f, 0.12f, 0.16f), yellowMaterial, ranchSwordPivot.transform);
        guard.transform.localPosition = new Vector3(0f, 0.65f, 0f);
        RemoveCollider(guard);

        GameObject blade = CreatePrimitive(PrimitiveType.Cube, "Sword Blade", Vector3.zero,
            new Vector3(0.24f, 1.7f, 0.10f), brownMaterial, ranchSwordPivot.transform);
        blade.transform.localPosition = new Vector3(0f, 1.55f, 0f);
        swordBladeRenderer = blade.GetComponent<Renderer>();
        RemoveCollider(blade);
        UpdateSwordAppearance();
    }

    private void CreateDrew()
    {
        drew = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        drew.name = "Drew";
        drew.transform.position = new Vector3(-6f, 1.2f, -6f);
        drew.transform.localScale = new Vector3(0.85f, 0.85f, 0.85f);
        drew.GetComponent<Renderer>().material = whiteMaterial;
        RemoveCollider(drew);

        GameObject hat = CreatePrimitive(PrimitiveType.Cylinder, "Drew Hat", Vector3.zero,
            new Vector3(0.85f, 0.12f, 0.85f), yellowMaterial, drew.transform);
        hat.transform.localPosition = new Vector3(0f, 1.25f, 0f);
        RemoveCollider(hat);

        drew.SetActive(false);
    }

    private void RemoveCollider(GameObject obj)
    {
        Collider collider = obj.GetComponent<Collider>();
        if (collider != null) Destroy(collider);
    }

    // ============================================================
    // PLAYER MOVEMENT AND CAMERA
    // ============================================================

    private void HandleMovement()
    {
        if (player == null || controller == null) return;

        float mouseX = RanchInput.MouseX * mouseSensitivity;
        float mouseY = RanchInput.MouseY * mouseSensitivity;

        player.transform.Rotate(Vector3.up * mouseX);
        cameraPitch = Mathf.Clamp(cameraPitch - mouseY, -8f, 58f);

        Vector3 move = player.transform.right * RanchInput.MoveX + player.transform.forward * RanchInput.MoveY;
        if (move.sqrMagnitude > 1f) move.Normalize();

        if (controller.isGrounded && verticalVelocity < 0f) verticalVelocity = -2f;
        verticalVelocity += Gravity * Time.deltaTime;

        Vector3 velocity = move * moveSpeed;
        velocity.y = verticalVelocity;
        controller.Move(velocity * Time.deltaTime);
    }

    private void UpdateCamera()
    {
        if (mainCamera == null || player == null) return;

        Quaternion rotation = Quaternion.Euler(cameraPitch, player.transform.eulerAngles.y, 0f);
        Vector3 offset = rotation * new Vector3(0f, 4.2f, -8f);
        mainCamera.transform.position = player.transform.position + offset;
        mainCamera.transform.LookAt(player.transform.position + new Vector3(0f, 1.4f, 0f));
    }

    private void HandleCursor()
    {
        if (RanchInput.Down(RanchAction.Escape))
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        if (RanchInput.AttackDown)
        {
            LockCursor();
        }
    }

    private void LockCursor()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    // ============================================================
    // COMBAT
    // ============================================================

    private void HandleCombat()
    {
        swordCooldownTimer = Mathf.Max(0f, swordCooldownTimer - Time.deltaTime);
        invulnerabilityTimer = Mathf.Max(0f, invulnerabilityTimer - Time.deltaTime);

        if (swordAnimationTimer > 0f)
        {
            swordAnimationTimer -= Time.deltaTime;
            float progress = 1f - Mathf.Clamp01(swordAnimationTimer / SwordAnimationLength);
            float swing = Mathf.Sin(progress * Mathf.PI);
            ranchSwordPivot.transform.localRotation = Quaternion.Slerp(swordRestRotation, swordAttackRotation, swing);
        }
        else if (ranchSwordPivot != null)
        {
            ranchSwordPivot.transform.localRotation = swordRestRotation;
        }

        if (Cursor.lockState == CursorLockMode.Locked && RanchInput.AttackDown && swordCooldownTimer <= 0f)
        {
            SwingSword();
        }
    }

    private void SwingSword()
    {
        swordCooldownTimer = SwordCooldown;
        swordAnimationTimer = SwordAnimationLength;
        float damage = swordDamages[swordTier];
        int hitCount = 0;

        for (int i = activeEnemies.Count - 1; i >= 0; i--)
        {
            RanchEnemy enemy = activeEnemies[i];
            if (enemy == null)
            {
                activeEnemies.RemoveAt(i);
                continue;
            }

            Vector3 direction = enemy.transform.position - player.transform.position;
            direction.y = 0f;
            if (direction.magnitude > SwordRange) continue;

            float facing = Vector3.Dot(player.transform.forward, direction.normalized);
            if (facing >= -0.12f)
            {
                enemy.TakeDamage(damage);
                hitCount++;
            }
        }

        if (hitCount > 0)
        {
            LogEvent(swordNames[swordTier] + " hit " + hitCount + " enemy(s) for " + damage.ToString("F0") + " damage.");
        }
    }

    public Transform GetPlayerTransform()
    {
        return player != null ? player.transform : null;
    }

    public Vector3 GetHarvesterTargetPosition()
    {
        return warehouseBuilt ? new Vector3(-18f, 0f, -5f) : bottleStation.transform.position;
    }

    public void DamagePlayer(float rawDamage, string attackerName)
    {
        if (cjDefeated || invulnerabilityTimer > 0f || playerHealth <= 0f) return;

        float reduction = 1f;
        if (fortressBuilt) reduction *= 0.55f;
        if (citadelBuilt) reduction *= 0.75f;
        float damage = Mathf.Max(1f, rawDamage * reduction);

        playerHealth = Mathf.Max(0f, playerHealth - damage);
        invulnerabilityTimer = 0.18f;
        LogEvent(attackerName + " hit you for " + damage.ToString("F0") + ".");

        if (playerHealth <= 0f) RespawnPlayer();
    }

    private void RespawnPlayer()
    {
        playerHealth = playerMaxHealth;
        money *= 0.80d;
        ranch *= 0.50d;

        if (controller != null) controller.enabled = false;
        player.transform.position = new Vector3(0f, 1.2f, -3f);
        if (controller != null) controller.enabled = true;

        ClearAllEnemies();

        if (finalBattleActive)
        {
            finalBattleActive = false;
            finalBattlePhase = 0;
            LogEvent("CJ defeated you. The Citadel survives, but the final battle must be restarted.");
        }
        else
        {
            waveTimer = WaveInterval;
            LogEvent("You respawned. You kept 80% of your money and half your raw Ranch.");
        }
    }

    // ============================================================
    // INTERACTIONS
    // ============================================================

    private void UpdateInteractions()
    {
        currentPrompt = string.Empty;

        if (Near(ranchTree))
        {
            currentPrompt = "Hold E: extract " + ranchTypeNames[ranchTypeTier];
            if (RanchInput.Held(RanchAction.Extract)) ExtractRanch(Time.deltaTime);
        }

        if (Near(bottleStation))
        {
            currentPrompt = "B: bottle one | V: bottle all Ranch";
            if (RanchInput.Down(RanchAction.BottleOne)) BottleOne();
            if (RanchInput.Down(RanchAction.BottleAll)) BottleAll();
        }

        if (Near(sellStation))
        {
            currentPrompt = "F: sell one | Shift+F: sell every bottle";
            if (RanchInput.Down(RanchAction.Sell))
            {
                if (RanchInput.ShiftHeld) SellAllBottles();
                else SellBottles(1);
            }
        }

        if (Near(bottleUpgradeStation))
        {
            currentPrompt = "U: upgrade bottle size";
            if (RanchInput.Down(RanchAction.UpgradeBottle)) UpgradeBottle();
        }

        if (Near(drewStation))
        {
            currentPrompt = "G: hire or upgrade Drew";
            if (RanchInput.Down(RanchAction.UpgradeDrew)) UpgradeDrew();
        }

        if (Near(swordStation))
        {
            currentPrompt = "K: upgrade Ranch Sword";
            if (RanchInput.Down(RanchAction.UpgradeSword)) UpgradeSword();
        }

        if (Near(buildYard))
        {
            currentPrompt = "H: construct the next endgame building";
            if (RanchInput.Down(RanchAction.Build)) BuyNextBuilding();
        }

        if (Near(laboratoryStation))
        {
            currentPrompt = laboratoryBuilt
                ? "J: research a stronger Ranch type"
                : "Build the Ranch Laboratory first";
            if (RanchInput.Down(RanchAction.Research)) ResearchRanch();
        }

        if (Near(cjGate))
        {
            currentPrompt = citadelBuilt
                ? "C: begin CJ's three-phase final battle"
                : "CJ Gate locked: build the Ranch Citadel";
            if (RanchInput.Down(RanchAction.ChallengeCJ)) StartFinalBattle();
        }
    }

    private bool Near(GameObject obj)
    {
        return obj != null && player != null &&
               Vector3.Distance(player.transform.position, obj.transform.position) <= InteractionRange;
    }

    private void ExtractRanch(float deltaTime)
    {
        double amount = ranchPerSecond * extractionMultiplier * deltaTime;
        AddRanch(amount);
    }

    private void BottleOne()
    {
        long made = BottleQuantity(1);
        if (made == 0)
        {
            LogEvent("Not enough Ranch or storage space for one " + bottleNames[bottleTier] + ".");
        }
        else
        {
            LogEvent("Filled one " + bottleNames[bottleTier] + ".");
        }
    }

    public void BottleAll()
    {
        long made = BottleQuantity(long.MaxValue);
        if (made <= 0)
        {
            LogEvent("No Ranch could be bottled.");
        }
        else
        {
            LogEvent("Bottled all available Ranch: " + Compact(made) + " bottle(s).");
        }
    }

    private long BottleQuantity(long requestedMaximum)
    {
        int capacity = bottleCaps[bottleTier];
        double availableDouble = Math.Floor(ranch / capacity);
        if (availableDouble <= 0d) return 0;

        long available = availableDouble >= long.MaxValue ? long.MaxValue : (long)availableDouble;
        long storageRoom = GetBottleStorageCapacity() - bottles;
        if (storageRoom <= 0) return 0;

        long count = Math.Min(available, Math.Min(requestedMaximum, storageRoom));
        if (count <= 0) return 0;

        ranch -= count * (double)capacity;
        bottles += count;
        return count;
    }

    private void SellBottles(long count)
    {
        if (bottles <= 0)
        {
            LogEvent("You have no bottled Ranch to sell.");
            return;
        }

        long sold = Math.Min(count, bottles);
        bottles -= sold;
        bottlesSold = SafeAddLong(bottlesSold, sold);

        double baseValue = bottleCaps[bottleTier] * 5d;
        double bottleTierBonus = 1d + bottleTier * 0.22d;
        double value = sold * baseValue * bottleTierBonus * ranchValueMultipliers[ranchTypeTier];
        money += value;

        LogEvent("Sold " + Compact(sold) + " bottle(s) of " + ranchTypeNames[ranchTypeTier] +
                 " for $" + Compact(value) + ".");
    }

    private void SellAllBottles()
    {
        SellBottles(bottles);
    }

    private void UpgradeBottle()
    {
        if (bottleTier >= bottleNames.Length - 1)
        {
            LogEvent("Bottle size is already maxed: Ranchenator Vessel.");
            return;
        }

        double cost = GetBottleUpgradeCost();
        if (money < cost)
        {
            LogEvent("Need $" + Compact(cost) + " for the next bottle upgrade.");
            return;
        }

        money -= cost;
        bottleTier++;
        LogEvent("Bottle upgraded to " + bottleNames[bottleTier] + " (capacity " + bottleCaps[bottleTier] + ").");
    }

    private double GetBottleUpgradeCost()
    {
        return 80d * (bottleTier + 1d) * (bottleTier + 1d) * Math.Pow(2d, bottleTier);
    }

    private void UpgradeSword()
    {
        if (swordTier >= swordNames.Length - 1)
        {
            LogEvent("You already wield the CJ Slayer.");
            return;
        }

        double cost = swordUpgradeCosts[swordTier + 1];
        if (money < cost)
        {
            LogEvent("Need $" + Compact(cost) + " for " + swordNames[swordTier + 1] + ".");
            return;
        }

        money -= cost;
        swordTier++;
        UpdateSwordAppearance();
        LogEvent("Sword upgraded to " + swordNames[swordTier] + " — " + swordDamages[swordTier].ToString("F0") + " damage.");
    }

    private void UpdateSwordAppearance()
    {
        if (swordBladeRenderer == null) return;

        Material material = brownMaterial;
        if (swordTier == 1) material = grayMaterial;
        else if (swordTier == 2) material = whiteMaterial;
        else if (swordTier == 3) material = cyanMaterial;
        else if (swordTier == 4) material = purpleMaterial;
        else if (swordTier == 5) material = redMaterial;
        swordBladeRenderer.material = material;
    }

    // ============================================================
    // DREW SYSTEM
    // ============================================================

    private void UpgradeDrew()
    {
        if (!drewUnlocked)
        {
            double hireCost = 100d;
            if (money < hireCost)
            {
                LogEvent("Need $100 to hire Drew.");
                return;
            }

            money -= hireCost;
            drewUnlocked = true;
            drewLevel = 1;
            drew.SetActive(true);
            LogEvent("Drew hired. Level 1 Drew can perform 2 units of work per cycle.");
            return;
        }

        if (drewLevel >= 50)
        {
            LogEvent("Drew is Level 50: ASCENDED DREW.");
            return;
        }

        double cost = GetDrewUpgradeCost();
        if (money < cost)
        {
            LogEvent("Need $" + Compact(cost) + " to upgrade Drew.");
            return;
        }

        money -= cost;
        drewLevel++;
        drewWorkInterval = Mathf.Max(1.25f, 6f - drewLevel * 0.10f);

        if (drewLevel >= 20) EnsureMiniDrews();

        string milestone = GetDrewMilestoneMessage(drewLevel);
        LogEvent("Drew reached Level " + drewLevel + ". Work = 2^" + drewLevel + ". " + milestone);
    }

    private double GetDrewUpgradeCost()
    {
        return drewUpgradeBaseCost * Math.Pow(1.58d, drewLevel);
    }

    private string GetDrewMilestoneMessage(int level)
    {
        if (level == 5) return "Automatic bottling unlocked.";
        if (level == 10) return "Automatic selling unlocked.";
        if (level == 15) return "Automatic bottle upgrades unlocked.";
        if (level == 20) return "Mini-Drews unlocked.";
        if (level == 25) return "Drew now produces Ranch by himself.";
        if (level == 50) return "ASCENDED DREW produces absurd amounts of Ranch.";
        return string.Empty;
    }

    private void UpdateDrew()
    {
        if (!drewUnlocked || drew == null) return;

        drewTimer += Time.deltaTime;
        if (drewTimer >= drewWorkInterval)
        {
            drewTimer = 0f;
            PerformDrewWork();
        }

        Vector3 target = drewMovingToTree ? ranchTree.transform.position : bottleStation.transform.position;
        target.y = drew.transform.position.y;
        drew.transform.position = Vector3.MoveTowards(drew.transform.position, target,
            Time.deltaTime * (2f + drewLevel * 0.025f));

        Vector3 direction = target - drew.transform.position;
        if (direction.sqrMagnitude > 0.01f) drew.transform.rotation = Quaternion.LookRotation(direction);
        if (Vector3.Distance(drew.transform.position, target) < 0.35f) drewMovingToTree = !drewMovingToTree;

        UpdateMiniDrews();
    }

    private void PerformDrewWork()
    {
        double workAmount = Math.Pow(2d, Math.Min(drewLevel, 50));
        if (drewLevel >= 50) workAmount *= 1000d;

        // Drew extracts enough Ranch for the requested number of bottles.
        double extracted = workAmount * bottleCaps[bottleTier];
        AddRanch(extracted);

        long requestedBottles = workAmount >= long.MaxValue ? long.MaxValue : (long)Math.Floor(workAmount);

        if (drewLevel >= 5)
        {
            BottleQuantity(requestedBottles);
        }

        if (drewLevel >= 10 && bottles > 0)
        {
            SellAllBottles();
        }

        if (drewLevel >= 15)
        {
            while (bottleTier < bottleNames.Length - 1 && money >= GetBottleUpgradeCost())
            {
                money -= GetBottleUpgradeCost();
                bottleTier++;
            }
        }

        if (drewLevel >= 25)
        {
            AddRanch(workAmount * bottleCaps[bottleTier] * 10d);
        }

        if (drewLevel >= 50)
        {
            ranchTypeTier = Math.Max(ranchTypeTier, 5);
            AddRanch(workAmount * bottleCaps[bottleTier] * 1000d);
        }

        LogEvent("Drew completed " + Compact(workAmount) + " work. Level " + drewLevel + " Drew is accelerating the empire.");
    }

    private void EnsureMiniDrews()
    {
        int desired = Mathf.Clamp(1 + (drewLevel - 20) / 5, 1, 8);
        while (miniDrews.Count < desired)
        {
            int index = miniDrews.Count;
            GameObject mini = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            mini.name = "Mini-Drew " + (index + 1);
            mini.transform.localScale = Vector3.one * 0.38f;
            mini.GetComponent<Renderer>().material = cyanMaterial;
            RemoveCollider(mini);
            miniDrews.Add(mini);
        }
    }

    private void UpdateMiniDrews()
    {
        if (drewLevel < 20) return;
        EnsureMiniDrews();

        for (int i = 0; i < miniDrews.Count; i++)
        {
            GameObject mini = miniDrews[i];
            if (mini == null) continue;
            float angle = Time.time * 1.2f + i * Mathf.PI * 2f / miniDrews.Count;
            Vector3 orbit = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * (2.2f + i * 0.18f);
            mini.transform.position = drew.transform.position + orbit + Vector3.up * 0.3f;
        }

        miniDrewAttackTimer += Time.deltaTime;
        if (miniDrewAttackTimer < 1f) return;
        miniDrewAttackTimer = 0f;

        float damage = swordDamages[swordTier] * (0.25f + miniDrews.Count * 0.08f);
        for (int i = 0; i < miniDrews.Count; i++)
        {
            RanchEnemy enemy = FindClosestEnemy(miniDrews[i].transform.position);
            if (enemy != null) enemy.TakeDamage(damage);
        }
    }

    // ============================================================
    // BUILDINGS AND RANCH TYPES
    // ============================================================

    private void BuyNextBuilding()
    {
        if (nextBuildingIndex >= buildingNames.Length)
        {
            LogEvent("Every endgame building has been constructed.");
            return;
        }

        double cost = buildingCosts[nextBuildingIndex];
        if (money < cost)
        {
            LogEvent("Need $" + Compact(cost) + " to build the " + buildingNames[nextBuildingIndex] + ".");
            return;
        }

        money -= cost;
        string built = buildingNames[nextBuildingIndex];

        if (nextBuildingIndex == 0)
        {
            factoryBuilt = true;
            CreateBuildingModel("Ranch Factory", new Vector3(-18f, 2f, 4f), blueMaterial, new Vector3(7f, 4f, 6f));
        }
        else if (nextBuildingIndex == 1)
        {
            warehouseBuilt = true;
            CreateBuildingModel("Ranch Warehouse", new Vector3(-18f, 2f, -5f), yellowMaterial, new Vector3(8f, 4f, 7f));
        }
        else if (nextBuildingIndex == 2)
        {
            laboratoryBuilt = true;
            ranchTypeTier = Math.Max(ranchTypeTier, 1);
            CreateBuildingModel("Ranch Laboratory", new Vector3(18f, 2f, 4f), purpleMaterial, new Vector3(7f, 4f, 6f));
        }
        else if (nextBuildingIndex == 3)
        {
            fortressBuilt = true;
            playerMaxHealth += 500f;
            playerHealth = playerMaxHealth;
            CreateBuildingModel("Ranch Fortress", new Vector3(18f, 3f, -6f), grayMaterial, new Vector3(9f, 6f, 8f));
        }
        else if (nextBuildingIndex == 4)
        {
            citadelBuilt = true;
            playerMaxHealth += 2500f;
            playerHealth = playerMaxHealth;
            CreateBuildingModel("Ranch Citadel", new Vector3(0f, 5f, 30f), cyanMaterial, new Vector3(14f, 10f, 12f));
        }

        nextBuildingIndex++;
        LogEvent(built + " constructed.");
    }

    private void CreateBuildingModel(string buildingName, Vector3 position, Material material, Vector3 scale)
    {
        GameObject building = CreatePrimitive(PrimitiveType.Cube, buildingName, position, scale, material);
        CreateLabel(buildingName.ToUpperInvariant(), position + new Vector3(0f, scale.y * 0.7f + 2f, 0f));

        if (buildingName == "Ranch Fortress")
        {
            for (int i = -1; i <= 1; i += 2)
            {
                CreatePrimitive(PrimitiveType.Cylinder, "Fortress Tower", position + new Vector3(i * 4f, 4f, 0f),
                    new Vector3(1.2f, 4f, 1.2f), darkRedMaterial, building.transform);
            }
        }
    }

    private void ResearchRanch()
    {
        if (!laboratoryBuilt)
        {
            LogEvent("Construct the Ranch Laboratory before researching advanced Ranch.");
            return;
        }

        if (ranchTypeTier >= ranchTypeNames.Length - 1)
        {
            LogEvent("Divine Ranch is already unlocked.");
            return;
        }

        int nextTier = ranchTypeTier + 1;
        double cost = laboratoryResearchCosts[nextTier];
        if (money < cost)
        {
            LogEvent("Need $" + Compact(cost) + " to research " + ranchTypeNames[nextTier] + ".");
            return;
        }

        money -= cost;
        ranchTypeTier = nextTier;
        LogEvent(ranchTypeNames[ranchTypeTier] + " unlocked: " + ranchValueMultipliers[ranchTypeTier] + "x sale value.");
    }

    private void UpdateBuildings()
    {
        if (factoryBuilt)
        {
            factoryTimer += Time.deltaTime;
            if (factoryTimer >= 1f)
            {
                factoryTimer = 0f;
                long capacity = 100L * (bottleTier + 1L) * (bottleTier + 1L);
                BottleQuantity(capacity);
            }
        }

        if (fortressBuilt && activeEnemies.Count > 0)
        {
            fortressTimer += Time.deltaTime;
            if (fortressTimer >= 0.75f)
            {
                fortressTimer = 0f;
                int targets = citadelBuilt ? 5 : 2;
                float damage = 500f + waveNumber * 100f + swordDamages[swordTier] * 0.4f;
                for (int i = 0; i < targets; i++)
                {
                    RanchEnemy enemy = FindClosestEnemy(new Vector3(18f, 3f, -6f));
                    if (enemy != null) enemy.TakeDamage(damage);
                }
            }
        }
    }

    private double GetRanchStorageCapacity()
    {
        return warehouseBuilt ? 1e18d : 100000d;
    }

    private long GetBottleStorageCapacity()
    {
        return warehouseBuilt ? long.MaxValue : 100000L;
    }

    private void AddRanch(double amount)
    {
        if (amount <= 0d) return;
        ranch = Math.Min(GetRanchStorageCapacity(), ranch + amount);
    }

    public double StealRanch(double requestedAmount)
    {
        double stolen = Math.Min(ranch, Math.Max(0d, requestedAmount));
        ranch -= stolen;
        return stolen;
    }

    // ============================================================
    // ENEMY WAVES
    // ============================================================

    private void UpdateEnemyWaves()
    {
        CleanEnemyList();
        if (finalBattleActive) return;

        waveTimer -= Time.deltaTime;
        if (waveTimer <= 0f)
        {
            SpawnWave();
            waveTimer = WaveInterval;
        }
    }

    private void SpawnWave()
    {
        waveNumber++;
        int enemyCount = baseEnemyCount + 2 * (waveNumber - 1);

        float healthMultiplier = Mathf.Pow(HealthScalePerWave, waveNumber - 1);
        float damageMultiplier = Mathf.Pow(DamageScalePerWave, waveNumber - 1);
        float speedMultiplier = Mathf.Pow(SpeedScalePerWave, waveNumber - 1);

        for (int i = 0; i < enemyCount; i++)
        {
            EnemyKind kind = ChooseEnemyKind(waveNumber, i);
            Vector3 spawnPosition = GetEnemySpawnPosition(i, enemyCount, 40f + waveNumber * 0.4f);
            SpawnEnemy(kind, spawnPosition, healthMultiplier, damageMultiplier, speedMultiplier, false);
        }

        if (waveNumber >= 20)
        {
            SpawnEnemy(EnemyKind.UltimateRanchBeast, GetEnemySpawnPosition(enemyCount, enemyCount + 1, 44f),
                healthMultiplier, damageMultiplier, speedMultiplier, false);
        }

        LogEvent("WAVE " + waveNumber + " ARRIVED: " + enemyCount +
                 (waveNumber >= 20 ? "+ boss" : string.Empty) + " enemies. Scaling is now brutal.");
    }

    private EnemyKind ChooseEnemyKind(int wave, int index)
    {
        if (wave >= 20 && index % 17 == 0) return EnemyKind.MiniRanchenator;
        if (wave >= 15 && index % 13 == 0) return EnemyKind.MiniRanchenator;
        if (wave >= 10 && index % 9 == 0) return EnemyKind.CJEnforcer;
        if (wave >= 7 && index % 8 == 0) return EnemyKind.RanchHarvester;
        if (wave >= 6 && index % 7 == 0) return EnemyKind.RanchGolem;
        if (wave >= 4 && index % 6 == 0) return EnemyKind.CorruptedDrew;
        if (wave >= 3 && index % 4 == 0) return EnemyKind.RanchBandit;
        return EnemyKind.RanchRaider;
    }

    private Vector3 GetEnemySpawnPosition(int index, int total, float radius)
    {
        float angle = (index / Mathf.Max(1f, total)) * Mathf.PI * 2f + UnityEngine.Random.Range(-0.12f, 0.12f);
        return new Vector3(Mathf.Cos(angle) * radius, 1.1f, Mathf.Sin(angle) * radius);
    }

    private RanchEnemy SpawnEnemy(EnemyKind kind, Vector3 position, float healthMultiplier,
        float damageMultiplier, float speedMultiplier, bool finalBattleUnit)
    {
        EnemyStats stats = GetBaseEnemyStats(kind);
        GameObject body = CreateEnemyBody(kind, position);
        RanchEnemy enemy = body.AddComponent<RanchEnemy>();
        enemy.Initialize(this, kind, stats.Name,
            stats.Health * healthMultiplier,
            stats.Damage * damageMultiplier,
            stats.Speed * speedMultiplier,
            stats.AttackRange,
            stats.AttackCooldown,
            stats.RewardMoney * healthMultiplier,
            stats.RewardRanch * healthMultiplier,
            finalBattleUnit);

        activeEnemies.Add(enemy);
        return enemy;
    }

    private GameObject CreateEnemyBody(EnemyKind kind, Vector3 position)
    {
        PrimitiveType primitive = PrimitiveType.Capsule;
        Vector3 scale = Vector3.one;
        Material material = redMaterial;

        switch (kind)
        {
            case EnemyKind.RanchRaider:
                scale = new Vector3(0.9f, 1f, 0.9f);
                material = redMaterial;
                break;
            case EnemyKind.RanchBandit:
                scale = new Vector3(0.95f, 1f, 0.95f);
                material = orangeMaterial;
                break;
            case EnemyKind.CorruptedDrew:
                scale = new Vector3(0.8f, 0.9f, 0.8f);
                material = purpleMaterial;
                break;
            case EnemyKind.RanchGolem:
                primitive = PrimitiveType.Cube;
                scale = new Vector3(2.4f, 3.2f, 2.4f);
                material = grayMaterial;
                position.y = 1.6f;
                break;
            case EnemyKind.RanchHarvester:
                primitive = PrimitiveType.Cylinder;
                scale = new Vector3(1.3f, 1.4f, 1.3f);
                material = yellowMaterial;
                position.y = 1.4f;
                break;
            case EnemyKind.CJEnforcer:
                scale = new Vector3(1.25f, 1.4f, 1.25f);
                material = blackMaterial;
                position.y = 1.4f;
                break;
            case EnemyKind.MiniRanchenator:
                primitive = PrimitiveType.Sphere;
                scale = new Vector3(1.3f, 1.3f, 1.3f);
                material = cyanMaterial;
                break;
            case EnemyKind.UltimateRanchBeast:
                primitive = PrimitiveType.Cube;
                scale = new Vector3(5f, 5f, 5f);
                material = darkRedMaterial;
                position.y = 2.5f;
                break;
            case EnemyKind.CJ:
                scale = new Vector3(2.2f, 2.7f, 2.2f);
                material = blackMaterial;
                position.y = 2.7f;
                break;
        }

        GameObject body = GameObject.CreatePrimitive(primitive);
        body.name = GetBaseEnemyStats(kind).Name;
        body.transform.position = position;
        body.transform.localScale = scale;
        body.GetComponent<Renderer>().material = material;
        RemoveCollider(body);

        if (kind == EnemyKind.CorruptedDrew)
        {
            GameObject hat = CreatePrimitive(PrimitiveType.Cylinder, "Corrupted Drew Hat", Vector3.zero,
                new Vector3(0.9f, 0.12f, 0.9f), blackMaterial, body.transform);
            hat.transform.localPosition = new Vector3(0f, 1.4f, 0f);
            RemoveCollider(hat);
        }
        else if (kind == EnemyKind.CJ || kind == EnemyKind.MiniRanchenator)
        {
            GameObject crown = CreatePrimitive(PrimitiveType.Cylinder, "Ranchenator Crown", Vector3.zero,
                new Vector3(0.75f, 0.18f, 0.75f), yellowMaterial, body.transform);
            crown.transform.localPosition = new Vector3(0f, 1.25f, 0f);
            RemoveCollider(crown);
        }

        return body;
    }

    private EnemyStats GetBaseEnemyStats(EnemyKind kind)
    {
        switch (kind)
        {
            case EnemyKind.RanchBandit:
                return new EnemyStats("Ranch Bandit", 55f, 8f, 4.2f, 10f, 1.7f, 35d, 4d);
            case EnemyKind.CorruptedDrew:
                return new EnemyStats("Corrupted Drew", 80f, 22f, 8.5f, 1.8f, 0.65f, 60d, 8d);
            case EnemyKind.RanchGolem:
                return new EnemyStats("Ranch Golem", 650f, 30f, 1.45f, 2.3f, 1.8f, 180d, 20d);
            case EnemyKind.RanchHarvester:
                return new EnemyStats("Ranch Harvester", 95f, 5f, 5.7f, 2.2f, 0.7f, 120d, 15d);
            case EnemyKind.CJEnforcer:
                return new EnemyStats("CJ Enforcer", 850f, 55f, 5.2f, 2.2f, 0.75f, 500d, 50d);
            case EnemyKind.MiniRanchenator:
                return new EnemyStats("Mini-Ranchenator", 2200f, 120f, 7.3f, 7.5f, 0.7f, 1500d, 150d);
            case EnemyKind.UltimateRanchBeast:
                return new EnemyStats("Ultimate Ranch Beast", 25000f, 250f, 3.8f, 3.5f, 1f, 15000d, 1500d);
            case EnemyKind.CJ:
                return new EnemyStats("CJ, Ultimate Ranchenator", 250000f, 450f, 6.2f, 12f, 0.55f, 1000000d, 100000d);
            default:
                return new EnemyStats("Ranch Raider", 45f, 7f, 5.5f, 1.8f, 0.9f, 25d, 3d);
        }
    }

    public void EnemyDefeated(RanchEnemy enemy, double rewardMoney, double rewardRanch)
    {
        if (enemy == null) return;
        activeEnemies.Remove(enemy);
        enemiesDefeated++;
        money += rewardMoney;
        AddRanch(rewardRanch);
        Destroy(enemy.gameObject);
    }

    private void CleanEnemyList()
    {
        for (int i = activeEnemies.Count - 1; i >= 0; i--)
        {
            if (activeEnemies[i] == null) activeEnemies.RemoveAt(i);
        }
    }

    private void ClearAllEnemies()
    {
        for (int i = activeEnemies.Count - 1; i >= 0; i--)
        {
            if (activeEnemies[i] != null) Destroy(activeEnemies[i].gameObject);
        }
        activeEnemies.Clear();
    }

    private RanchEnemy FindClosestEnemy(Vector3 position)
    {
        RanchEnemy closest = null;
        float bestDistance = float.MaxValue;

        for (int i = 0; i < activeEnemies.Count; i++)
        {
            RanchEnemy enemy = activeEnemies[i];
            if (enemy == null) continue;
            float distance = (enemy.transform.position - position).sqrMagnitude;
            if (distance < bestDistance)
            {
                bestDistance = distance;
                closest = enemy;
            }
        }

        return closest;
    }

    // ============================================================
    // CJ FINAL BATTLE
    // ============================================================

    private void StartFinalBattle()
    {
        if (!citadelBuilt)
        {
            LogEvent("The Ranch Citadel is required before challenging CJ.");
            return;
        }

        if (finalBattleActive)
        {
            LogEvent("The CJ battle is already active.");
            return;
        }

        ClearAllEnemies();
        finalBattleActive = true;
        finalBattlePhase = 1;
        SpawnCJPhaseOne();
        LogEvent("CJ FINAL BATTLE — PHASE 1: CJ Enforcers invade the Citadel.");
    }

    private void SpawnCJPhaseOne()
    {
        float scale = GetFinalBattleScale();
        for (int i = 0; i < 8; i++)
        {
            SpawnEnemy(EnemyKind.CJEnforcer, GetEnemySpawnPosition(i, 8, 28f),
                3f * scale, 2.2f * scale, 1.25f * scale, true);
        }
    }

    private void SpawnCJPhaseTwo()
    {
        float scale = GetFinalBattleScale();
        RanchEnemy golem = SpawnEnemy(EnemyKind.RanchGolem, new Vector3(0f, 3f, 35f),
            80f * scale, 5f * scale, 1.15f * scale, true);
        golem.OverrideDisplayName("GIANT RANCH GOLEM");
        LogEvent("CJ FINAL BATTLE — PHASE 2: The Giant Ranch Golem awakens.");
    }

    private void SpawnCJPhaseThree()
    {
        float scale = GetFinalBattleScale();
        SpawnEnemy(EnemyKind.CJ, new Vector3(0f, 2.7f, 38f),
            scale, scale, Mathf.Sqrt(scale), true);
        LogEvent("CJ FINAL BATTLE — PHASE 3: CJ, the Ultimate Ranchenator, enters battle.");
    }

    private float GetFinalBattleScale()
    {
        return Mathf.Pow(1.18f, Mathf.Max(0, waveNumber - 10));
    }

    private void UpdateFinalBattle()
    {
        if (!finalBattleActive) return;
        CleanEnemyList();
        if (activeEnemies.Count > 0) return;

        if (finalBattlePhase == 1)
        {
            finalBattlePhase = 2;
            SpawnCJPhaseTwo();
        }
        else if (finalBattlePhase == 2)
        {
            finalBattlePhase = 3;
            SpawnCJPhaseThree();
        }
        else if (finalBattlePhase == 3)
        {
            finalBattlePhase = 4;
            finalBattleActive = false;
            cjDefeated = true;
            UnlockCursorForVictory();
        }
    }

    private void UnlockCursorForVictory()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    // ============================================================
    // PROJECTILES
    // ============================================================

    public void SpawnBottleProjectile(Vector3 origin, Transform target, float damage, string ownerName)
    {
        if (target == null) return;

        GameObject projectileObject = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        projectileObject.name = "Thrown Empty Ranch Bottle";
        projectileObject.transform.position = origin;
        projectileObject.transform.localScale = new Vector3(0.18f, 0.36f, 0.18f);
        projectileObject.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        projectileObject.GetComponent<Renderer>().material = whiteMaterial;
        RemoveCollider(projectileObject);

        RanchProjectile projectile = projectileObject.AddComponent<RanchProjectile>();
        projectile.Initialize(this, target, damage, ownerName);
    }

    // ============================================================
    // UI
    // ============================================================

    private void OnGUI()
    {
        GUIStyle normal = new GUIStyle(GUI.skin.label)
        {
            fontSize = 20,
            normal = { textColor = Color.white }
        };

        GUIStyle small = new GUIStyle(GUI.skin.label)
        {
            fontSize = 16,
            normal = { textColor = Color.white }
        };

        GUIStyle title = new GUIStyle(GUI.skin.label)
        {
            fontSize = 34,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = Color.white }
        };

        GUI.Box(new Rect(14f, 14f, 410f, 315f), string.Empty);
        GUI.Label(new Rect(28f, 24f, 380f, 30f), "RANCH SIMULATOR", normal);
        GUI.Label(new Rect(28f, 60f, 380f, 28f), "Health: " + playerHealth.ToString("F0") + "/" + playerMaxHealth.ToString("F0"), normal);
        GUI.Label(new Rect(28f, 90f, 380f, 28f), "Raw Ranch: " + Compact(ranch), normal);
        GUI.Label(new Rect(28f, 120f, 380f, 28f), "Bottles: " + Compact(bottles), normal);
        GUI.Label(new Rect(28f, 150f, 380f, 28f), "Money: $" + Compact(money), normal);
        GUI.Label(new Rect(28f, 180f, 380f, 28f), "Ranch Type: " + ranchTypeNames[ranchTypeTier] + " (" + ranchValueMultipliers[ranchTypeTier] + "x)", small);
        GUI.Label(new Rect(28f, 207f, 380f, 28f), "Bottle: " + bottleNames[bottleTier] + " (" + bottleCaps[bottleTier] + ")", small);
        GUI.Label(new Rect(28f, 234f, 380f, 28f), "Sword: " + swordNames[swordTier] + " — " + swordDamages[swordTier].ToString("F0") + " dmg", small);
        GUI.Label(new Rect(28f, 261f, 380f, 28f), "Drew: " + (drewUnlocked ? "Level " + drewLevel + GetDrewStatusSuffix() : "not hired"), small);
        GUI.Label(new Rect(28f, 288f, 380f, 28f), "Wave: " + waveNumber + " | Enemies: " + activeEnemies.Count, small);

        GUI.Box(new Rect(Screen.width - 430f, 14f, 416f, 315f), string.Empty);
        GUI.Label(new Rect(Screen.width - 412f, 25f, 385f, 30f), "EMPIRE STATUS", normal);
        GUI.Label(new Rect(Screen.width - 412f, 62f, 385f, 25f), BuildingStatus("Factory", factoryBuilt), small);
        GUI.Label(new Rect(Screen.width - 412f, 88f, 385f, 25f), BuildingStatus("Warehouse", warehouseBuilt), small);
        GUI.Label(new Rect(Screen.width - 412f, 114f, 385f, 25f), BuildingStatus("Laboratory", laboratoryBuilt), small);
        GUI.Label(new Rect(Screen.width - 412f, 140f, 385f, 25f), BuildingStatus("Fortress", fortressBuilt), small);
        GUI.Label(new Rect(Screen.width - 412f, 166f, 385f, 25f), BuildingStatus("Citadel", citadelBuilt), small);

        string nextBuilding = nextBuildingIndex < buildingNames.Length
            ? "Next: " + buildingNames[nextBuildingIndex] + " — $" + Compact(buildingCosts[nextBuildingIndex])
            : "All buildings complete";
        GUI.Label(new Rect(Screen.width - 412f, 202f, 385f, 42f), nextBuilding, small);

        if (finalBattleActive)
        {
            GUI.Label(new Rect(Screen.width - 412f, 246f, 385f, 28f), "CJ BATTLE: PHASE " + finalBattlePhase, normal);
        }
        else
        {
            GUI.Label(new Rect(Screen.width - 412f, 246f, 385f, 28f), "Next wave: " + FormatTime(waveTimer), normal);
        }

        GUI.Label(new Rect(Screen.width - 412f, 280f, 385f, 25f), "Enemies defeated: " + Compact(enemiesDefeated), small);

        if (!string.IsNullOrEmpty(currentPrompt))
        {
            GUI.Box(new Rect(Screen.width / 2f - 360f, Screen.height - 92f, 720f, 56f), string.Empty);
            GUI.Label(new Rect(Screen.width / 2f - 335f, Screen.height - 78f, 670f, 30f), currentPrompt, normal);
        }

        if (!string.IsNullOrEmpty(lastEvent))
        {
            GUI.Box(new Rect(14f, Screen.height - 150f, 860f, 110f), string.Empty);
            GUI.Label(new Rect(30f, Screen.height - 136f, 830f, 82f), lastEvent, normal);
        }

        if (cjDefeated)
        {
            GUI.Box(new Rect(Screen.width / 2f - 390f, Screen.height / 2f - 205f, 780f, 410f), string.Empty);
            GUI.Label(new Rect(Screen.width / 2f - 360f, Screen.height / 2f - 175f, 720f, 60f),
                "CJ HAS BEEN OVERTHROWN", title);
            GUI.Label(new Rect(Screen.width / 2f - 330f, Screen.height / 2f - 80f, 660f, 45f),
                "CJ: Impossible...", normal);
            GUI.Label(new Rect(Screen.width / 2f - 330f, Screen.height / 2f - 35f, 660f, 45f),
                "You have surpassed the Ranch.", normal);
            GUI.Label(new Rect(Screen.width / 2f - 330f, Screen.height / 2f + 35f, 660f, 45f),
                "Drew walks up: \"There is another.\"", normal);
            GUI.Label(new Rect(Screen.width / 2f - 330f, Screen.height / 2f + 95f, 660f, 45f),
                "Ranch Simulator 2 is coming.", normal);
            GUI.Label(new Rect(Screen.width / 2f - 330f, Screen.height / 2f + 145f, 660f, 35f),
                "Press R to restart.", normal);
        }
    }

    private string BuildingStatus(string name, bool built)
    {
        return (built ? "[BUILT] " : "[LOCKED] ") + name;
    }

    private string GetDrewStatusSuffix()
    {
        if (drewLevel >= 50) return " — ASCENDED";
        if (drewLevel >= 25) return " — self-producing";
        if (drewLevel >= 20) return " — Mini-Drews active";
        if (drewLevel >= 15) return " — auto-upgrading";
        if (drewLevel >= 10) return " — auto-selling";
        if (drewLevel >= 5) return " — auto-bottling";
        return string.Empty;
    }

    private string FormatTime(float seconds)
    {
        int totalSeconds = Mathf.Max(0, Mathf.CeilToInt(seconds));
        return (totalSeconds / 60).ToString("00") + ":" + (totalSeconds % 60).ToString("00");
    }

    private string Compact(double value)
    {
        double absolute = Math.Abs(value);
        if (absolute >= 1e15d) return (value / 1e15d).ToString("0.##") + "Q";
        if (absolute >= 1e12d) return (value / 1e12d).ToString("0.##") + "T";
        if (absolute >= 1e9d) return (value / 1e9d).ToString("0.##") + "B";
        if (absolute >= 1e6d) return (value / 1e6d).ToString("0.##") + "M";
        if (absolute >= 1e3d) return (value / 1e3d).ToString("0.##") + "K";
        return value.ToString("0.##");
    }

    private string Compact(long value)
    {
        return Compact((double)value);
    }

    private long SafeAddLong(long a, long b)
    {
        if (b > 0 && a > long.MaxValue - b) return long.MaxValue;
        if (b < 0 && a < long.MinValue - b) return long.MinValue;
        return a + b;
    }

    private void LogEvent(string message)
    {
        lastEvent = message;
        eventTimer = 5f;
        Debug.Log(message);
    }

    private void UpdateTimers()
    {
        if (eventTimer > 0f) eventTimer -= Time.deltaTime;
    }
}

// ============================================================================
// ENEMY AI
// ============================================================================

public class RanchEnemy : MonoBehaviour
{
    private RanchSimulatorFullGame game;
    private EnemyKind kind;
    private string displayName;
    private float maxHealth;
    private float health;
    private float damage;
    private float speed;
    private float attackRange;
    private float attackCooldown;
    private float attackTimer;
    private double rewardMoney;
    private double rewardRanch;
    private bool finalBattleUnit;
    private TextMesh label;
    private float labelTimer;
    private float stolenReportTimer;

    public void Initialize(RanchSimulatorFullGame owner, EnemyKind enemyKind, string enemyName,
        float enemyHealth, float enemyDamage, float enemySpeed, float enemyAttackRange,
        float enemyAttackCooldown, double moneyReward, double ranchReward, bool isFinalBattleUnit)
    {
        game = owner;
        kind = enemyKind;
        displayName = enemyName;
        maxHealth = Mathf.Max(1f, enemyHealth);
        health = maxHealth;
        damage = Mathf.Max(1f, enemyDamage);
        speed = Mathf.Max(0.1f, enemySpeed);
        attackRange = enemyAttackRange;
        attackCooldown = Mathf.Max(0.1f, enemyAttackCooldown);
        rewardMoney = moneyReward;
        rewardRanch = ranchReward;
        finalBattleUnit = isFinalBattleUnit;

        CreateHealthLabel();
    }

    public void OverrideDisplayName(string newName)
    {
        displayName = newName;
        UpdateLabel();
    }

    private void CreateHealthLabel()
    {
        GameObject labelObject = new GameObject("Enemy Health Label");
        labelObject.transform.SetParent(transform);
        labelObject.transform.localPosition = Vector3.up * 2.2f;
        label = labelObject.AddComponent<TextMesh>();
        label.fontSize = 38;
        label.characterSize = 0.10f;
        label.anchor = TextAnchor.MiddleCenter;
        label.alignment = TextAlignment.Center;
        label.color = Color.white;
        labelObject.AddComponent<LabelBillboard>();
        UpdateLabel();
    }

    private void Update()
    {
        if (game == null || game.GetPlayerTransform() == null) return;

        attackTimer -= Time.deltaTime;
        labelTimer -= Time.deltaTime;
        stolenReportTimer -= Time.deltaTime;

        if (labelTimer <= 0f)
        {
            labelTimer = 0.25f;
            UpdateLabel();
        }

        if (kind == EnemyKind.RanchHarvester)
        {
            UpdateHarvester();
        }
        else if (kind == EnemyKind.RanchBandit || kind == EnemyKind.MiniRanchenator || kind == EnemyKind.CJ)
        {
            UpdateRangedAttacker();
        }
        else
        {
            UpdateMeleeAttacker();
        }
    }

    private void UpdateMeleeAttacker()
    {
        Transform player = game.GetPlayerTransform();
        Vector3 target = player.position;
        target.y = transform.position.y;
        float distance = Vector3.Distance(transform.position, target);

        if (distance > attackRange)
        {
            MoveToward(target);
        }
        else if (attackTimer <= 0f)
        {
            attackTimer = attackCooldown;
            game.DamagePlayer(damage, displayName);
        }
    }

    private void UpdateRangedAttacker()
    {
        Transform player = game.GetPlayerTransform();
        Vector3 target = player.position;
        target.y = transform.position.y;
        float distance = Vector3.Distance(transform.position, target);

        float preferredRange = kind == EnemyKind.CJ ? 10f : 7f;
        if (distance > preferredRange + 1.5f)
        {
            MoveToward(target);
        }
        else if (distance < preferredRange - 2f)
        {
            Vector3 away = transform.position + (transform.position - target).normalized * 4f;
            MoveToward(away);
        }

        if (distance <= attackRange && attackTimer <= 0f)
        {
            attackTimer = attackCooldown;
            game.SpawnBottleProjectile(transform.position + Vector3.up * 1.1f,
                player, damage, displayName);
        }
    }

    private void UpdateHarvester()
    {
        Vector3 target = game.GetHarvesterTargetPosition();
        target.y = transform.position.y;
        float distance = Vector3.Distance(transform.position, target);

        if (distance > attackRange)
        {
            MoveToward(target);
            return;
        }

        double stolen = game.StealRanch(damage * 8d * Time.deltaTime);
        if (stolen > 0d && stolenReportTimer <= 0f)
        {
            stolenReportTimer = 1f;
            Debug.Log(displayName + " is stealing Ranch from storage.");
        }

        if (stolen <= 0d)
        {
            UpdateMeleeAttacker();
        }
    }

    private void MoveToward(Vector3 target)
    {
        Vector3 direction = target - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude <= 0.001f) return;

        transform.position += direction.normalized * speed * Time.deltaTime;
        transform.rotation = Quaternion.Slerp(transform.rotation,
            Quaternion.LookRotation(direction.normalized), Time.deltaTime * 8f);
    }

    public void TakeDamage(float amount)
    {
        if (health <= 0f) return;
        health -= Mathf.Max(0f, amount);
        UpdateLabel();

        if (health <= 0f)
        {
            game.EnemyDefeated(this, rewardMoney, rewardRanch);
        }
    }

    private void UpdateLabel()
    {
        if (label == null) return;
        label.text = displayName + "\n" + Mathf.Max(0f, health).ToString("0") + "/" + maxHealth.ToString("0");
    }
}

// ============================================================================
// PROJECTILE
// ============================================================================

public class RanchProjectile : MonoBehaviour
{
    private RanchSimulatorFullGame game;
    private Transform target;
    private float damage;
    private string ownerName;
    private float life = 7f;
    private float speed = 14f;

    public void Initialize(RanchSimulatorFullGame owner, Transform projectileTarget, float projectileDamage,
        string projectileOwnerName)
    {
        game = owner;
        target = projectileTarget;
        damage = projectileDamage;
        ownerName = projectileOwnerName;
    }

    private void Update()
    {
        life -= Time.deltaTime;
        if (life <= 0f || target == null)
        {
            Destroy(gameObject);
            return;
        }

        Vector3 aim = target.position + Vector3.up * 0.8f;
        transform.position = Vector3.MoveTowards(transform.position, aim, speed * Time.deltaTime);
        transform.Rotate(Vector3.right, 600f * Time.deltaTime, Space.Self);

        if (Vector3.Distance(transform.position, aim) <= 0.45f)
        {
            game.DamagePlayer(damage, ownerName + "'s bottle");
            Destroy(gameObject);
        }
    }
}

// ============================================================================
// SUPPORT TYPES
// ============================================================================

public enum EnemyKind
{
    RanchRaider,
    RanchBandit,
    CorruptedDrew,
    RanchGolem,
    RanchHarvester,
    CJEnforcer,
    MiniRanchenator,
    UltimateRanchBeast,
    CJ
}

public struct EnemyStats
{
    public string Name;
    public float Health;
    public float Damage;
    public float Speed;
    public float AttackRange;
    public float AttackCooldown;
    public double RewardMoney;
    public double RewardRanch;

    public EnemyStats(string name, float health, float damage, float speed, float attackRange,
        float attackCooldown, double rewardMoney, double rewardRanch)
    {
        Name = name;
        Health = health;
        Damage = damage;
        Speed = speed;
        AttackRange = attackRange;
        AttackCooldown = attackCooldown;
        RewardMoney = rewardMoney;
        RewardRanch = rewardRanch;
    }
}

public class LabelBillboard : MonoBehaviour
{
    private void LateUpdate()
    {
        Camera camera = Camera.main;
        if (camera == null) return;
        transform.LookAt(transform.position + camera.transform.rotation * Vector3.forward,
            camera.transform.rotation * Vector3.up);
    }
}

// ============================================================================
// INPUT BRIDGE: SUPPORTS OLD INPUT MANAGER, NEW INPUT SYSTEM, OR BOTH
// ============================================================================

public enum RanchAction
{
    Extract,
    BottleOne,
    BottleAll,
    Sell,
    UpgradeBottle,
    UpgradeDrew,
    UpgradeSword,
    Build,
    Research,
    ChallengeCJ,
    Restart,
    Escape
}

public static class RanchInput
{
    public static float MoveX
    {
        get
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetAxisRaw("Horizontal");
#elif ENABLE_INPUT_SYSTEM
            if (Keyboard.current == null) return 0f;
            float value = 0f;
            if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) value -= 1f;
            if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) value += 1f;
            return value;
#else
            return 0f;
#endif
        }
    }

    public static float MoveY
    {
        get
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetAxisRaw("Vertical");
#elif ENABLE_INPUT_SYSTEM
            if (Keyboard.current == null) return 0f;
            float value = 0f;
            if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed) value -= 1f;
            if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed) value += 1f;
            return value;
#else
            return 0f;
#endif
        }
    }

    public static float MouseX
    {
        get
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetAxis("Mouse X");
#elif ENABLE_INPUT_SYSTEM
            return Mouse.current != null ? Mouse.current.delta.ReadValue().x * 0.08f : 0f;
#else
            return 0f;
#endif
        }
    }

    public static float MouseY
    {
        get
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetAxis("Mouse Y");
#elif ENABLE_INPUT_SYSTEM
            return Mouse.current != null ? Mouse.current.delta.ReadValue().y * 0.08f : 0f;
#else
            return 0f;
#endif
        }
    }

    public static bool AttackDown
    {
        get
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetMouseButtonDown(0);
#elif ENABLE_INPUT_SYSTEM
            return Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
#else
            return false;
#endif
        }
    }

    public static bool ShiftHeld
    {
        get
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
#elif ENABLE_INPUT_SYSTEM
            return Keyboard.current != null &&
                   (Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed);
#else
            return false;
#endif
        }
    }

    public static bool Down(RanchAction action)
    {
#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKeyDown(ToKeyCode(action));
#elif ENABLE_INPUT_SYSTEM
        if (Keyboard.current == null) return false;
        switch (action)
        {
            case RanchAction.Extract: return Keyboard.current.eKey.wasPressedThisFrame;
            case RanchAction.BottleOne: return Keyboard.current.bKey.wasPressedThisFrame;
            case RanchAction.BottleAll: return Keyboard.current.vKey.wasPressedThisFrame;
            case RanchAction.Sell: return Keyboard.current.fKey.wasPressedThisFrame;
            case RanchAction.UpgradeBottle: return Keyboard.current.uKey.wasPressedThisFrame;
            case RanchAction.UpgradeDrew: return Keyboard.current.gKey.wasPressedThisFrame;
            case RanchAction.UpgradeSword: return Keyboard.current.kKey.wasPressedThisFrame;
            case RanchAction.Build: return Keyboard.current.hKey.wasPressedThisFrame;
            case RanchAction.Research: return Keyboard.current.jKey.wasPressedThisFrame;
            case RanchAction.ChallengeCJ: return Keyboard.current.cKey.wasPressedThisFrame;
            case RanchAction.Restart: return Keyboard.current.rKey.wasPressedThisFrame;
            case RanchAction.Escape: return Keyboard.current.escapeKey.wasPressedThisFrame;
            default: return false;
        }
#else
        return false;
#endif
    }

    public static bool Held(RanchAction action)
    {
#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKey(ToKeyCode(action));
#elif ENABLE_INPUT_SYSTEM
        if (Keyboard.current == null) return false;
        switch (action)
        {
            case RanchAction.Extract: return Keyboard.current.eKey.isPressed;
            case RanchAction.BottleOne: return Keyboard.current.bKey.isPressed;
            case RanchAction.BottleAll: return Keyboard.current.vKey.isPressed;
            case RanchAction.Sell: return Keyboard.current.fKey.isPressed;
            case RanchAction.UpgradeBottle: return Keyboard.current.uKey.isPressed;
            case RanchAction.UpgradeDrew: return Keyboard.current.gKey.isPressed;
            case RanchAction.UpgradeSword: return Keyboard.current.kKey.isPressed;
            case RanchAction.Build: return Keyboard.current.hKey.isPressed;
            case RanchAction.Research: return Keyboard.current.jKey.isPressed;
            case RanchAction.ChallengeCJ: return Keyboard.current.cKey.isPressed;
            case RanchAction.Restart: return Keyboard.current.rKey.isPressed;
            case RanchAction.Escape: return Keyboard.current.escapeKey.isPressed;
            default: return false;
        }
#else
        return false;
#endif
    }

#if ENABLE_LEGACY_INPUT_MANAGER
    private static KeyCode ToKeyCode(RanchAction action)
    {
        switch (action)
        {
            case RanchAction.Extract: return KeyCode.E;
            case RanchAction.BottleOne: return KeyCode.B;
            case RanchAction.BottleAll: return KeyCode.V;
            case RanchAction.Sell: return KeyCode.F;
            case RanchAction.UpgradeBottle: return KeyCode.U;
            case RanchAction.UpgradeDrew: return KeyCode.G;
            case RanchAction.UpgradeSword: return KeyCode.K;
            case RanchAction.Build: return KeyCode.H;
            case RanchAction.Research: return KeyCode.J;
            case RanchAction.ChallengeCJ: return KeyCode.C;
            case RanchAction.Restart: return KeyCode.R;
            case RanchAction.Escape: return KeyCode.Escape;
            default: return KeyCode.None;
        }
    }
#endif
}

// ============================================================================
// AUTO-BOOTSTRAP: NO MANUAL GAMEOBJECT SETUP REQUIRED
// ============================================================================

public static class RanchSimulatorBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CreateGame()
    {
        if (UnityEngine.Object.FindObjectOfType<RanchSimulatorFullGame>() != null) return;
        GameObject gameObject = new GameObject("Ranch Simulator - Ultimate Update");
        gameObject.AddComponent<RanchSimulatorFullGame>();
    }
}
