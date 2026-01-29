using System;
using UnityEngine;

[DisallowMultipleComponent]
public class EnemyBase : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private HealthComponent health;
    [SerializeField] private Collider[] collidersToDisable;
    [SerializeField] private Behaviour[] behavioursToDisableOnDeath; // AI, NavMeshAgent, etc.

    [Header("Death")]
    [SerializeField] private bool destroyOnDeath = true;

    public event Action<EnemyBase> OnDied; // удобно для фабрики/пула

    public HealthComponent Health => health;
    public bool IsDead => health != null && health.IsDead;

    private void Reset()
    {
        health = GetComponent<HealthComponent>();
        collidersToDisable = GetComponentsInChildren<Collider>(true);

        // Авто-подхват: отключаем всё поведенческое, кроме самого EnemyBase/Health
        behavioursToDisableOnDeath = GetComponentsInChildren<Behaviour>(true);
    }

    private void Awake()
    {
        if (health == null)
            health = GetComponent<HealthComponent>();

        if (health == null)
        {
            Debug.LogError($"{nameof(EnemyBase)}: HealthComponent not found on the same GameObject.", this);
            enabled = false;
            return;
        }

        health.OnDeath += HandleDeath;

        // На случай, если объект возвращается из пула уже "мертвым" состоянием
        SetAliveState(true);
    }

    private void OnDestroy()
    {
        if (health != null)
            health.OnDeath -= HandleDeath;
    }

    /// <summary>
    /// Вызывать при выдаче из пула (или при спавне), чтобы враг "ожил".
    /// </summary>
    public void Activate()
    {
        gameObject.SetActive(true);
        health.ResetHealth();
        SetAliveState(true);
    }

    private void HandleDeath()
    {
        SetAliveState(false);
        OnDied?.Invoke(this);

        if (destroyOnDeath)
            Destroy(gameObject);
        else
            gameObject.SetActive(false); // под пул
    }

    private void SetAliveState(bool alive)
    {
        // Коллайдеры
        if (collidersToDisable != null)
        {
            foreach (var c in collidersToDisable)
            {
                if (c != null) c.enabled = alive;
            }
        }

        // Поведение (AI/Agent/и т.п.)
        if (behavioursToDisableOnDeath != null)
        {
            foreach (var b in behavioursToDisableOnDeath)
            {
                if (b == null) continue;
                if (b == this) continue;           // EnemyBase
                if (b == health) continue;         // HealthComponent
                b.enabled = alive;
            }
        }
    }
}
