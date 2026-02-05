using System;
using UnityEngine;

[DisallowMultipleComponent]
public class EnemyBase : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private HealthComponent health;
    [SerializeField] private Collider[] collidersToDisable;
    [SerializeField] private Behaviour[] behavioursToDisableOnDeath; 

    [Header("Death")]
    [SerializeField] private bool destroyOnDeath = true;

    public event Action<EnemyBase> OnDied; 
    public HealthComponent Health => health;
    public bool IsDead => health != null && health.IsDead;

    private void InitializeReferences()
    {
        if (health == null)
            health = GetComponent<HealthComponent>();

        if (collidersToDisable == null || collidersToDisable.Length == 0)
            collidersToDisable = GetComponentsInChildren<Collider>(true);


        if (behavioursToDisableOnDeath == null || behavioursToDisableOnDeath.Length == 0)
            behavioursToDisableOnDeath = GetComponentsInChildren<Behaviour>(true);
    }

    private void Reset()
    {
        InitializeReferences();
    }

    private void Awake()
    {
        InitializeReferences();

        if (health == null)
        {
            Debug.LogError($"{nameof(EnemyBase)}: HealthComponent not found on the same GameObject.", this);
            enabled = false;
            return;
        }

        health.OnDeath += HandleDeath;


        SetAliveState(true);
    }

    private void OnDestroy()
    {
        if (health != null)
            health.OnDeath -= HandleDeath;
    }


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
            gameObject.SetActive(false);
    }

    private void SetAliveState(bool alive)
    {
        SetComponentsState(collidersToDisable, alive);
        SetBehavioursState(behavioursToDisableOnDeath, alive);
    }

    private void SetComponentsState(Collider[] components, bool enabled)
    {
        if (components == null) return;

        foreach (var c in components)
        {
            if (c != null) c.enabled = enabled;
        }
    }

    private void SetBehavioursState(Behaviour[] behaviours, bool enabled)
    {
        if (behaviours == null) return;

        foreach (var b in behaviours)
        {
            if (b == null) continue;
            if (b == this) continue;           // EnemyBase
            if (b == health) continue;         // HealthComponent
            b.enabled = enabled;
        }
    }
}
