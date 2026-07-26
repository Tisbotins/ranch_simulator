using UnityEngine;

/// <summary>
/// The doorway of the Research Facility. The same component serves both sides:
/// the exterior door carries entering = true, the interior pad entering = false.
/// </summary>
public class RanchFacilityDoor : RanchInteractable
{
    private RanchFacilitySystem facility;
    private bool entering;

    public void Initialize(RanchFacilitySystem system, bool isEntrance)
    {
        facility = system;
        entering = isEntrance;
    }

    public override string Prompt => entering
        ? "Press E: Enter the Ranch Research Facility"
        : "Press E: Step back outside";

    public override void Interact(RanchPlayerController player, bool held, float deltaTime)
    {
        if (held || facility == null)
            return;

        if (entering)
            facility.EnterFacility();
        else
            facility.ExitFacility();
    }
}
