using UnityEngine;

[CreateAssetMenu(
    fileName = "WeaponDefinition_",
    menuName = "Game/Weapons/Weapon Definition",
    order = 1)]
public class WeaponDefinition : ScriptableObject
{
    [Header("General")]
    public string weaponName = "Pistol";

    [Header("Combat")]
    public float damage = 10f;
    public float fireRate = 6f;     // shots per second
    public float range = 30f;

    [Header("Behaviour")]
    public bool automatic = true;   // true = hold to keep firing
    // false = single-shot only
}
