// UnitStats.cs
using UnityEngine;

[CreateAssetMenu(menuName = "RTS/UnitStats")]
public class UnitStats : ScriptableObject
{
    public float moveSpeed;
    public float attackRange;
    public int damage;
    public float fireRate;
    public int hp = 1;
}