public class DamageApplier
{
    private const float CriticalDamageMultiplier = 2f;
    private const float ArmorDamageMultiplier = 0.7f;

    public struct DamageContext
    {
        public bool isCritical;
        public bool hasArmor;
    }

    public void DealDamage(
        HealthComponent target,
        float baseDamage,
        DamageContext context = default)
    {
        float finalDamage = CalculateDamage(baseDamage, context);
        target.ApplyDelta(-finalDamage);
    }

    private float CalculateDamage(float baseDamage, DamageContext context)
    {
        float dmg = baseDamage;

        if (context.isCritical)
            dmg *= CriticalDamageMultiplier;

        if (context.hasArmor)
            dmg *= ArmorDamageMultiplier;

        return dmg;
    }

}
