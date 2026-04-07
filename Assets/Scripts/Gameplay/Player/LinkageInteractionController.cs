using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
[RequireComponent(typeof(ArrowSequenceMiniGame))]
public class LinkageInteractionController : MonoBehaviour
{
    private const float MinNavigateInputSqrMagnitude = 0.25f;

    [Header("Interaction")]
    [SerializeField] private float interactionDistance = 3f;
    [SerializeField] private LinkageNode linkageNode;
    [SerializeField] private Fabricator fabricator;
    [SerializeField] private LinkageNode requiredLinkageNode;
    [SerializeField] private ArrowSequenceMiniGame miniGame;

    public bool IsNearNode { get; private set; }
    public bool CanRenderUi => CanShowUi && IsNearNode;
    public bool ShowPrompt => CanRenderUi && !miniGame.IsRunning;
    public bool ShowWindow => CanRenderUi && miniGame.IsRunning;
    public LinkageInteractionTextModel TextModel => LinkageInteractionTextModel.Create(captureTarget, miniGame);

    private bool CanInteract => captureTarget != null && captureTarget.CanInteract;
    private bool CanShowUi => captureTarget != null && captureTarget.CanShowUi;
    private PlayerController PlayerController => linkageNode != null
        ? linkageNode.PlayerController
        : GetTargetPlayerController();
    private PlayerInput PlayerInput => GetTargetPlayerInput();

    private LinkageCaptureTarget captureTarget;
    private InputAction interactAction;
    private InputAction miniGameNavigateAction;
    private InputAction moveAction;
    private Vector2 lastNavigateInput;
    private bool moveActionWasEnabled;

    private void Awake()
    {
        if (linkageNode == null)
            linkageNode = GetComponent<LinkageNode>();

        if (fabricator == null)
            fabricator = GetComponent<Fabricator>();

        if (miniGame == null)
            miniGame = GetComponent<ArrowSequenceMiniGame>();

        RefreshCaptureTarget();
        ResolveInputActions();
    }

    private void OnEnable()
    {
        RefreshCaptureTarget();
        ResolveInputActions();
        lastNavigateInput = Vector2.zero;
    }

    private void Update()
    {
        if (!CanShowUi)
        {
            EndInteraction(clearStatusMessage: true);
            return;
        }

        IsNearNode = IsWithinInteractionRange();
        if (!IsNearNode)
        {
            EndInteraction(clearStatusMessage: true);
            return;
        }

        if (WasInteractPressedThisFrame())
        {
            if (miniGame.IsRunning)
            {
                CloseMiniGame();
                return;
            }

            if (!CanInteract)
                return;

            OpenMiniGame();
            return;
        }

        if (!miniGame.IsActive)
            return;

        if (!TryGetNavigatePressedThisFrame(out Vector2 navigateInput))
        {
            if (miniGame.IsCooldownActive)
                SetPlayerMovementLocked(false);

            return;
        }

        if (!miniGame.TryConsumeArrowInput(navigateInput, out bool completed))
            return;

        if (completed)
            CompleteMiniGame();
    }

    private void OnDisable()
    {
        EndInteraction(clearStatusMessage: false);
    }

    private bool IsWithinInteractionRange()
    {
        PlayerController playerController = PlayerController;
        if (playerController == null)
            return false;

        return Vector3.Distance(playerController.transform.position, transform.position) <= interactionDistance;
    }

    private void OpenMiniGame()
    {
        miniGame.StartGame();
        SetPlayerMovementLocked(true);
        SetMovementInputEnabled(false);
    }

    private void CompleteMiniGame()
    {
        bool captured = captureTarget != null && captureTarget.TryCapture();
        miniGame.SetSuccessMessage(captured ? "Захват выполнен." : "Захват не выполнен.");
        FinishMiniGameSession();
    }

    private void CloseMiniGame()
    {
        miniGame.Close();
        FinishMiniGameSession();
    }

    private void EndInteraction(bool clearStatusMessage)
    {
        IsNearNode = false;
        miniGame.ResetState(clearStatusMessage);
        FinishMiniGameSession();
    }

    private void FinishMiniGameSession()
    {
        SetPlayerMovementLocked(false);
        SetMovementInputEnabled(true);
    }

    private void SetPlayerMovementLocked(bool locked)
    {
        if (PlayerController != null)
            PlayerController.SetMovementLocked(locked);
    }

    private void ResolveInputActions()
    {
        PlayerInput playerInput = PlayerInput;
        interactAction = playerInput?.actions?.FindAction("Player/Interact", throwIfNotFound: false);
        miniGameNavigateAction = playerInput?.actions?.FindAction("Player/MiniGameNavigate", throwIfNotFound: false);
        moveAction = playerInput?.actions?.FindAction("Player/Move", throwIfNotFound: false);
    }

    private bool WasInteractPressedThisFrame()
    {
        if (interactAction == null)
            ResolveInputActions();

        return interactAction != null && interactAction.WasPressedThisFrame();
    }

    private bool TryGetNavigatePressedThisFrame(out Vector2 navigateInput)
    {
        navigateInput = Vector2.zero;

        if (miniGameNavigateAction == null)
            ResolveInputActions();

        if (miniGameNavigateAction == null)
            return false;

        Vector2 currentInput = miniGameNavigateAction.ReadValue<Vector2>();
        if (currentInput.sqrMagnitude < MinNavigateInputSqrMagnitude)
        {
            lastNavigateInput = Vector2.zero;
            return false;
        }

        if (lastNavigateInput.sqrMagnitude >= MinNavigateInputSqrMagnitude &&
            Vector2.Dot(lastNavigateInput.normalized, currentInput.normalized) > 0.95f)
        {
            return false;
        }

        lastNavigateInput = currentInput;
        navigateInput = currentInput;
        return true;
    }

    public void ConfigureFabricatorInteraction(Fabricator targetFabricator, LinkageNode prerequisiteNode)
    {
        fabricator = targetFabricator;
        requiredLinkageNode = prerequisiteNode;
        linkageNode = null;
        RefreshCaptureTarget();
        ResolveInputActions();
    }

    private PlayerController GetTargetPlayerController()
    {
        GameObject player = requiredLinkageNode != null ? requiredLinkageNode.Player : GameObject.FindGameObjectWithTag("Player");
        return player != null ? player.GetComponent<PlayerController>() : null;
    }

    private PlayerInput GetTargetPlayerInput()
    {
        PlayerController playerController = PlayerController;
        return playerController != null ? playerController.GetComponent<PlayerInput>() : null;
    }

    private void SetMovementInputEnabled(bool enabled)
    {
        if (moveAction == null)
            ResolveInputActions();

        if (moveAction == null)
            return;

        if (!enabled)
        {
            moveActionWasEnabled = moveAction.enabled;
            if (moveActionWasEnabled)
                moveAction.Disable();
            return;
        }

        if (moveActionWasEnabled && !moveAction.enabled)
            moveAction.Enable();

        moveActionWasEnabled = false;
    }

    private void RefreshCaptureTarget()
    {
        captureTarget = new LinkageCaptureTarget(fabricator, linkageNode, requiredLinkageNode);
    }
}
