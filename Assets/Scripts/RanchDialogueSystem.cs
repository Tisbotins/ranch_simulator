using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// RPG-style conversation layer used by every NPC.
///
/// A conversation is a queue of lines attributed to a speaker. Text reveals a
/// character at a time, the player advances with E or left-click (or skips the
/// reveal by pressing during it), and a conversation can end in a set of
/// choices that invoke callbacks — which is how NPCs open their menus, hand out
/// classes, or branch.
///
/// The game pauses while a conversation runs, using the same rules as the other
/// full-screen menus so nothing can open on top of it.
/// </summary>
[DefaultExecutionOrder(-800)]
[DisallowMultipleComponent]
public class RanchDialogueSystem : MonoBehaviour
{
    private const float CharactersPerSecond = 45f;

    private sealed class Line
    {
        public string Speaker;
        public string Text;
    }

    /// <summary>A player-selectable reply. Disabled options show a reason.</summary>
    public sealed class Choice
    {
        public string Text;
        public Action OnChosen;
        public bool Enabled = true;
        public string DisabledReason;
    }

    public bool IsOpen { get; private set; }
    public string CurrentSpeaker { get; private set; } = "";

    private readonly Queue<Line> pending = new Queue<Line>();
    private readonly List<Choice> choices = new List<Choice>();

    private RanchGameCore core;
    private string fullText = "";
    private float revealed;
    private bool showingChoices;
    private float previousTimeScale = 1f;

    private Texture2D boxTexture;
    private Texture2D namePlateTexture;
    private Texture2D choiceTexture;
    private Texture2D choiceHoverTexture;
    private GUIStyle boxStyle;
    private GUIStyle namePlateStyle;
    private GUIStyle nameStyle;
    private GUIStyle bodyStyle;
    private GUIStyle choiceStyle;
    private GUIStyle hintStyle;
    private bool stylesReady;

    public void Initialize(RanchGameCore gameCore)
    {
        core = gameCore;
    }

    // ------------------------------------------------------------------ start

    /// <summary>Begins a conversation. Later calls replace any in progress.</summary>
    public void Begin(string speaker, params string[] lines)
    {
        BeginWithChoices(speaker, null, lines);
    }

    /// <summary>
    /// Begins a conversation that ends in a choice menu. Pass null or an empty
    /// list to simply end after the final line.
    /// </summary>
    public void BeginWithChoices(
        string speaker,
        List<Choice> endChoices,
        params string[] lines)
    {
        if (core == null || lines == null || lines.Length == 0)
            return;

        pending.Clear();
        choices.Clear();

        for (int i = 0; i < lines.Length; i++)
        {
            if (!string.IsNullOrEmpty(lines[i]))
                pending.Enqueue(new Line { Speaker = speaker, Text = lines[i] });
        }

        if (pending.Count == 0)
            return;

        if (endChoices != null)
            choices.AddRange(endChoices);

        if (!IsOpen)
        {
            IsOpen = true;
            previousTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        showingChoices = false;
        AdvanceLine();
    }

    private void AdvanceLine()
    {
        if (pending.Count == 0)
        {
            // Out of lines: either offer the choices or close.
            if (choices.Count > 0)
            {
                showingChoices = true;
                return;
            }

            Close();
            return;
        }

        Line line = pending.Dequeue();
        CurrentSpeaker = line.Speaker;
        fullText = line.Text;
        revealed = 0f;
    }

    public void Close()
    {
        if (!IsOpen)
            return;

        IsOpen = false;
        showingChoices = false;
        pending.Clear();
        choices.Clear();
        fullText = "";
        CurrentSpeaker = "";

        Time.timeScale = previousTimeScale <= 0f ? 1f : previousTimeScale;

        bool busy = core != null &&
            (core.IsAnyMenuOpen ||
             core.GameWon ||
             (core.Health != null && (core.Health.IsDead || core.Health.IsDowned)));

        if (!busy)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    // ----------------------------------------------------------------- update

    private void Update()
    {
        if (!IsOpen)
            return;

        // Real time, because the conversation itself pauses the game.
        if (!showingChoices && revealed < fullText.Length)
            revealed += CharactersPerSecond * Time.unscaledDeltaTime;

        if (showingChoices)
            return;

        bool advance =
            Input.GetKeyDown(KeyCode.E) ||
            Input.GetMouseButtonDown(0) ||
            Input.GetKeyDown(KeyCode.Return);

        if (!advance)
            return;

        // First press completes the reveal, second moves on — the standard RPG
        // contract, so an impatient player never loses text.
        if (revealed < fullText.Length)
            revealed = fullText.Length;
        else
            AdvanceLine();
    }

    // ------------------------------------------------------------------- draw

    private void OnGUI()
    {
        if (!IsOpen)
            return;

        EnsureStyles();

        float width = Mathf.Min(980f, Screen.width - 80f);
        float height = 210f;
        float x = (Screen.width - width) * 0.5f;
        float y = Screen.height - height - 48f;

        Rect box = new Rect(x, y, width, height);
        GUI.Box(box, GUIContent.none, boxStyle);

        // Name plate sits above the box, overlapping its top edge.
        if (!string.IsNullOrEmpty(CurrentSpeaker))
        {
            Rect plate = new Rect(x + 26f, y - 20f, 300f, 40f);
            GUI.Box(plate, GUIContent.none, namePlateStyle);
            GUI.Label(plate, CurrentSpeaker.ToUpperInvariant(), nameStyle);
        }

        int shown = Mathf.Clamp(Mathf.FloorToInt(revealed), 0, fullText.Length);
        GUI.Label(
            new Rect(box.x + 30f, box.y + 34f, box.width - 60f, box.height - 80f),
            fullText.Substring(0, shown),
            bodyStyle
        );

        if (showingChoices)
            DrawChoices(box);
        else if (shown >= fullText.Length)
            GUI.Label(
                new Rect(box.x, box.y + box.height - 34f, box.width - 30f, 24f),
                pending.Count > 0 || choices.Count > 0 ? "E  ▸" : "E  ▪",
                hintStyle
            );
    }

    private void DrawChoices(Rect box)
    {
        float buttonHeight = 34f;
        float totalHeight = choices.Count * (buttonHeight + 6f);
        float startY = box.y + box.height - totalHeight - 14f;

        for (int i = 0; i < choices.Count; i++)
        {
            Choice choice = choices[i];
            Rect rect = new Rect(
                box.x + 40f,
                startY + i * (buttonHeight + 6f),
                box.width - 80f,
                buttonHeight
            );

            bool wasEnabled = GUI.enabled;
            GUI.enabled = choice.Enabled;

            string label = choice.Enabled
                ? "› " + choice.Text
                : "✕ " + choice.Text +
                  (string.IsNullOrEmpty(choice.DisabledReason)
                      ? ""
                      : "   — " + choice.DisabledReason);

            if (GUI.Button(rect, label, choiceStyle))
            {
                Action callback = choice.OnChosen;
                Close();
                callback?.Invoke();
                GUI.enabled = wasEnabled;
                GUIUtility.ExitGUI();
                return;
            }

            GUI.enabled = wasEnabled;
        }
    }

    private void EnsureStyles()
    {
        if (stylesReady)
            return;

        const int radius = 14;
        RectOffset border = RanchVisuals.PanelBorder(radius);

        boxTexture = RanchVisuals.CreatePanelTexture(
            new Color(0.09f, 0.10f, 0.14f, 0.98f),
            new Color(0.03f, 0.04f, 0.06f, 0.99f),
            new Color(0.42f, 0.48f, 0.58f, 0.95f), radius);

        namePlateTexture = RanchVisuals.CreatePanelTexture(
            RanchVisuals.AccentTop, RanchVisuals.AccentBottom,
            RanchVisuals.AccentBorder, 10);

        choiceTexture = RanchVisuals.CreatePanelTexture(
            new Color(0.14f, 0.18f, 0.24f, 0.95f),
            new Color(0.08f, 0.11f, 0.15f, 0.97f),
            new Color(0.30f, 0.38f, 0.48f, 0.8f), 8);

        choiceHoverTexture = RanchVisuals.CreatePanelTexture(
            new Color(0.18f, 0.42f, 0.46f, 1f),
            new Color(0.10f, 0.26f, 0.30f, 1f),
            new Color(0.45f, 0.88f, 0.85f, 1f), 8);

        boxStyle = new GUIStyle(GUI.skin.box);
        boxStyle.normal.background = boxTexture;
        boxStyle.border = border;

        namePlateStyle = new GUIStyle(GUI.skin.box);
        namePlateStyle.normal.background = namePlateTexture;
        namePlateStyle.border = RanchVisuals.PanelBorder(10);

        nameStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 19,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        nameStyle.normal.textColor = Color.white;
        RanchVisuals.UseDisplayFont(nameStyle);

        bodyStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 19,
            wordWrap = true,
            alignment = TextAnchor.UpperLeft,
            richText = true
        };
        bodyStyle.normal.textColor = RanchVisuals.TextPrimary;

        choiceStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 17,
            alignment = TextAnchor.MiddleLeft,
            padding = new RectOffset(16, 16, 4, 4)
        };
        choiceStyle.normal.background = choiceTexture;
        choiceStyle.hover.background = choiceHoverTexture;
        choiceStyle.active.background = choiceHoverTexture;
        choiceStyle.border = RanchVisuals.PanelBorder(8);
        choiceStyle.normal.textColor = RanchVisuals.TextPrimary;
        choiceStyle.hover.textColor = Color.white;

        hintStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 15,
            alignment = TextAnchor.MiddleRight
        };
        hintStyle.normal.textColor = RanchVisuals.TextMuted;

        stylesReady = true;
    }
}
