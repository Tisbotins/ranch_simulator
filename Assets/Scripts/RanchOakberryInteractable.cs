using UnityEngine;

public class RanchOakberryInteractable : RanchInteractable
{
    private RanchClassSystem classSystem;

    public void Initialize(RanchClassSystem system)
    {
        classSystem = system;
    }

    public override string Prompt => "Press E: Talk to Dr. Oakberry and change class";

    public override void Interact(RanchPlayerController player, bool held, float deltaTime)
    {
        if (!held)
            classSystem?.TalkToOakberry();
    }
}
