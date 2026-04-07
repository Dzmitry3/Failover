using UnityEngine;

[DisallowMultipleComponent]
public class LinkageNode : MonoBehaviour
{
    [SerializeField] private Fabricator fabricator;
    [SerializeField] private GameObject player;
    [SerializeField, InspectorName("Captured")] private bool isCaptured;

    private PlayerController playerController;

    public Fabricator Fabricator => fabricator;
    public GameObject Player => player;
    public PlayerController PlayerController => playerController;
    public bool IsCaptured => isCaptured;
    public bool CanInteract => !isCaptured;

    private void Awake()
    {
        if (fabricator == null)
            fabricator = FindFirstObjectByType<Fabricator>();

        ResolvePlayer();
    }

    private void OnValidate()
    {
        if (player == null)
            player = GameObject.FindGameObjectWithTag("Player");
    }

    public bool TryCapture()
    {
        if (!CanInteract)
            return false;

        isCaptured = true;
        return true;
    }

    private void ResolvePlayer()
    {
        if (player == null)
            player = GameObject.FindGameObjectWithTag("Player");

        playerController = player != null ? player.GetComponent<PlayerController>() : null;
    }
}
