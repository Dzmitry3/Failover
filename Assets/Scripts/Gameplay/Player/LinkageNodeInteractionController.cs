using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
[RequireComponent(typeof(LinkageMiniGame))]
public class LinkageNodeInteractionController : MonoBehaviour
{
    private const float MinNavigateInputSqrMagnitude = 0.25f;

    [Header("Interaction")]
    [SerializeField] private float interactionDistance = 3f;
    [SerializeField] private LinkageNode linkageNode;
    [SerializeField] private Fabricator fabricator;
    [SerializeField] private LinkageNode requiredLinkageNode;
    [SerializeField] private LinkageMiniGame miniGame;

    public bool IsNearNode { get; private set; }
    public bool CanRenderUi => CanShowUi && IsNearNode;
    public bool ShowPrompt => CanRenderUi && !miniGame.IsRunning;
    public bool ShowWindow => CanRenderUi && miniGame.IsRunning;
    public string SequenceText => miniGame.SequenceText;
    public string ProgressText => miniGame.ProgressText;
    public string StatusMessage => miniGame.StatusMessage;
    public string PromptText => IsFabricatorInteraction
        ? (IsPrerequisiteMet ? "Нажмите F, чтобы захватить Fabricator" : "Сначала захватите узел связи")
        : "Нажмите F для взлома узла";
    public string WindowTitle => IsFabricatorInteraction ? "Взлом Fabricator" : "Взлом LinkageNode";

    private bool IsFabricatorInteraction => fabricator != null;
    private bool IsPrerequisiteMet => !IsFabricatorInteraction || requiredLinkageNode == null || requiredLinkageNode.IsCaptured;
    private bool CanInteract => IsFabricatorInteraction
        ? fabricator.CurrentState != FabricatorState.Captured &&
          fabricator.CurrentState != FabricatorState.Destroyed &&
          IsPrerequisiteMet
        : linkageNode != null && linkageNode.CanInteract;
    private bool CanShowUi => IsFabricatorInteraction
        ? fabricator != null &&
          fabricator.CurrentState != FabricatorState.Captured &&
          fabricator.CurrentState != FabricatorState.Destroyed
        : linkageNode != null && linkageNode.CanInteract;
    private PlayerController PlayerController => linkageNode != null
        ? linkageNode.PlayerController
        : GetTargetPlayerController();
    private PlayerInput PlayerInput => GetTargetPlayerInput();

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
            miniGame = GetComponent<LinkageMiniGame>();

        ResolveInputActions();
    }

    private void OnEnable()
    {
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
        bool captured = TryCaptureTarget();
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
        ResolveInputActions();
    }

    private bool TryCaptureTarget()
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
}
