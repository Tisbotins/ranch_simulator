using System.Collections.Generic;
using System.Text;
using UnityEngine;

public class RanchAreaSystem : MonoBehaviour
{
    public const int AreaCount = 4;

    private readonly string[] areaNames =
    {
        "Ranch Homestead",
        "Laboratory District",
        "Industrial Expanse",
        "Citadel Grounds"
    };

    private readonly float[] unlockCosts =
    {
        0f,
        500f,
        2000f,
        6000f
    };

    private readonly bool[] unlocked =
    {
        true,
        false,
        false,
        false
    };

    private readonly List<RanchAreaGate> gates = new List<RanchAreaGate>();
    private RanchGameCore core;

    public void Initialize(RanchGameCore gameCore)
    {
        core = gameCore;
    }

    public string GetAreaName(int areaIndex)
    {
        return ValidArea(areaIndex) ? areaNames[areaIndex] : "Unknown Area";
    }

    public float GetUnlockCost(int areaIndex)
    {
        return ValidArea(areaIndex) ? unlockCosts[areaIndex] : -1f;
    }

    public bool IsUnlocked(int areaIndex)
    {
        return ValidArea(areaIndex) && unlocked[areaIndex];
    }

    public void RegisterGate(RanchAreaGate gate)
    {
        if (gate == null || gates.Contains(gate))
            return;

        gates.Add(gate);
        gate.RefreshState();
    }

    public bool TryUnlock(int areaIndex)
    {
        if (!ValidArea(areaIndex) || areaIndex == 0)
            return false;

        if (unlocked[areaIndex])
        {
            core.ShowMessage(GetAreaName(areaIndex) + " is already unlocked.");
            return true;
        }

        if (!unlocked[areaIndex - 1])
        {
            core.ShowMessage("Unlock " + GetAreaName(areaIndex - 1) + " first.");
            return false;
        }

        float cost = unlockCosts[areaIndex];
        if (!core.Inventory.TrySpendRawRanch(cost))
        {
            core.ShowMessage(
                "Need " + cost.ToString("F0") + " raw Ranch to unlock " + GetAreaName(areaIndex) + ".",
                7f
            );
            return false;
        }

        unlocked[areaIndex] = true;
        RefreshGates();
        core.Progression.AddExperience(120f * areaIndex, "Area unlocked");
        core.Save.RequestSave();
        core.ShowMessage(GetAreaName(areaIndex) + " unlocked. The white barrier has opened.", 8f);
        return true;
    }

    public bool CanBuildStructureLevel(int nextStructureLevel, out string reason)
    {
        int requiredArea = GetRequiredAreaForStructure(nextStructureLevel);
        if (IsUnlocked(requiredArea))
        {
            reason = "";
            return true;
        }

        reason = "Unlock " + GetAreaName(requiredArea) + " before building that structure.";
        return false;
    }

    public int GetRequiredAreaForStructure(int structureLevel)
    {
        if (structureLevel <= 2)
            return 0;
        if (structureLevel <= 5)
            return 1;
        if (structureLevel <= 7)
            return 2;
        return 3;
    }

    public bool[] GetUnlockStateCopy()
    {
        bool[] copy = new bool[AreaCount];
        for (int i = 0; i < AreaCount; i++)
            copy[i] = unlocked[i];
        return copy;
    }

    public void RestoreState(bool[] savedUnlocks)
    {
        unlocked[0] = true;

        for (int i = 1; i < AreaCount; i++)
        {
            bool saved = savedUnlocks != null && i < savedUnlocks.Length && savedUnlocks[i];
            unlocked[i] = saved && unlocked[i - 1];
        }

        RefreshGates();
    }

    public string GetStatusSummary()
    {
        StringBuilder text = new StringBuilder();
        for (int i = 0; i < AreaCount; i++)
        {
            text.Append(areaNames[i]);
            text.Append(": ");
            text.Append(unlocked[i] ? "OPEN" : unlockCosts[i].ToString("F0") + " Ranch");
            if (i < AreaCount - 1)
                text.AppendLine();
        }
        return text.ToString();
    }

    private void RefreshGates()
    {
        for (int i = gates.Count - 1; i >= 0; i--)
        {
            if (gates[i] == null)
                gates.RemoveAt(i);
            else
                gates[i].RefreshState();
        }
    }

    private static bool ValidArea(int areaIndex)
    {
        return areaIndex >= 0 && areaIndex < AreaCount;
    }
}
