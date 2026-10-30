using UnityEngine;

public enum UnitArchetype { Melee, Ranged, Support }

[CreateAssetMenu(fileName = "NewUnitData", menuName = "Autobattler/Unit Data")]
public class UnitData : ScriptableObject
{
    public string unitName;
    public UnitArchetype archetype;
    
    [Header("Combat Stats")]
    public int maxHealth = 100;
    public int attackDamage = 15;
    public float attackRange = 1.5f; // Small for Melee, larger for Ranged/Support
    public float attackSpeed = 1.0f; // Attacks per second
    public float moveSpeed = 3.0f;
}