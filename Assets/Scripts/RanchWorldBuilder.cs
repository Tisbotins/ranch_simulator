using System.Collections.Generic;
using UnityEngine;

public class RanchWorldBuilder : MonoBehaviour
{
    private RanchGameCore core;
    private bool built;

    private Material green;
    private Material lightGreen;
    private Material sand;
    private Material stone;
    private Material brown;
    private Material blue;
    private Material yellow;
    private Material red;
    private Material white;
    private Material black;
    private Material ranch;
    private Material purple;
    private Material teal;
    private Material gray;
    private Material gold;
    private Material translucentWhite;

    public void Initialize(RanchGameCore gameCore)
    {
        core = gameCore;
    }

    public void BuildWorld()
    {
        if (built || core == null)
            return;

        built = true;
        CreateMaterials();

        Transform world = new GameObject("Generated Ranch World").transform;
        Transform enemies = new GameObject("Ranch Raiders").transform;
        enemies.SetParent(world);

        CreateExpandedGround(world);
        CreateAreaBarriers(world);

        Transform tree = CreateRanchTree(world);
        GameObject bottleStation = CreateStation(
            world,
            "Bottle Station",
            new Vector3(-13f, 0.5f, 0f),
            blue,
            RanchStationType.Bottle,
            "BOTTLE STATION"
        );

        CreateStation(
            world,
            "Sell Station",
            new Vector3(13f, 0.5f, 0f),
            yellow,
            RanchStationType.Sell,
            "SELL STATION"
        );

        CreateStation(
            world,
            "Bottle Upgrade Station",
            new Vector3(-2f, 0.5f, -15f),
            red,
            RanchStationType.BottleUpgrade,
            "BOTTLE UPGRADES"
        );

        CreateStation(
            world,
            "Drew Station",
            new Vector3(-15f, 0.5f, -15f),
            white,
            RanchStationType.Drew,
            "DREW STATION"
        );

        CreateStation(
            world,
            "Ranch Empire Shop Terminal",
            new Vector3(13f, 0.8f, -15f),
            teal,
            RanchStationType.Shop,
            "RANCH EMPIRE SHOP\nPress E or P"
        );

        CreateStation(
            world,
            "CJ Gate",
            new Vector3(160f, 0.8f, 24f),
            black,
            RanchStationType.CJGate,
            "CJ GATE"
        );

        Transform arenaPlayerStart;
        Transform arenaBossSpawn;
        Transform cjArena = CreateCJArena(world, out arenaPlayerStart, out arenaBossSpawn);

        RanchPlayerController player = CreatePlayer(world);
        GameObject drew = CreateDrew(world);
        CreateEmpireVisual(world);
        CreateDecorations(world);

        core.Tree.RegisterTreeVisual(tree);
        core.Drew.RegisterVisual(drew, tree, bottleStation.transform);
        core.Waves.RegisterWorld(tree, enemies, player.transform);
        core.RegisterWorld(player, tree);
        core.Shop.RegisterPlayer(player);
        core.CJ.RegisterArena(cjArena, arenaPlayerStart, arenaBossSpawn);
    }

    /// <summary>
    /// Every primitive in the game goes through here. It now delegates to
    /// RanchVisuals, which gives each surface a real finish (instead of Unity's
    /// default flat 0.5 smoothness) and caches materials so the world builder
    /// stops allocating a Material and running Shader.Find per primitive.
    /// </summary>
    public static Material CreateRuntimeMaterial(Color color)
    {
        return RanchVisuals.GetMaterial(color, RanchVisuals.InferSurface(color));
    }

    /// <summary>Explicit finish, for surfaces where the guess is not good enough.</summary>
    public static Material CreateRuntimeMaterial(
        Color color,
        RanchVisuals.Surface surface)
    {
        return RanchVisuals.GetMaterial(color, surface);
    }

    private static Material CreateTransparentMaterial(Color color)
    {
        // Must be unshared: this mutates blend mode and render queue, and
        // RanchVisuals.GetMaterial hands back a cached material that other
        // opaque objects are using.
        Material material =
            RanchVisuals.CreateUnique(color, RanchVisuals.Surface.Glossy);

        if (material.HasProperty("_Mode"))
        {
            material.SetFloat("_Mode", 3f);
            material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetInt("_ZWrite", 0);
            material.DisableKeyword("_ALPHATEST_ON");
            material.EnableKeyword("_ALPHABLEND_ON");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        }

        if (material.HasProperty("_Surface"))
            material.SetFloat("_Surface", 1f);

        material.renderQueue = 3000;
        return material;
    }

    private void CreateMaterials()
    {
        green = CreateRuntimeMaterial(new Color(0.25f, 0.65f, 0.25f));
        lightGreen = CreateRuntimeMaterial(new Color(0.38f, 0.72f, 0.34f));
        sand = CreateRuntimeMaterial(new Color(0.60f, 0.54f, 0.34f));
        stone = CreateRuntimeMaterial(new Color(0.40f, 0.42f, 0.46f));
        brown = CreateRuntimeMaterial(new Color(0.35f, 0.20f, 0.09f));
        blue = CreateRuntimeMaterial(new Color(0.20f, 0.45f, 0.90f));
        yellow = CreateRuntimeMaterial(new Color(0.95f, 0.75f, 0.25f));
        red = CreateRuntimeMaterial(new Color(0.85f, 0.20f, 0.15f));
        white = CreateRuntimeMaterial(new Color(0.90f, 0.90f, 0.85f));
        black = CreateRuntimeMaterial(new Color(0.05f, 0.05f, 0.05f));
        ranch = CreateRuntimeMaterial(new Color(1f, 0.92f, 0.68f));
        purple = CreateRuntimeMaterial(new Color(0.55f, 0.25f, 0.80f));
        teal = CreateRuntimeMaterial(new Color(0.08f, 0.60f, 0.55f));
        gray = CreateRuntimeMaterial(new Color(0.32f, 0.34f, 0.38f));
        gold = CreateRuntimeMaterial(new Color(1f, 0.75f, 0.12f));
        translucentWhite = CreateTransparentMaterial(new Color(1f, 1f, 1f, 0.28f));
    }

    private void CreateExpandedGround(Transform parent)
    {
        CreateFloorSlab(parent, "Ranch Homestead Floor", new Vector3(-5f, -0.5f, 0f), new Vector3(80f, 1f, 90f), green);
        CreateFloorSlab(parent, "Laboratory District Floor", new Vector3(57.5f, -0.5f, 0f), new Vector3(45f, 1f, 90f), lightGreen);
        CreateFloorSlab(parent, "Industrial Expanse Floor", new Vector3(105f, -0.5f, 0f), new Vector3(50f, 1f, 90f), sand);
        CreateFloorSlab(parent, "Citadel Grounds Floor", new Vector3(155f, -0.5f, 0f), new Vector3(50f, 1f, 90f), stone);

        CreateWall(parent, "West Perimeter", new Vector3(-45f, 2.5f, 0f), new Vector3(1f, 5f, 90f), translucentWhite);
        CreateWall(parent, "East Perimeter", new Vector3(180f, 2.5f, 0f), new Vector3(1f, 5f, 90f), translucentWhite);
        CreateWall(parent, "North Perimeter", new Vector3(67.5f, 2.5f, 45f), new Vector3(226f, 5f, 1f), translucentWhite);
        CreateWall(parent, "South Perimeter", new Vector3(67.5f, 2.5f, -45f), new Vector3(226f, 5f, 1f), translucentWhite);
    }

    private void CreateFloorSlab(Transform parent, string name, Vector3 position, Vector3 scale, Material material)
    {
        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.name = name;
        floor.transform.SetParent(parent);
        floor.transform.position = position;
        floor.transform.localScale = scale;
        floor.GetComponent<Renderer>().material = material;
    }

    private void CreateAreaBarriers(Transform parent)
    {
        CreateAreaGate(parent, 1, 35f);
        CreateAreaGate(parent, 2, 80f);
        CreateAreaGate(parent, 3, 130f);
    }

    private void CreateAreaGate(Transform parent, int areaIndex, float xPosition)
    {
        Transform root = new GameObject(core.Areas.GetAreaName(areaIndex) + " Barrier").transform;
        root.SetParent(parent);
        root.position = new Vector3(xPosition, 0f, 0f);

        CreateWall(root, "Barrier South Segment", new Vector3(0f, 2.5f, -25.5f), new Vector3(0.8f, 5f, 39f), translucentWhite, true);
        CreateWall(root, "Barrier North Segment", new Vector3(0f, 2.5f, 25.5f), new Vector3(0.8f, 5f, 39f), translucentWhite, true);

        GameObject blocker = GameObject.CreatePrimitive(PrimitiveType.Cube);
        blocker.name = "Unlockable White Gate";
        blocker.transform.SetParent(root, false);
        blocker.transform.localPosition = new Vector3(0f, 2.5f, 0f);
        blocker.transform.localScale = new Vector3(0.8f, 5f, 12f);
        blocker.GetComponent<Renderer>().material = translucentWhite;

        BoxCollider interaction = root.gameObject.AddComponent<BoxCollider>();
        interaction.center = new Vector3(-1.5f, 2f, 0f);
        interaction.size = new Vector3(5f, 4f, 14f);
        interaction.isTrigger = true;

        TextMesh label = CreateLabel(
            root,
            core.Areas.GetAreaName(areaIndex).ToUpperInvariant(),
            new Vector3(0f, 7f, 0f),
            false
        );
        label.color = Color.white;

        RanchAreaGate gate = root.gameObject.AddComponent<RanchAreaGate>();
        gate.Initialize(core, areaIndex, blocker, label);
    }

    private void CreateWall(
        Transform parent,
        string name,
        Vector3 position,
        Vector3 scale,
        Material material,
        bool local = false)
    {
        GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.name = name;
        wall.transform.SetParent(parent, false);

        if (local)
            wall.transform.localPosition = position;
        else
            wall.transform.position = position;

        wall.transform.localScale = scale;
        wall.GetComponent<Renderer>().material = material;
    }

    private Transform CreateRanchTree(Transform parent)
    {
        GameObject root = new GameObject("Ranch Tree");
        root.transform.SetParent(parent);
        root.transform.position = new Vector3(0f, 0f, 12f);

        RanchTreeInteractable interactable = root.AddComponent<RanchTreeInteractable>();
        interactable.Initialize(core.Tree);

        CreatePrimitive(root.transform, PrimitiveType.Cylinder, "Ranch Tree Trunk", new Vector3(0f, 1.5f, 0f), new Vector3(0.8f, 1.8f, 0.8f), brown, true);
        CreatePrimitive(root.transform, PrimitiveType.Sphere, "Creamy Ranch Crown", new Vector3(0f, 4.1f, 0f), new Vector3(3f, 2.2f, 3f), ranch, true);
        CreatePrimitive(root.transform, PrimitiveType.Cube, "Ranch Tap", new Vector3(0f, 2.1f, -0.75f), new Vector3(0.35f, 0.25f, 1f), blue, true);

        BoxCollider trigger = root.AddComponent<BoxCollider>();
        trigger.center = new Vector3(0f, 2f, 0f);
        trigger.size = new Vector3(3f, 5f, 3f);
        trigger.isTrigger = true;

        CreateLabel(root.transform, "RANCH TREE\nHold E to extract", new Vector3(0f, 6f, 0f), true);
        return root.transform;
    }

    private GameObject CreateStation(
        Transform parent,
        string objectName,
        Vector3 position,
        Material material,
        RanchStationType type,
        string label)
    {
        GameObject station = GameObject.CreatePrimitive(PrimitiveType.Cube);
        station.name = objectName;
        station.transform.SetParent(parent);
        station.transform.position = position;
        station.transform.localScale = new Vector3(2.8f, type == RanchStationType.Shop ? 1.8f : 1f, 2.8f);
        station.GetComponent<Renderer>().material = material;

        RanchStation component = station.AddComponent<RanchStation>();
        component.Initialize(core, type);

        TextMesh labelMesh = CreateLabel(
            station.transform,
            label,
            new Vector3(0f, type == RanchStationType.Shop ? 2.5f : 2f, 0f),
            false
        );

        if (type == RanchStationType.CJGate)
        {
            labelMesh.color = Color.white;
            core.CJ.RegisterGateVisual(station.transform, labelMesh);
        }

        return station;
    }

    private RanchPlayerController CreatePlayer(Transform parent)
    {
        // The Player root remains the real gameplay object. Movement, collision,
        // combat, saving, and the camera all continue to use this object.
        GameObject player = new GameObject("Player");
        player.transform.SetParent(parent);
        player.transform.position = new Vector3(0f, 1.1f, -10f);

        CharacterController controller = player.AddComponent<CharacterController>();
        controller.height = 2f;
        controller.radius = 0.45f;
        controller.center = Vector3.zero;
        controller.skinWidth = 0.08f;
        controller.stepOffset = 0.35f;

        // Load the editable wrapper prefab from:
        // Assets/Resources/Prefabs/PlayerModel.prefab
        GameObject playerPrefab = Resources.Load<GameObject>("Prefabs/PlayerModel");
        GameObject playerModel = null;

        if (playerPrefab != null)
        {
            playerModel = Instantiate(playerPrefab, player.transform);
            playerModel.name = "Player Character Model";
            playerModel.transform.localPosition = Vector3.zero;
            playerModel.transform.localRotation = Quaternion.identity;

            // Preserve the position, rotation, and scale saved inside the prefab.
            // Adjust the imported model child inside PlayerModel.prefab if needed.
            CleanImportedCharacterModel(playerModel);

            Debug.Log(
                "Player custom model loaded from " +
                "Resources/Prefabs/PlayerModel."
            );
        }
        else
        {
            Debug.LogWarning(
                "PlayerModel.prefab was not found. Expected path: " +
                "Assets/Resources/Prefabs/PlayerModel.prefab"
            );

            // Keep the original capsule as a visible fallback when the custom
            // player prefab has not been created yet.
            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Fallback Player Body";
            body.transform.SetParent(player.transform, false);
            body.GetComponent<Renderer>().material = blue;

            Collider bodyCollider = body.GetComponent<Collider>();
            if (bodyCollider != null)
                Destroy(bodyCollider);
        }

        // A PlayerModel prefab may optionally contain an object named
        // RightHandAnchor. Put it under the correct hand bone if you want the
        // generated equipment to follow that hand. Otherwise the old fallback
        // anchor is created on the Player root.
        Transform handAnchor =
            playerModel != null
                ? FindChildRecursive(playerModel.transform, "RightHandAnchor")
                : null;

        if (handAnchor == null)
        {
            GameObject handAnchorObject =
                new GameObject("Right Hand Equipment Anchor");

            handAnchorObject.transform.SetParent(player.transform, false);
            handAnchorObject.transform.localPosition =
                new Vector3(0.72f, 0.05f, 0.05f);

            handAnchor = handAnchorObject.transform;
        }

        Transform sword = CreateSword(handAnchor);
        Transform spear = CreateSpear(handAnchor);
        Transform bow = CreateBow(handAnchor);
        Transform extractor = CreateExtractor(handAnchor);
        Transform trap = CreateHeldTrap(handAnchor);
        Transform wand = CreateWand(handAnchor);

        Camera camera = Camera.main;
        if (camera == null)
        {
            GameObject cameraObject = new GameObject("Main Camera");
            camera = cameraObject.AddComponent<Camera>();
            cameraObject.tag = "MainCamera";
        }

        RanchPlayerController playerController =
            player.AddComponent<RanchPlayerController>();

        playerController.Initialize(core, controller, camera);
        core.Equipment.RegisterVisuals(extractor, sword, spear, bow, trap, wand);
        return playerController;
    }

    private void CleanImportedCharacterModel(GameObject modelRoot)
    {
        // Imported cameras and listeners can take over the gameplay view/audio.
        Camera[] importedCameras =
            modelRoot.GetComponentsInChildren<Camera>(true);

        foreach (Camera importedCamera in importedCameras)
        {
            importedCamera.enabled = false;
            Destroy(importedCamera);
        }

        AudioListener[] importedListeners =
            modelRoot.GetComponentsInChildren<AudioListener>(true);

        foreach (AudioListener importedListener in importedListeners)
        {
            importedListener.enabled = false;
            Destroy(importedListener);
        }

        Light[] importedLights =
            modelRoot.GetComponentsInChildren<Light>(true);

        foreach (Light importedLight in importedLights)
        {
            importedLight.enabled = false;
            Destroy(importedLight);
        }

        // The Player root's CharacterController is the only gameplay collider.
        // Extra model colliders or rigidbodies can make movement jitter or fail.
        Collider[] importedColliders =
            modelRoot.GetComponentsInChildren<Collider>(true);

        foreach (Collider importedCollider in importedColliders)
        {
            importedCollider.enabled = false;
            Destroy(importedCollider);
        }

        Rigidbody[] importedRigidbodies =
            modelRoot.GetComponentsInChildren<Rigidbody>(true);

        foreach (Rigidbody importedRigidbody in importedRigidbodies)
        {
            importedRigidbody.isKinematic = true;
            importedRigidbody.detectCollisions = false;
            Destroy(importedRigidbody);
        }

        // Prevent an imported object from being mistaken for MainCamera or
        // another tagged gameplay object. Animators and renderers are retained.
        Transform[] importedObjects =
            modelRoot.GetComponentsInChildren<Transform>(true);

        foreach (Transform importedObject in importedObjects)
            importedObject.gameObject.tag = "Untagged";
    }

    private Transform FindChildRecursive(Transform root, string childName)
    {
        if (root == null)
            return null;

        if (root.name == childName)
            return root;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform result =
                FindChildRecursive(root.GetChild(i), childName);

            if (result != null)
                return result;
        }

        return null;
    }

    private Transform CreateSword(Transform anchor)
    {
        GameObject root = new GameObject("Ranch Defender Sword");
        root.transform.SetParent(anchor, false);
        root.transform.localRotation = Quaternion.Euler(20f, 0f, -25f);
        CreatePrimitive(root.transform, PrimitiveType.Cube, "Sword Blade", new Vector3(0f, 0.55f, 0f), new Vector3(0.12f, 1.2f, 0.08f), white, false);
        CreatePrimitive(root.transform, PrimitiveType.Cube, "Sword Handle", new Vector3(0f, -0.15f, 0f), new Vector3(0.18f, 0.35f, 0.14f), brown, false);
        CreatePrimitive(root.transform, PrimitiveType.Cube, "Sword Guard", new Vector3(0f, 0.05f, 0f), new Vector3(0.55f, 0.08f, 0.12f), yellow, false);
        return root.transform;
    }

    private Transform CreateSpear(Transform anchor)
    {
        GameObject root = new GameObject("Ranch Spear");
        root.transform.SetParent(anchor, false);
        root.transform.localPosition = new Vector3(0f, 0.15f, 0.1f);
        root.transform.localRotation = Quaternion.Euler(75f, 0f, 0f);
        CreatePrimitive(root.transform, PrimitiveType.Cylinder, "Spear Shaft", new Vector3(0f, 0.8f, 0f), new Vector3(0.07f, 1.4f, 0.07f), brown, false);
        CreatePrimitive(root.transform, PrimitiveType.Cube, "Spear Head", new Vector3(0f, 2.3f, 0f), new Vector3(0.22f, 0.48f, 0.10f), white, false);
        return root.transform;
    }

    private Transform CreateBow(Transform anchor)
    {
        GameObject root = new GameObject("Ranch Bow");
        root.transform.SetParent(anchor, false);
        root.transform.localPosition = new Vector3(0f, 0.25f, 0.1f);
        root.transform.localRotation = Quaternion.Euler(0f, 0f, -10f);

        CreatePrimitive(root.transform, PrimitiveType.Cube, "Bow Upper Limb", new Vector3(0f, 0.75f, 0f), new Vector3(0.10f, 0.75f, 0.10f), brown, false).transform.localRotation = Quaternion.Euler(0f, 0f, -18f);
        CreatePrimitive(root.transform, PrimitiveType.Cube, "Bow Lower Limb", new Vector3(0f, -0.75f, 0f), new Vector3(0.10f, 0.75f, 0.10f), brown, false).transform.localRotation = Quaternion.Euler(0f, 0f, 18f);
        CreatePrimitive(root.transform, PrimitiveType.Cube, "Bow Grip", Vector3.zero, new Vector3(0.16f, 0.35f, 0.14f), black, false);
        CreatePrimitive(root.transform, PrimitiveType.Cube, "Bow String", Vector3.zero, new Vector3(0.025f, 1.55f, 0.025f), white, false);
        return root.transform;
    }

    private Transform CreateExtractor(Transform anchor)
    {
        GameObject root = new GameObject("Held Ranch Extractor");
        root.transform.SetParent(anchor, false);
        root.transform.localPosition = new Vector3(0f, 0.05f, 0.05f);
        root.transform.localRotation = Quaternion.Euler(0f, 0f, -8f);

        GameObject body = CreatePrimitive(root.transform, PrimitiveType.Cylinder, "Extractor Body", Vector3.zero, new Vector3(0.23f, 0.55f, 0.23f), purple, false);
        body.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        CreatePrimitive(root.transform, PrimitiveType.Cube, "Extractor Nozzle", new Vector3(0f, 0f, 0.65f), new Vector3(0.14f, 0.14f, 0.8f), black, false);
        CreatePrimitive(root.transform, PrimitiveType.Cube, "Extractor Grip", new Vector3(0f, -0.34f, 0.05f), new Vector3(0.16f, 0.48f, 0.16f), brown, false);
        return root.transform;
    }

    private Transform CreateHeldTrap(Transform anchor)
    {
        GameObject root = new GameObject("Held Ranch Trap");
        root.transform.SetParent(anchor, false);
        root.transform.localPosition = new Vector3(0f, 0.05f, 0.05f);
        root.transform.localRotation = Quaternion.Euler(0f, 0f, -12f);

        CreatePrimitive(root.transform, PrimitiveType.Cylinder, "Held Trap Base", Vector3.zero, new Vector3(0.42f, 0.10f, 0.42f), black, false);
        for (int i = 0; i < 4; i++)
        {
            float angle = i * Mathf.PI * 2f / 4f;
            Vector3 position =
                new Vector3(Mathf.Cos(angle) * 0.30f, 0.16f, Mathf.Sin(angle) * 0.30f);
            GameObject spike = CreatePrimitive(
                root.transform,
                PrimitiveType.Cube,
                "Held Trap Spike " + i,
                position,
                new Vector3(0.08f, 0.28f, 0.08f),
                yellow,
                false
            );
            spike.transform.localRotation =
                Quaternion.Euler(0f, -angle * Mathf.Rad2Deg, 0f) *
                Quaternion.Euler(18f, 0f, 18f);
        }

        return root.transform;
    }

    private Transform CreateWand(Transform anchor)
    {
        GameObject root = new GameObject("Held Delulu Wand");
        root.transform.SetParent(anchor, false);
        root.transform.localPosition = new Vector3(0f, 0.06f, 0.06f);
        root.transform.localRotation = Quaternion.Euler(18f, 0f, -18f);

        GameObject shaft = CreatePrimitive(root.transform, PrimitiveType.Cylinder, "Wand Shaft", Vector3.zero, new Vector3(0.08f, 0.85f, 0.08f), purple, false);
        shaft.transform.localRotation = Quaternion.Euler(0f, 0f, 0f);
        CreatePrimitive(root.transform, PrimitiveType.Sphere, "Wand Core", new Vector3(0f, 0.55f, 0f), Vector3.one * 0.28f, ranch, false);
        CreatePrimitive(root.transform, PrimitiveType.Cube, "Wand Grip", new Vector3(0f, -0.42f, 0f), new Vector3(0.15f, 0.30f, 0.15f), brown, false);

        return root.transform;
    }

    private GameObject CreateDrew(Transform parent)
    {
        // This root is the actual Drew NPC object registered with RanchDrewSystem.
        // RanchDrewSystem moves and hides/shows this root during gameplay.
        GameObject drew = new GameObject("Drew");
        drew.transform.SetParent(parent);
        drew.transform.position = new Vector3(-18f, 1.1f, -10f);

        // The prefab must exist at:
        // Assets/Resources/Prefabs/DrewModel.prefab
        GameObject drewPrefab = Resources.Load<GameObject>("Prefabs/DrewModel");

        if (drewPrefab != null)
        {
            // Instantiate the visual model as a CHILD of the real Drew NPC root.
            // This makes the custom model move wherever Drew moves.
            GameObject drewModel = Instantiate(drewPrefab);
            drewModel.name = "Drew Character Model";
            drewModel.transform.SetParent(drew.transform, false);
            drewModel.transform.localPosition = Vector3.zero;
            drewModel.transform.localRotation = Quaternion.identity;

            // Keep the scale saved inside DrewModel.prefab.
            // Do not force the model back to Vector3.one here.

            // Imported character files sometimes contain cameras, listeners, or lights.
            // Disable them so they cannot take over the game camera or lighting.
            Camera[] importedCameras = drewModel.GetComponentsInChildren<Camera>(true);
            foreach (Camera importedCamera in importedCameras)
            {
                importedCamera.enabled = false;
                Destroy(importedCamera);
            }

            AudioListener[] importedListeners = drewModel.GetComponentsInChildren<AudioListener>(true);
            foreach (AudioListener importedListener in importedListeners)
            {
                importedListener.enabled = false;
                Destroy(importedListener);
            }

            Light[] importedLights = drewModel.GetComponentsInChildren<Light>(true);
            foreach (Light importedLight in importedLights)
            {
                importedLight.enabled = false;
                Destroy(importedLight);
            }

            // Prevent any imported child from being mistaken for the gameplay camera.
            Transform[] importedObjects = drewModel.GetComponentsInChildren<Transform>(true);
            foreach (Transform importedObject in importedObjects)
                importedObject.gameObject.tag = "Untagged";

            Debug.Log("Drew custom model loaded from Resources/Prefabs/DrewModel.");
        }
        else
        {
            Debug.LogWarning(
                "DrewModel.prefab was not found. Expected path: " +
                "Assets/Resources/Prefabs/DrewModel.prefab"
            );

            // Fallback Drew appears if the custom prefab cannot be loaded.
            CreatePrimitive(
                drew.transform,
                PrimitiveType.Capsule,
                "Fallback Drew Body",
                Vector3.zero,
                Vector3.one,
                white,
                true
            );

            CreatePrimitive(
                drew.transform,
                PrimitiveType.Cylinder,
                "Fallback Drew Hat",
                new Vector3(0f, 1.25f, 0f),
                new Vector3(0.85f, 0.12f, 0.85f),
                yellow,
                true
            );
        }

        CreateLabel(
            drew.transform,
            "DREW\nHelper NPC",
            new Vector3(0f, 2.7f, 0f),
            false
        );

        return drew;
    }

    private void CreateEmpireVisual(Transform parent)
    {
        Transform root = new GameObject("Ranch Empire Structures").transform;
        root.SetParent(parent);
        root.position = Vector3.zero;

        List<GameObject> groups = new List<GameObject>();
        groups.Add(CreateRoadsideStand(root, new Vector3(23f, 0f, -28f)));
        groups.Add(CreateWorkshop(root, new Vector3(23f, 0f, 26f)));
        // The Ranch Laboratory is the enterable Research Facility built by
        // RanchFacilitySystem, so only a placeholder occupies this slot —
        // the index still has to line up with RefreshEmpireVisual.
        GameObject laboratorySlot = new GameObject("Ranch Laboratory (Facility)");
        laboratorySlot.transform.SetParent(root, false);
        groups.Add(laboratorySlot);
        groups.Add(CreateLaboratory(root, new Vector3(68f, 0f, -24f), true));
        groups.Add(CreateResearchCampus(root, new Vector3(66f, 0f, 24f)));
        groups.Add(CreateIndustrialComplex(root, new Vector3(98f, 0f, -10f)));
        groups.Add(CreateStronghold(root, new Vector3(113f, 0f, 20f)));
        groups.Add(CreateCitadel(root, new Vector3(145f, 0f, -8f), false));
        groups.Add(CreateCitadel(root, new Vector3(165f, 0f, 10f), true));

        Transform statusMarker = new GameObject("Empire Status Marker").transform;
        statusMarker.SetParent(parent);
        statusMarker.position = new Vector3(23f, 0f, -28f);

        GameObject labelObject = new GameObject("Ranch Empire Label");
        TextMesh label = labelObject.AddComponent<TextMesh>();
        label.fontSize = 50;
        label.characterSize = 0.075f;
        label.anchor = TextAnchor.MiddleCenter;
        label.alignment = TextAlignment.Center;
        label.color = Color.white;
        RanchWorldLabel follower = labelObject.AddComponent<RanchWorldLabel>();
        follower.Initialize(statusMarker, new Vector3(0f, 7f, 0f), false);

        core.Shop.RegisterEmpireVisual(root, groups.ToArray(), label);
    }

    private GameObject CreateRoadsideStand(Transform root, Vector3 position)
    {
        GameObject group = NewEmpireGroup(root, "Roadside Ranch Stand", position);
        CreatePrimitive(group.transform, PrimitiveType.Cube, "Stand Counter", new Vector3(0f, 1f, 0f), new Vector3(5f, 2f, 2f), brown, true);
        CreatePrimitive(group.transform, PrimitiveType.Cube, "Stand Roof", new Vector3(0f, 3.2f, 0f), new Vector3(7f, 0.35f, 4f), red, true);
        return group;
    }

    private GameObject CreateWorkshop(Transform root, Vector3 position)
    {
        GameObject group = NewEmpireGroup(root, "Ranch Workshop", position);
        CreatePrimitive(group.transform, PrimitiveType.Cube, "Workshop Building", new Vector3(0f, 2.5f, 0f), new Vector3(10f, 5f, 9f), brown, true);
        CreatePrimitive(group.transform, PrimitiveType.Cube, "Workshop Door", new Vector3(0f, 1.5f, -4.6f), new Vector3(2f, 3f, 0.3f), black, false);
        return group;
    }

    private GameObject CreateLaboratory(Transform root, Vector3 position, bool advanced)
    {
        GameObject group = NewEmpireGroup(root, advanced ? "Advanced Ranch Laboratory" : "Ranch Laboratory", position);
        Material bodyMaterial = advanced ? blue : white;
        CreatePrimitive(group.transform, PrimitiveType.Cube, "Laboratory Main Building", new Vector3(0f, 3f, 0f), new Vector3(12f, 6f, 12f), bodyMaterial, true);
        CreatePrimitive(group.transform, PrimitiveType.Cylinder, "Laboratory Ranch Tank", new Vector3(7f, 3f, 0f), new Vector3(2f, 3f, 2f), ranch, true);
        CreatePrimitive(group.transform, PrimitiveType.Cube, "Laboratory Entrance", new Vector3(0f, 1.8f, -6.2f), new Vector3(2.5f, 3.6f, 0.3f), black, false);
        return group;
    }

    private GameObject CreateResearchCampus(Transform root, Vector3 position)
    {
        GameObject group = NewEmpireGroup(root, "Ranch Research Campus", position);
        CreatePrimitive(group.transform, PrimitiveType.Cube, "Research Campus Base", new Vector3(0f, 2f, 0f), new Vector3(14f, 4f, 12f), white, true);
        CreatePrimitive(group.transform, PrimitiveType.Cylinder, "Research Tower", new Vector3(0f, 8f, 0f), new Vector3(2f, 6f, 2f), blue, true);
        CreatePrimitive(group.transform, PrimitiveType.Cylinder, "Research Antenna", new Vector3(0f, 15f, 0f), new Vector3(0.35f, 2f, 0.35f), yellow, true);
        return group;
    }

    private GameObject CreateIndustrialComplex(Transform root, Vector3 position)
    {
        GameObject group = NewEmpireGroup(root, "Ranch Industrial Complex", position);
        CreatePrimitive(group.transform, PrimitiveType.Cube, "Industrial Hall", new Vector3(0f, 3f, 0f), new Vector3(16f, 6f, 14f), gray, true);
        CreatePrimitive(group.transform, PrimitiveType.Cylinder, "Industrial Stack One", new Vector3(-5f, 9f, 2f), new Vector3(1f, 5f, 1f), black, true);
        CreatePrimitive(group.transform, PrimitiveType.Cylinder, "Industrial Stack Two", new Vector3(5f, 9f, 2f), new Vector3(1f, 5f, 1f), black, true);
        return group;
    }

    private GameObject CreateStronghold(Transform root, Vector3 position)
    {
        GameObject group = NewEmpireGroup(root, "Ranch Stronghold", position);
        CreatePrimitive(group.transform, PrimitiveType.Cube, "Stronghold Back Wall", new Vector3(0f, 3f, 8f), new Vector3(18f, 6f, 1f), gray, true);
        CreatePrimitive(group.transform, PrimitiveType.Cube, "Stronghold Left Wall", new Vector3(-8f, 3f, 0f), new Vector3(1f, 6f, 16f), gray, true);
        CreatePrimitive(group.transform, PrimitiveType.Cube, "Stronghold Right Wall", new Vector3(8f, 3f, 0f), new Vector3(1f, 6f, 16f), gray, true);
        CreatePrimitive(group.transform, PrimitiveType.Cube, "Stronghold Front Left", new Vector3(-5.5f, 3f, -8f), new Vector3(5f, 6f, 1f), gray, true);
        CreatePrimitive(group.transform, PrimitiveType.Cube, "Stronghold Front Right", new Vector3(5.5f, 3f, -8f), new Vector3(5f, 6f, 1f), gray, true);
        return group;
    }

    private GameObject CreateCitadel(Transform root, Vector3 position, bool golden)
    {
        GameObject group = NewEmpireGroup(root, golden ? "Golden Ranch Citadel" : "Ranch Citadel", position);
        Material citadelMaterial = golden ? gold : black;
        CreatePrimitive(group.transform, PrimitiveType.Cube, "Citadel Main Tower", new Vector3(0f, 8f, 0f), new Vector3(10f, 16f, 10f), citadelMaterial, true);
        CreatePrimitive(group.transform, PrimitiveType.Cube, "Citadel Crown", new Vector3(0f, 16.5f, 0f), new Vector3(13f, 1f, 13f), golden ? white : gray, true);
        CreatePrimitive(group.transform, PrimitiveType.Cylinder, "Citadel Beacon", new Vector3(0f, 21f, 0f), new Vector3(1.2f, 4f, 1.2f), golden ? ranch : gold, true);
        return group;
    }

    private GameObject NewEmpireGroup(Transform root, string groupName, Vector3 position)
    {
        GameObject group = new GameObject(groupName);
        group.transform.SetParent(root, false);
        group.transform.localPosition = position;
        return group;
    }

    private Transform CreateCJArena(
        Transform parent,
        out Transform playerStart,
        out Transform bossSpawn)
    {
        Transform root = new GameObject("CJ Final Boss Arena").transform;
        root.SetParent(parent);
        root.position = new Vector3(150f, 0f, 110f);

        CreatePrimitive(root, PrimitiveType.Cube, "CJ Arena Solid Floor", new Vector3(0f, -1f, 0f), new Vector3(32f, 2f, 32f), gray, true);
        CreatePrimitive(root, PrimitiveType.Cube, "CJ Arena Safety Foundation", new Vector3(0f, -4.5f, 0f), new Vector3(38f, 5f, 38f), black, true);
        CreatePrimitive(root, PrimitiveType.Cube, "CJ Arena Left Wall", new Vector3(-16f, 3f, 0f), new Vector3(1f, 6f, 32f), black, true);
        CreatePrimitive(root, PrimitiveType.Cube, "CJ Arena Right Wall", new Vector3(16f, 3f, 0f), new Vector3(1f, 6f, 32f), black, true);
        CreatePrimitive(root, PrimitiveType.Cube, "CJ Arena Back Wall", new Vector3(0f, 3f, 16f), new Vector3(32f, 6f, 1f), black, true);
        CreatePrimitive(root, PrimitiveType.Cube, "CJ Arena Front Wall", new Vector3(0f, 3f, -16f), new Vector3(32f, 6f, 1f), black, true);

        for (int i = 0; i < 4; i++)
        {
            float x = i < 2 ? -12f : 12f;
            float z = i % 2 == 0 ? -12f : 12f;
            CreatePrimitive(root, PrimitiveType.Cylinder, "CJ Arena Pillar " + i, new Vector3(x, 3f, z), new Vector3(1.2f, 3f, 1.2f), gold, true);
        }

        Transform playerMarker = new GameObject("CJ Arena Player Start").transform;
        playerMarker.SetParent(root, false);
        playerMarker.localPosition = new Vector3(0f, 1.1f, -10f);
        playerMarker.localRotation = Quaternion.identity;

        Transform bossMarker = new GameObject("CJ Arena Boss Spawn").transform;
        bossMarker.SetParent(root, false);
        bossMarker.localPosition = new Vector3(0f, 1f, 9f);
        bossMarker.localRotation = Quaternion.Euler(0f, 180f, 0f);

        CreateLabel(root, "CJ FINAL BOSS ARENA", new Vector3(0f, 9f, 0f), false).color = Color.white;

        playerStart = playerMarker;
        bossSpawn = bossMarker;
        return root;
    }

    private void CreateDecorations(Transform parent)
    {
        Vector3[] positions =
        {
            new Vector3(-35f, 0f, -35f), new Vector3(-35f, 0f, 35f),
            new Vector3(25f, 0f, -38f), new Vector3(25f, 0f, 38f),
            new Vector3(55f, 0f, -38f), new Vector3(55f, 0f, 38f),
            new Vector3(100f, 0f, -38f), new Vector3(100f, 0f, 38f),
            new Vector3(150f, 0f, -38f), new Vector3(170f, 0f, 38f)
        };

        for (int i = 0; i < positions.Length; i++)
        {
            GameObject root = new GameObject("Background Tree " + i);
            root.transform.SetParent(parent);
            root.transform.position = positions[i];
            CreatePrimitive(root.transform, PrimitiveType.Cylinder, "Tree Trunk", new Vector3(0f, 1.2f, 0f), new Vector3(0.5f, 1.4f, 0.5f), brown, true);
            CreatePrimitive(root.transform, PrimitiveType.Sphere, "Tree Crown", new Vector3(0f, 3.2f, 0f), Vector3.one * 2.2f, green, true);
        }
    }

    private GameObject CreatePrimitive(
        Transform parent,
        PrimitiveType type,
        string objectName,
        Vector3 localPosition,
        Vector3 localScale,
        Material material,
        bool keepCollider)
    {
        GameObject piece = GameObject.CreatePrimitive(type);
        piece.name = objectName;
        piece.transform.SetParent(parent, false);
        piece.transform.localPosition = localPosition;
        piece.transform.localScale = localScale;
        piece.GetComponent<Renderer>().material = material;

        if (!keepCollider)
        {
            Collider collider = piece.GetComponent<Collider>();
            if (collider != null)
                Destroy(collider);
        }

        return piece;
    }

    private TextMesh CreateLabel(Transform target, string text, Vector3 offset, bool scaleHeight)
    {
        GameObject labelObject = new GameObject(target.name + " Label");
        TextMesh mesh = labelObject.AddComponent<TextMesh>();
        mesh.text = text;
        mesh.fontSize = 58;
        mesh.characterSize = 0.072f;
        mesh.anchor = TextAnchor.MiddleCenter;
        mesh.alignment = TextAlignment.Center;
        mesh.color = Color.black;

        RanchWorldLabel label = labelObject.AddComponent<RanchWorldLabel>();
        label.Initialize(target, offset, scaleHeight);
        return mesh;
    }
}
