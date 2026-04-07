using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class ArrowSequenceMiniGame : MonoBehaviour
{
    [SerializeField] private int sequenceLength = 8;
    [SerializeField] private float retryDelaySeconds = 2f;

    private ArrowDirection[] sequence = System.Array.Empty<ArrowDirection>();
    private int currentInputIndex;
    private string statusMessage = string.Empty;
    private bool isActive;
    private bool isCooldownActive;
    private Coroutine retryCoroutine;

    public bool IsActive => isActive;
    public bool IsCooldownActive => isCooldownActive;
    public bool IsRunning => isActive || isCooldownActive;
    public string StatusMessage => statusMessage;

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

    public void StartGame()
    {
        StopRetryCoroutine();
        SetState(active: true, cooldown: false, inputIndex: 0, clearStatusMessage: true);

        int resolvedLength = Mathf.Max(1, sequenceLength);
        sequence = new ArrowDirection[resolvedLength];
        for (int i = 0; i < sequence.Length; i++)
            sequence[i] = (ArrowDirection)Random.Range(0, 4);
    }

    public void Close()
    {
        StopRetryCoroutine();
        SetState(active: false, cooldown: false, inputIndex: 0, clearStatusMessage: true);
    }

    public bool TryConsumeArrowInput(Vector2 navigationInput, out bool completed)
    {
        completed = false;
        if (!isActive || sequence.Length == 0)
            return false;

        if (!TryGetPressedArrow(navigationInput, out ArrowDirection pressedDirection))
            return false;

        if (pressedDirection == sequence[currentInputIndex])
        {
            currentInputIndex++;
            completed = currentInputIndex >= sequence.Length;
            return true;
        }

        EnterCooldown();
        return false;
    }

    public void SetSuccessMessage(string message)
    {
        SetState(active: false, cooldown: false, inputIndex: 0, clearStatusMessage: false);
        statusMessage = message;
    }

    public void ResetState(bool clearStatusMessage)
    {
        StopRetryCoroutine();
        SetState(active: false, cooldown: false, inputIndex: 0, clearStatusMessage: clearStatusMessage);
    }

    private void OnDisable()
    {
        ResetState(clearStatusMessage: false);
    }

    private void EnterCooldown()
    {
        SetState(active: false, cooldown: true, inputIndex: 0, clearStatusMessage: false);
        statusMessage = $"\u041d\u0435\u0432\u0435\u0440\u043d\u0430\u044f \u043f\u043e\u0441\u043b\u0435\u0434\u043e\u0432\u0430\u0442\u0435\u043b\u044c\u043d\u043e\u0441\u0442\u044c. \u041f\u043e\u0432\u0442\u043e\u0440 \u0447\u0435\u0440\u0435\u0437 {Mathf.CeilToInt(retryDelaySeconds)} \u0441\u0435\u043a\u0443\u043d\u0434\u044b...";

        StopRetryCoroutine();
        retryCoroutine = StartCoroutine(RestartAfterDelay());
    }

    private IEnumerator RestartAfterDelay()
    {
        yield return new WaitForSeconds(retryDelaySeconds);
        retryCoroutine = null;

        SetState(active: true, cooldown: false, inputIndex: 0, clearStatusMessage: true);
    }

    private void StopRetryCoroutine()
    {
        if (retryCoroutine == null)
            return;

        StopCoroutine(retryCoroutine);
        retryCoroutine = null;
    }

    private void SetState(bool active, bool cooldown, int inputIndex, bool clearStatusMessage)
    {
        isActive = active;
        isCooldownActive = cooldown;
        currentInputIndex = inputIndex;

        if (clearStatusMessage)
            statusMessage = string.Empty;
    }

    private static bool TryGetPressedArrow(Vector2 navigationInput, out ArrowDirection pressedDirection)
    {
        pressedDirection = ArrowDirection.Up;

        if (navigationInput.sqrMagnitude < 0.25f)
            return false;

        if (Mathf.Abs(navigationInput.x) > Mathf.Abs(navigationInput.y))
        {
            pressedDirection = navigationInput.x > 0f ? ArrowDirection.Right : ArrowDirection.Left;
            return true;
        }

        pressedDirection = navigationInput.y > 0f ? ArrowDirection.Up : ArrowDirection.Down;
        return true;
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
