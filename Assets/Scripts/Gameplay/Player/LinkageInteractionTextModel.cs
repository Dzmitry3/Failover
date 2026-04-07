public readonly struct LinkageInteractionTextModel
{
    public LinkageInteractionTextModel(
        string promptText,
        string windowTitle,
        string sequenceText,
        string progressText,
        string statusMessage)
    {
        PromptText = promptText;
        WindowTitle = windowTitle;
        SequenceText = sequenceText;
        ProgressText = progressText;
        StatusMessage = statusMessage;
    }

    public string PromptText { get; }
    public string WindowTitle { get; }
    public string SequenceText { get; }
    public string ProgressText { get; }
    public string StatusMessage { get; }

    public static LinkageInteractionTextModel Create(LinkageCaptureTarget captureTarget, ArrowSequenceMiniGame miniGame)
    {
        if (miniGame == null)
            return default;

        bool isFabricatorInteraction = captureTarget != null && captureTarget.IsFabricatorInteraction;
        string promptText = isFabricatorInteraction
            ? (captureTarget.IsPrerequisiteMet
                ? "Нажмите F, чтобы захватить Fabricator"
                : "Сначала захватите узел связи")
            : "Нажмите F для взлома узла";
        string windowTitle = isFabricatorInteraction ? "Взлом Fabricator" : "Взлом LinkageNode";

        return new LinkageInteractionTextModel(
            promptText,
            windowTitle,
            miniGame.SequenceText,
            miniGame.ProgressText,
            miniGame.StatusMessage);
    }
}
