using UnityEngine;

public class RanchLaboratorySystem : MonoBehaviour
{
    public bool IsOpen { get; private set; }

    private RanchGameCore core;
    private RanchPlayerController player;
    private GameObject terminalRoot;
    private float previousTimeScale = 1f;

    private const float VirtualWidth = 1600f;
    private const float VirtualHeight = 900f;
    private Texture2D backgroundTexture;
    private Texture2D cardTexture;
    private Texture2D buttonTexture;
    private GUIStyle backgroundStyle;
    private GUIStyle cardStyle;
    private GUIStyle titleStyle;
    private GUIStyle bodyStyle;
    private GUIStyle buttonStyle;
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
        if (terminalRoot != null)
            return;

        Transform world = GameObject.Find("Generated Ranch World")?.transform;
        terminalRoot = new GameObject("Ranch Laboratory Production Terminal");
        if (world != null)
            terminalRoot.transform.SetParent(world);
        terminalRoot.transform.position = new Vector3(51f, 0f, -7.5f);

        Material white = RanchWorldBuilder.CreateRuntimeMaterial(new Color(0.90f, 0.95f, 1f));
        Material teal = RanchWorldBuilder.CreateRuntimeMaterial(new Color(0.08f, 0.60f, 0.55f));
        Material dark = RanchWorldBuilder.CreateRuntimeMaterial(new Color(0.06f, 0.08f, 0.10f));

        CreatePiece(PrimitiveType.Cube, "Terminal Base", new Vector3(0f, 0.7f, 0f), new Vector3(2.2f, 1.4f, 1.5f), dark);
        CreatePiece(PrimitiveType.Cube, "Terminal Screen", new Vector3(0f, 1.65f, -0.55f), new Vector3(1.75f, 0.9f, 0.12f), teal);
        CreatePiece(PrimitiveType.Cylinder, "Ranch Sample", new Vector3(0f, 2.55f, 0f), new Vector3(0.38f, 0.65f, 0.38f), white);

        BoxCollider trigger = terminalRoot.AddComponent<BoxCollider>();
        trigger.center = new Vector3(0f, 1.2f, 0f);
        trigger.size = new Vector3(3.2f, 3f, 3.2f);
        trigger.isTrigger = true;

        RanchLaboratoryInteractable interactable = terminalRoot.AddComponent<RanchLaboratoryInteractable>();
        interactable.Initialize(this);

        GameObject labelObject = new GameObject("Laboratory Production Label");
        TextMesh label = labelObject.AddComponent<TextMesh>();
        label.text = "RANCH LABORATORY\nProduction Research";
        label.fontSize = 46;
        label.characterSize = 0.075f;
        label.anchor = TextAnchor.MiddleCenter;
        label.alignment = TextAlignment.Center;
        label.color = Color.white;
        labelObject.transform.SetParent(terminalRoot.transform, false);
        labelObject.transform.localPosition = new Vector3(0f, 3.65f, 0f);
        labelObject.AddComponent<RanchBillboard>();

        RefreshTerminalVisibility();
    }

    private void Update()
    {
        if (core == null)
            return;

        RefreshTerminalVisibility();

        if (IsOpen && Input.GetKeyDown(KeyCode.Escape))
            CloseMenu();
    }

    public void OpenMenu()
    {
        if (core.Shop.StructureLevel < 3)
        {
            core.ShowMessage("Build the Ranch Laboratory before researching production.");
            return;
        }

        if (IsOpen || core.Shop.IsOpen || core.Progression.IsOpen || core.Settings.IsOpen ||
            (core.Classes != null && core.Classes.IsOpen) || core.GameWon || core.Health.IsDead)
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

    private void RefreshTerminalVisibility()
    {
        bool visible = core != null && core.Shop != null && core.Shop.StructureLevel >= 3;
        if (terminalRoot != null && terminalRoot.activeSelf != visible)
            terminalRoot.SetActive(visible);
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

        GUI.Box(new Rect(300f, 100f, 1000f, 700f), GUIContent.none, backgroundStyle);
        GUI.Label(new Rect(380f, 135f, 840f, 60f), "RANCH LABORATORY", titleStyle);

        GUI.Box(new Rect(390f, 230f, 820f, 390f), GUIContent.none, cardStyle);
        float cost = core.Shop.GetNextResearchCost();
        string status =
            "Production Research Level: " + core.Shop.ResearchLevel + "/6\n\n" +
            "Hand extraction multiplier: " + core.Shop.ExtractionResearchMultiplier.ToString("0.00") + "x\n" +
            "Passive Ranch multiplier: " + (1f + core.Shop.ResearchLevel * 0.20f).ToString("0.00") + "x\n\n" +
            "Production research is no longer part of the Ranch Knowledge tree. All Ranch production upgrades are performed here.";
        GUI.Label(new Rect(440f, 280f, 720f, 220f), status, bodyStyle);

        bool oldEnabled = GUI.enabled;
        GUI.enabled = cost >= 0f;
        if (GUI.Button(new Rect(500f, 525f, 600f, 65f),
            cost < 0f ? "PRODUCTION RESEARCH MAXED" : "UPGRADE PRODUCTION — $" + cost.ToString("F0"),
            buttonStyle))
        {
            core.Shop.BuyNextResearch();
        }
        GUI.enabled = oldEnabled;

        if (GUI.Button(new Rect(600f, 690f, 400f, 62f), "LEAVE LABORATORY [ESC]", buttonStyle))
        {
            CloseMenu();
            GUIUtility.ExitGUI();
        }

        GUI.matrix = old;
    }

    private GameObject CreatePiece(PrimitiveType type, string name, Vector3 localPosition, Vector3 localScale, Material material)
    {
        GameObject piece = GameObject.CreatePrimitive(type);
        piece.name = name;
        piece.transform.SetParent(terminalRoot.transform, false);
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
        buttonTexture = MakeTexture(new Color(0.10f, 0.52f, 0.45f, 1f));
        backgroundStyle = new GUIStyle(GUI.skin.box);
        backgroundStyle.normal.background = backgroundTexture;
        cardStyle = new GUIStyle(GUI.skin.box);
        cardStyle.normal.background = cardTexture;
        titleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 36,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        titleStyle.normal.textColor = Color.white;
        bodyStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 21,
            wordWrap = true,
            alignment = TextAnchor.UpperLeft
        };
        bodyStyle.normal.textColor = Color.white;
        buttonStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 20,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            wordWrap = true
        };
        buttonStyle.normal.background = buttonTexture;
        buttonStyle.normal.textColor = Color.white;
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
