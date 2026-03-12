using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerController))]
public class LinkageNodeInteractionController : MonoBehaviour
{
    [Header("Interaction")]
    [SerializeField] private string linkageNodeName = "LinkageNode";
    [SerializeField] private float interactionDistance = 3f;
    [SerializeField] private LinkageNode linkageNode;

    [Header("Mini-game")]
    [SerializeField] private int sequenceLength = 4;
    [SerializeField] private float retryDelaySeconds = 2f;

    [Header("References")]
    [SerializeField] private PlayerController playerController;

    private ArrowDirection[] sequence = System.Array.Empty<ArrowDirection>();
    private int currentInputIndex;
    private string statusMessage = string.Empty;
    private bool isNearNode;
    private bool isMiniGameActive;
    private bool isCooldownActive;
    private Coroutine retryCoroutine;

    public bool IsNearNode => isNearNode;
    public bool IsMiniGameActive => isMiniGameActive;
    public bool IsCooldownActive => isCooldownActive;
    public int CurrentInputIndex => currentInputIndex;
    public int SequenceLength => sequence.Length;
    public string StatusMessage => statusMessage;
    public bool CanRenderUi => CanInteract && isNearNode;
    public bool ShowPrompt => CanRenderUi && !isMiniGameActive && !isCooldownActive;
    public bool ShowWindow => CanRenderUi && (isMiniGameActive || isCooldownActive);

    public string SequenceText
    {
        get
        {
            if (sequence.Length == 0)
                return string.Empty;

            string text = string.Empty;
            for (int i = 0; i < sequence.Length; i++)
            {
                if (i > 0)
                    text += "  ";

                text += DirectionToSymbol(sequence[i]);
            }

            return text;
        }
    }

    public string ProgressText => isCooldownActive
        ? "\u041e\u0436\u0438\u0434\u0430\u043d\u0438\u0435 \u043f\u0435\u0440\u0435\u0434 \u043f\u043e\u0432\u0442\u043e\u0440\u043e\u043c..."
        : $"\u0412\u0432\u0435\u0434\u0435\u043d\u043e: {currentInputIndex}/{sequence.Length}";

    private bool CanInteract => linkageNode != null && !linkageNode.IsCaptured;

    private void Awake()
    {
        if (playerController == null)
            playerController = GetComponent<PlayerController>();

        if (linkageNode == null)
        {
            GameObject linkageNodeObject = GameObject.Find(linkageNodeName);
            if (linkageNodeObject != null)
                linkageNode = linkageNodeObject.GetComponent<LinkageNode>();
        }
    }

    private void Update()
    {
        if (!CanInteract)
        {
            ResetInteraction(clearStatusMessage: true);
            return;
        }

        isNearNode = IsWithinInteractionRange();
        if (!isNearNode)
        {
            ResetInteraction(clearStatusMessage: true);
            return;
        }

        if (Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame)
        {
            if (isMiniGameActive || isCooldownActive)
            {
                CloseMiniGameWindow();
                return;
            }

            StartMiniGame();
            return;
        }

        if (!isMiniGameActive)
            return;

        if (TryGetPressedArrow(out ArrowDirection pressedDirection))
            ProcessArrowInput(pressedDirection);
    }

    private void OnDisable()
    {
        ResetInteraction(clearStatusMessage: false);
    }

    private bool IsWithinInteractionRange()
    {
        return Vector3.Distance(transform.position, linkageNode.transform.position) <= interactionDistance;
    }

    private void StartMiniGame()
    {
        if (retryCoroutine != null)
        {
            StopCoroutine(retryCoroutine);
            retryCoroutine = null;
        }

        isMiniGameActive = true;
        isCooldownActive = false;
        currentInputIndex = 0;
        statusMessage = string.Empty;

        int resolvedLength = Mathf.Max(1, sequenceLength);
        sequence = new ArrowDirection[resolvedLength];
        for (int i = 0; i < sequence.Length; i++)
            sequence[i] = (ArrowDirection)Random.Range(0, 4);

        if (playerController != null)
            playerController.SetMovementLocked(true);
    }

    private bool TryGetPressedArrow(out ArrowDirection pressedDirection)
    {
        pressedDirection = ArrowDirection.Up;
        if (Keyboard.current == null)
            return false;

        if (Keyboard.current.upArrowKey.wasPressedThisFrame)
        {
            pressedDirection = ArrowDirection.Up;
            return true;
        }

        if (Keyboard.current.downArrowKey.wasPressedThisFrame)
        {
            pressedDirection = ArrowDirection.Down;
            return true;
        }

        if (Keyboard.current.leftArrowKey.wasPressedThisFrame)
        {
            pressedDirection = ArrowDirection.Left;
            return true;
        }

        if (Keyboard.current.rightArrowKey.wasPressedThisFrame)
        {
            pressedDirection = ArrowDirection.Right;
            return true;
        }

        return false;
    }

    private void ProcessArrowInput(ArrowDirection pressedDirection)
    {
        if (sequence.Length == 0)
            return;

        if (pressedDirection == sequence[currentInputIndex])
        {
            currentInputIndex++;
            if (currentInputIndex >= sequence.Length)
                CompleteMiniGame();

            return;
        }

        isMiniGameActive = false;
        isCooldownActive = true;
        currentInputIndex = 0;
        statusMessage = "\u041d\u0435\u0432\u0435\u0440\u043d\u0430\u044f \u043f\u043e\u0441\u043b\u0435\u0434\u043e\u0432\u0430\u0442\u0435\u043b\u044c\u043d\u043e\u0441\u0442\u044c. \u041f\u043e\u0432\u0442\u043e\u0440 \u0447\u0435\u0440\u0435\u0437 2 \u0441\u0435\u043a\u0443\u043d\u0434\u044b...";

        if (playerController != null)
            playerController.SetMovementLocked(false);

        if (retryCoroutine != null)
            StopCoroutine(retryCoroutine);

        retryCoroutine = StartCoroutine(RestartMiniGameAfterDelay());
    }

    private IEnumerator RestartMiniGameAfterDelay()
    {
        yield return new WaitForSeconds(retryDelaySeconds);
        retryCoroutine = null;

        if (!CanInteract || !isNearNode)
        {
            ResetInteraction(clearStatusMessage: true);
            yield break;
        }

        isCooldownActive = false;
        isMiniGameActive = true;
        currentInputIndex = 0;
        statusMessage = string.Empty;

        if (playerController != null)
            playerController.SetMovementLocked(true);
    }

    private void CompleteMiniGame()
    {
        bool captured = linkageNode.TryCapture();

        isMiniGameActive = false;
        isCooldownActive = false;
        currentInputIndex = 0;
        statusMessage = captured
            ? "\u0417\u0430\u0445\u0432\u0430\u0442 \u0432\u044b\u043f\u043e\u043b\u043d\u0435\u043d."
            : "\u041d\u0435 \u0443\u0434\u0430\u043b\u043e\u0441\u044c \u0437\u0430\u0445\u0432\u0430\u0442\u0438\u0442\u044c Fabricator.";

        if (playerController != null)
            playerController.SetMovementLocked(false);
    }

    private void CloseMiniGameWindow()
    {
        if (retryCoroutine != null)
        {
            StopCoroutine(retryCoroutine);
            retryCoroutine = null;
        }

        isMiniGameActive = false;
        isCooldownActive = false;
        currentInputIndex = 0;
        statusMessage = string.Empty;

        if (playerController != null)
            playerController.SetMovementLocked(false);
    }

    private void ResetInteraction(bool clearStatusMessage)
    {
        if (retryCoroutine != null)
        {
            StopCoroutine(retryCoroutine);
            retryCoroutine = null;
        }

        isNearNode = false;
        isMiniGameActive = false;
        isCooldownActive = false;
        currentInputIndex = 0;

        if (clearStatusMessage)
            statusMessage = string.Empty;

        if (playerController != null)
            playerController.SetMovementLocked(false);
    }

    private static string DirectionToSymbol(ArrowDirection direction)
    {
        switch (direction)
        {
            case ArrowDirection.Up:
                return "\u2191";
            case ArrowDirection.Down:
                return "\u2193";
            case ArrowDirection.Left:
                return "\u2190";
            case ArrowDirection.Right:
                return "\u2192";
            default:
                return "?";
        }
    }
}
