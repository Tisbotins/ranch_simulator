using UnityEngine;

/// <summary>Giada Jade, who staffs the Research Facility.</summary>
public class RanchGiadaInteractable : RanchInteractable
{
    private RanchFacilitySystem facility;

    public void Initialize(RanchFacilitySystem system)
    {
        facility = system;
    }

    public override string Prompt => "Press E: Talk to Giada Jade";

    public override void Interact(RanchPlayerController player, bool held, float deltaTime)
    {
        if (!held)
            facility?.TalkToGiada();
    }
}
