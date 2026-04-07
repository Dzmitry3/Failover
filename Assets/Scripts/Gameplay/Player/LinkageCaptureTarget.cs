public sealed class LinkageCaptureTarget
{
    private readonly Fabricator fabricator;
    private readonly LinkageNode linkageNode;
    private readonly LinkageNode requiredLinkageNode;

    public LinkageCaptureTarget(Fabricator fabricator, LinkageNode linkageNode, LinkageNode requiredLinkageNode)
    {
        this.fabricator = fabricator;
        this.linkageNode = linkageNode;
        this.requiredLinkageNode = requiredLinkageNode;
    }

    public bool IsFabricatorInteraction => fabricator != null;
    public bool IsPrerequisiteMet => !IsFabricatorInteraction || requiredLinkageNode == null || requiredLinkageNode.IsCaptured;
    public bool CanInteract => IsFabricatorInteraction
        ? fabricator != null &&
          fabricator.CurrentState != FabricatorState.Captured &&
          fabricator.CurrentState != FabricatorState.Destroyed &&
          IsPrerequisiteMet
        : linkageNode != null && linkageNode.CanInteract;
    public bool CanShowUi => IsFabricatorInteraction
        ? fabricator != null &&
          fabricator.CurrentState != FabricatorState.Captured &&
          fabricator.CurrentState != FabricatorState.Destroyed
        : linkageNode != null && linkageNode.CanInteract;

    public bool TryCapture()
    {
        if (IsFabricatorInteraction)
        {
            if (!IsPrerequisiteMet || fabricator == null)
                return false;

            fabricator.SetCaptured();
            return fabricator.CurrentState == FabricatorState.Captured;
        }

        return linkageNode != null && linkageNode.TryCapture();
    }
}
