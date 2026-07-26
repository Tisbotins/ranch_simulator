using System.Collections.Generic;
using UnityEngine;

public enum RanchClassType
{
    Sword = 0,
    Spear = 1,
    Ranged = 2,
    Summoner = 3
}

public class RanchClassSystem : MonoBehaviour
{
    public RanchClassType CurrentClass { get; private set; } = RanchClassType.Sword;
    public bool OakberryIntroduced { get; private set; }
    public bool IsOpen { get; private set; }

    public string CurrentClassName
    {
        get
        {
            switch (CurrentClass)
            {
                case RanchClassType.Spear: return "Spear";
                case RanchClassType.Ranged: return "Ranged";
                case RanchClassType.Summoner: return "Summoner";
                default: return "Sword";
            }
        }
    }

    /// <summary>Display name for any class, not just the current one.</summary>
    public static string GetClassName(RanchClassType classType)
    {
        switch (classType)
        {
            case RanchClassType.Spear: return "Spear";
            case RanchClassType.Ranged: return "Ranged";
            case RanchClassType.Summoner: return "Summoner";
            default: return "Sword";
        }
    }

    private RanchGameCore core;
    private RanchPlayerController player;
    private GameObject oakberryRoot;
    private float previousTimeScale = 1f;

    private const float VirtualWidth = 1600f;
    private const float VirtualHeight = 900f;

    private Texture2D backgroundTexture;
    private Texture2D cardTexture;
    private Texture2D buttonTexture;
    private Texture2D selectedTexture;
    private GUIStyle backgroundStyle;
    private GUIStyle cardStyle;
    private GUIStyle titleStyle;
    private GUIStyle bodyStyle;
    private GUIStyle buttonStyle;
    private GUIStyle selectedButtonStyle;
    private bool stylesReady;

    public void Initialize(RanchGameCore gameCore)
    {
        core = gameCore;
    }

    public void RegisterPlayer(RanchPlayerController playerController)
    {
        player = playerController;
    }

    public void BuildWorldObjects()
    {
        if (oakberryRoot != null)
            return;

        Transform world = GameObject.Find("Generated Ranch World")?.transform;
        oakberryRoot = new GameObject("Dr. Oakberry");
        if (world != null)
            oakberryRoot.transform.SetParent(world);

        Vector3 treePosition = core != null && core.RanchTreeTransform != null
            ? core.RanchTreeTransform.position
            : new Vector3(0f, 0f, 12f);
        oakberryRoot.transform.position = treePosition + new Vector3(-7f, 0f, -5f);

        Material coat = RanchWorldBuilder.CreateRuntimeMaterial(new Color(0.90f, 0.96f, 1f));
        Material skin = RanchWorldBuilder.CreateRuntimeMaterial(new Color(0.72f, 0.48f, 0.30f));
        Material green = RanchWorldBuilder.CreateRuntimeMaterial(new Color(0.18f, 0.55f, 0.25f));
        Material dark = RanchWorldBuilder.CreateRuntimeMaterial(new Color(0.10f, 0.12f, 0.14f));

        CreatePrimitive(PrimitiveType.Capsule, "Oakberry Body", oakberryRoot.transform,
            new Vector3(0f, 1f, 0f), new Vector3(0.9f, 1.05f, 0.9f), coat);
        CreatePrimitive(PrimitiveType.Sphere, "Oakberry Head", oakberryRoot.transform,
            new Vector3(0f, 2.25f, 0f), Vector3.one * 0.72f, skin);
        CreatePrimitive(PrimitiveType.Cylinder, "Oakberry Hat", oakberryRoot.transform,
            new Vector3(0f, 2.85f, 0f), new Vector3(0.75f, 0.12f, 0.75f), green);
        CreatePrimitive(PrimitiveType.Cube, "Oakberry Clipboard", oakberryRoot.transform,
            new Vector3(0.55f, 1.2f, 0.25f), new Vector3(0.45f, 0.65f, 0.08f), dark);

        BoxCollider trigger = oakberryRoot.AddComponent<BoxCollider>();
        trigger.center = new Vector3(0f, 1.3f, 0f);
        trigger.size = new Vector3(2.4f, 3.2f, 2.4f);
        trigger.isTrigger = true;

        RanchOakberryInteractable interactable = oakberryRoot.AddComponent<RanchOakberryInteractable>();
        interactable.Initialize(this);

        GameObject labelObject = new GameObject("Dr. Oakberry Label");
        TextMesh label = labelObject.AddComponent<TextMesh>();
        label.text = "DR. OAKBERRY\nClass Specialist";
        label.fontSize = 48;
        label.characterSize = 0.075f;
        label.anchor = TextAnchor.MiddleCenter;
        label.alignment = TextAlignment.Center;
        label.color = Color.white;
        labelObject.transform.SetParent(oakberryRoot.transform, false);
        labelObject.transform.localPosition = new Vector3(0f, 3.55f, 0f);
        labelObject.AddComponent<RanchBillboard>();

        RefreshOakberryVisibility();
    }

    private void Update()
    {
        if (core == null)
            return;

        RefreshOakberryVisibility();

        if (IsOpen && Input.GetKeyDown(KeyCode.Escape))
            CloseMenu();
    }

    public void NotifyKnowledgePointEarned()
    {
        if (OakberryIntroduced)
            return;

        OakberryIntroduced = true;
        RefreshOakberryVisibility();
        core.ShowMessage(
            "You earned your first Ranch Knowledge Point. Dr. Oakberry has arrived near the Ranch Tree to teach you about classes.",
            10f
        );
        core.Save?.RequestSave();
    }

    /// <summary>
    /// Dr. Oakberry's conversation. Classes are offered as dialogue choices so
    /// the unlock conditions are visible in-fiction: a locked class still
    /// appears, greyed out, with the requirement attached, instead of silently
    /// not being there.
    /// </summary>
    public void TalkToOakberry()
    {
        if (core == null || core.Dialogue == null)
            return;

        if (!OakberryIntroduced)
        {
            core.Dialogue.Begin(
                "Dr. Oakberry",
                "Ah — you're the one working that tree. I've been watching the yields.",
                "Come back once you've earned a Ranch Knowledge Point. I can't teach technique to someone who hasn't learned anything yet."
            );
            return;
        }

        List<RanchDialogueSystem.Choice> options =
            new List<RanchDialogueSystem.Choice>();

        foreach (RanchClassType classType in new[]
                 {
                     RanchClassType.Sword,
                     RanchClassType.Spear,
                     RanchClassType.Ranged,
                     RanchClassType.Summoner
                 })
        {
            RanchClassType captured = classType;
            bool unlocked = IsClassUnlocked(captured);
            bool current = CurrentClass == captured;

            options.Add(new RanchDialogueSystem.Choice
            {
                Text = current
                    ? GetClassName(captured) + "  (current)"
                    : "Train me as " + GetClassName(captured),
                Enabled = unlocked && !current,
                DisabledReason = unlocked ? "" : GetClassLockReason(captured),
                OnChosen = () => ChangeClass(captured)
            });
        }

        options.Add(new RanchDialogueSystem.Choice
        {
            Text = "Just passing through.",
            OnChosen = null
        });

        core.Dialogue.BeginWithChoices(
            "Dr. Oakberry",
            options,
            "You're currently fighting as " + CurrentClassName + ".",
            "Switching costs nothing, and every tree you've grown stays exactly where you left it. So — what shall it be?"
        );
    }

    public void OpenMenu()
    {
        if (!OakberryIntroduced)
        {
            core.ShowMessage("Earn one Ranch Knowledge Point before meeting Dr. Oakberry.");
            return;
        }

        if (IsOpen || core.Shop.IsOpen || core.Progression.IsOpen || core.Settings.IsOpen ||
            (core.Laboratory != null && core.Laboratory.IsOpen) ||
            (core.Space != null && core.Space.IsOpen) || core.GameWon ||
            core.Health.IsDead || core.Health.IsDowned)
        {
            return;
        }

        IsOpen = true;
        previousTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        if (player != null)
            player.enabled = false;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void CloseMenu()
    {
        if (!IsOpen)
            return;

        IsOpen = false;
        Time.timeScale = previousTimeScale <= 0f ? 1f : previousTimeScale;
        if (player != null && !core.Health.IsDead && !core.GameWon)
            player.enabled = true;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    /// <summary>
    /// Classes unlock on different progression axes, so each rewards a
    /// different kind of play rather than one shared currency.
    /// </summary>
    public bool IsClassUnlocked(RanchClassType classType)
    {
        switch (classType)
        {
            case RanchClassType.Spear:
                return core.Progression != null && core.Progression.Level >= 3;
            case RanchClassType.Ranged:
                return core.Waves != null && core.Waves.HighestWaveCleared >= 10;
            case RanchClassType.Summoner:
                return core.Shop != null && core.Shop.StructureLevel >= 3;
            default:
                return true;
        }
    }

    public string GetClassLockReason(RanchClassType classType)
    {
        switch (classType)
        {
            case RanchClassType.Spear:
                return "Reach Ranch Knowledge Level 3";
            case RanchClassType.Ranged:
                return "Clear wave 10";
            case RanchClassType.Summoner:
                return "Build the Ranch Laboratory";
            default:
                return "";
        }
    }

    public void ChangeClass(RanchClassType newClass)
    {
        if (!IsClassUnlocked(newClass))
        {
            core.ShowMessage(
                GetClassName(newClass) + " is locked. " +
                GetClassLockReason(newClass) + ".",
                6f
            );
            return;
        }

        CurrentClass = newClass;
        core.Equipment.ApplyClass(newClass, true);

        if (newClass == RanchClassType.Summoner)
            core.Deployables.UnlockDeluluWandForClass();

        core.ShowMessage(
            "Dr. Oakberry changed your class to " + CurrentClassName + ". Your Weapons shop and Ranch Knowledge tree now match this class.",
            8f
        );
        core.Save.RequestSave();
        core.NotifyResourcesChanged();
    }

    public void RestoreState(int classValue, bool introduced)
    {
        CurrentClass = (RanchClassType)Mathf.Clamp(classValue, 0, 3);
        OakberryIntroduced = introduced;
        RefreshOakberryVisibility();
    }

    public void EnsureCurrentClassEquipment(bool selectClassSlot)
    {
        core.Equipment.ApplyClass(CurrentClass, selectClassSlot);
        if (CurrentClass == RanchClassType.Summoner)
            core.Deployables.UnlockDeluluWandForClass(false);
    }

    public string GetClassDescription(RanchClassType classType)
    {
        switch (classType)
        {
            case RanchClassType.Spear:
                return "Long reach and armor-piercing thrusts. Strong knockback and controlled melee spacing.";
            case RanchClassType.Ranged:
                return "Fires visible Ranch projectiles from a distance. Lower raw damage, safer positioning, and no blocking.";
            case RanchClassType.Summoner:
                return "Uses the Delulu Wand to summon temporary protectors that chase and attack enemies automatically.";
            default:
                return "Balanced close-range fighter with quick combos, critical hits, blocking, and reliable damage.";
        }
    }

    private void RefreshOakberryVisibility()
    {
        if (oakberryRoot != null && oakberryRoot.activeSelf != OakberryIntroduced)
            oakberryRoot.SetActive(OakberryIntroduced);
    }

    private void OnGUI()
    {
        if (!IsOpen || core == null)
            return;

        EnsureStyles();
        Matrix4x4 old = GUI.matrix;
        float scale = Mathf.Min(Screen.width / VirtualWidth, Screen.height / VirtualHeight);
        GUI.matrix = Matrix4x4.TRS(
            new Vector3((Screen.width - VirtualWidth * scale) * 0.5f,
                (Screen.height - VirtualHeight * scale) * 0.5f, 0f),
            Quaternion.identity,
            new Vector3(scale, scale, 1f)
        );

        GUI.Box(new Rect(90f, 60f, 1420f, 790f), GUIContent.none, backgroundStyle);
        GUI.Label(new Rect(140f, 90f, 1320f, 60f), "DR. OAKBERRY — CLASS LAB", titleStyle);
        GUI.Label(new Rect(220f, 155f, 1160f, 48f),
            "Current class: " + CurrentClassName + "  |  Class changes are free and preserve every class tree.",
            bodyStyle);

        DrawClassCard(new Rect(130f, 235f, 315f, 450f), RanchClassType.Sword, "SWORD");
        DrawClassCard(new Rect(470f, 235f, 315f, 450f), RanchClassType.Spear, "SPEAR");
        DrawClassCard(new Rect(810f, 235f, 315f, 450f), RanchClassType.Ranged, "RANGED");
        DrawClassCard(new Rect(1150f, 235f, 315f, 450f), RanchClassType.Summoner, "SUMMONER");

        if (GUI.Button(new Rect(600f, 745f, 400f, 62f), "LEAVE DR. OAKBERRY [ESC]", buttonStyle))
        {
            CloseMenu();
            GUIUtility.ExitGUI();
        }

        GUI.matrix = old;
    }

    private void DrawClassCard(Rect rect, RanchClassType classType, string heading)
    {
        GUI.Box(rect, GUIContent.none, cardStyle);
        GUI.Label(new Rect(rect.x + 18f, rect.y + 18f, rect.width - 36f, 45f), heading, titleStyle);
        GUI.Label(new Rect(rect.x + 25f, rect.y + 85f, rect.width - 50f, 230f),
            GetClassDescription(classType), bodyStyle);

        bool current = CurrentClass == classType;
        GUIStyle style = current ? selectedButtonStyle : buttonStyle;
        if (GUI.Button(new Rect(rect.x + 35f, rect.y + 350f, rect.width - 70f, 65f),
            current ? "CURRENT CLASS" : "CHANGE CLASS", style))
        {
            ChangeClass(classType);
        }
    }

    private static GameObject CreatePrimitive(
        PrimitiveType type,
        string name,
        Transform parent,
        Vector3 localPosition,
        Vector3 localScale,
        Material material)
    {
        GameObject piece = GameObject.CreatePrimitive(type);
        piece.name = name;
        piece.transform.SetParent(parent, false);
        piece.transform.localPosition = localPosition;
        piece.transform.localScale = localScale;
        Renderer renderer = piece.GetComponent<Renderer>();
        if (renderer != null)
            renderer.sharedMaterial = material;
        Collider collider = piece.GetComponent<Collider>();
        if (collider != null)
            Destroy(collider);
        return piece;
    }

    private void EnsureStyles()
    {
        if (stylesReady)
            return;

        backgroundTexture = MakeTexture(new Color(0.015f, 0.025f, 0.035f, 0.99f));
        cardTexture = MakeTexture(new Color(0.07f, 0.11f, 0.15f, 0.99f));
        buttonTexture = MakeTexture(new Color(0.12f, 0.40f, 0.52f, 1f));
        selectedTexture = MakeTexture(new Color(0.10f, 0.58f, 0.43f, 1f));

        backgroundStyle = new GUIStyle(GUI.skin.box);
        backgroundStyle.normal.background = backgroundTexture;
        cardStyle = new GUIStyle(GUI.skin.box);
        cardStyle.normal.background = cardTexture;
        titleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 28,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            wordWrap = true
        };
        titleStyle.normal.textColor = Color.white;
        bodyStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 19,
            alignment = TextAnchor.UpperLeft,
            wordWrap = true
        };
        bodyStyle.normal.textColor = Color.white;
        buttonStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 19,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            wordWrap = true
        };
        buttonStyle.normal.background = buttonTexture;
        buttonStyle.normal.textColor = Color.white;
        selectedButtonStyle = new GUIStyle(buttonStyle);
        selectedButtonStyle.normal.background = selectedTexture;
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
