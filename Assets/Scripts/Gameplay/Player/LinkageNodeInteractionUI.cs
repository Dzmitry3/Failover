using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(LinkageNodeInteractionController))]
public class LinkageNodeInteractionUI : MonoBehaviour
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

    private LinkageNodeInteractionController interactionController;

    private void Awake()
    {
        interactionController = GetComponent<LinkageNodeInteractionController>();
    }

    private void OnGUI()
    {
        if (interactionController == null || !interactionController.CanRenderUi)
            return;

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

            GUI.Label(promptRect, interactionController.PromptText, centeredStyle);
            return;
        }

        if (!interactionController.ShowWindow)
            return;

        Rect windowRect = new Rect(
            (Screen.width - windowWidth) * 0.5f,
            (Screen.height - windowHeight) * 0.5f,
            windowWidth,
            windowHeight);

        GUI.Box(windowRect, interactionController.WindowTitle);

        GUIStyle sequenceStyle = new GUIStyle(centeredStyle)
        {
            fontSize = sequenceFontSize
        };

        GUI.Label(
            new Rect(windowRect.x + 20f, windowRect.y + 65f, windowRect.width - 40f, 50f),
            interactionController.SequenceText,
            sequenceStyle);

        GUI.Label(
            new Rect(windowRect.x + 20f, windowRect.y + 130f, windowRect.width - 40f, 30f),
            interactionController.ProgressText,
            centeredStyle);

        if (!string.IsNullOrEmpty(interactionController.StatusMessage))
        {
            GUI.Label(
                new Rect(windowRect.x + 20f, windowRect.y + 160f, windowRect.width - 40f, 30f),
                interactionController.StatusMessage,
                centeredStyle);
        }
    }
}
