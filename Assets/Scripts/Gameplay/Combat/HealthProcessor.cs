public class HealthProcessor
{
    public struct DamageContext
    {
        public bool isCritical;
        public bool hasArmor;
    }

    public struct HealContext
    {
        public bool boosted;
    }


    public void DealDamage(
        HealthComponent target,
        float baseDamage,
        DamageContext context = default)
    {
        float finalDamage = CalculateDamage(baseDamage, context);
        target.ApplyDelta(-finalDamage);
    }

    public void Heal(
        HealthComponent target,
        float amount,
        HealContext context = default)
    {
        float finalHeal = CalculateHeal(amount, context);
        target.ApplyDelta(+finalHeal);
    }

    private float CalculateDamage(float baseDamage, DamageContext context)
    {
        float dmg = baseDamage;

        if (context.isCritical)
            dmg *= 2f;

        if (context.hasArmor)
            dmg *= 0.7f;

        return dmg;
    }

    private float CalculateHeal(float amount, HealContext context)
    {
        return amount;
    }
}