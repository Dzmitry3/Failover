using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(LinkageInteractionController))]
public class LinkageInteractionOverlay : MonoBehaviour
{
    [Header("Prompt")]
    [SerializeField] private float promptWidth = 520f;
    [SerializeField] private float promptBottomOffset = 110f;

    [Header("Window")]
    [SerializeField] private float windowWidth = 540f;
    [SerializeField] private float windowHeight = 220f;

    [Header("Style")]
    [SerializeField] private int labelFontSize = 22;
    [SerializeField] private int sequenceFontSize = 34;

    private LinkageInteractionController interactionController;

    private void Awake()
    {
        interactionController = GetComponent<LinkageInteractionController>();
    }

    private void OnGUI()
    {
        if (interactionController == null || !interactionController.CanRenderUi)
            return;

        LinkageInteractionTextModel textModel = interactionController.TextModel;
        GUIStyle centeredStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = labelFontSize
        };
        centeredStyle.normal.textColor = Color.white;

        if (interactionController.ShowPrompt)
        {
            Rect promptRect = new Rect(
                (Screen.width - promptWidth) * 0.5f,
                Screen.height - promptBottomOffset,
                promptWidth,
                40f);

            GUI.Label(promptRect, textModel.PromptText, centeredStyle);
            return;
        }

        if (!interactionController.ShowWindow)
            return;

        Rect windowRect = new Rect(
            (Screen.width - windowWidth) * 0.5f,
            (Screen.height - windowHeight) * 0.5f,
            windowWidth,
            windowHeight);

        GUI.Box(windowRect, textModel.WindowTitle);

        GUIStyle sequenceStyle = new GUIStyle(centeredStyle)
        {
            fontSize = sequenceFontSize
        };

        GUI.Label(
            new Rect(windowRect.x + 20f, windowRect.y + 65f, windowRect.width - 40f, 50f),
            textModel.SequenceText,
            sequenceStyle);

        GUI.Label(
            new Rect(windowRect.x + 20f, windowRect.y + 130f, windowRect.width - 40f, 30f),
            textModel.ProgressText,
            centeredStyle);

        if (!string.IsNullOrEmpty(textModel.StatusMessage))
        {
            GUI.Label(
                new Rect(windowRect.x + 20f, windowRect.y + 160f, windowRect.width - 40f, 30f),
                textModel.StatusMessage,
                centeredStyle);
        }
    }
}
