// UnitStats.cs
using UnityEngine;

[CreateAssetMenu(menuName = "RTS/UnitStats")]
public class UnitStats : ScriptableObject
{
    public float moveSpeed;
    public float attackRange;
    public float damage;
    public float fireRate;
}