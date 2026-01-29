using UnityEngine;

[DisallowMultipleComponent]
public class EnemyDummy : MonoBehaviour
{
    [SerializeField] private HealthComponent health;

    private void Reset()
    {
        health = GetComponent<HealthComponent>();
    }

    private void Awake()
    {
        if (health == null)
            health = GetComponent<HealthComponent>();

        if (health == null)
        {
            Debug.LogError($"{nameof(EnemyDummy)}: HealthComponent not found on the same GameObject.", this);
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