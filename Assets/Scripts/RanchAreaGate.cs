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

            float requirement = core.Areas.GetUnlockRequirement(AreaIndex);
            float collected = core.Areas.GetTotalRanchProgress();

            if (collected + 0.0001f >= requirement)
            {
                return
                    "Press E: Open " +
                    core.Areas.GetAreaName(AreaIndex) +
                    " — total Ranch requirement reached";
            }

            return
                "Collect total Ranch: " +
                collected.ToString("F0") +
                "/" +
                requirement.ToString("F0") +
                " for " +
                core.Areas.GetAreaName(AreaIndex);
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
            float requirement = core.Areas.GetUnlockRequirement(AreaIndex);
            float collected = core.Areas.GetTotalRanchProgress();
            bool ready = collected + 0.0001f >= requirement;

            if (open)
            {
                label.text =
                    core.Areas.GetAreaName(AreaIndex).ToUpperInvariant() +
                    "\nOPEN";
                label.color = new Color(0.45f, 1f, 0.55f);
            }
            else if (ready)
            {
                label.text =
                    core.Areas.GetAreaName(AreaIndex).ToUpperInvariant() +
                    "\nREADY — PRESS E" +
                    "\n" + requirement.ToString("F0") + " TOTAL RANCH COLLECTED";
                label.color = new Color(1f, 0.9f, 0.35f);
            }
            else
            {
                label.text =
                    core.Areas.GetAreaName(AreaIndex).ToUpperInvariant() +
                    "\nLOCKED" +
                    "\n" + collected.ToString("F0") +
                    "/" + requirement.ToString("F0") +
                    " TOTAL RANCH";
                label.color = Color.white;
            }
        }
    }
}
