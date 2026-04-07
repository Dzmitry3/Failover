using UnityEngine;

[DisallowMultipleComponent]
public class DestroyOnDeath : MonoBehaviour
{
    [SerializeField] private HealthComponent health;

    private void InitializeReferences()
    {
        if (health == null)
            health = GetComponent<HealthComponent>();
    }

    private void Reset()
    {
        InitializeReferences();
    }

    private void Awake()
    {
        InitializeReferences();

        if (GetComponent<EnemyLifecycle>() != null)
        {
            Debug.LogError(
                $"{nameof(DestroyOnDeath)} should not be used together with {nameof(EnemyLifecycle)}. Remove {nameof(DestroyOnDeath)} from pooled enemy prefabs.",
                this);
            enabled = false;
            return;
        }

        if (health == null)
        {
            Debug.LogError($"{nameof(DestroyOnDeath)}: HealthComponent not found on the same GameObject.", this);
            enabled = false;
            return;
        }

        health.OnDeath += HandleDeath;
    }

    private void OnDestroy()
    {
        if (health != null)
            health.OnDeath -= HandleDeath;
    }

    private void HandleDeath()
    {
        Destroy(gameObject);
    }
}
