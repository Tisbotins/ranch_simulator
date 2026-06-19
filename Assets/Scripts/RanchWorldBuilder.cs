using System.Collections.Generic;
using UnityEngine;

public class RanchWorldBuilder : MonoBehaviour
{
    private RanchGameCore core;
    private bool built;
    private Material green, brown, blue, yellow, red, white, black, ranch, purple, teal, gray, gold;

    public void Initialize(RanchGameCore gameCore) => core = gameCore;

    public void BuildWorld()
    {
        if (built || core == null) return;
        built = true;
        CreateMaterials();

        Transform world = new GameObject("Generated Ranch World").transform;
        Transform enemies = new GameObject("Ranch Raiders").transform;
        enemies.SetParent(world);

        CreateGround(world);
        Transform tree = CreateRanchTree(world);
        GameObject bottleStation = CreateStation(world, "Bottle Station", new Vector3(-8f, 0.5f, 0f), blue, RanchStationType.Bottle, "BOTTLE STATION");
        CreateStation(world, "Sell Station", new Vector3(8f, 0.5f, 0f), yellow, RanchStationType.Sell, "SELL STATION");
        CreateStation(world, "Bottle Upgrade Station", new Vector3(0f, 0.5f, -8f), red, RanchStationType.BottleUpgrade, "BOTTLE UPGRADES");
        CreateStation(world, "Extractor Upgrade Station", new Vector3(7f, 0.5f, -8f), purple, RanchStationType.ToolUpgrade, "EXTRACTOR UPGRADES");
        CreateStation(world, "Drew Station", new Vector3(-8f, 0.5f, -8f), white, RanchStationType.Drew, "DREW STATION");
        CreateStation(world, "CJ Gate", new Vector3(8f, 0.5f, 8f), black, RanchStationType.CJGate, "CJ GATE");
        CreateStation(world, "Ranch Empire Shop Terminal", new Vector3(4f, 0.8f, -4.5f), teal, RanchStationType.Shop, "RANCH EMPIRE SHOP\nPress E or P");

        RanchPlayerController player = CreatePlayer(world);
        GameObject drew = CreateDrew(world);
        CreateEmpireVisual(world);
        CreateDecorations(world);

        core.Tree.RegisterTreeVisual(tree);
        core.Drew.RegisterVisual(drew, tree, bottleStation.transform);
        core.Waves.RegisterWorld(tree, enemies, player.transform);
        core.RegisterWorld(player, tree);
        core.Shop.RegisterPlayer(player);
    }

    public static Material CreateRuntimeMaterial(Color color)
    {
        Shader shader = Shader.Find("Standard");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        Material material = new Material(shader);
        material.color = color;
        return material;
    }

    private void CreateMaterials()
    {
        green = CreateRuntimeMaterial(new Color(0.25f, 0.65f, 0.25f));
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
    }

    private void CreateGround(Transform parent)
    {
        GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "McMinnville Oregon Ground";
        ground.transform.SetParent(parent);
        ground.transform.localScale = new Vector3(7f, 1f, 7f);
        ground.GetComponent<Renderer>().material = green;
    }

    private Transform CreateRanchTree(Transform parent)
    {
        GameObject root = new GameObject("Ranch Tree");
        root.transform.SetParent(parent);
        root.transform.position = new Vector3(0f, 0f, 8f);

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

    private GameObject CreateStation(Transform parent, string objectName, Vector3 position, Material material, RanchStationType type, string label)
    {
        GameObject station = GameObject.CreatePrimitive(PrimitiveType.Cube);
        station.name = objectName;
        station.transform.SetParent(parent);
        station.transform.position = position;
        station.transform.localScale = new Vector3(2.4f, type == RanchStationType.Shop ? 1.6f : 1f, 2.4f);
        station.GetComponent<Renderer>().material = material;
        RanchStation component = station.AddComponent<RanchStation>();
        component.Initialize(core, type);
        CreateLabel(station.transform, label, new Vector3(0f, type == RanchStationType.Shop ? 2.2f : 1.8f, 0f), false);
        return station;
    }

    private RanchPlayerController CreatePlayer(Transform parent)
    {
        GameObject player = new GameObject("Player");
        player.transform.SetParent(parent);
        player.transform.position = new Vector3(0f, 1.1f, -3f);

        CharacterController controller = player.AddComponent<CharacterController>();
        controller.height = 2f;
        controller.radius = 0.45f;
        controller.center = Vector3.zero;

        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        body.name = "Player Body";
        body.transform.SetParent(player.transform, false);
        body.GetComponent<Renderer>().material = blue;
        Collider bodyCollider = body.GetComponent<Collider>();
        if (bodyCollider != null) Destroy(bodyCollider);

        GameObject handAnchor = new GameObject("Right Hand Tool Anchor");
        handAnchor.transform.SetParent(player.transform, false);
        handAnchor.transform.localPosition = new Vector3(0.72f, 0.05f, 0.05f);

        Transform sword = CreateSword(handAnchor.transform);
        Transform extractor = CreateExtractor(handAnchor.transform);

        Camera camera = Camera.main;
        if (camera == null)
        {
            GameObject cameraObject = new GameObject("Main Camera");
            camera = cameraObject.AddComponent<Camera>();
            cameraObject.tag = "MainCamera";
        }

        RanchPlayerController playerController = player.AddComponent<RanchPlayerController>();
        playerController.Initialize(core, controller, camera, sword, extractor);
        return playerController;
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

    private GameObject CreateDrew(Transform parent)
    {
        GameObject drew = new GameObject("Drew");
        drew.transform.SetParent(parent);
        drew.transform.position = new Vector3(-6f, 1.1f, -6f);
        CreatePrimitive(drew.transform, PrimitiveType.Capsule, "Drew Body", Vector3.zero, Vector3.one, white, true);
        CreatePrimitive(drew.transform, PrimitiveType.Cylinder, "Drew Hat", new Vector3(0f, 1.25f, 0f), new Vector3(0.85f, 0.12f, 0.85f), yellow, true);
        CreateLabel(drew.transform, "DREW\nHelper NPC", new Vector3(0f, 2.7f, 0f), false);
        return drew;
    }

    private void CreateEmpireVisual(Transform parent)
    {
        Transform root = new GameObject("Ranch Empire Structure").transform;
        root.SetParent(parent);
        root.position = new Vector3(16f, 0f, -14f);
        List<GameObject> groups = new List<GameObject>();

        groups.Add(CreateEmpireGroup(root, "Roadside Ranch Stand", PrimitiveType.Cube, new Vector3(0f, 1f, 0f), new Vector3(4f, 2f, 2f), brown));
        groups.Add(CreateEmpireGroup(root, "Ranch Workshop", PrimitiveType.Cube, new Vector3(0f, 2f, 0f), new Vector3(6f, 4f, 6f), brown));
        groups.Add(CreateEmpireGroup(root, "Ranch Laboratory", PrimitiveType.Cube, new Vector3(-4f, 2f, 0f), new Vector3(4f, 4f, 6f), white));
        groups.Add(CreateEmpireGroup(root, "Advanced Ranch Laboratory", PrimitiveType.Cube, new Vector3(4f, 2f, -4f), new Vector3(5f, 4f, 4f), blue));
        groups.Add(CreateEmpireGroup(root, "Ranch Research Campus", PrimitiveType.Cube, new Vector3(0f, 6f, 0f), new Vector3(3f, 8f, 3f), blue));
        groups.Add(CreateEmpireGroup(root, "Ranch Industrial Complex", PrimitiveType.Cube, new Vector3(0f, 2f, 6f), new Vector3(10f, 4f, 5f), gray));

        GameObject stronghold = new GameObject("Ranch Stronghold");
        stronghold.transform.SetParent(root, false);
        CreatePrimitive(stronghold.transform, PrimitiveType.Cube, "North Wall", new Vector3(0f, 2f, -8f), new Vector3(18f, 4f, 1f), gray, true);
        CreatePrimitive(stronghold.transform, PrimitiveType.Cube, "South Wall", new Vector3(0f, 2f, 8f), new Vector3(18f, 4f, 1f), gray, true);
        CreatePrimitive(stronghold.transform, PrimitiveType.Cube, "West Wall", new Vector3(-8f, 2f, 0f), new Vector3(1f, 4f, 18f), gray, true);
        CreatePrimitive(stronghold.transform, PrimitiveType.Cube, "East Wall", new Vector3(8f, 2f, 0f), new Vector3(1f, 4f, 18f), gray, true);
        groups.Add(stronghold);

        groups.Add(CreateEmpireGroup(root, "Ranch Citadel", PrimitiveType.Cube, new Vector3(0f, 9f, 0f), new Vector3(6f, 12f, 6f), black));
        groups.Add(CreateEmpireGroup(root, "Golden Ranch Citadel", PrimitiveType.Cylinder, new Vector3(0f, 19f, 0f), new Vector3(1.2f, 3f, 1.2f), gold));

        GameObject labelObject = new GameObject("Ranch Empire Label");
        TextMesh label = labelObject.AddComponent<TextMesh>();
        label.fontSize = 55;
        label.characterSize = 0.08f;
        label.anchor = TextAnchor.MiddleCenter;
        label.alignment = TextAlignment.Center;
        label.color = Color.white;
        RanchWorldLabel follower = labelObject.AddComponent<RanchWorldLabel>();
        follower.Initialize(root, new Vector3(0f, 16f, 0f), false);

        core.Shop.RegisterEmpireVisual(root, groups.ToArray(), label);
    }

    private GameObject CreateEmpireGroup(Transform root, string groupName, PrimitiveType type, Vector3 position, Vector3 scale, Material material)
    {
        GameObject group = new GameObject(groupName);
        group.transform.SetParent(root, false);
        CreatePrimitive(group.transform, type, groupName + " Visual", position, scale, material, true);
        return group;
    }

    private GameObject CreatePrimitive(Transform parent, PrimitiveType type, string objectName, Vector3 localPosition, Vector3 localScale, Material material, bool keepCollider)
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
            if (collider != null) Destroy(collider);
        }
        return piece;
    }

    private void CreateDecorations(Transform parent)
    {
        for (int i = 0; i < 14; i++)
        {
            float angle = i * Mathf.PI * 2f / 14f;
            Vector3 position = new Vector3(Mathf.Cos(angle) * 27f, 0f, Mathf.Sin(angle) * 27f);
            GameObject root = new GameObject("Background Tree");
            root.transform.SetParent(parent);
            root.transform.position = position;
            CreatePrimitive(root.transform, PrimitiveType.Cylinder, "Tree Trunk", new Vector3(0f, 1f, 0f), new Vector3(0.5f, 1.3f, 0.5f), brown, true);
            CreatePrimitive(root.transform, PrimitiveType.Sphere, "Tree Crown", new Vector3(0f, 2.8f, 0f), Vector3.one * 2.2f, green, true);
        }
    }

    private void CreateLabel(Transform target, string text, Vector3 offset, bool scaleHeight)
    {
        GameObject labelObject = new GameObject(target.name + " Label");
        TextMesh mesh = labelObject.AddComponent<TextMesh>();
        mesh.text = text;
        mesh.fontSize = 64;
        mesh.characterSize = 0.075f;
        mesh.anchor = TextAnchor.MiddleCenter;
        mesh.alignment = TextAlignment.Center;
        mesh.color = Color.black;
        RanchWorldLabel label = labelObject.AddComponent<RanchWorldLabel>();
        label.Initialize(target, offset, scaleHeight);
    }
}
