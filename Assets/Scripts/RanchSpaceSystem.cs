using UnityEngine;

/// <summary>
/// The Cosmic Journey — the post-CJ endgame.
///
/// Once CJ, the Ultimate Ranchenator, is defeated a cosmic rift opens and the
/// Ranch Rocket launches. The player travels between planets, each with its own
/// new type of Ranch. Harvesting Ranch on a planet refines into rocket fuel;
/// once the tank is full you launch to the next world. The final destination is
/// the Cosmic Core, where Cosmic CJ controls all the Ranch in the galaxy and
/// must be defeated for the true ending.
///
/// This is a self-contained meta-layer on top of the normal loop: it reuses the
/// existing extraction/economy (any Ranch collected becomes fuel) and the
/// existing enemy/wave code for the final boss, and only re-themes the sky and
/// Ranch Tree per planet.
/// </summary>
[DefaultExecutionOrder(-820)]
[DisallowMultipleComponent]
public class RanchSpaceSystem : MonoBehaviour
{
    private sealed class Planet
    {
        public string Name;
        public string RanchType;
        public string Intro;
        public float FuelNeeded;
        public Color SkyColor;
        public Color RanchColor;
        public bool IsFinal;
    }

    public bool JourneyUnlocked { get; private set; }
    public bool JourneyCompleted { get; private set; }
    public bool CosmicCJDefeated { get; private set; }
    public int PlanetIndex { get; private set; }
    public float Fuel { get; private set; }
    public bool IsOpen { get; private set; }

    public string CurrentPlanetName =>
        (planets != null && PlanetIndex >= 0 && PlanetIndex < planets.Length)
            ? planets[PlanetIndex].Name
            : "Ranch Homestead";

    public string CurrentRanchType =>
        (planets != null && PlanetIndex >= 0 && PlanetIndex < planets.Length)
            ? planets[PlanetIndex].RanchType
            : "Creamy Ranch";

    private RanchGameCore core;
    private Planet[] planets;
    private float lastTotalRanch;
    private float previousTimeScale = 1f;

    private RanchEnemy cosmicBoss;
    private bool cosmicBossSpawned;

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
        BuildPlanets();
    }

    private void BuildPlanets()
    {
        planets = new[]
        {
            new Planet
            {
                Name = "Verdant Moon",
                RanchType = "Mint Ranch",
                FuelNeeded = 400f,
                SkyColor = new Color(0.05f, 0.20f, 0.18f),
                RanchColor = new Color(0.45f, 0.95f, 0.55f),
                Intro =
                    "Ranch Rocket touchdown: VERDANT MOON.\n\n" +
                    "The soil hums with Mint Ranch. Drew: \"CJ wasn't the top of the chain — " +
                    "someone off-world has been farming these variants for years.\"\n\n" +
                    "Harvest Mint Ranch to refuel the rocket, then launch onward (press J)."
            },
            new Planet
            {
                Name = "Ember Reach",
                RanchType = "Ember Ranch",
                FuelNeeded = 900f,
                SkyColor = new Color(0.25f, 0.06f, 0.03f),
                RanchColor = new Color(1f, 0.45f, 0.15f),
                Intro =
                    "Ranch Rocket touchdown: EMBER REACH.\n\n" +
                    "Volcanic Ember Ranch burns hotter and fuels harder. You meet a scarred " +
                    "variant of yourself who fled the Core. \"He rewrites anyone who refuses him.\"\n\n" +
                    "Refuel with Ember Ranch and press on."
            },
            new Planet
            {
                Name = "Frost Halo",
                RanchType = "Frost Ranch",
                FuelNeeded = 1800f,
                SkyColor = new Color(0.06f, 0.14f, 0.30f),
                RanchColor = new Color(0.55f, 0.85f, 1f),
                Intro =
                    "Ranch Rocket touchdown: FROST HALO.\n\n" +
                    "Frost Ranch crystallizes the tank with dense cosmic fuel. Drew goes quiet — " +
                    "a broadcast from the Core is calling crew home. \"Don't listen to it,\" you tell him.\n\n" +
                    "Fill the tank and launch."
            },
            new Planet
            {
                Name = "Nebula Bazaar",
                RanchType = "Prism Ranch",
                FuelNeeded = 3500f,
                SkyColor = new Color(0.18f, 0.05f, 0.28f),
                RanchColor = new Color(0.95f, 0.45f, 1f),
                Intro =
                    "Ranch Rocket touchdown: NEBULA BAZAAR.\n\n" +
                    "Every Ranch variant in the galaxy trades here as Prism Ranch. Drew is gone — " +
                    "a note reads: \"Cosmic CJ showed me what we really are. Come and see.\"\n\n" +
                    "Refuel with Prism Ranch. The Core is the last jump."
            },
            new Planet
            {
                Name = "The Cosmic Core",
                RanchType = "Cosmic Ranch",
                FuelNeeded = 0f,
                SkyColor = new Color(0.02f, 0.0f, 0.06f),
                RanchColor = new Color(1f, 0.92f, 0.55f),
                IsFinal = true,
                Intro =
                    "Ranch Rocket touchdown: THE COSMIC CORE.\n\n" +
                    "All the Ranch in the galaxy flows to one being. COSMIC CJ, with a converted " +
                    "Drew at his side, controls it all.\n\n" +
                    "\"You freed one ranch. I AM the ranch.\" Open the Journey menu (J) and CONFRONT COSMIC CJ."
            }
        };
    }

    /// <summary>Called from RanchGameCore.WinGame the moment CJ is defeated.</summary>
    public void BeginJourney()
    {
        if (JourneyUnlocked)
            return;

        JourneyUnlocked = true;
        PlanetIndex = 0;
        Fuel = 0f;
        lastTotalRanch = core != null && core.Inventory != null
            ? core.Inventory.TotalRanchCollected
            : 0f;

        ApplyPlanetTheme();
        AnnouncePlanet();
        core.ShowMessage(
            "COSMIC JOURNEY UNLOCKED — press J to open the Ranch Rocket console.",
            10f
        );
    }

    private void Update()
    {
        if (!JourneyUnlocked || core == null)
            return;

        if (JourneyCompleted)
            return;

        // Detect the Cosmic CJ defeat and trigger the true ending.
        if (cosmicBossSpawned && !CosmicCJDefeated &&
            (cosmicBoss == null || cosmicBoss.Health <= 0f))
        {
            CosmicCJDefeated = true;
            JourneyCompleted = true;
            CloseConsole();
            core.WinGame();
            return;
        }

        AccumulateFuel();
        HandleConsoleToggle();
    }

    private void AccumulateFuel()
    {
        if (IsOpen || core.Inventory == null)
            return;

        if (core.Health != null && (core.Health.IsDead || core.Health.IsDowned))
        {
            lastTotalRanch = core.Inventory.TotalRanchCollected;
            return;
        }

        float total = core.Inventory.TotalRanchCollected;
        float gained = total - lastTotalRanch;
        lastTotalRanch = total;

        if (gained > 0f)
            Fuel += gained;
    }

    private void HandleConsoleToggle()
    {
        // Closing must always be possible, otherwise the console could trap the
        // game at timeScale 0. Only OPENING is gated on other menus.
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

    private void OpenConsole()
    {
        if (IsOpen)
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

        bool incapacitated = core.Health != null &&
            (core.Health.IsDead || core.Health.IsDowned);

        if (!incapacitated && !JourneyCompleted)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    private Planet Current =>
        planets != null && PlanetIndex >= 0 && PlanetIndex < planets.Length
            ? planets[PlanetIndex]
            : null;

    private void Launch()
    {
        Planet planet = Current;
        if (planet == null || planet.IsFinal)
            return;

        if (Fuel < planet.FuelNeeded)
        {
            core.ShowMessage("Not enough " + planet.RanchType + " to fuel the rocket yet.", 4f);
            return;
        }

        PlanetIndex = Mathf.Min(PlanetIndex + 1, planets.Length - 1);
        Fuel = 0f;
        lastTotalRanch = core.Inventory != null ? core.Inventory.TotalRanchCollected : 0f;

        ApplyPlanetTheme();
        AnnouncePlanet();
        CloseConsole();
    }

    private void ConfrontCosmicCJ()
    {
        if (cosmicBossSpawned || core.Player == null || core.Waves == null)
            return;

        cosmicBossSpawned = true;
        CloseConsole();

        Vector3 spawn = core.Player.transform.position +
                        core.Player.transform.forward * 9f + Vector3.up * 0.5f;

        GameObject bossObject = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        bossObject.name = "Cosmic CJ, Warden of All Ranch";
        bossObject.transform.position = spawn;
        bossObject.transform.localScale = new Vector3(2.4f, 2.6f, 2.4f);
        bossObject.GetComponent<Renderer>().material =
            RanchWorldBuilder.CreateRuntimeMaterial(new Color(0.75f, 0.35f, 1f));

        GameObject halo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        halo.name = "Cosmic Aura";
        halo.transform.SetParent(bossObject.transform, false);
        halo.transform.localScale = new Vector3(1.4f, 1.4f, 1.4f);
        Collider haloCollider = halo.GetComponent<Collider>();
        if (haloCollider != null)
            Destroy(haloCollider);
        halo.GetComponent<Renderer>().material =
            RanchWorldBuilder.CreateRuntimeMaterial(new Color(1f, 0.92f, 0.55f));

        RanchEnemy boss = bossObject.AddComponent<RanchEnemy>();
        boss.Initialize(core, core.Waves, core.Player.transform, 4, 99999, 140,
            RanchEnemy.EnemyArchetype.Raider);
        core.Waves.RegisterFinalBattleEnemy(boss);
        cosmicBoss = boss;

        core.ShowMessage(
            "COSMIC CJ: \"All the Ranch in the galaxy answers to me. Kneel.\"",
            8f
        );
    }

    private void AnnouncePlanet()
    {
        Planet planet = Current;
        if (planet != null)
            core.ShowMessage(planet.Intro, 12f);
    }

    private void ApplyPlanetTheme()
    {
        Planet planet = Current;
        if (planet == null)
            return;

        Camera cam = Camera.main;
        if (cam != null)
        {
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = planet.SkyColor;
        }

        // Alien skies get the same fog + gradient ambient treatment as the
        // homestead, tinted by the planet's own ranch colour so the ground
        // bounce matches what grows there.
        RanchVisuals.ApplyAtmosphere(
            planet.SkyColor,
            planet.RanchColor * 0.35f,
            0.55f
        );

        TintRanchTree(planet.RanchColor);
    }

    private void TintRanchTree(Color color)
    {
        if (core.RanchTreeTransform == null)
            return;

        Renderer[] renderers =
            core.RanchTreeTransform.GetComponentsInChildren<Renderer>(true);

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null && renderers[i].name.Contains("Crown"))
                renderers[i].material = RanchWorldBuilder.CreateRuntimeMaterial(color);
        }
    }

    public void RestoreState(
        bool journeyUnlocked,
        int planetIndex,
        float fuel,
        bool cosmicCJDefeated)
    {
        JourneyUnlocked = journeyUnlocked;
        PlanetIndex = planets != null
            ? Mathf.Clamp(planetIndex, 0, planets.Length - 1)
            : 0;
        Fuel = Mathf.Max(0f, fuel);
        CosmicCJDefeated = cosmicCJDefeated;
        JourneyCompleted = cosmicCJDefeated;

        lastTotalRanch = core != null && core.Inventory != null
            ? core.Inventory.TotalRanchCollected
            : 0f;

        if (JourneyUnlocked && !JourneyCompleted)
            ApplyPlanetTheme();
    }

    private void OnGUI()
    {
        if (!JourneyUnlocked || JourneyCompleted || core == null)
            return;

        EnsureStyles();

        if (!IsOpen)
        {
            DrawHudHint();
            return;
        }

        DrawConsole();
    }

    private void DrawHudHint()
    {
        Planet planet = Current;
        if (planet == null)
            return;

        string line = planet.IsFinal
            ? "COSMIC JOURNEY — " + planet.Name + "  |  Press J: CONFRONT COSMIC CJ"
            : "COSMIC JOURNEY — " + planet.Name + "  |  " + planet.RanchType + " fuel " +
              Mathf.FloorToInt(Fuel) + " / " + Mathf.CeilToInt(planet.FuelNeeded) + "  |  Press J";

        float width = 620f;
        Rect rect = new Rect((Screen.width - width) * 0.5f, Screen.height - 44f, width, 32f);
        GUI.Box(rect, GUIContent.none, panelStyle);
        GUI.Label(rect, line, hudStyle);
    }

    private void DrawConsole()
    {
        Planet planet = Current;
        if (planet == null)
            return;

        float w = 720f;
        float h = 520f;
        Rect panel = new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h);
        GUI.Box(panel, GUIContent.none, panelStyle);

        GUI.Label(new Rect(panel.x + 30f, panel.y + 24f, w - 60f, 44f),
            "RANCH ROCKET CONSOLE", titleStyle);

        string status =
            "Current planet: " + planet.Name + "\n" +
            "Ranch discovered here: " + planet.RanchType + "\n\n";

        if (planet.IsFinal)
        {
            status +=
                "This is the Cosmic Core — the end of the journey.\n" +
                "All the galaxy's Ranch flows through Cosmic CJ.\n\n" +
                "Confront him to free every ranch and reach the true ending.";
        }
        else
        {
            float pct = planet.FuelNeeded > 0f
                ? Mathf.Clamp01(Fuel / planet.FuelNeeded) * 100f
                : 100f;
            status +=
                planet.RanchType + " fuel: " + Mathf.FloorToInt(Fuel) + " / " +
                Mathf.CeilToInt(planet.FuelNeeded) + "   (" + pct.ToString("F0") + "%)\n\n" +
                "Harvest, bottle, and automate Ranch on this planet to refine rocket " +
                "fuel. When the tank is full, launch to the next world.\n\n" +
                "Next: " + (PlanetIndex + 1 < planets.Length ? planets[PlanetIndex + 1].Name : "—");
        }

        GUI.Label(new Rect(panel.x + 30f, panel.y + 80f, w - 60f, h - 190f), status, bodyStyle);

        Rect actionRect = new Rect(panel.x + 60f, panel.y + h - 96f, w - 120f, 56f);
        if (planet.IsFinal)
        {
            if (GUI.Button(actionRect,
                cosmicBossSpawned ? "COSMIC CJ IS ON THE FIELD — DEFEAT HIM" : "CONFRONT COSMIC CJ",
                buttonStyle))
            {
                if (!cosmicBossSpawned)
                {
                    ConfrontCosmicCJ();
                    GUIUtility.ExitGUI();
                }
            }
        }
        else
        {
            bool ready = Fuel >= planet.FuelNeeded;
            bool old = GUI.enabled;
            GUI.enabled = ready;
            if (GUI.Button(actionRect,
                ready
                    ? "LAUNCH TO " + planets[Mathf.Min(PlanetIndex + 1, planets.Length - 1)].Name.ToUpperInvariant()
                    : "TANK NOT FULL — KEEP HARVESTING " + planet.RanchType.ToUpperInvariant(),
                buttonStyle))
            {
                Launch();
                GUIUtility.ExitGUI();
            }
            GUI.enabled = old;
        }

        GUI.Label(new Rect(panel.x + 30f, panel.y + h - 32f, w - 60f, 24f),
            "Press J or Esc to close the console.", hudStyle);
    }

    private void EnsureStyles()
    {
        if (stylesReady)
            return;

        const int radius = 14;
        RectOffset border = RanchVisuals.PanelBorder(radius);

        // Deep-space violet, to distinguish the rocket console from the
        // homestead's blue-grey panels.
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

    private static Texture2D MakeTexture(Color color)
    {
        Texture2D texture = new Texture2D(1, 1);
        texture.SetPixel(0, 0, color);
        texture.Apply();
        return texture;
    }
}
