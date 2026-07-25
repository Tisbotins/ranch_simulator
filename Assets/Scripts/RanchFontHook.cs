using UnityEngine;

/// <summary>
/// Installs the project's body font as the global IMGUI default.
///
/// Every GUIStyle in the game is built from GUI.skin (label/box/button) and
/// none of them set their own font, so their font field stays null and IMGUI
/// falls back to GUI.skin.font at draw time. Assigning that one field therefore
/// re-fonts every menu, HUD, and panel in the project at once, without editing
/// the eleven files that construct styles.
///
/// The execution order is deliberately lower than every other OnGUI in the
/// project (the multiplayer overlay is -850) so the font is in place before
/// anything draws this frame.
/// </summary>
[DefaultExecutionOrder(-2000)]
[DisallowMultipleComponent]
public class RanchFontHook : MonoBehaviour
{
    private void OnGUI()
    {
        // Re-applied each frame rather than once at startup: GUI.skin can be
        // reassigned by other code, and this is a cheap field write.
        RanchVisuals.ApplyGlobalFont();
    }
}
