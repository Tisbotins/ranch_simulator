using UnityEngine;

/// <summary>The Ranch Rocket standing on each world's landing pad.</summary>
public class RanchRocketInteractable : RanchInteractable
{
    private RanchSpaceSystem space;

    public void Initialize(RanchSpaceSystem system)
    {
        space = system;
    }

    public override string Prompt => "Press E: Ranch Rocket console";

    public override void Interact(RanchPlayerController player, bool held, float deltaTime)
    {
        if (!held)
            space?.OpenConsole();
    }
}
