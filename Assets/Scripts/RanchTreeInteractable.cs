using UnityEngine;

public class RanchTreeInteractable : RanchInteractable
{
    private RanchTreeSystem tree;

    public override string Prompt => RanchGameCore.Instance == null
        ? "Hold E: Extract Ranch"
        : $"Hold E: Extract Ranch with {RanchGameCore.Instance.Upgrades.CurrentToolName}";

    public override bool UsesHeldInteraction => true;

    public void Initialize(RanchTreeSystem treeSystem) => tree = treeSystem;

    public override void Interact(RanchPlayerController player, bool held, float deltaTime)
    {
        if (held && tree != null) tree.Extract(deltaTime);
    }
}
