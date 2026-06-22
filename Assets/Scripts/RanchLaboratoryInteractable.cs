using UnityEngine;

public class RanchLaboratoryInteractable : RanchInteractable
{
    private RanchLaboratorySystem laboratory;

    public void Initialize(RanchLaboratorySystem system)
    {
        laboratory = system;
    }

    public override string Prompt => "Press E: Open Ranch Laboratory production research";

    public override void Interact(RanchPlayerController player, bool held, float deltaTime)
    {
        if (!held)
            laboratory?.OpenMenu();
    }
}
