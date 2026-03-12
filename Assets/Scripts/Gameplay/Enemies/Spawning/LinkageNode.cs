using UnityEngine;

[DisallowMultipleComponent]
public class LinkageNode : MonoBehaviour
{
    [SerializeField] private Fabricator fabricator;

    public Fabricator Fabricator => fabricator;
    public bool IsCaptured => fabricator != null && fabricator.CurrentState == FabricatorState.Captured;

    private void Awake()
    {
        if (fabricator == null)
            fabricator = FindFirstObjectByType<Fabricator>();
    }

    public bool TryCapture()
    {
        if (fabricator == null)
            return false;

        if (IsCaptured)
            return true;

        fabricator.SetCaptured();
        return fabricator.CurrentState == FabricatorState.Captured;
    }
}
