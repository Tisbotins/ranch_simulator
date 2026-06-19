using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// RANCH SIMULATOR - COMPLETE PLAYABLE UNITY PROTOTYPE
/// 
/// Setup:
/// 1. Create a new Unity 3D project.
/// 2. In Assets, create a folder named Scripts.
/// 3. Put this file in Assets/Scripts.
/// 4. Create an Empty GameObject in the scene named RanchGame.
/// 5. Drag this script onto RanchGame.
/// 6. Press Play.
/// 
/// Controls:
/// WASD = move
/// Mouse = look around
/// E = hold near Ranch Tree to extract Ranch
/// B = bottle Ranch near Bottle Station
/// F = sell one bottle near Sell Station
/// Left Shift + F = sell all bottles near Sell Station
/// U = upgrade bottle size near Upgrade Station
/// G = upgrade Drew near Drew Station
/// Left click = swing Ranch Sword
/// T = spawn a test enemy wave immediately
/// C = challenge CJ once unlocked
/// R = restart after winning
/// Escape = unlock cursor
/// Left click = lock cursor again
/// 
/// This is built as one big script so you can actually get a working game fast.
/// Later, you should split it into separate files.
/// </summary>
public class RanchSimulatorFullGame : MonoBehaviour
{
    // ---------- Core resources ----------
    private float ranch = 0f;
    private float money = 0f;
    private int bottles = 0;
    private int bottlesSold = 0;

    // ---------- Bottle upgrades ----------
    private int bottleTier = 0;

    private readonly string[] bottleNames =
    {
        "Cracked Bottle",
        "Tiny Bottle",
        "Standard Bottle",
        "Family Bottle",
        "Mega Jug",
        "Industrial Drum",
        "Ranch Tanker",
        "Ranchenator Vessel"
    };

    private readonly int[] bottleCaps =
    {
        1, 2, 5, 12, 30, 75, 200, 500
    };

    // ---------- Ranch Tree ----------
    private float ranchPerSecond = 1.2f;
    private float extractionMultiplier = 1f;
    private float treeUpgradeCost = 150f;
    private int treeLevel = 1;

    // ---------- Drew ----------
    private GameObject drew;
    private int drewLevel = 0;
    private float drewTimer = 0f;
    private float drewWorkInterval = 6f;
    private float drewUpgradeCost = 100f;
    private bool drewUnlocked = false;
    private Vector3 drewTarget;
    private bool drewMovingToTree = true;

    // ---------- CJ ----------
    private int cjHeat = 0;
    private bool cjHasWarned = false;
    private bool cjBattleUnlocked = false;
    private bool cjDefeated = false;
    private float cjPower = 10000f;

    // ---------- Player / camera ----------
    private GameObject player;
    private CharacterController controller;
    private Camera mainCam;

    private float moveSpeed = 6f;
    private float mouseSensitivity = 2.2f;
    private float cameraPitch = 15f;
    private float gravity = -18f;
    private float yVelocity = 0f;

    // ---------- Player health / Ranch Sword ----------
    private float playerMaxHealth = 100f;
    private float playerHealth = 100f;
    private float swordRange = 3.25f;
    private float swordBaseDamage = 25f;
    private float swordCooldown = 0.45f;
    private float swordCooldownTimer = 0f;
    private float swordAnimationTimer = 0f;
    private float swordAnimationLength = 0.28f;
    private GameObject ranchSwordPivot;
    private Quaternion swordRestRotation;
    private Quaternion swordAttackRotation;
    private float hurtMessageCooldown = 0f;

    // ---------- Enemy waves ----------
    private readonly List<RanchEnemy> activeEnemies = new List<RanchEnemy>();
    private float enemyWaveTimer = 0f;
    private float enemyWaveInterval = 300f; // Five minutes
    private int enemyWaveNumber = 0;
    private int enemiesDefeated = 0;

    // ---------- World objects ----------
    private GameObject ranchTree;
    private GameObject bottleStation;
    private GameObject sellStation;
    private GameObject upgradeStation;
    private GameObject drewStation;
    private GameObject cjGate;

    // ---------- Interaction ----------
    private float interactRange = 3.2f;
    private string currentPrompt = "";
    private string lastEvent = "Welcome to Ranch Simulator. Extract Ranch. Bottle Ranch. Overthrow CJ.";
    private float eventTimer = 0f;

    // ---------- Visual polish ----------
    private List<GameObject> spawnedObjects = new List<GameObject>();
    private Material greenMat;
    private Material brownMat;
    private Material blueMat;
    private Material yellowMat;
    private Material redMat;
    private Material whiteMat;
    private Material blackMat;
    private Material ranchMat;
    private Material purpleMat;

    private void Start()
    {
        CreateMaterials();
        BuildWorld();
        CreatePlayer();
        CreateRanchSword();
        CreateDrew();
        LockCursor();

        LogEvent("Welcome to McMinnville Ranch Country. CJ controls the market. Start small.");
    }

    private void Update()
    {
        if (cjDefeated)
        {
            if (Input.GetKeyDown(KeyCode.R))
            {
                UnityEngine.SceneManagement.SceneManager.LoadScene(
                    UnityEngine.SceneManagement.SceneManager.GetActiveScene().name
                );
            }

            return;
        }

        HandleCursor();
        HandleMovement();
        UpdateCamera();
        HandleCombat();
        UpdatePromptsAndInteractions();
        UpdateDrew();
        UpdateEnemyWaves();
        UpdateCJ();
        UpdateEventTimer();
    }

    // ============================================================
    // WORLD BUILDING
    // ============================================================

    private void CreateMaterials()
    {
        greenMat = MakeMat(new Color(0.25f, 0.65f, 0.25f));
        brownMat = MakeMat(new Color(0.35f, 0.2f, 0.09f));
        blueMat = MakeMat(new Color(0.2f, 0.45f, 0.9f));
        yellowMat = MakeMat(new Color(0.95f, 0.75f, 0.25f));
        redMat = MakeMat(new Color(0.85f, 0.2f, 0.15f));
        whiteMat = MakeMat(new Color(0.9f, 0.9f, 0.85f));
        blackMat = MakeMat(new Color(0.05f, 0.05f, 0.05f));
        ranchMat = MakeMat(new Color(1.0f, 0.92f, 0.68f));
        purpleMat = MakeMat(new Color(0.45f, 0.12f, 0.65f));
    }

    private Material MakeMat(Color color)
    {
        Material mat = new Material(Shader.Find("Standard"));
        mat.color = color;
        return mat;
    }

    private void BuildWorld()
    {
        // Ground
        GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "McMinnville Oregon Ground";
        ground.transform.position = Vector3.zero;
        ground.transform.localScale = new Vector3(6f, 1f, 6f);
        ground.GetComponent<Renderer>().material = greenMat;
        spawnedObjects.Add(ground);

        // Ranch Tree
        ranchTree = new GameObject("Ranch Tree");
        ranchTree.transform.position = new Vector3(0, 0, 8);

        GameObject trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        trunk.name = "Ranch Tree Trunk";
        trunk.transform.parent = ranchTree.transform;
        trunk.transform.localPosition = new Vector3(0, 1.5f, 0);
        trunk.transform.localScale = new Vector3(0.8f, 1.8f, 0.8f);
        trunk.GetComponent<Renderer>().material = brownMat;

        GameObject crown = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        crown.name = "Ranch Tree Crown";
        crown.transform.parent = ranchTree.transform;
        crown.transform.localPosition = new Vector3(0, 4.1f, 0);
        crown.transform.localScale = new Vector3(3.0f, 2.2f, 3.0f);
        crown.GetComponent<Renderer>().material = ranchMat;

        GameObject tap = GameObject.CreatePrimitive(PrimitiveType.Cube);
        tap.name = "Ranch Tap";
        tap.transform.parent = ranchTree.transform;
        tap.transform.localPosition = new Vector3(0, 2.1f, -0.75f);
        tap.transform.localScale = new Vector3(0.35f, 0.25f, 1.0f);
        tap.GetComponent<Renderer>().material = blueMat;

        spawnedObjects.Add(ranchTree);

        CreateLabel("RANCH TREE\nHold E to extract", ranchTree.transform.position + new Vector3(0, 5.8f, 0));

        // Stations
        bottleStation = CreateStation("Bottle Station", new Vector3(-8, 0.5f, 0), blueMat, "BOTTLE STATION\nPress B");
        sellStation = CreateStation("Sell Station", new Vector3(8, 0.5f, 0), yellowMat, "SELL STATION\nPress F");
        upgradeStation = CreateStation("Bottle Upgrade Station", new Vector3(0, 0.5f, -8), redMat, "UPGRADE BOTTLES\nPress U");
        drewStation = CreateStation("Drew Station", new Vector3(-8, 0.5f, -8), whiteMat, "DREW STATION\nPress G");
        cjGate = CreateStation("CJ Gate", new Vector3(8, 0.5f, 8), blackMat, "CJ GATE\nPress C when ready");

        // Decorative trees / ranch barrels
        for (int i = 0; i < 12; i++)
        {
            float angle = i * Mathf.PI * 2f / 12f;
            Vector3 pos = new Vector3(Mathf.Cos(angle) * 22f, 0, Mathf.Sin(angle) * 22f);
            CreateDecorTree(pos);
        }

        CreateBarrel(new Vector3(-3, 0.7f, 2));
        CreateBarrel(new Vector3(-4.2f, 0.7f, 2.6f));
        CreateBarrel(new Vector3(3.5f, 0.7f, -2.5f));
    }

    private GameObject CreateStation(string name, Vector3 pos, Material mat, string label)
    {
        GameObject station = GameObject.CreatePrimitive(PrimitiveType.Cube);
        station.name = name;
        station.transform.position = pos;
        station.transform.localScale = new Vector3(2.4f, 1f, 2.4f);
        station.GetComponent<Renderer>().material = mat;
        spawnedObjects.Add(station);

        CreateLabel(label, pos + new Vector3(0, 2.4f, 0));
        return station;
    }

    private void CreateDecorTree(Vector3 pos)
    {
        GameObject root = new GameObject("Background Tree");
        root.transform.position = pos;

        GameObject trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        trunk.transform.parent = root.transform;
        trunk.transform.localPosition = new Vector3(0, 1, 0);
        trunk.transform.localScale = new Vector3(0.5f, 1.3f, 0.5f);
        trunk.GetComponent<Renderer>().material = brownMat;

        GameObject top = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        top.transform.parent = root.transform;
        top.transform.localPosition = new Vector3(0, 2.8f, 0);
        top.transform.localScale = new Vector3(2.2f, 2.2f, 2.2f);
        top.GetComponent<Renderer>().material = greenMat;

        spawnedObjects.Add(root);
    }

    private void CreateBarrel(Vector3 pos)
    {
        GameObject barrel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        barrel.name = "Ranch Barrel";
        barrel.transform.position = pos;
        barrel.transform.localScale = new Vector3(0.8f, 0.7f, 0.8f);
        barrel.GetComponent<Renderer>().material = ranchMat;
        spawnedObjects.Add(barrel);
    }

    private void CreateLabel(string text, Vector3 pos)
    {
        GameObject labelObj = new GameObject("Label");
        labelObj.transform.position = pos;

        TextMesh mesh = labelObj.AddComponent<TextMesh>();
        mesh.text = text;
        mesh.fontSize = 42;
        mesh.characterSize = 0.12f;
        mesh.anchor = TextAnchor.MiddleCenter;
        mesh.alignment = TextAlignment.Center;
        mesh.color = Color.black;

        LabelBillboard billboard = labelObj.AddComponent<LabelBillboard>();

        spawnedObjects.Add(labelObj);
    }

    private void CreatePlayer()
    {
        player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        player.name = "Player";
        player.transform.position = new Vector3(0, 1.2f, -3);
        player.GetComponent<Renderer>().material = blueMat;

        controller = player.AddComponent<CharacterController>();
        controller.height = 2f;
        controller.radius = 0.45f;
        controller.center = new Vector3(0, 0, 0);

        // Remove capsule collider because CharacterController handles collision.
        CapsuleCollider capsule = player.GetComponent<CapsuleCollider>();
        if (capsule != null) Destroy(capsule);

        mainCam = Camera.main;
        if (mainCam == null)
        {
            GameObject camObj = new GameObject("Main Camera");
            mainCam = camObj.AddComponent<Camera>();
            camObj.tag = "MainCamera";
        }

        mainCam.transform.position = player.transform.position + new Vector3(0, 5, -7);
        mainCam.transform.LookAt(player.transform.position + Vector3.up);
    }

    private void CreateRanchSword()
    {
        ranchSwordPivot = new GameObject("Ranch Sword Pivot");
        ranchSwordPivot.transform.SetParent(player.transform);
        ranchSwordPivot.transform.localPosition = new Vector3(0.72f, 0.45f, 0.55f);

        swordRestRotation = Quaternion.Euler(12f, 0f, -35f);
        swordAttackRotation = Quaternion.Euler(12f, 0f, 75f);
        ranchSwordPivot.transform.localRotation = swordRestRotation;

        GameObject handle = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        handle.name = "Ranch Sword Handle";
        handle.transform.SetParent(ranchSwordPivot.transform);
        handle.transform.localPosition = new Vector3(0f, 0.25f, 0f);
        handle.transform.localRotation = Quaternion.identity;
        handle.transform.localScale = new Vector3(0.12f, 0.35f, 0.12f);
        handle.GetComponent<Renderer>().material = brownMat;
        Destroy(handle.GetComponent<Collider>());

        GameObject guard = GameObject.CreatePrimitive(PrimitiveType.Cube);
        guard.name = "Ranch Sword Guard";
        guard.transform.SetParent(ranchSwordPivot.transform);
        guard.transform.localPosition = new Vector3(0f, 0.65f, 0f);
        guard.transform.localRotation = Quaternion.identity;
        guard.transform.localScale = new Vector3(0.75f, 0.12f, 0.16f);
        guard.GetComponent<Renderer>().material = yellowMat;
        Destroy(guard.GetComponent<Collider>());

        GameObject blade = GameObject.CreatePrimitive(PrimitiveType.Cube);
        blade.name = "Ranch Sword Blade";
        blade.transform.SetParent(ranchSwordPivot.transform);
        blade.transform.localPosition = new Vector3(0f, 1.55f, 0f);
        blade.transform.localRotation = Quaternion.identity;
        blade.transform.localScale = new Vector3(0.24f, 1.7f, 0.10f);
        blade.GetComponent<Renderer>().material = ranchMat;
        Destroy(blade.GetComponent<Collider>());
    }

    private void CreateDrew()
    {
        drew = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        drew.name = "Drew";
        drew.transform.position = new Vector3(-6, 1.2f, -6);
        drew.transform.localScale = new Vector3(0.85f, 0.85f, 0.85f);
        drew.GetComponent<Renderer>().material = whiteMat;

        GameObject hat = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        hat.name = "Drew Hat";
        hat.transform.parent = drew.transform;
        hat.transform.localPosition = new Vector3(0, 1.25f, 0);
        hat.transform.localScale = new Vector3(0.85f, 0.12f, 0.85f);
        hat.GetComponent<Renderer>().material = yellowMat;

        drewTarget = ranchTree.transform.position;
        drew.SetActive(false);

        CreateLabel("DREW\nhelper NPC", new Vector3(-6, 2.8f, -6));
    }

    // ============================================================
    // PLAYER / CAMERA
    // ============================================================

    private void HandleMovement()
    {
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

        player.transform.Rotate(Vector3.up * mouseX);

        cameraPitch -= mouseY;
        cameraPitch = Mathf.Clamp(cameraPitch, -5f, 55f);

        float x = Input.GetAxis("Horizontal");
        float z = Input.GetAxis("Vertical");

        Vector3 move = player.transform.right * x + player.transform.forward * z;
        if (move.magnitude > 1f) move.Normalize();

        if (controller.isGrounded && yVelocity < 0f)
        {
            yVelocity = -2f;
        }

        yVelocity += gravity * Time.deltaTime;

        Vector3 velocity = move * moveSpeed;
        velocity.y = yVelocity;

        controller.Move(velocity * Time.deltaTime);
    }

    private void UpdateCamera()
    {
        if (mainCam == null || player == null) return;

        Quaternion camRotation = Quaternion.Euler(cameraPitch, player.transform.eulerAngles.y, 0);
        Vector3 offset = camRotation * new Vector3(0, 3.8f, -7f);

        mainCam.transform.position = player.transform.position + offset;
        mainCam.transform.LookAt(player.transform.position + new Vector3(0, 1.4f, 0));
    }

    private void HandleCursor()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        if (Input.GetMouseButtonDown(0))
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
    // COMBAT / RANCH SWORD
    // ============================================================

    private void HandleCombat()
    {
        swordCooldownTimer = Mathf.Max(0f, swordCooldownTimer - Time.deltaTime);
        hurtMessageCooldown = Mathf.Max(0f, hurtMessageCooldown - Time.deltaTime);

        if (swordAnimationTimer > 0f)
        {
            swordAnimationTimer -= Time.deltaTime;
            float progress = 1f - Mathf.Clamp01(swordAnimationTimer / swordAnimationLength);
            float swing = Mathf.Sin(progress * Mathf.PI);
            ranchSwordPivot.transform.localRotation = Quaternion.Slerp(
                swordRestRotation,
                swordAttackRotation,
                swing
            );
        }
        else if (ranchSwordPivot != null)
        {
            ranchSwordPivot.transform.localRotation = swordRestRotation;
        }

        if (Cursor.lockState == CursorLockMode.Locked &&
            Input.GetMouseButtonDown(0) &&
            swordCooldownTimer <= 0f)
        {
            SwingRanchSword();
        }
    }

    private void SwingRanchSword()
    {
        swordCooldownTimer = swordCooldown;
        swordAnimationTimer = swordAnimationLength;

        float damage = swordBaseDamage + bottleTier * 5f;
        int enemiesHit = 0;

        for (int i = activeEnemies.Count - 1; i >= 0; i--)
        {
            RanchEnemy enemy = activeEnemies[i];
            if (enemy == null)
            {
                activeEnemies.RemoveAt(i);
                continue;
            }

            Vector3 toEnemy = enemy.transform.position - player.transform.position;
            toEnemy.y = 0f;

            if (toEnemy.magnitude <= swordRange)
            {
                float facing = Vector3.Dot(player.transform.forward, toEnemy.normalized);
                if (facing >= -0.15f)
                {
                    enemy.TakeDamage(damage);
                    enemiesHit++;
                }
            }
        }

        if (enemiesHit > 0)
        {
            LogEvent("Ranch Sword hit " + enemiesHit + " enemy(s) for " + damage.ToString("F0") + " damage.");
        }
    }

    public Transform GetPlayerTransform()
    {
        return player != null ? player.transform : null;
    }

    public void DamagePlayer(float amount)
    {
        if (cjDefeated || playerHealth <= 0f) return;

        playerHealth = Mathf.Max(0f, playerHealth - amount);

        if (hurtMessageCooldown <= 0f)
        {
            LogEvent("A Ranch Raider hit you. Health: " + playerHealth.ToString("F0") + "/" + playerMaxHealth.ToString("F0"));
            hurtMessageCooldown = 1.2f;
        }

        if (playerHealth <= 0f)
        {
            RespawnPlayer();
        }
    }

    private void RespawnPlayer()
    {
        playerHealth = playerMaxHealth;
        money *= 0.75f;
        ranch *= 0.5f;

        if (controller != null) controller.enabled = false;
        player.transform.position = new Vector3(0f, 1.2f, -3f);
        if (controller != null) controller.enabled = true;

        for (int i = activeEnemies.Count - 1; i >= 0; i--)
        {
            if (activeEnemies[i] != null)
            {
                Destroy(activeEnemies[i].gameObject);
            }
        }
        activeEnemies.Clear();

        LogEvent("You were overwhelmed and respawned. You kept 75% of your money and half your raw Ranch.");
    }

    // ============================================================
    // INTERACTION
    // ============================================================

    private void UpdatePromptsAndInteractions()
    {
        currentPrompt = "";

        if (Near(ranchTree))
        {
            currentPrompt = "Hold E: Extract Ranch from the Ranch Tree";

            if (Input.GetKey(KeyCode.E))
            {
                ExtractRanch(Time.deltaTime);
            }
        }

        if (Near(bottleStation))
        {
            currentPrompt = "Press B: Bottle Ranch";

            if (Input.GetKeyDown(KeyCode.B))
            {
                BottleRanch();
            }
        }

        if (Near(sellStation))
        {
            currentPrompt = "Press F: Sell Bottle | Shift + F: Sell All";

            if (Input.GetKeyDown(KeyCode.F))
            {
                if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
                {
                    SellAllBottles();
                }
                else
                {
                    SellOneBottle();
                }
            }
        }

        if (Near(upgradeStation))
        {
            currentPrompt = "Press U: Upgrade Bottle Size";

            if (Input.GetKeyDown(KeyCode.U))
            {
                UpgradeBottle();
            }
        }

        if (Near(drewStation))
        {
            currentPrompt = "Press G: Hire / Upgrade Drew";

            if (Input.GetKeyDown(KeyCode.G))
            {
                UpgradeDrew();
            }
        }

        if (Near(cjGate))
        {
            if (cjBattleUnlocked)
            {
                currentPrompt = "Press C: Challenge CJ, the Ultimate Ranchenator";

                if (Input.GetKeyDown(KeyCode.C))
                {
                    ChallengeCJ();
                }
            }
            else
            {
                currentPrompt = "CJ Gate: Sell more Ranch before challenging CJ.";
            }
        }
    }

    private bool Near(GameObject obj)
    {
        if (obj == null || player == null) return false;
        return Vector3.Distance(player.transform.position, obj.transform.position) <= interactRange;
    }

    private void ExtractRanch(float deltaTime)
    {
        float amount = ranchPerSecond * extractionMultiplier * deltaTime;
        ranch += amount;

        if (Random.value < 0.015f)
        {
            LogEvent("The Ranch Tree releases a creamy burst of raw Ranch.");
        }
    }

    private bool BottleRanch()
    {
        int cap = bottleCaps[bottleTier];

        if (ranch >= cap)
        {
            ranch -= cap;
            bottles += 1;
            LogEvent("Bottled " + cap + " Ranch into a " + bottleNames[bottleTier] + ".");
            return true;
        }

        LogEvent("Not enough Ranch to fill a " + bottleNames[bottleTier] + ". Need " + cap + ".");
        return false;
    }

    private void SellOneBottle()
    {
        if (bottles <= 0)
        {
            LogEvent("No bottled Ranch to sell.");
            return;
        }

        bottles -= 1;
        SellBottleReward(1);
    }

    private void SellAllBottles()
    {
        if (bottles <= 0)
        {
            LogEvent("No bottled Ranch to sell.");
            return;
        }

        int amount = bottles;
        bottles = 0;
        SellBottleReward(amount);
    }

    private void SellBottleReward(int amount)
    {
        int cap = bottleCaps[bottleTier];

        float basePrice = cap * 5f;
        float bottleBonus = 1f + bottleTier * 0.22f;
        float drewReputationBonus = 1f + drewLevel * 0.03f;

        float reward = amount * basePrice * bottleBonus * drewReputationBonus;

        money += reward;
        bottlesSold += amount;

        cjHeat += amount * cap;

        LogEvent("Sold " + amount + " bottle(s) for $" + reward.ToString("F0") + ".");

        if (bottlesSold == 1)
        {
            LogEvent("Your first bottle sells. A local McMinnville customer says, 'This is alarmingly good.'");
        }
    }

    private void UpgradeBottle()
    {
        if (bottleTier >= bottleNames.Length - 1)
        {
            LogEvent("Bottle size is maxed. You possess the Ranchenator Vessel.");
            return;
        }

        float cost = GetBottleUpgradeCost();

        if (money >= cost)
        {
            money -= cost;
            bottleTier += 1;
            LogEvent("Bottle upgraded to " + bottleNames[bottleTier] + ". Capacity: " + bottleCaps[bottleTier] + " Ranch.");

            // Bigger bottles also impress the market.
            cjHeat += 5 * bottleTier;
        }
        else
        {
            LogEvent("Need $" + cost.ToString("F0") + " to upgrade bottles.");
        }
    }

    private float GetBottleUpgradeCost()
    {
        return 80f * (bottleTier + 1) * (bottleTier + 1);
    }

    private void UpgradeDrew()
    {
        if (!drewUnlocked)
        {
            if (money >= drewUpgradeCost)
            {
                money -= drewUpgradeCost;
                drewUnlocked = true;
                drewLevel = 1;
                drew.SetActive(true);
                LogEvent("Drew hired. He looks confused but enthusiastic.");
            }
            else
            {
                LogEvent("Need $" + drewUpgradeCost.ToString("F0") + " to hire Drew.");
            }

            return;
        }

        if (drewLevel >= 10)
        {
            LogEvent("Drew is max level. He has achieved Advanced Ranching.");
            return;
        }

        float cost = GetDrewUpgradeCost();

        if (money >= cost)
        {
            money -= cost;
            drewLevel += 1;
            drewWorkInterval = Mathf.Max(1.2f, drewWorkInterval - 0.45f);
            LogEvent("Drew upgraded to Level " + drewLevel + ". Drew understands Ranch slightly better.");
        }
        else
        {
            LogEvent("Need $" + cost.ToString("F0") + " to upgrade Drew.");
        }
    }

    private float GetDrewUpgradeCost()
    {
        return drewUpgradeCost + drewLevel * drewLevel * 85f;
    }

    // ============================================================
    // DREW
    // ============================================================

    private void UpdateDrew()
    {
        if (!drewUnlocked || drew == null) return;

        drewTimer += Time.deltaTime;

        if (drewTimer >= drewWorkInterval)
        {
            drewTimer = 0f;
            DrewWork();
        }

        // Simple visual movement.
        Vector3 target = drewMovingToTree ? ranchTree.transform.position : bottleStation.transform.position;
        target.y = drew.transform.position.y;

        drew.transform.position = Vector3.MoveTowards(
            drew.transform.position,
            target,
            Time.deltaTime * (1.8f + drewLevel * 0.15f)
        );

        Vector3 dir = target - drew.transform.position;
        if (dir.sqrMagnitude > 0.01f)
        {
            drew.transform.rotation = Quaternion.LookRotation(dir);
        }

        if (Vector3.Distance(drew.transform.position, target) < 0.3f)
        {
            drewMovingToTree = !drewMovingToTree;
        }
    }

    private void DrewWork()
    {
        float drewRanch = drewLevel * 0.75f;
        ranch += drewRanch;

        int bottleAttempts = Mathf.Max(1, drewLevel / 2);

        int made = 0;
        for (int i = 0; i < bottleAttempts; i++)
        {
            if (BottleRanch())
            {
                made++;
            }
        }

        if (made > 0)
        {
            LogEvent("Drew extracted " + drewRanch.ToString("F1") + " Ranch and bottled " + made + " bottle(s).");
        }
        else
        {
            LogEvent("Drew extracted " + drewRanch.ToString("F1") + " Ranch. He did not bottle anything useful.");
        }
    }

    // ============================================================
    // ENEMY WAVES
    // ============================================================

    private void UpdateEnemyWaves()
    {
        enemyWaveTimer += Time.deltaTime;

        if (Input.GetKeyDown(KeyCode.T))
        {
            SpawnEnemyWave(true);
        }

        if (enemyWaveTimer >= enemyWaveInterval)
        {
            enemyWaveTimer = 0f;
            SpawnEnemyWave(false);
        }
    }

    private void SpawnEnemyWave(bool testWave)
    {
        enemyWaveNumber += 1;
        int enemyCount = testWave ? 2 : Mathf.Min(3 + enemyWaveNumber, 12);

        for (int i = 0; i < enemyCount; i++)
        {
            float angle = Random.Range(0f, Mathf.PI * 2f);
            float radius = Random.Range(20f, 25f);
            Vector3 spawnPosition = new Vector3(
                Mathf.Cos(angle) * radius,
                1.1f,
                Mathf.Sin(angle) * radius
            );

            CreateEnemy(spawnPosition);
        }

        string waveType = testWave ? "Test wave" : "Five-minute wave";
        LogEvent(waveType + " " + enemyWaveNumber + ": " + enemyCount + " Ranch Raiders are attacking!");
    }

    private void CreateEnemy(Vector3 position)
    {
        GameObject enemyObject = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        enemyObject.name = "Ranch Raider";
        enemyObject.transform.position = position;
        enemyObject.transform.localScale = new Vector3(1.0f, 1.0f, 1.0f);
        enemyObject.GetComponent<Renderer>().material = purpleMat;

        GameObject eyeLeft = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        eyeLeft.name = "Raider Eye Left";
        eyeLeft.transform.SetParent(enemyObject.transform);
        eyeLeft.transform.localPosition = new Vector3(-0.20f, 0.42f, 0.43f);
        eyeLeft.transform.localScale = new Vector3(0.13f, 0.13f, 0.13f);
        eyeLeft.GetComponent<Renderer>().material = redMat;
        Destroy(eyeLeft.GetComponent<Collider>());

        GameObject eyeRight = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        eyeRight.name = "Raider Eye Right";
        eyeRight.transform.SetParent(enemyObject.transform);
        eyeRight.transform.localPosition = new Vector3(0.20f, 0.42f, 0.43f);
        eyeRight.transform.localScale = new Vector3(0.13f, 0.13f, 0.13f);
        eyeRight.GetComponent<Renderer>().material = redMat;
        Destroy(eyeRight.GetComponent<Collider>());

        GameObject stolenBottle = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        stolenBottle.name = "Stolen Ranch Bottle";
        stolenBottle.transform.SetParent(enemyObject.transform);
        stolenBottle.transform.localPosition = new Vector3(0.62f, 0f, 0f);
        stolenBottle.transform.localScale = new Vector3(0.18f, 0.45f, 0.18f);
        stolenBottle.GetComponent<Renderer>().material = ranchMat;
        Destroy(stolenBottle.GetComponent<Collider>());

        RanchEnemy enemy = enemyObject.AddComponent<RanchEnemy>();
        float health = 40f + enemyWaveNumber * 12f;
        float speed = Mathf.Min(2.4f + enemyWaveNumber * 0.12f, 4.5f);
        float damage = Mathf.Min(7f + enemyWaveNumber * 1.5f, 22f);
        enemy.Initialize(this, health, speed, damage);

        activeEnemies.Add(enemy);
    }

    public void OnEnemyDefeated(RanchEnemy enemy)
    {
        activeEnemies.Remove(enemy);
        enemiesDefeated += 1;

        float reward = 25f + enemyWaveNumber * 8f;
        money += reward;
        ranch += 2f + enemyWaveNumber * 0.5f;
        cjHeat += 10;

        LogEvent("Ranch Raider defeated. Earned $" + reward.ToString("F0") + " and recovered stolen Ranch.");

        if (activeEnemies.Count == 0)
        {
            playerHealth = Mathf.Min(playerMaxHealth, playerHealth + 20f);
            LogEvent("Wave cleared. You recovered 20 health.");
        }
    }

    private string GetWaveCountdownText()
    {
        float remaining = Mathf.Max(0f, enemyWaveInterval - enemyWaveTimer);
        int minutes = Mathf.FloorToInt(remaining / 60f);
        int seconds = Mathf.FloorToInt(remaining % 60f);
        return minutes.ToString("0") + ":" + seconds.ToString("00");
    }

    // ============================================================
    // CJ SYSTEM
    // ============================================================

    private void UpdateCJ()
    {
        if (cjHeat >= 250 && !cjHasWarned)
        {
            cjHasWarned = true;
            LogEvent("CJ: Cute little ranch operation you've got there.");
        }

        if (cjHeat >= 1500 && !cjBattleUnlocked)
        {
            cjBattleUnlocked = true;
            LogEvent("CJ Battle Unlocked. Go to the black CJ Gate and press C.");
        }
    }

    private void ChallengeCJ()
    {
        float playerPower =
            money +
            ranch * 2f +
            bottles * bottleCaps[bottleTier] * 8f +
            bottlesSold * 12f +
            drewLevel * 500f +
            bottleTier * 900f +
            treeLevel * 600f;

        if (playerPower >= cjPower)
        {
            cjDefeated = true;
            LogEvent("You defeated CJ, the Ultimate Ranchenator.");
        }
        else
        {
            float missing = cjPower - playerPower;
            LogEvent("CJ defeats you economically. Gain about " + missing.ToString("F0") + " more Ranch Power.");
            cjHeat += 100;
        }
    }

    // ============================================================
    // HUD / UI
    // ============================================================

    private void OnGUI()
    {
        GUIStyle big = new GUIStyle(GUI.skin.label);
        big.fontSize = 22;
        big.normal.textColor = Color.white;

        GUIStyle box = new GUIStyle(GUI.skin.box);
        box.fontSize = 18;
        box.alignment = TextAnchor.UpperLeft;

        GUIStyle title = new GUIStyle(GUI.skin.label);
        title.fontSize = 34;
        title.fontStyle = FontStyle.Bold;
        title.normal.textColor = Color.white;
        title.alignment = TextAnchor.MiddleCenter;

        GUI.Box(new Rect(15, 15, 380, 340), "");
        GUI.Label(new Rect(30, 25, 330, 30), "RANCH SIMULATOR", big);
        GUI.Label(new Rect(30, 65, 330, 30), "Raw Ranch: " + ranch.ToString("F1"), big);
        GUI.Label(new Rect(30, 95, 330, 30), "Bottles: " + bottles, big);
        GUI.Label(new Rect(30, 125, 330, 30), "Money: $" + money.ToString("F0"), big);
        GUI.Label(new Rect(30, 155, 330, 30), "Bottle: " + bottleNames[bottleTier] + " (" + bottleCaps[bottleTier] + ")", big);
        GUI.Label(new Rect(30, 185, 330, 30), "Drew Level: " + drewLevel, big);
        GUI.Label(new Rect(30, 215, 350, 30), "CJ Heat: " + cjHeat, big);
        GUI.Label(new Rect(30, 245, 350, 30), "Health: " + playerHealth.ToString("F0") + "/" + playerMaxHealth.ToString("F0"), big);
        GUI.Label(new Rect(30, 275, 350, 30), "Enemies: " + activeEnemies.Count + " | Defeated: " + enemiesDefeated, big);
        GUI.Label(new Rect(30, 305, 350, 30), "Next wave: " + GetWaveCountdownText(), big);

        GUI.Box(new Rect(Screen.width - 410, 15, 395, 250), "");
        GUI.Label(new Rect(Screen.width - 390, 35, 370, 30), "Next Upgrades / Combat", big);

        string bottleUpgradeText = bottleTier < bottleNames.Length - 1
            ? "Bottle Upgrade: $" + GetBottleUpgradeCost().ToString("F0")
            : "Bottle Upgrade: MAX";

        string drewUpgradeText = !drewUnlocked
            ? "Hire Drew: $" + drewUpgradeCost.ToString("F0")
            : drewLevel >= 10
                ? "Drew Upgrade: MAX"
                : "Drew Upgrade: $" + GetDrewUpgradeCost().ToString("F0");

        GUI.Label(new Rect(Screen.width - 390, 80, 370, 30), bottleUpgradeText, big);
        GUI.Label(new Rect(Screen.width - 390, 115, 370, 30), drewUpgradeText, big);
        GUI.Label(new Rect(Screen.width - 390, 150, 370, 30), cjBattleUnlocked ? "CJ Gate: UNLOCKED" : "CJ Gate: locked", big);
        GUI.Label(new Rect(Screen.width - 390, 185, 370, 30), "Ranch Sword Damage: " + (swordBaseDamage + bottleTier * 5f).ToString("F0"), big);
        GUI.Label(new Rect(Screen.width - 390, 215, 370, 30), "Left click: attack | T: test wave", big);

        if (!string.IsNullOrEmpty(currentPrompt))
        {
            GUI.Box(new Rect(Screen.width / 2 - 330, Screen.height - 90, 660, 55), "");
            GUI.Label(new Rect(Screen.width / 2 - 310, Screen.height - 78, 620, 30), currentPrompt, big);
        }

        if (!string.IsNullOrEmpty(lastEvent))
        {
            GUI.Box(new Rect(15, Screen.height - 120, 720, 95), "");
            GUI.Label(new Rect(30, Screen.height - 105, 690, 65), lastEvent, big);
        }

        if (cjDefeated)
        {
            GUI.Box(new Rect(Screen.width / 2 - 360, Screen.height / 2 - 160, 720, 320), "");
            GUI.Label(new Rect(Screen.width / 2 - 340, Screen.height / 2 - 130, 680, 60), "CJ HAS BEEN OVERTHROWN", title);
            GUI.Label(new Rect(Screen.width / 2 - 310, Screen.height / 2 - 45, 620, 40), "CJ: You have become... the Ranch Simulator.", big);
            GUI.Label(new Rect(Screen.width / 2 - 310, Screen.height / 2 + 5, 620, 40), "Drew: There is another.", big);
            GUI.Label(new Rect(Screen.width / 2 - 310, Screen.height / 2 + 75, 620, 40), "Press R to restart.", big);
        }
    }

    private void LogEvent(string message)
    {
        lastEvent = message;
        eventTimer = 5f;
        Debug.Log(message);
    }

    private void UpdateEventTimer()
    {
        if (eventTimer > 0f)
        {
            eventTimer -= Time.deltaTime;
        }
    }

    // ============================================================
    // UNUSED BUT READY FOR EXPANSION
    // ============================================================

    private void UpgradeTree()
    {
        if (money >= treeUpgradeCost)
        {
            money -= treeUpgradeCost;
            treeLevel += 1;
            ranchPerSecond += 0.7f;
            extractionMultiplier += 0.2f;
            treeUpgradeCost *= 2.1f;
            LogEvent("Ranch Tree upgraded to Level " + treeLevel + ".");
        }
    }
}

/// <summary>
/// Simple enemy controller used by the generated Ranch Raiders.
/// </summary>
public class RanchEnemy : MonoBehaviour
{
    private RanchSimulatorFullGame game;
    private Transform player;
    private float health;
    private float moveSpeed;
    private float attackDamage;
    private float attackRange = 1.7f;
    private float attackCooldown = 1.1f;
    private float attackTimer = 0f;
    private bool defeated = false;

    public void Initialize(
        RanchSimulatorFullGame owner,
        float startingHealth,
        float speed,
        float damage
    )
    {
        game = owner;
        player = owner.GetPlayerTransform();
        health = startingHealth;
        moveSpeed = speed;
        attackDamage = damage;
    }

    private void Update()
    {
        if (defeated || game == null || player == null) return;

        attackTimer = Mathf.Max(0f, attackTimer - Time.deltaTime);

        Vector3 target = player.position;
        target.y = transform.position.y;
        Vector3 direction = target - transform.position;
        float distance = direction.magnitude;

        if (distance > attackRange)
        {
            transform.position = Vector3.MoveTowards(
                transform.position,
                target,
                moveSpeed * Time.deltaTime
            );
        }
        else if (attackTimer <= 0f)
        {
            attackTimer = attackCooldown;
            game.DamagePlayer(attackDamage);
        }

        if (direction.sqrMagnitude > 0.01f)
        {
            transform.rotation = Quaternion.LookRotation(direction.normalized);
        }
    }

    public void TakeDamage(float amount)
    {
        if (defeated) return;

        health -= amount;
        transform.localScale = Vector3.one * 0.92f;

        if (health <= 0f)
        {
            defeated = true;
            game.OnEnemyDefeated(this);
            Destroy(gameObject);
        }
    }
}

/// <summary>
/// Makes world labels face the camera.
/// This can live in the same file as the main game script.
/// </summary>
public class LabelBillboard : MonoBehaviour
{
    private void LateUpdate()
    {
        Camera cam = Camera.main;
        if (cam == null) return;

        transform.LookAt(transform.position + cam.transform.rotation * Vector3.forward,
                         cam.transform.rotation * Vector3.up);
    }
}
