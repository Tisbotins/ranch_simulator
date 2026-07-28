using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The Cosmic Journey — the post-CJ endgame.
///
/// Beating CJ opens a rift and launches the Ranch Rocket. Each world is a real
/// place the player is teleported to, not a menu: its own ground, sky, Ranch
/// Tree, hazard, and a nemesis. Evil Drew — the Drew that Cosmic CJ turned —
/// meets the player on every moon and must be beaten before the rocket will
/// launch onward. The last world is the Cosmic Core, where Cosmic CJ waits.
///
/// Design notes that are easy to get wrong and were:
///
/// * Surfaces live far apart on +X at ground level, never underground.
///   RanchPlayerController treats anything below FallRecoveryHeight as fallen
///   off the map, so a buried world ejects the player the moment they arrive.
/// * Floors are thick and oversized and the player lands well above them.
///   A thin slab plus a low spawn let the player drop through before the
///   freshly activated colliders entered the physics scene.
/// * Everything stays in one scene, so inventory, waves, saving, and any
///   multiplayer session keep running while the player is off-world.
/// </summary>
[DefaultExecutionOrder(-820)]
[DisallowMultipleComponent]
public class RanchSpaceSystem : MonoBehaviour
{
    /// <summary>What a world does to you while you stand on it.</summary>
    public enum Hazard
    {
        None,
        SporeBloom,     // drains stamina
        Emberfall,      // burns health
        DeepFreeze,     // slows movement
        PrismFlux,      // reinforcements
        CosmicPressure  // heavy, relentless damage
    }

    private sealed class World
    {
        public string Name;
        public string RanchType;
        public float FuelNeeded;
        public Color SkyColor;
        public Color RanchColor;
        public Color GroundColor;
        public Hazard Danger;
        public string HazardName;
        public string Intro;
        public string[] DrewLines;
        public bool IsFinal;

        public Transform Surface;
        public Vector3 Origin;
        public bool DrewDefeated;

        /// <summary>How much richer this world's Ranch is than the homestead.</summary>
        public float YieldMultiplier;
        /// <summary>Enemy strength and spawn pressure scale for this world.</summary>
        public float ThreatScale;
    }

    // Worlds sit far apart on +X so nothing overlaps the ranch or each other.
    private static Vector3 OriginFor(int index)
    {
        return new Vector3(2000f + index * 600f, 0f, 0f);
    }

    public bool JourneyUnlocked { get; private set; }
    public bool JourneyCompleted { get; private set; }
    public bool CosmicCJDefeated { get; private set; }
    public int PlanetIndex { get; private set; }
    public float Fuel { get; private set; }
    public bool IsOpen { get; private set; }

    /// <summary>True while the player is standing on an off-world surface.</summary>
    public bool IsOffWorld { get; private set; }

    /// <summary>
    /// Extraction bonus for the world the player is standing on. The homestead
    /// tree never stops being available, so an off-world tree has to pay enough
    /// to justify the hazard, the mobs, and the trip.
    /// </summary>
    public float WorldExtractionMultiplier =>
        IsOffWorld && Current != null ? Current.YieldMultiplier : 1f;

    // ---- Cosmic upgrades: progression for an already-maxed player ---------

    public const int MaxCosmicTier = 8;

    public int HullTier { get; private set; }
    public int ForgeTier { get; private set; }
    public int HarvesterTier { get; private set; }
    public int ThrusterTier { get; private set; }

    /// <summary>Flat max-health added on top of the normal health track.</summary>
    public float CosmicHealthBonus => HullTier * 260f;
    public float CosmicDamageMultiplier => 1f + ForgeTier * 0.55f;
    public float CosmicExtractionMultiplier => 1f + HarvesterTier * 0.9f;
    public float CosmicSpeedMultiplier => 1f + ThrusterTier * 0.11f;

    /// <summary>Steep: the player arrives with millions and nothing to buy.</summary>
    public float GetCosmicCost(int tier)
    {
        return 250000f * Mathf.Pow(2.35f, tier);
    }

    public string CurrentPlanetName => Current != null ? Current.Name : "Ranch Homestead";
    public string CurrentRanchType => Current != null ? Current.RanchType : "Creamy Ranch";

    private World[] worlds;
    private RanchGameCore core;
    private float lastTotalRanch;
    private float previousTimeScale = 1f;

    private RanchEnemy evilDrew;
    private bool evilDrewSpawned;

    private RanchEnemy cosmicBoss;
    private bool cosmicBossSpawned;
    // Set only when the boss is actually seen at zero health. A boss that
    // merely vanishes (scene reload, wave cleanup) must NOT count as a win —
    // that handed out the true ending for free and permanently locked the save.
    private bool cosmicBossConfirmedDead;

    private float hazardTimer;
    private Vector3 ranchReturnPosition = new Vector3(0f, 1.1f, -10f);

    private Texture2D panelTexture;
    private Texture2D buttonTexture;
    private GUIStyle panelStyle;
    private GUIStyle titleStyle;
    private GUIStyle bodyStyle;
    private GUIStyle buttonStyle;
    private GUIStyle hudStyle;
    private bool stylesReady;

    public void Initialize(RanchGameCore gameCore)
    {
        core = gameCore;
        BuildWorldData();
    }

    private World Current =>
        worlds != null && PlanetIndex >= 0 && PlanetIndex < worlds.Length
            ? worlds[PlanetIndex]
            : null;

    // ------------------------------------------------------------ world data

    private void BuildWorldData()
    {
        worlds = new[]
        {
            new World
            {
                Name = "Verdant Moon",
                YieldMultiplier = 6f,
                ThreatScale = 1.6f,
                RanchType = "Mint Ranch",
                FuelNeeded = 5000f,
                SkyColor = new Color(0.05f, 0.20f, 0.18f),
                RanchColor = new Color(0.45f, 0.95f, 0.55f),
                GroundColor = new Color(0.16f, 0.34f, 0.22f),
                Danger = Hazard.SporeBloom,
                HazardName = "Spore Bloom",
                Intro =
                    "VERDANT MOON.\n\nThe air is thick with spores that eat at your stamina. " +
                    "Mint Ranch grows straight out of the rock here.\n\n" +
                    "Something is waiting by the tree. It is wearing Drew's coat.",
                DrewLines = new[]
                {
                    "You made it. I wasn't sure you would.",
                    "CJ showed me what we actually are, boss. Farmhands. Livestock that thinks it owns the barn.",
                    "I'm not going to let you reach him. Not on my moon."
                }
            },
            new World
            {
                Name = "Ember Reach",
                YieldMultiplier = 20f,
                ThreatScale = 2.6f,
                RanchType = "Ember Ranch",
                FuelNeeded = 15000f,
                SkyColor = new Color(0.25f, 0.06f, 0.03f),
                RanchColor = new Color(1f, 0.45f, 0.15f),
                GroundColor = new Color(0.28f, 0.12f, 0.08f),
                Danger = Hazard.Emberfall,
                HazardName = "Emberfall",
                Intro =
                    "EMBER REACH.\n\nBurning ash falls without stopping. Ember Ranch runs hot " +
                    "enough to scorch the bottles.\n\nHe healed. He got here first. Again.",
                DrewLines = new[]
                {
                    "Still coming. I respected that, once.",
                    "You know what the worst part is? You never asked what I wanted. You just hired me.",
                    "Burn with the rest of it."
                }
            },
            new World
            {
                Name = "Frost Halo",
                YieldMultiplier = 70f,
                ThreatScale = 4f,
                RanchType = "Frost Ranch",
                FuelNeeded = 40000f,
                SkyColor = new Color(0.06f, 0.14f, 0.30f),
                RanchColor = new Color(0.55f, 0.85f, 1f),
                GroundColor = new Color(0.62f, 0.72f, 0.85f),
                Danger = Hazard.DeepFreeze,
                HazardName = "Deep Freeze",
                Intro =
                    "FROST HALO.\n\nThe cold sinks into your legs and will not let go. " +
                    "Frost Ranch crystallises the instant it leaves the tree.\n\n" +
                    "He is not taunting this time. He just looks tired.",
                DrewLines = new[]
                {
                    "I keep losing and I keep waking up. He rebuilds me every time.",
                    "Do you understand what that's like? To be a thing that gets rebuilt?",
                    "Just stop walking toward him. Please. Then I can stop."
                }
            },
            new World
            {
                Name = "Nebula Bazaar",
                YieldMultiplier = 240f,
                ThreatScale = 6f,
                RanchType = "Prism Ranch",
                FuelNeeded = 100000f,
                SkyColor = new Color(0.18f, 0.05f, 0.28f),
                RanchColor = new Color(0.95f, 0.45f, 1f),
                GroundColor = new Color(0.24f, 0.14f, 0.32f),
                Danger = Hazard.PrismFlux,
                HazardName = "Prism Flux",
                Intro =
                    "NEBULA BAZAAR.\n\nEvery Ranch variant in the galaxy is traded here as " +
                    "Prism Ranch, and the light splits everything — including its defenders.\n\n" +
                    "There are several of him now.",
                DrewLines = new[]
                {
                    "Careful. The light here makes copies, and they all agree with me.",
                    "You built an empire out of a tree and never once looked up from it.",
                    "The Core is right there. You will not see it."
                }
            },
            new World
            {
                Name = "The Cosmic Core",
                YieldMultiplier = 800f,
                ThreatScale = 9f,
                RanchType = "Cosmic Ranch",
                FuelNeeded = 0f,
                SkyColor = new Color(0.02f, 0f, 0.06f),
                RanchColor = new Color(1f, 0.92f, 0.55f),
                GroundColor = new Color(0.10f, 0.08f, 0.16f),
                Danger = Hazard.CosmicPressure,
                HazardName = "Cosmic Pressure",
                IsFinal = true,
                Intro =
                    "THE COSMIC CORE.\n\nEvery strand of Ranch in the galaxy runs here, into one " +
                    "being. The pressure alone is trying to fold you.\n\n" +
                    "Drew is standing at his side, and he is not fighting you this time.",
                DrewLines = new[]
                {
                    "I'm done fighting you. Look at it. Look at what he actually is.",
                    "I was never the nemesis, boss. I was the warning."
                }
            }
        };

        for (int i = 0; i < worlds.Length; i++)
            worlds[i].Origin = OriginFor(i);
    }

    // ------------------------------------------------------------- surfaces

    private void EnsureSurface(World world)
    {
        if (world == null || world.Surface != null)
            return;

        GameObject root = new GameObject("World — " + world.Name);
        root.transform.position = world.Origin;
        world.Surface = root.transform;

        // Thick, oversized ground. See the class notes: a thin floor plus a low
        // spawn dropped the player straight through on arrival.
        Slab(root.transform, "Surface", new Vector3(0f, -1f, 0f),
            new Vector3(160f, 2f, 160f), world.GroundColor, RanchVisuals.Surface.Matte);

        // A low wall so the player cannot wander off the edge into nothing.
        for (int i = 0; i < 4; i++)
        {
            float a = i * 90f * Mathf.Deg2Rad;
            Vector3 p = new Vector3(Mathf.Sin(a) * 78f, 3f, Mathf.Cos(a) * 78f);
            Vector3 s = i % 2 == 0
                ? new Vector3(160f, 8f, 2f)
                : new Vector3(2f, 8f, 160f);
            Slab(root.transform, "Boundary " + i, p, s,
                world.SkyColor * 1.6f, RanchVisuals.Surface.Satin);
        }

        BuildLandmarks(root.transform, world);
        BuildAlienTree(root.transform, world);
        BuildRocketPad(root.transform, world);

        root.SetActive(false);
    }

    /// <summary>
    /// Terrain that makes each world read as a distinct place rather than a
    /// flat arena. The shapes are drawn from the world's own hazard, so what
    /// the player sees is what is hurting them.
    /// </summary>
    private void BuildLandmarks(Transform parent, World world)
    {
        GameObject group = new GameObject("Landmarks");
        group.transform.SetParent(parent, false);
        Transform t = group.transform;

        switch (world.Danger)
        {
            case Hazard.SporeBloom:
                // Fungal towers with drifting caps.
                for (int i = 0; i < 9; i++)
                {
                    float a = i * 40f * Mathf.Deg2Rad;
                    float r = 30f + (i % 3) * 12f;
                    Vector3 p = new Vector3(Mathf.Sin(a) * r, 0f, Mathf.Cos(a) * r);
                    float h = 6f + (i % 4) * 3f;
                    Slab(t, "Stalk " + i, p + Vector3.up * (h * 0.5f),
                        new Vector3(1.6f, h, 1.6f),
                        new Color(0.38f, 0.46f, 0.30f), RanchVisuals.Surface.Rough);
                    Slab(t, "Cap " + i, p + Vector3.up * h,
                        new Vector3(7f, 1.2f, 7f),
                        world.RanchColor * 0.8f, RanchVisuals.Surface.Emissive);
                }
                break;

            case Hazard.Emberfall:
                // Lava vents and cooled basalt slabs.
                for (int i = 0; i < 10; i++)
                {
                    float a = i * 36f * Mathf.Deg2Rad;
                    float r = 26f + (i % 4) * 11f;
                    Vector3 p = new Vector3(Mathf.Sin(a) * r, 0f, Mathf.Cos(a) * r);
                    Slab(t, "Vent " + i, p + Vector3.up * 0.6f,
                        new Vector3(9f, 1.2f, 9f),
                        new Color(0.16f, 0.09f, 0.07f), RanchVisuals.Surface.Rough);
                    Slab(t, "Magma " + i, p + Vector3.up * 1.1f,
                        new Vector3(6f, 0.4f, 6f),
                        new Color(1f, 0.42f, 0.08f), RanchVisuals.Surface.Emissive);
                    Slab(t, "Spire " + i, p + Vector3.up * 5f,
                        new Vector3(2.2f, 10f, 2.2f),
                        new Color(0.13f, 0.10f, 0.10f), RanchVisuals.Surface.Rough);
                }
                break;

            case Hazard.DeepFreeze:
                // Ice shards angled out of the ground.
                for (int i = 0; i < 14; i++)
                {
                    float a = i * 26f * Mathf.Deg2Rad;
                    float r = 22f + (i % 5) * 10f;
                    Vector3 p = new Vector3(Mathf.Sin(a) * r, 0f, Mathf.Cos(a) * r);
                    float h = 8f + (i % 4) * 5f;
                    GameObject shard = Slab(t, "Shard " + i, p + Vector3.up * (h * 0.4f),
                        new Vector3(2.6f, h, 2.6f),
                        new Color(0.68f, 0.86f, 1f), RanchVisuals.Surface.Glossy);
                    shard.transform.localRotation =
                        Quaternion.Euler((i % 3) * 9f - 9f, i * 26f, (i % 4) * 7f - 10f);
                }
                break;

            case Hazard.PrismFlux:
                // Refracting pillars in a market ring.
                for (int i = 0; i < 12; i++)
                {
                    float a = i * 30f * Mathf.Deg2Rad;
                    Vector3 p = new Vector3(Mathf.Sin(a) * 34f, 0f, Mathf.Cos(a) * 34f);
                    Slab(t, "Pillar " + i, p + Vector3.up * 7f,
                        new Vector3(2.4f, 14f, 2.4f),
                        new Color(0.30f, 0.18f, 0.40f), RanchVisuals.Surface.Satin);
                    Slab(t, "Prism " + i, p + Vector3.up * 15f,
                        Vector3.one * 3.4f,
                        Color.Lerp(world.RanchColor, Color.white, (i % 4) * 0.22f),
                        RanchVisuals.Surface.Emissive);
                    Slab(t, "Stall " + i, p * 0.72f + Vector3.up * 1.2f,
                        new Vector3(5f, 2.4f, 5f),
                        new Color(0.34f, 0.22f, 0.44f), RanchVisuals.Surface.Satin);
                }
                break;

            case Hazard.CosmicPressure:
                // Monoliths funnelling every strand of Ranch inward.
                for (int i = 0; i < 8; i++)
                {
                    float a = i * 45f * Mathf.Deg2Rad;
                    Vector3 p = new Vector3(Mathf.Sin(a) * 40f, 0f, Mathf.Cos(a) * 40f);
                    Slab(t, "Monolith " + i, p + Vector3.up * 12f,
                        new Vector3(5f, 24f, 5f),
                        new Color(0.08f, 0.06f, 0.14f), RanchVisuals.Surface.Satin);
                    Slab(t, "Conduit " + i, p * 0.5f + Vector3.up * 0.8f,
                        new Vector3(2f, 0.6f, 40f),
                        world.RanchColor, RanchVisuals.Surface.Emissive);
                }
                Slab(t, "Core Altar", new Vector3(0f, 1f, 0f),
                    new Vector3(18f, 2f, 18f),
                    new Color(0.16f, 0.12f, 0.24f), RanchVisuals.Surface.Metal);
                break;
        }
    }

    // Each world grows its own Ranch, so the player can actually refuel here.
    // It drives the same RanchTreeSystem as the homestead tree.
    private void BuildAlienTree(Transform parent, World world)
    {
        GameObject tree = new GameObject(world.RanchType + " Tree");
        tree.transform.SetParent(parent, false);
        tree.transform.localPosition = new Vector3(0f, 0f, 22f);

        Slab(tree.transform, "Trunk", new Vector3(0f, 3f, 0f), new Vector3(2.2f, 6f, 2.2f),
            new Color(0.30f, 0.22f, 0.16f), RanchVisuals.Surface.Rough);

        for (int i = 0; i < 4; i++)
        {
            float a = i * 90f * Mathf.Deg2Rad;
            GameObject crown = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            crown.name = "Crown " + i;
            crown.transform.SetParent(tree.transform, false);
            crown.transform.localPosition =
                new Vector3(Mathf.Sin(a) * 2.2f, 7.2f, Mathf.Cos(a) * 2.2f);
            crown.transform.localScale = Vector3.one * 5.5f;
            Renderer r = crown.GetComponent<Renderer>();
            if (r != null)
                r.sharedMaterial = RanchVisuals.GetMaterial(
                    world.RanchColor, RanchVisuals.Surface.Emissive);
        }

        BoxCollider trigger = tree.AddComponent<BoxCollider>();
        trigger.center = new Vector3(0f, 3f, 0f);
        trigger.size = new Vector3(6f, 6f, 6f);
        trigger.isTrigger = true;

        tree.AddComponent<RanchTreeInteractable>().Initialize(core.Tree);
        Label(tree.transform, new Vector3(0f, 11f, 0f),
            world.RanchType.ToUpperInvariant() + "\nHold E to extract");
    }

    private void BuildRocketPad(Transform parent, World world)
    {
        GameObject pad = new GameObject("Ranch Rocket");
        pad.transform.SetParent(parent, false);
        pad.transform.localPosition = new Vector3(0f, 0f, -22f);

        Slab(pad.transform, "Pad", new Vector3(0f, 0.3f, 0f), new Vector3(12f, 0.6f, 12f),
            new Color(0.30f, 0.32f, 0.38f), RanchVisuals.Surface.Metal);
        Slab(pad.transform, "Hull", new Vector3(0f, 5f, 0f), new Vector3(3.4f, 9f, 3.4f),
            new Color(0.82f, 0.84f, 0.88f), RanchVisuals.Surface.Metal);
        Slab(pad.transform, "Fin A", new Vector3(2.2f, 1.8f, 0f), new Vector3(1f, 3.4f, 2.6f),
            new Color(0.85f, 0.30f, 0.22f), RanchVisuals.Surface.Satin);
        Slab(pad.transform, "Fin B", new Vector3(-2.2f, 1.8f, 0f), new Vector3(1f, 3.4f, 2.6f),
            new Color(0.85f, 0.30f, 0.22f), RanchVisuals.Surface.Satin);
        Slab(pad.transform, "Thruster Glow", new Vector3(0f, 0.9f, 0f),
            new Vector3(2.6f, 0.5f, 2.6f), world.RanchColor, RanchVisuals.Surface.Emissive);

        BoxCollider trigger = pad.AddComponent<BoxCollider>();
        trigger.center = new Vector3(0f, 3f, 0f);
        trigger.size = new Vector3(8f, 8f, 8f);
        trigger.isTrigger = true;

        pad.AddComponent<RanchRocketInteractable>().Initialize(this);
        Label(pad.transform, new Vector3(0f, 11f, 0f), "RANCH ROCKET\nPress E");
    }

    // ------------------------------------------------------------- journey

    public void BeginJourney()
    {
        if (JourneyUnlocked)
            return;

        JourneyUnlocked = true;
        PlanetIndex = 0;
        Fuel = 0f;
        ResetFuelBaseline();

        core.ShowMessage(
            "COSMIC JOURNEY UNLOCKED — the Ranch Rocket is fuelled and waiting. Press J.",
            10f
        );

        TravelTo(0, true);
    }

    /// <summary>Teleports the player onto a world and makes it the current one.</summary>
    private void TravelTo(int index, bool announce)
    {
        if (worlds == null || core == null || core.Player == null)
            return;

        index = Mathf.Clamp(index, 0, worlds.Length - 1);

        if (!IsOffWorld)
        {
            // Remember the ranch so the player can come home.
            ranchReturnPosition = core.Player.transform.position;
        }

        PlanetIndex = index;
        World world = worlds[index];
        EnsureSurface(world);

        SetActiveSurface(world);
        IsOffWorld = true;

        ApplyTheme(world);
        ResetFuelBaseline();

        // Two units of clearance: arriving flush with the floor let the player
        // fall through before the new colliders reached the physics scene.
        core.Player.Teleport(world.Origin + new Vector3(0f, 2f, -14f), 0f);

        DespawnEvilDrew();

        if (announce)
            core.ShowMessage(world.Intro, 14f);

        if (!world.DrewDefeated)
            SpawnEvilDrew(world);
    }

    public void ReturnToRanch()
    {
        if (!IsOffWorld || core == null || core.Player == null)
            return;

        IsOffWorld = false;
        SetActiveSurface(null);
        DespawnEvilDrew();

        RanchVisuals.ApplyAtmosphere(
            new Color(0.42f, 0.66f, 0.92f), new Color(0.20f, 0.17f, 0.12f), 0.9f);

        core.Player.Teleport(ranchReturnPosition, 0f);
        core.ShowMessage("Back on the ranch. Press J to return to the stars.", 5f);
    }

    private void SetActiveSurface(World active)
    {
        if (worlds == null)
            return;

        for (int i = 0; i < worlds.Length; i++)
        {
            if (worlds[i].Surface != null)
                worlds[i].Surface.gameObject.SetActive(worlds[i] == active);
        }
    }

    private void ApplyTheme(World world)
    {
        Camera cam = Camera.main;
        if (cam != null)
        {
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = world.SkyColor;
        }

        RanchVisuals.ApplyAtmosphere(world.SkyColor, world.GroundColor * 0.6f, 0.5f);
    }

    private void ResetFuelBaseline()
    {
        lastTotalRanch = core != null && core.Inventory != null
            ? core.Inventory.TotalRanchCollected
            : 0f;
    }

    private void BuyCosmic(string label, int tier, System.Action grant)
    {
        if (tier >= MaxCosmicTier)
        {
            core.ShowMessage(label + " is already at maximum.", 4f);
            return;
        }

        float cost = GetCosmicCost(tier);
        if (core.Inventory == null || !core.Inventory.TrySpendMoney(cost))
        {
            core.ShowMessage("Need $" + cost.ToString("F0") + " for " + label + ".", 5f);
            return;
        }

        grant();
        core.Save?.RequestSave();
        core.ShowMessage(label + " upgraded to tier " + (tier + 1) + ".", 5f);
        RanchJuiceSystem.Shake(0.1f, 0.25f);
    }

    public void RestoreCosmicTiers(int hull, int forge, int harvester, int thruster)
    {
        HullTier = Mathf.Clamp(hull, 0, MaxCosmicTier);
        ForgeTier = Mathf.Clamp(forge, 0, MaxCosmicTier);
        HarvesterTier = Mathf.Clamp(harvester, 0, MaxCosmicTier);
        ThrusterTier = Mathf.Clamp(thruster, 0, MaxCosmicTier);
    }

    // ------------------------------------------------------------ Evil Drew

    private void SpawnEvilDrew(World world)
    {
        if (evilDrewSpawned || core.Waves == null || core.Player == null || world.IsFinal)
            return;

        evilDrewSpawned = true;

        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        body.name = "Evil Drew — " + world.Name;
        body.transform.position = world.Origin + new Vector3(0f, 1.5f, 8f);
        body.transform.localScale = new Vector3(1.9f, 2.1f, 1.9f);
        body.GetComponent<Renderer>().sharedMaterial =
            RanchVisuals.GetMaterial(new Color(0.85f, 0.72f, 0.10f), RanchVisuals.Surface.Satin);

        GameObject aura = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        aura.name = "Corruption";
        aura.transform.SetParent(body.transform, false);
        aura.transform.localScale = Vector3.one * 1.35f;
        Collider auraCollider = aura.GetComponent<Collider>();
        if (auraCollider != null)
            Destroy(auraCollider);
        aura.GetComponent<Renderer>().sharedMaterial =
            RanchVisuals.GetMaterial(world.RanchColor, RanchVisuals.Surface.Emissive);

        RanchEnemy enemy = body.AddComponent<RanchEnemy>();
        // Scales with how deep into the journey the player is.
        enemy.Initialize(core, core.Waves, core.Player.transform,
            4, 90000 + PlanetIndex, 60 + PlanetIndex * 25);
        core.Waves.RegisterFinalBattleEnemy(enemy);
        evilDrew = enemy;

        if (core.Dialogue != null && world.DrewLines != null)
            core.Dialogue.Begin("Evil Drew", world.DrewLines);
    }

    private void DespawnEvilDrew()
    {
        if (evilDrew != null)
            Destroy(evilDrew.gameObject);

        evilDrew = null;
        evilDrewSpawned = false;
    }

    private void UpdateEvilDrew()
    {
        World world = Current;
        if (world == null || world.DrewDefeated || !evilDrewSpawned)
            return;

        // Only a confirmed kill counts. A vanished enemy means it was cleaned
        // up, not beaten — respawn it rather than handing out the progress.
        if (evilDrew != null && evilDrew.Health > 0f)
            return;

        if (evilDrew == null)
        {
            evilDrewSpawned = false;
            return;
        }

        world.DrewDefeated = true;
        evilDrew = null;
        evilDrewSpawned = false;

        core.ShowMessage(
            "Evil Drew falls. The rocket's clamps release — you can leave " +
            world.Name + " once it is fuelled.",
            8f
        );
        RanchJuiceSystem.Shake(0.3f, 0.5f);
    }

    // -------------------------------------------------------------- hazards

    private void UpdateHazard()
    {
        World world = Current;
        if (world == null || !IsOffWorld || IsOpen)
            return;

        if (core.Health == null || core.Health.IsDead || core.Health.IsDowned)
            return;

        hazardTimer -= Time.deltaTime;
        if (hazardTimer > 0f)
            return;

        hazardTimer = 1.6f;

        switch (world.Danger)
        {
            case Hazard.SporeBloom:
                core.Stamina?.Drain(16f);
                break;

            case Hazard.Emberfall:
                core.Health.TakeDamage(18f + PlanetIndex * 6f, "Emberfall");
                break;

            case Hazard.DeepFreeze:
                core.Player?.ApplySlow(0.4f, 3f);
                core.Stamina?.Drain(10f);
                break;

            case Hazard.PrismFlux:
                // The light makes copies: a steady trickle of reinforcements.
                if (core.Waves != null && core.Player != null)
                    core.Waves.SpawnFinalBattleGuard(core.Player.transform.position, PlanetIndex);
                break;

            case Hazard.CosmicPressure:
                core.Health.TakeDamage(38f, "Cosmic Pressure");
                core.Stamina?.Drain(14f);
                break;
        }
    }

    private float ambientTimer;

    /// <summary>
    /// A slow trickle of native hostiles, so a world is a place with things
    /// living on it rather than an empty arena between nemesis fights. Capped,
    /// and paused while a nemesis or the final boss is on the field so fights
    /// do not turn into a swarm.
    /// </summary>
    private void UpdateAmbientLife()
    {
        if (!IsOffWorld || IsOpen || core.Waves == null || core.Player == null)
            return;

        if (evilDrewSpawned || cosmicBossSpawned)
            return;

        ambientTimer -= Time.deltaTime;
        if (ambientTimer > 0f)
            return;

        World world = Current;
        float threat = world != null ? world.ThreatScale : 1f;

        // Constant pressure. These are endgame worlds — parking at the tree
        // and holding E should never be safe.
        ambientTimer = Mathf.Max(0.9f, 3.4f - PlanetIndex * 0.5f);

        int cap = Mathf.RoundToInt(10f + threat * 4f);
        if (core.Waves.ActiveEnemies.Count >= cap)
            return;

        int burst = 1 + Mathf.FloorToInt(threat * 0.5f);
        for (int i = 0; i < burst; i++)
            core.Waves.SpawnFinalBattleGuard(core.Player.transform.position, PlanetIndex + i);
    }

    // --------------------------------------------------------------- update

    private void Update()
    {
        if (!JourneyUnlocked || core == null || JourneyCompleted)
            return;

        UpdateCosmicBoss();
        UpdateEvilDrew();
        UpdateHazard();
        UpdateAmbientLife();
        AccumulateFuel();
        HandleConsoleToggle();
    }

    private void UpdateCosmicBoss()
    {
        if (!cosmicBossSpawned || CosmicCJDefeated)
            return;

        // Record the kill only while the boss still exists at zero health.
        if (cosmicBoss != null)
        {
            if (cosmicBoss.Health <= 0f)
                cosmicBossConfirmedDead = true;

            if (!cosmicBossConfirmedDead)
                return;
        }
        else if (!cosmicBossConfirmedDead)
        {
            // The boss vanished without dying — cleaned up rather than beaten.
            // Awarding the win here previously granted the true ending for free
            // and left the save permanently "finished". Allow another attempt.
            cosmicBossSpawned = false;
            core.ShowMessage("Cosmic CJ withdrew into the Core. Confront him again.", 6f);
            return;
        }

        CosmicCJDefeated = true;
        JourneyCompleted = true;
        CloseConsole();
        core.WinGame();
    }

    private void AccumulateFuel()
    {
        if (IsOpen || core.Inventory == null)
            return;

        if (core.Health != null && (core.Health.IsDead || core.Health.IsDowned))
        {
            ResetFuelBaseline();
            return;
        }

        float total = core.Inventory.TotalRanchCollected;
        float gained = total - lastTotalRanch;
        lastTotalRanch = total;

        if (gained > 0f)
            Fuel += gained;
    }

    // -------------------------------------------------------------- console

    private void HandleConsoleToggle()
    {
        if (IsOpen)
        {
            if (Input.GetKeyDown(KeyCode.J) || Input.GetKeyDown(KeyCode.Escape))
                CloseConsole();

            return;
        }

        bool blocked =
            core.IsAnyMenuOpen ||
            core.GameWon ||
            (core.Health != null && (core.Health.IsDead || core.Health.IsDowned));

        if (Input.GetKeyDown(KeyCode.J) && !blocked)
            OpenConsole();
    }

    /// <summary>Opened from the rocket pad as well as the J key.</summary>
    public void OpenConsole()
    {
        if (IsOpen || core == null)
            return;

        IsOpen = true;
        previousTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void CloseConsole()
    {
        if (!IsOpen)
            return;

        IsOpen = false;
        Time.timeScale = previousTimeScale <= 0f ? 1f : previousTimeScale;

        bool busy = core.Health != null && (core.Health.IsDead || core.Health.IsDowned);
        if (!busy && !JourneyCompleted)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    private void Launch()
    {
        World world = Current;
        if (world == null || world.IsFinal)
            return;

        if (!world.DrewDefeated)
        {
            core.ShowMessage("Evil Drew is holding the clamps. Defeat him first.", 5f);
            return;
        }

        if (Fuel < world.FuelNeeded)
        {
            core.ShowMessage("Not enough " + world.RanchType + " to fuel the rocket.", 5f);
            return;
        }

        Fuel = 0f;
        CloseConsole();
        TravelTo(PlanetIndex + 1, true);
    }

    private void ConfrontCosmicCJ()
    {
        if (cosmicBossSpawned || core.Player == null || core.Waves == null)
            return;

        cosmicBossSpawned = true;
        cosmicBossConfirmedDead = false;
        CloseConsole();

        Vector3 spawn = core.Player.transform.position +
                        core.Player.transform.forward * 12f + Vector3.up * 0.5f;

        GameObject bossObject = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        bossObject.name = "Cosmic CJ, Warden of All Ranch";
        bossObject.transform.position = spawn;
        bossObject.transform.localScale = new Vector3(3f, 3.2f, 3f);
        bossObject.GetComponent<Renderer>().sharedMaterial =
            RanchVisuals.GetMaterial(new Color(0.75f, 0.35f, 1f), RanchVisuals.Surface.Satin);

        GameObject halo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        halo.name = "Cosmic Aura";
        halo.transform.SetParent(bossObject.transform, false);
        halo.transform.localScale = Vector3.one * 1.4f;
        Collider haloCollider = halo.GetComponent<Collider>();
        if (haloCollider != null)
            Destroy(haloCollider);
        halo.GetComponent<Renderer>().sharedMaterial =
            RanchVisuals.GetMaterial(new Color(1f, 0.92f, 0.55f), RanchVisuals.Surface.Emissive);

        RanchEnemy boss = bossObject.AddComponent<RanchEnemy>();
        boss.Initialize(core, core.Waves, core.Player.transform, 4, 99999, 200);
        core.Waves.RegisterFinalBattleEnemy(boss);
        cosmicBoss = boss;

        if (core.Dialogue != null)
        {
            core.Dialogue.Begin(
                "Cosmic CJ",
                "You walked past four of my moons and a friend to get here.",
                "All the Ranch in the galaxy runs through me. You are a hand that learned to hold a bottle.",
                "Kneel, and I will let the ranch keep growing."
            );
        }
    }

    // ------------------------------------------------------------- restore

    public void RestoreState(
        bool journeyUnlocked,
        int planetIndex,
        float fuel,
        bool cosmicCJDefeated)
    {
        JourneyUnlocked = journeyUnlocked;
        PlanetIndex = worlds != null ? Mathf.Clamp(planetIndex, 0, worlds.Length - 1) : 0;
        Fuel = Mathf.Max(0f, fuel);
        CosmicCJDefeated = cosmicCJDefeated;
        JourneyCompleted = cosmicCJDefeated;

        // Loading always puts the player on the ranch, never mid-world. The
        // surfaces are rebuilt on demand when they next travel.
        IsOffWorld = false;
        SetActiveSurface(null);
        DespawnEvilDrew();
        ResetFuelBaseline();
    }

    /// <summary>Worlds whose nemesis is already beaten, for saving.</summary>
    public bool[] GetDrewProgressCopy()
    {
        bool[] copy = new bool[worlds != null ? worlds.Length : 5];
        for (int i = 0; worlds != null && i < worlds.Length; i++)
            copy[i] = worlds[i].DrewDefeated;
        return copy;
    }

    public void RestoreDrewProgress(bool[] progress)
    {
        if (worlds == null || progress == null)
            return;

        for (int i = 0; i < worlds.Length && i < progress.Length; i++)
            worlds[i].DrewDefeated = progress[i];
    }

    // ---------------------------------------------------------------- draw

    private void OnGUI()
    {
        if (!JourneyUnlocked || JourneyCompleted || core == null)
            return;

        EnsureStyles();

        if (!IsOpen)
        {
            DrawHud();
            return;
        }

        DrawConsole();
    }

    private void DrawHud()
    {
        World world = Current;
        if (world == null || core.IsAnyMenuOpen)
            return;

        string line;
        if (!IsOffWorld)
        {
            line = "RANCH HOMESTEAD  |  Press J for the Ranch Rocket";
        }
        else if (world.IsFinal)
        {
            line = world.Name + "  |  " + world.HazardName + "  |  Press J: CONFRONT COSMIC CJ";
        }
        else
        {
            line =
                world.Name + "  |  " + world.HazardName + "  |  " +
                (world.DrewDefeated ? "" : "EVIL DREW ALIVE  |  ") +
                Mathf.FloorToInt(Fuel) + " / " + Mathf.CeilToInt(world.FuelNeeded) + " fuel";
        }

        float width = 720f;
        Rect rect = new Rect((Screen.width - width) * 0.5f, Screen.height - 44f, width, 32f);
        GUI.Box(rect, GUIContent.none, panelStyle);
        GUI.Label(rect, line, hudStyle);
    }

    private void DrawConsole()
    {
        World world = Current;
        if (world == null)
            return;

        float w = 760f;
        float h = 700f;
        Rect panel = new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h);
        GUI.Box(panel, GUIContent.none, panelStyle);
        GUI.Label(new Rect(panel.x + 30f, panel.y + 22f, w - 60f, 44f),
            "RANCH ROCKET CONSOLE", titleStyle);

        string status =
            "World: " + world.Name + "\n" +
            "Ranch: " + world.RanchType + "\n" +
            "Hazard: " + world.HazardName + "\n\n";

        if (world.IsFinal)
        {
            status += "The end of the journey. Cosmic CJ holds every strand of Ranch " +
                      "in the galaxy.\n\nDefeat him to free all of it.";
        }
        else
        {
            status +=
                "Nemesis: " + (world.DrewDefeated ? "Evil Drew defeated" : "EVIL DREW STILL STANDING") + "\n" +
                "Fuel: " + Mathf.FloorToInt(Fuel) + " / " + Mathf.CeilToInt(world.FuelNeeded) + "\n\n" +
                "Harvest this world's Ranch Tree to refine fuel. The rocket will not " +
                "launch while Evil Drew holds the clamps.\n\n" +
                "Next: " + (PlanetIndex + 1 < worlds.Length ? worlds[PlanetIndex + 1].Name : "—");
        }

        GUI.Label(new Rect(panel.x + 30f, panel.y + 78f, w - 60f, h - 320f), status, bodyStyle);

        DrawCosmicUpgrades(panel, w);

        float by = panel.y + h - 150f;

        if (world.IsFinal)
        {
            if (GUI.Button(new Rect(panel.x + 60f, by, w - 120f, 52f),
                cosmicBossSpawned ? "COSMIC CJ IS ON THE FIELD" : "CONFRONT COSMIC CJ", buttonStyle))
            {
                if (!cosmicBossSpawned)
                {
                    ConfrontCosmicCJ();
                    GUIUtility.ExitGUI();
                    return;
                }
            }
        }
        else
        {
            bool ready = world.DrewDefeated && Fuel >= world.FuelNeeded;
            bool old = GUI.enabled;
            GUI.enabled = ready;
            if (GUI.Button(new Rect(panel.x + 60f, by, w - 120f, 52f),
                ready
                    ? "LAUNCH TO " + worlds[PlanetIndex + 1].Name.ToUpperInvariant()
                    : (world.DrewDefeated ? "TANK NOT FULL" : "EVIL DREW BLOCKS THE LAUNCH"),
                buttonStyle))
            {
                Launch();
                GUIUtility.ExitGUI();
                return;
            }
            GUI.enabled = old;
        }

        if (GUI.Button(new Rect(panel.x + 60f, by + 62f, w - 120f, 44f),
            IsOffWorld ? "RETURN TO THE RANCH" : "TRAVEL TO " + world.Name.ToUpperInvariant(),
            buttonStyle))
        {
            if (IsOffWorld)
                ReturnToRanch();
            else
                TravelTo(PlanetIndex, false);

            CloseConsole();
            GUIUtility.ExitGUI();
            return;
        }

        GUI.Label(new Rect(panel.x + 30f, panel.y + h - 34f, w - 60f, 24f),
            "J or Esc to close.", hudStyle);
    }

    /// <summary>
    /// The endgame upgrade track. By the time a player reaches the journey the
    /// shop is maxed and money has nowhere to go, so this is where it goes.
    /// </summary>
    private void DrawCosmicUpgrades(Rect panel, float w)
    {
        float y = panel.y + panel.height - 300f;
        float bw = (w - 90f) * 0.5f;

        GUI.Label(new Rect(panel.x + 30f, y - 26f, w - 60f, 24f),
            "COSMIC UPGRADES     Money: $" +
            (core.Inventory != null ? core.Inventory.Money.ToString("F0") : "0"),
            hudStyle);

        DrawUpgradeButton(new Rect(panel.x + 30f, y, bw, 40f),
            "Cosmic Hull", HullTier, "+260 max HP",
            () => BuyCosmic("Cosmic Hull", HullTier, () => HullTier++));

        DrawUpgradeButton(new Rect(panel.x + 60f + bw, y, bw, 40f),
            "Star Forge", ForgeTier, "+55% damage",
            () => BuyCosmic("Star Forge", ForgeTier, () => ForgeTier++));

        DrawUpgradeButton(new Rect(panel.x + 30f, y + 46f, bw, 40f),
            "Quantum Harvester", HarvesterTier, "+90% extraction",
            () => BuyCosmic("Quantum Harvester", HarvesterTier, () => HarvesterTier++));

        DrawUpgradeButton(new Rect(panel.x + 60f + bw, y + 46f, bw, 40f),
            "Void Thrusters", ThrusterTier, "+11% speed",
            () => BuyCosmic("Void Thrusters", ThrusterTier, () => ThrusterTier++));
    }

    private void DrawUpgradeButton(
        Rect rect, string label, int tier, string effect, System.Action onBuy)
    {
        bool maxed = tier >= MaxCosmicTier;
        string text = maxed
            ? label + "  MAX"
            : label + "  " + tier + "/" + MaxCosmicTier +
              "  —  $" + GetCosmicCost(tier).ToString("F0") + "   (" + effect + ")";

        bool old = GUI.enabled;
        GUI.enabled = !maxed;
        if (GUI.Button(rect, text, buttonStyle))
        {
            onBuy();
            GUI.enabled = old;
            GUIUtility.ExitGUI();
            return;
        }
        GUI.enabled = old;
    }

    // --------------------------------------------------------------- helpers

    private static GameObject Slab(
        Transform parent, string name, Vector3 localPosition,
        Vector3 scale, Color color, RanchVisuals.Surface surface)
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
        label.characterSize = 0.1f;
        label.anchor = TextAnchor.MiddleCenter;
        label.alignment = TextAlignment.Center;
        label.color = Color.white;
        labelObject.AddComponent<RanchBillboard>();
    }

    private void EnsureStyles()
    {
        if (stylesReady)
            return;

        const int radius = 14;
        RectOffset border = RanchVisuals.PanelBorder(radius);

        panelTexture = RanchVisuals.CreatePanelTexture(
            new Color(0.10f, 0.07f, 0.20f, 0.97f),
            new Color(0.03f, 0.02f, 0.07f, 0.98f),
            new Color(0.52f, 0.42f, 0.85f, 0.9f), radius);

        buttonTexture = RanchVisuals.CreatePanelTexture(
            new Color(0.42f, 0.26f, 0.66f, 1f),
            new Color(0.22f, 0.12f, 0.40f, 1f),
            new Color(0.72f, 0.58f, 1f, 0.95f), radius);

        panelStyle = new GUIStyle(GUI.skin.box);
        panelStyle.normal.background = panelTexture;
        panelStyle.border = border;

        titleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 26,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        titleStyle.normal.textColor = new Color(0.85f, 0.80f, 1f);
        RanchVisuals.UseDisplayFont(titleStyle);

        bodyStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 17,
            wordWrap = true,
            alignment = TextAnchor.UpperLeft
        };
        bodyStyle.normal.textColor = Color.white;

        buttonStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 18,
            fontStyle = FontStyle.Bold,
            wordWrap = true
        };
        buttonStyle.normal.background = buttonTexture;
        buttonStyle.hover.background = buttonTexture;
        buttonStyle.active.background = buttonTexture;
        buttonStyle.border = border;
        buttonStyle.normal.textColor = Color.white;
        buttonStyle.hover.textColor = Color.white;
        buttonStyle.active.textColor = Color.white;

        hudStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 14,
            alignment = TextAnchor.MiddleCenter
        };
        hudStyle.normal.textColor = new Color(0.85f, 0.82f, 1f);

        stylesReady = true;
    }
}
