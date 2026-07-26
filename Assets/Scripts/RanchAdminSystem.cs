using UnityEngine;

/// <summary>
/// Developer commands. Currently just the save wipe.
///
/// Ctrl + Shift + Delete clears all save data. It is deliberately awkward to
/// reach and deliberately two-step: the first press only arms the command and
/// shows a warning, and a second press inside the confirmation window carries
/// it out. Erasing a run is not undoable, so a single stray keypress must not
/// be able to do it.
/// </summary>
[DefaultExecutionOrder(-780)]
[DisallowMultipleComponent]
public class RanchAdminSystem : MonoBehaviour
{
    private const float ConfirmWindowSeconds = 6f;

    private RanchGameCore core;
    private float armedUntil;

    private GUIStyle warningStyle;
    private Texture2D warningTexture;
    private bool stylesReady;

    private bool IsArmed => Time.unscaledTime < armedUntil;

    public void Initialize(RanchGameCore gameCore)
    {
        core = gameCore;
    }

    private void Update()
    {
        if (core == null)
            return;

        bool modifiers =
            (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) &&
            (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift));

        if (!modifiers || !Input.GetKeyDown(KeyCode.Delete))
            return;

        if (IsArmed)
            WipeSaveData();
        else
            Arm();
    }

    private void Arm()
    {
        armedUntil = Time.unscaledTime + ConfirmWindowSeconds;
        core.ShowMessage(
            "ADMIN: press Ctrl+Shift+Delete again within " +
            Mathf.CeilToInt(ConfirmWindowSeconds) +
            "s to ERASE ALL SAVE DATA. This cannot be undone.",
            ConfirmWindowSeconds
        );
    }

    private void WipeSaveData()
    {
        armedUntil = 0f;

        if (core.Save != null)
            core.Save.DeleteAllSaves();

        Debug.Log("ADMIN: all Ranch Simulator save data erased.");

        // Reloading is part of the wipe, not a convenience. The systems still
        // hold the old run in memory, and the autosave timer would write it
        // straight back to disk within a minute — the file would reappear and
        // the wipe would look like it had failed.
        RanchGameModeState.ResetToTitle();
        Time.timeScale = 1f;

        if (core != null)
            core.RestartScene();
    }

    private void OnGUI()
    {
        if (!IsArmed)
            return;

        EnsureStyles();

        float width = 640f;
        Rect rect = new Rect((Screen.width - width) * 0.5f, 24f, width, 44f);
        GUI.Box(rect, GUIContent.none, warningStyle);
        GUI.Label(
            rect,
            "⚠  ERASE ALL SAVE DATA?  Press Ctrl+Shift+Delete again to confirm  (" +
            Mathf.CeilToInt(armedUntil - Time.unscaledTime) + "s)",
            warningStyle
        );
    }

    private void EnsureStyles()
    {
        if (stylesReady)
            return;

        warningTexture = RanchVisuals.CreatePanelTexture(
            RanchVisuals.DangerTop,
            RanchVisuals.DangerBottom,
            RanchVisuals.DangerBorder,
            10
        );

        warningStyle = new GUIStyle(GUI.skin.box)
        {
            fontSize = 16,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            wordWrap = true
        };
        warningStyle.normal.background = warningTexture;
        warningStyle.border = RanchVisuals.PanelBorder(10);
        warningStyle.normal.textColor = Color.white;

        stylesReady = true;
    }
}
