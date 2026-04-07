using System;
using UnityEngine;

public class HealthComponent : MonoBehaviour
{
    [SerializeField] private float maxHealth = 100f;

    private float currentHealth;

    public float Current => currentHealth;
    public float Max => maxHealth;
    public bool IsDead => currentHealth <= 0f;

    public event Action<float, float> OnHealthChanged;
    public event Action OnDeath;

    // Optional filter that decides whether a delta can be applied.
    // Return true to allow the change, false to block it.
    public Func<float, bool> CanApplyDelta;

    private void Awake()
    {
        currentHealth = maxHealth;
    }

    public void ApplyDelta(float delta)
    {
        if (IsDead) return;

        // Let the owning object decide whether this delta is allowed.
        if (CanApplyDelta != null && !CanApplyDelta(delta))
            return;

        currentHealth = Mathf.Clamp(currentHealth + delta, 0f, maxHealth);
        OnHealthChanged?.Invoke(currentHealth, maxHealth);

        if (currentHealth <= 0f)
            OnDeath?.Invoke();
    }

    public void ResetHealth()
    {
        currentHealth = maxHealth;
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }
}
