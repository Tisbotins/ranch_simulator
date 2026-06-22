using System.Collections.Generic;
using UnityEngine;

public class RanchDeployableSystem : MonoBehaviour
{
    public const int TrapPackSize = 3;
    public const float TrapPackCost = 600f;
    public const float DeluluWandCost = 4500f;
    public const int MaxActiveDelulus = 3;

    public int TrapCount { get; private set; }
    public bool DeluluWandUnlocked { get; private set; }
    public float DeluluCooldownRemaining { get; private set; }

    public int ActiveTrapCount
    {
        get
        {
            RemoveMissingObjects();
            return activeTraps.Count;
        }
    }

    public int ActiveDeluluCount
    {
        get
        {
            RemoveMissingObjects();
            return activeDelulus.Count;
        }
    }

    private readonly List<RanchTrap> activeTraps = new List<RanchTrap>();
    private readonly List<RanchDelulu> activeDelulus = new List<RanchDelulu>();
    private RanchGameCore core;
    private bool clearing;

    public void Initialize(RanchGameCore gameCore)
    {
        core = gameCore;
    }

    private void Update()
    {
        if (DeluluCooldownRemaining > 0f)
            DeluluCooldownRemaining = Mathf.Max(0f, DeluluCooldownRemaining - Time.deltaTime);

        RemoveMissingObjects();
    }

    public void BuyTrapPack()
    {
        if (core == null)
            return;

        if (!core.Inventory.TrySpendMoney(TrapPackCost))
        {
            core.ShowMessage("Need $" + TrapPackCost.ToString("F0") + " for a pack of Ranch Traps.");
            return;
        }

        TrapCount += TrapPackSize;
        core.Equipment.SelectSlot(2);
        core.Progression.AddExperience(25f, "Ranch Trap purchase");
        core.Save.RequestSave();
        core.NotifyResourcesChanged();
        core.ShowMessage("Bought " + TrapPackSize + " Ranch Traps. Select Slot 3 and press Left Click or F to place one.", 7f);
    }

    public void BuyDeluluWand()
    {
        if (core == null)
            return;

        if (DeluluWandUnlocked)
        {
            core.Equipment.SelectSlot(3);
            core.ShowMessage("The Delulu Wand is already unlocked and selected in Slot 4.");
            return;
        }

        if (!core.Inventory.TrySpendMoney(DeluluWandCost))
        {
            core.ShowMessage("Need $" + DeluluWandCost.ToString("F0") + " to unlock the Delulu Wand.");
            return;
        }

        DeluluWandUnlocked = true;
        core.Equipment.SelectSlot(3);
        core.Progression.AddExperience(100f, "Delulu Wand unlocked");
        core.Save.RequestSave();
        core.NotifyResourcesChanged();
        core.ShowMessage("Delulu Wand unlocked. Select Slot 4 and press Left Click or F to summon Delulu.", 8f);
    }

    public void UseSelectedDeployable(Transform player)
    {
        if (core == null || player == null || core.GameWon || core.Health.IsDead)
            return;

        if (core.Equipment.TrapSlotActive)
            TryPlaceTrap(player);
        else if (core.Equipment.WandSlotActive)
            TrySummonDelulu(player);
    }

    public void RestoreState(int trapCount, bool wandUnlocked)
    {
        PrepareForLoad();
        TrapCount = Mathf.Max(0, trapCount);
        DeluluWandUnlocked = wandUnlocked;
        DeluluCooldownRemaining = 0f;
        core?.NotifyResourcesChanged();
    }

    public void PrepareForLoad()
    {
        clearing = true;

        foreach (RanchTrap trap in activeTraps)
        {
            if (trap != null)
                Destroy(trap.gameObject);
        }

        foreach (RanchDelulu delulu in activeDelulus)
        {
            if (delulu != null)
                Destroy(delulu.gameObject);
        }

        activeTraps.Clear();
        activeDelulus.Clear();
        DeluluCooldownRemaining = 0f;
        clearing = false;
    }

    internal void NotifyTrapRemoved(RanchTrap trap)
    {
        if (!clearing)
            activeTraps.Remove(trap);
    }

    internal void NotifyDeluluRemoved(RanchDelulu delulu)
    {
        if (!clearing)
            activeDelulus.Remove(delulu);
    }

    private void TryPlaceTrap(Transform player)
    {
        if (TrapCount <= 0)
        {
            core.ShowMessage("You have no Ranch Traps. Buy more from the Defense tab in the shop.");
            return;
        }

        Vector3 probeStart = player.position + player.forward * 3f + Vector3.up * 5f;
        RaycastHit hit;

        if (!Physics.Raycast(
            probeStart,
            Vector3.down,
            out hit,
            12f,
            Physics.DefaultRaycastLayers,
            QueryTriggerInteraction.Ignore))
        {
            core.ShowMessage("No valid ground was found for the Ranch Trap.");
            return;
        }

        if (hit.normal.y < 0.55f)
        {
            core.ShowMessage("Ranch Traps must be placed on mostly flat ground.");
            return;
        }

        Vector3 placement = hit.point + hit.normal * 0.08f;

        foreach (RanchTrap existing in activeTraps)
        {
            if (existing != null && Vector3.Distance(existing.transform.position, placement) < 1.5f)
            {
                core.ShowMessage("That Ranch Trap is too close to another trap.");
                return;
            }
        }

        GameObject root = new GameObject("Placed Ranch Trap");
        root.transform.position = placement;
        root.transform.rotation = Quaternion.FromToRotation(Vector3.up, hit.normal);
        CreateTrapVisual(root.transform);

        RanchTrap trap = root.AddComponent<RanchTrap>();
        float damage = Mathf.Max(55f, core.Shop.CurrentSwordDamage * 1.35f);
        trap.Initialize(this, core, damage, 120f);
        activeTraps.Add(trap);

        TrapCount--;
        core.Save.RequestSave();
        core.NotifyResourcesChanged();
        core.ShowMessage("Ranch Trap placed. It will expire after 120 seconds if unused.", 5f);
    }

    private void TrySummonDelulu(Transform player)
    {
        if (!DeluluWandUnlocked)
        {
            core.ShowMessage("The Delulu Wand is locked. Buy it from the Defense tab in the shop.");
            return;
        }

        if (DeluluCooldownRemaining > 0f)
        {
            core.ShowMessage("The Delulu Wand is recharging for " + DeluluCooldownRemaining.ToString("0.0") + " more seconds.");
            return;
        }

        if (ActiveDeluluCount >= MaxActiveDelulus)
        {
            core.ShowMessage("You already have the maximum of " + MaxActiveDelulus + " active Delulus.");
            return;
        }

        Vector3 desired = player.position + player.forward * 1.8f + player.right * Random.Range(-0.7f, 0.7f);
        RaycastHit hit;
        Vector3 spawnPosition = desired + Vector3.up * 0.45f;

        if (Physics.Raycast(
            desired + Vector3.up * 5f,
            Vector3.down,
            out hit,
            12f,
            Physics.DefaultRaycastLayers,
            QueryTriggerInteraction.Ignore))
        {
            spawnPosition = hit.point + Vector3.up * 0.45f;
        }

        GameObject root = new GameObject("Delulu Protector");
        root.transform.position = spawnPosition;

        CharacterController controller = root.AddComponent<CharacterController>();
        controller.radius = 0.28f;
        controller.height = 0.9f;
        controller.center = new Vector3(0f, 0.45f, 0f);
        controller.stepOffset = 0.18f;

        Renderer[] renderers = CreateDeluluVisual(root.transform);
        RanchDelulu delulu = root.AddComponent<RanchDelulu>();
        float damage = Mathf.Max(18f, core.Shop.CurrentSwordDamage * 0.32f);
        delulu.Initialize(this, core, controller, renderers, damage, 35f);
        activeDelulus.Add(delulu);

        DeluluCooldownRemaining = 5f;
        core.ShowMessage("Delulu summoned. It will protect you for a limited time.", 5f);
    }

    private void RemoveMissingObjects()
    {
        activeTraps.RemoveAll(trap => trap == null);
        activeDelulus.RemoveAll(delulu => delulu == null);
    }

    private static void CreateTrapVisual(Transform root)
    {
        Material baseMaterial = RanchWorldBuilder.CreateRuntimeMaterial(new Color(0.10f, 0.10f, 0.12f));
        Material spikeMaterial = RanchWorldBuilder.CreateRuntimeMaterial(new Color(0.95f, 0.65f, 0.10f));

        CreatePrimitive(root, PrimitiveType.Cylinder, "Trap Base", new Vector3(0f, 0.06f, 0f), new Vector3(0.85f, 0.06f, 0.85f), baseMaterial);

        for (int i = 0; i < 6; i++)
        {
            float angle = i * Mathf.PI * 2f / 6f;
            Vector3 position = new Vector3(Mathf.Cos(angle) * 0.48f, 0.25f, Mathf.Sin(angle) * 0.48f);
            GameObject spike = CreatePrimitive(root, PrimitiveType.Cube, "Trap Spike " + i, position, new Vector3(0.10f, 0.42f, 0.10f), spikeMaterial);
            spike.transform.localRotation = Quaternion.Euler(0f, -angle * Mathf.Rad2Deg, 0f) * Quaternion.Euler(18f, 0f, 18f);
        }
    }

    private static Renderer[] CreateDeluluVisual(Transform root)
    {
        Material bodyMaterial = CreateTransparentMaterial(new Color(0.95f, 0.35f, 0.80f, 1f));
        Material faceMaterial = CreateTransparentMaterial(new Color(1f, 0.90f, 0.20f, 1f));
        Material darkMaterial = CreateTransparentMaterial(new Color(0.12f, 0.04f, 0.18f, 1f));

        CreatePrimitive(root, PrimitiveType.Sphere, "Delulu Body", new Vector3(0f, 0.48f, 0f), new Vector3(0.72f, 0.72f, 0.72f), bodyMaterial);
        CreatePrimitive(root, PrimitiveType.Sphere, "Delulu Left Eye", new Vector3(-0.16f, 0.58f, 0.31f), Vector3.one * 0.11f, faceMaterial);
        CreatePrimitive(root, PrimitiveType.Sphere, "Delulu Right Eye", new Vector3(0.16f, 0.58f, 0.31f), Vector3.one * 0.11f, faceMaterial);
        CreatePrimitive(root, PrimitiveType.Cube, "Delulu Smile", new Vector3(0f, 0.39f, 0.34f), new Vector3(0.22f, 0.05f, 0.05f), darkMaterial);
        CreatePrimitive(root, PrimitiveType.Sphere, "Delulu Aura", new Vector3(0f, 0.48f, 0f), Vector3.one * 0.92f, CreateTransparentMaterial(new Color(0.75f, 0.25f, 1f, 0.22f)));

        return root.GetComponentsInChildren<Renderer>(true);
    }

    private static GameObject CreatePrimitive(
        Transform parent,
        PrimitiveType type,
        string name,
        Vector3 localPosition,
        Vector3 localScale,
        Material material)
    {
        GameObject piece = GameObject.CreatePrimitive(type);
        piece.name = name;
        piece.transform.SetParent(parent, false);
        piece.transform.localPosition = localPosition;
        piece.transform.localScale = localScale;
        piece.GetComponent<Renderer>().material = material;

        Collider collider = piece.GetComponent<Collider>();
        if (collider != null)
            Destroy(collider);

        return piece;
    }

    private static Material CreateTransparentMaterial(Color color)
    {
        Material material = RanchWorldBuilder.CreateRuntimeMaterial(color);

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
}
