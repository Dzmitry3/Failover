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
    [Tooltip("Если true — объект будет выключаться (возврат в пул). Destroy в пуле не используем.")]
    [SerializeField] private bool despawnOnDeath = true;

    public event Action<EnemyBase> OnDied;

    public HealthComponent Health => health;
    public bool IsDead => health != null && health.IsDead;

    private bool _subscribed;

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
            Debug.LogError($"{nameof(EnemyBase)}: HealthComponent not found in children.", this);
            enabled = false;
            return;
        }
    }

    private void OnEnable()
    {
        // Для пула: объект будет много раз включаться/выключаться.
        SubscribeDeath();
        SetAliveState(true);

        // Если спавнер уже делает ResetHealth — это не обязательно,
        // но безопасно гарантировать "живой" враг при активации.
        if (health != null && health.IsDead)
            health.ResetHealth();
    }

    private void OnDisable()
    {
        // При пуле объект чаще отключается, чем уничтожается.
        UnsubscribeDeath();
    }

    private void SubscribeDeath()
    {
        if (_subscribed) return;
        if (health == null) return;

        health.OnDeath += HandleDeath;
        _subscribed = true;
    }

    private void UnsubscribeDeath()
    {
        if (!_subscribed) return;
        if (health == null) return;

        health.OnDeath -= HandleDeath;
        _subscribed = false;
    }

    // Вызывай из спавнера после выдачи из пула (опционально).
    public void Activate()
    {
        gameObject.SetActive(true);
        if (health != null) health.ResetHealth();
        SetAliveState(true);
    }

    // Можно вызывать вручную, если нужен принудительный возврат в пул.
    public void Deactivate()
    {
        SetAliveState(false);
        gameObject.SetActive(false);
    }

    private void HandleDeath()
    {
        // 1) Сначала выключаем функциональные компоненты (чтобы “мертвый” не наносил урон и не двигался).
        SetAliveState(false);

        // 2) Событие для систем выше (статистика, спавнер, волны и т.п.)
        OnDied?.Invoke(this);

        // 3) Деспаун (возврат в пул)
        if (despawnOnDeath)
        {
            gameObject.SetActive(false);
        }
        else
        {
            // Если когда-нибудь понадобится другой сценарий — оставляем выключение как безопасный дефолт.
            gameObject.SetActive(false);
        }
    }

    private void SetAliveState(bool alive)
    {
        SetCollidersState(collidersToDisable, alive);
        SetBehavioursState(behavioursToDisableOnDeath, alive);
    }

    private void SetCollidersState(Collider[] components, bool enabled)
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

            // не отключаем "скелет"
            if (b == this) continue;    // EnemyBase
            if (b == health) continue;  // HealthComponent

            b.enabled = enabled;
        }
    }
}
