using UnityEngine;

public class RanchAreaGate : RanchInteractable
{
    public int AreaIndex { get; private set; }

    private RanchGameCore core;
    private GameObject blocker;
    private TextMesh label;

    public override string Prompt
    {
        get
        {
            if (core == null || core.Areas == null)
                return "Area gate unavailable";

            if (core.Areas.IsUnlocked(AreaIndex))
                return core.Areas.GetAreaName(AreaIndex) + " — OPEN";

            return
                "Press E: Unlock " +
                core.Areas.GetAreaName(AreaIndex) +
                " for " +
                core.Areas.GetUnlockCost(AreaIndex).ToString("F0") +
                " raw Ranch";
        }
    }

    public void Initialize(
        RanchGameCore gameCore,
        int areaIndex,
        GameObject gateBlocker,
        TextMesh gateLabel)
    {
        core = gameCore;
        AreaIndex = areaIndex;
        blocker = gateBlocker;
        label = gateLabel;
        core.Areas.RegisterGate(this);
        RefreshState();
    }

    public override void Interact(
        RanchPlayerController player,
        bool held,
        float deltaTime)
    {
        if (!held && core != null)
            core.Areas.TryUnlock(AreaIndex);
    }

    public void RefreshState()
    {
        if (core == null || core.Areas == null)
            return;

        bool open = core.Areas.IsUnlocked(AreaIndex);

        if (blocker != null)
            blocker.SetActive(!open);

        if (label != null)
        {
            label.text = open
                ? core.Areas.GetAreaName(AreaIndex).ToUpperInvariant() + "\nOPEN"
                : core.Areas.GetAreaName(AreaIndex).ToUpperInvariant() +
                  "\nLOCKED — " +
                  core.Areas.GetUnlockCost(AreaIndex).ToString("F0") +
                  " RAW RANCH";

            label.color = open ? new Color(0.45f, 1f, 0.55f) : Color.white;
        }
    }
}
