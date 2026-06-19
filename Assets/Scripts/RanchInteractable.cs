using UnityEngine;

public abstract class RanchInteractable : MonoBehaviour
{
    public abstract string Prompt { get; }
    public virtual bool UsesHeldInteraction => false;
    public abstract void Interact(RanchPlayerController player, bool held, float deltaTime);
}
