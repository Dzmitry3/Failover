using UnityEngine;
[DisallowMultipleComponent]
public class PlayerUpperBodyAim : MonoBehaviour
{
    private const string UpperBodyLayerName = "UpperBody";

    [Header("References")]
    [SerializeField] private Animator animator;
    [SerializeField] private WeaponController weaponController;

    [Header("Look At")]
    [SerializeField] private float bodyWeight = 0.2f;
    [SerializeField] private float headWeight = 0.65f;
    [SerializeField] private float eyesWeight = 0.8f;
    [SerializeField] private float clampWeight = 0.5f;

    private int upperBodyLayerIndex = -1;

    private void Awake()
    {
        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        if (weaponController == null)
            weaponController = GetComponent<WeaponController>();

        if (animator == null || !animator.isHuman)
            return;

        upperBodyLayerIndex = animator.GetLayerIndex(UpperBodyLayerName);
        if (upperBodyLayerIndex >= 0)
            animator.SetLayerWeight(upperBodyLayerIndex, 1f);
    }

    private void OnAnimatorIK(int layerIndex)
    {
        if (animator == null || weaponController == null)
            return;

        if (upperBodyLayerIndex >= 0 && layerIndex != upperBodyLayerIndex)
            return;

        if (!weaponController.TryGetAimPointWorld(out var aimPoint))
        {
            animator.SetLookAtWeight(0f);
            return;
        }

        animator.SetLookAtWeight(1f, bodyWeight, headWeight, eyesWeight, clampWeight);
        animator.SetLookAtPosition(aimPoint);
    }
}
