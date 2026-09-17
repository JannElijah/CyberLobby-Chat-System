public interface IInteractable
{
    /// <summary>
    /// Called when a player interacts with this object.
    /// </summary>
    /// <param name="interactor">The player script performing the interaction.</param>
    void Interact(PlayerInteraction interactor);
    
    /// <summary>
    /// Called to visually highlight the object when looked at.
    /// </summary>
    void SetHighlight(bool isHighlighted);
}
