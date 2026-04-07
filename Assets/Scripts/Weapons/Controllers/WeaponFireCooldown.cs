using UnityEngine;

public sealed class WeaponFireCooldown
{
    private readonly float minimumFireRate;
    private float nextFireTime;

    public WeaponFireCooldown(float minimumFireRate)
    {
        this.minimumFireRate = minimumFireRate;
    }

    public bool CanFire(float time)
    {
        return time >= nextFireTime;
    }

    public void Consume(float time, float fireRate)
    {
        float rate = Mathf.Max(minimumFireRate, fireRate);
        nextFireTime = time + (1f / rate);
    }
}
