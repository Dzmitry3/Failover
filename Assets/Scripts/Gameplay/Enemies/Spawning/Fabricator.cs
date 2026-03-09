using System;
using UnityEngine;

[DisallowMultipleComponent]
public class Fabricator : MonoBehaviour
{
    [Header("State")]
    [SerializeField] private FabricatorState initialState = FabricatorState.Active;

    private FabricatorState currentState;

    public event Action<FabricatorState> StateChanged;

    public FabricatorState CurrentState => currentState;
    public bool CanSpawn => currentState == FabricatorState.Active;
    public bool IsPermanentlyShutdown =>
        currentState == FabricatorState.Captured || currentState == FabricatorState.Destroyed;

    public string StopReason
    {
        get
        {
            switch (currentState)
            {
                case FabricatorState.Active:
                    return string.Empty;
                case FabricatorState.Disabled:
                    return "Fabricator is temporarily disabled.";
                case FabricatorState.Captured:
                    return "Fabricator was captured.";
                case FabricatorState.Destroyed:
                    return "Fabricator was destroyed.";
                default:
                    return "Fabricator state blocks spawning.";
            }
        }
    }

    private void Awake()
    {
        currentState = initialState;
    }

    public void SetActive() => TrySetState(FabricatorState.Active);
    public void SetDisabled() => TrySetState(FabricatorState.Disabled);
    public void SetCaptured() => TrySetState(FabricatorState.Captured);
    public void SetDestroyed() => TrySetState(FabricatorState.Destroyed);

    private void TrySetState(FabricatorState newState)
    {
        if (currentState == newState)
            return;

        if (IsPermanentlyShutdown)
        {
            Debug.LogWarning(
                $"{nameof(Fabricator)}: cannot change state from {currentState} to {newState} because shutdown is permanent.",
                this);
            return;
        }

        currentState = newState;
        StateChanged?.Invoke(currentState);

        Debug.Log($"{nameof(Fabricator)} state changed to {currentState}.", this);
    }
}
