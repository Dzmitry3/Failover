using System;
using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class EnemyLifecycle : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private HealthComponent health;
    [SerializeField] private Collider[] collidersToDisable;
    [SerializeField] private Behaviour[] behavioursToDisableOnDeath;

    [Header("Death")]
    [Tooltip("If true, the object is disabled on death instead of being destroyed.")]
    [SerializeField] private bool despawnOnDeath = true;
    [SerializeField, Min(0f)] private float despawnDelaySeconds;

    public event Action<EnemyLifecycle, bool> AliveStateChanged;

    public HealthComponent Health => health;
    public bool IsDead => health != null && health.IsDead;

    private bool _subscribed;
    private Coroutine _despawnCoroutine;
    private bool isAlive;

    private void InitializeReferences()
    {
        if (health == null)
            health = GetComponentInChildren<HealthComponent>(true);

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
            Debug.LogError($"{nameof(EnemyLifecycle)}: HealthComponent not found in children.", this);
            enabled = false;
        }
    }

    private void OnEnable()
    {
        SubscribeDeath();
        PrepareForSpawn();
    }

    private void OnDisable()
    {
        StopDespawnCoroutine();
        SetAlive(false);
        UnsubscribeDeath();
    }

    private void SubscribeDeath()
    {
        if (_subscribed || health == null)
            return;

        health.OnDeath += HandleDeath;
        _subscribed = true;
    }

    private void UnsubscribeDeath()
    {
        if (!_subscribed || health == null)
            return;

        health.OnDeath -= HandleDeath;
        _subscribed = false;
    }

    public void PrepareForSpawn()
    {
        StopDespawnCoroutine();
        if (health != null)
            health.ResetHealth();

        SetAliveState(true);
        SetAlive(true);
    }

    private void HandleDeath()
    {
        SetAliveState(false);
        SetAlive(false);

        if (!despawnOnDeath)
            return;

        if (despawnDelaySeconds > 0f && gameObject.activeInHierarchy)
        {
            StopDespawnCoroutine();
            _despawnCoroutine = StartCoroutine(DespawnAfterDelay());
            return;
        }

        gameObject.SetActive(false);
    }

    private IEnumerator DespawnAfterDelay()
    {
        yield return new WaitForSeconds(despawnDelaySeconds);

        _despawnCoroutine = null;

        if (gameObject.activeSelf)
            gameObject.SetActive(false);
    }

    private void StopDespawnCoroutine()
    {
        if (_despawnCoroutine == null)
            return;

        StopCoroutine(_despawnCoroutine);
        _despawnCoroutine = null;
    }

    private void SetAliveState(bool alive)
    {
        SetCollidersState(collidersToDisable, alive);
        SetBehavioursState(behavioursToDisableOnDeath, alive);
    }

    private static void SetCollidersState(Collider[] components, bool enabled)
    {
        if (components == null)
            return;

        foreach (var component in components)
        {
            if (component != null)
                component.enabled = enabled;
        }
    }

    private void SetBehavioursState(Behaviour[] behaviours, bool enabled)
    {
        if (behaviours == null)
            return;

        foreach (var behaviour in behaviours)
        {
            if (behaviour == null || behaviour == this || behaviour == health)
                continue;

            behaviour.enabled = enabled;
        }
    }

    private void SetAlive(bool alive)
    {
        if (isAlive == alive)
            return;

        isAlive = alive;
        AliveStateChanged?.Invoke(this, isAlive);
    }
}
