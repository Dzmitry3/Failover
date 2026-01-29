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
    public float fireRate = 6f;     // выстрелов в секунду
    public float range = 30f;

    [Header("Behaviour")]
    public bool automatic = true;   // true = держишь → стреляет
    // false = только одиночные

    [Header("Debug")]
    public Color debugRayColor = Color.yellow;
}