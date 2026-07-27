using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The Ranch Research Facility: a building in the Laboratory District that the
/// player physically walks into.
///
/// Entering is a real transition, not a menu — the player is teleported to an
/// interior room built far outside the play area (x = 1000). Two details drove
/// that choice:
///
/// * The interior cannot go underground. RanchPlayerController treats anything
///   below FallRecoveryHeight (-8) as having fallen off the map and yanks the
///   player back to safety, so a basement would eject them instantly.
/// * Keeping the interior in the same scene (rather than loading another one)
///   preserves every system's state — inventory, waves, saves, and the
///   multiplayer session all keep running while you are inside.
///
/// Giada Jade staffs the facility and is the only way to reach production
/// research, which used to be a terminal standing in an open field.
/// </summary>
[DefaultExecutionOrder(-795)]
[DisallowMultipleComponent]
public class RanchFacilitySystem : MonoBehaviour
{
    /// <summary>Far outside the play area, at ground level. See class notes.</summary>
    private static readonly Vector3 InteriorOrigin = new Vector3(1000f, 0f, 0f);

    private static readonly Vector3 EntrancePosition = new Vector3(51f, 0f, 0f);

    public bool IsInside { get; private set; }
    public bool HasMetGiada { get; private set; }

    private RanchGameCore core;
    private Transform exteriorRoot;
    private Transform interiorRoot;
    private Vector3 returnPosition;
    private float returnRotationY;

    public void Initialize(RanchGameCore gameCore)
    {
        core = gameCore;
    }

    public void BuildWorldObjects()
    {
        if (exteriorRoot != null)
            return;

        BuildExterior();
        BuildInterior();
        RefreshVisibility();
    }

    // -------------------------------------------------------------- exterior

    private void BuildExterior()
    {
        GameObject root = new GameObject("Ranch Laboratory");
        root.transform.position = EntrancePosition;
        exteriorRoot = root.transform;

        Color wall = new Color(0.78f, 0.80f, 0.85f);
        Color trim = new Color(0.20f, 0.55f, 0.62f);

        // A simple blocky lab: side walls, back wall, roof, and a front face
        // split either side of the doorway so the entrance reads as a gap.
        Slab(root.transform, "Facility Floor", new Vector3(0f, 0.05f, 0f), new Vector3(14f, 0.2f, 12f), wall, RanchVisuals.Surface.Satin);
        Slab(root.transform, "Facility Back Wall", new Vector3(0f, 2.5f, 6f), new Vector3(14f, 5f, 0.5f), wall, RanchVisuals.Surface.Satin);
        Slab(root.transform, "Facility Left Wall", new Vector3(-7f, 2.5f, 0f), new Vector3(0.5f, 5f, 12f), wall, RanchVisuals.Surface.Satin);
        Slab(root.transform, "Facility Right Wall", new Vector3(7f, 2.5f, 0f), new Vector3(0.5f, 5f, 12f), wall, RanchVisuals.Surface.Satin);
        Slab(root.transform, "Facility Roof", new Vector3(0f, 5.2f, 0f), new Vector3(14.5f, 0.4f, 12.5f), trim, RanchVisuals.Surface.Satin);
        Slab(root.transform, "Facility Front Left", new Vector3(-4.6f, 2.5f, -6f), new Vector3(4.8f, 5f, 0.5f), wall, RanchVisuals.Surface.Satin);
        Slab(root.transform, "Facility Front Right", new Vector3(4.6f, 2.5f, -6f), new Vector3(4.8f, 5f, 0.5f), wall, RanchVisuals.Surface.Satin);
        Slab(root.transform, "Facility Lintel", new Vector3(0f, 4.4f, -6f), new Vector3(4.4f, 1.2f, 0.5f), trim, RanchVisuals.Surface.Satin);

        // The doorway itself: a glowing panel you walk into.
        GameObject door = Slab(
            root.transform, "Facility Door",
            new Vector3(0f, 1.8f, -6f), new Vector3(4.4f, 3.6f, 0.15f),
            new Color(0.35f, 0.95f, 0.90f), RanchVisuals.Surface.Emissive
        );
        // Solid, not a trigger: walking into the door must stop you. Otherwise
        // you pass through the wall into the empty shell behind it. Pressing E
        // is the only way in. OverlapSphere still finds solid colliders, so the
        // interaction prompt is unaffected.
        Collider doorCollider = door.GetComponent<Collider>();
        if (doorCollider != null)
            doorCollider.isTrigger = false;

        RanchFacilityDoor entry = door.AddComponent<RanchFacilityDoor>();
        entry.Initialize(this, true);

        Label(root.transform, new Vector3(0f, 6f, -6f), "RANCH LABORATORY\nGiada Jade, Lead Researcher");
    }

    // -------------------------------------------------------------- interior

    private void BuildInterior()
    {
        GameObject root = new GameObject("Ranch Laboratory Interior");
        root.transform.position = InteriorOrigin;
        interiorRoot = root.transform;

        Color floor = new Color(0.24f, 0.26f, 0.30f);
        Color wall = new Color(0.32f, 0.36f, 0.42f);
        Color glow = new Color(0.45f, 0.95f, 0.85f);

        // The floor is deliberately thick and oversized. A thin slab combined
        // with a low spawn let the player drop straight through on arrival, and
        // fall recovery then stranded them in empty space out at x = 1000.
        // It also overhangs the walls so you cannot slip off an edge.
        Slab(root.transform, "Interior Floor", new Vector3(0f, -0.6f, 0f), new Vector3(44f, 1.2f, 36f), floor, RanchVisuals.Surface.Satin);
        Slab(root.transform, "Interior Ceiling", new Vector3(0f, 9f, 0f), new Vector3(44f, 0.6f, 36f), wall, RanchVisuals.Surface.Matte);
        Slab(root.transform, "Interior North Wall", new Vector3(0f, 4.5f, 17f), new Vector3(40f, 9f, 1f), wall, RanchVisuals.Surface.Satin);
        Slab(root.transform, "Interior South Wall", new Vector3(0f, 4.5f, -17f), new Vector3(40f, 9f, 1f), wall, RanchVisuals.Surface.Satin);
        Slab(root.transform, "Interior West Wall", new Vector3(-20f, 4.5f, 0f), new Vector3(1f, 9f, 36f), wall, RanchVisuals.Surface.Satin);
        Slab(root.transform, "Interior East Wall", new Vector3(20f, 4.5f, 0f), new Vector3(1f, 9f, 36f), wall, RanchVisuals.Surface.Satin);

        // Ceiling strips, so the room is lit even though the sun is elsewhere.
        for (int i = -1; i <= 1; i++)
        {
            Slab(root.transform, "Interior Light " + i,
                new Vector3(i * 12f, 8.6f, 0f), new Vector3(1.4f, 0.15f, 30f),
                glow, RanchVisuals.Surface.Emissive);
        }

        // Lab benches for flavour.
        for (int i = -1; i <= 1; i += 2)
        {
            Slab(root.transform, "Lab Bench " + i,
                new Vector3(i * 14f, 0.9f, 4f), new Vector3(6f, 0.3f, 14f),
                new Color(0.55f, 0.58f, 0.62f), RanchVisuals.Surface.Metal);
        }

        BuildGiada(root.transform);

        // Exit pad, mirroring the entrance.
        GameObject exit = Slab(
            root.transform, "Facility Exit",
            new Vector3(0f, 1.8f, -16.6f), new Vector3(4.6f, 3.6f, 0.3f),
            new Color(1f, 0.62f, 0.30f), RanchVisuals.Surface.Emissive
        );
        Collider exitCollider = exit.GetComponent<Collider>();
        if (exitCollider != null)
            exitCollider.isTrigger = false;

        RanchFacilityDoor leave = exit.AddComponent<RanchFacilityDoor>();
        leave.Initialize(this, false);

        Label(root.transform, new Vector3(0f, 4.2f, -16.6f), "EXIT TO THE RANCH");

        root.SetActive(false);
    }

    private void BuildGiada(Transform parent)
    {
        GameObject giada = new GameObject("Giada Jade");
        giada.transform.SetParent(parent, false);
        giada.transform.localPosition = new Vector3(0f, 0f, 9f);
        giada.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);

        // Lab coat over dark trousers, with jade-green hair to match her name.
        Slab(giada.transform, "Giada Legs", new Vector3(0f, 0.45f, 0f), new Vector3(0.55f, 0.9f, 0.45f), new Color(0.18f, 0.19f, 0.24f), RanchVisuals.Surface.Matte);
        Slab(giada.transform, "Giada Coat", new Vector3(0f, 1.25f, 0f), new Vector3(0.85f, 0.95f, 0.55f), new Color(0.93f, 0.95f, 0.97f), RanchVisuals.Surface.Matte);
        Slab(giada.transform, "Giada Head", new Vector3(0f, 1.95f, 0f), new Vector3(0.5f, 0.5f, 0.5f), new Color(0.85f, 0.70f, 0.58f), RanchVisuals.Surface.Matte);
        Slab(giada.transform, "Giada Hair", new Vector3(0f, 2.16f, -0.03f), new Vector3(0.56f, 0.22f, 0.56f), new Color(0.20f, 0.62f, 0.48f), RanchVisuals.Surface.Matte);

        BoxCollider trigger = giada.AddComponent<BoxCollider>();
        trigger.size = new Vector3(2.4f, 2.6f, 2.4f);
        trigger.center = new Vector3(0f, 1.3f, 0f);
        trigger.isTrigger = true;

        giada.AddComponent<RanchGiadaInteractable>().Initialize(this);

        Label(giada.transform, new Vector3(0f, 2.7f, 0f), "GIADA JADE");
    }

    // ------------------------------------------------------------ transition

    public void EnterFacility()
    {
        if (IsInside || core == null || core.Player == null)
            return;

        // Gated on the Laboratory being built, same as the research it houses.
        if (!IsBuilt)
        {
            core.ShowMessage(
                "The Research Facility is sealed. Build the Ranch Laboratory first.",
                6f
            );
            return;
        }

        returnPosition = core.Player.transform.position;
        returnRotationY = core.Player.transform.eulerAngles.y;

        IsInside = true;
        RefreshVisibility();

        // Two metres of clearance above a floor whose surface is at y = 0.
        // Arriving almost touching the floor let the player fall through
        // before the freshly activated colliders entered the physics scene.
        core.Player.Teleport(InteriorOrigin + new Vector3(0f, 2f, -13f), 0f);
    }

    public void ExitFacility()
    {
        if (!IsInside || core == null || core.Player == null)
            return;

        IsInside = false;
        RefreshVisibility();

        // Step back out in front of the door rather than inside its trigger,
        // otherwise you would immediately re-enter.
        Vector3 outside = returnPosition;
        if (Vector3.Distance(outside, EntrancePosition) < 1f)
            outside = EntrancePosition + new Vector3(0f, 0f, -9f);

        core.Player.Teleport(outside, returnRotationY);
    }

    /// <summary>True once the Ranch Laboratory (structure 3) has been bought.</summary>
    public bool IsBuilt =>
        core != null && core.Shop != null && core.Shop.StructureLevel >= 3;

    private void RefreshVisibility()
    {
        // The building only exists once you have paid for it, so an unbuilt
        // laboratory is not standing in the field waiting to be walked into.
        if (exteriorRoot != null)
            exteriorRoot.gameObject.SetActive(IsBuilt);

        if (interiorRoot != null)
            interiorRoot.gameObject.SetActive(IsInside);
    }

    /// <summary>Where the player stands on entering, also the recovery point.</summary>
    public Vector3 InteriorSpawn => InteriorOrigin + new Vector3(0f, 2f, -13f);

    private void Update()
    {
        // If anything drops the player below the interior floor while they are
        // inside, put them back in the room. The controller's own fall recovery
        // would send them to their last outdoor position, which is empty space
        // relative to an interior sitting far out at x = 1000.
        if (IsInside && core != null && core.Player != null &&
            core.Player.transform.position.y < -5f)
        {
            core.Player.Teleport(InteriorSpawn, 0f);
        }

        // Structure level changes in the shop, so visibility is re-evaluated
        // rather than set once at build time.
        if (exteriorRoot == null)
            return;

        bool shouldShow = IsBuilt && !IsInside;
        if (exteriorRoot.gameObject.activeSelf != shouldShow)
            exteriorRoot.gameObject.SetActive(shouldShow);
    }

    // -------------------------------------------------------------- dialogue

    public void TalkToGiada()
    {
        if (core == null || core.Dialogue == null)
            return;

        if (!HasMetGiada)
        {
            HasMetGiada = true;
            core.Dialogue.Begin(
                "Giada Jade",
                "Oh — a visitor. Mind the centrifuge, it's still spinning.",
                "I'm Giada Jade. I run production research here, which is a polite way of saying I work out why the Ranch Tree yields what it yields.",
                "Bring me funding and I'll turn it into throughput. That's the arrangement."
            );
            return;
        }

        List<RanchDialogueSystem.Choice> options =
            new List<RanchDialogueSystem.Choice>();

        options.Add(new RanchDialogueSystem.Choice
        {
            Text = "Show me the research terminal.",
            OnChosen = () => core.Laboratory?.OpenMenu()
        });

        options.Add(new RanchDialogueSystem.Choice
        {
            Text = "What are you working on?",
            OnChosen = () => core.Dialogue.Begin(
                "Giada Jade",
                "Extraction yield, mostly. Every level of production research multiplies what the tree gives you — by hand and passively.",
                "Level " + (core.Shop != null ? core.Shop.ResearchLevel : 0) +
                " of six, currently. The curve gets expensive, I won't pretend otherwise."
            )
        });

        options.Add(new RanchDialogueSystem.Choice
        {
            Text = "Nothing right now.",
            OnChosen = null
        });

        core.Dialogue.BeginWithChoices(
            "Giada Jade",
            options,
            "Back again. What do you need?"
        );
    }

    public void RestoreState(bool metGiada)
    {
        HasMetGiada = metGiada;
    }

    // --------------------------------------------------------------- helpers

    private static GameObject Slab(
        Transform parent,
        string name,
        Vector3 localPosition,
        Vector3 scale,
        Color color,
        RanchVisuals.Surface surface)
    {
        GameObject piece = GameObject.CreatePrimitive(PrimitiveType.Cube);
        piece.name = name;
        piece.transform.SetParent(parent, false);
        piece.transform.localPosition = localPosition;
        piece.transform.localScale = scale;

        Renderer renderer = piece.GetComponent<Renderer>();
        if (renderer != null)
            renderer.sharedMaterial = RanchVisuals.GetMaterial(color, surface);

        return piece;
    }

    private static void Label(Transform parent, Vector3 localPosition, string text)
    {
        GameObject labelObject = new GameObject("Label");
        labelObject.transform.SetParent(parent, false);
        labelObject.transform.localPosition = localPosition;

        TextMesh label = labelObject.AddComponent<TextMesh>();
        label.text = text;
        label.fontSize = 48;
        label.characterSize = 0.08f;
        label.anchor = TextAnchor.MiddleCenter;
        label.alignment = TextAlignment.Center;
        label.color = Color.white;
        labelObject.AddComponent<RanchBillboard>();
    }
}
