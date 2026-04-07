using UnityEngine;

public sealed class FabricatorInteractionInstaller
{
    public LinkageInteractionController EnsureInstalled(Fabricator fabricator, LinkageNode prerequisiteNode)
    {
        if (fabricator == null)
            return null;

        if (fabricator.GetComponent<ArrowSequenceMiniGame>() == null)
            fabricator.gameObject.AddComponent<ArrowSequenceMiniGame>();

        LinkageInteractionController controller = fabricator.GetComponent<LinkageInteractionController>();
        if (controller == null)
            controller = fabricator.gameObject.AddComponent<LinkageInteractionController>();

        if (fabricator.GetComponent<LinkageInteractionOverlay>() == null)
            fabricator.gameObject.AddComponent<LinkageInteractionOverlay>();

        controller.ConfigureFabricatorInteraction(fabricator, prerequisiteNode);
        return controller;
    }
}
