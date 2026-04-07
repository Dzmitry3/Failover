using UnityEngine;

[CreateAssetMenu(
    fileName = "WeaponData_",
    menuName = "Game/Weapons/Weapon Data",
    order = 1)]
public class WeaponData : ScriptableObject
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
