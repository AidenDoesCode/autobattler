using UnityEngine;

public enum UnitArchetype { Melee, Ranged, Support }
public enum UnitUltimate { ArchetypeDefault, CleaveStun, Snipe, HealingChorus, SecondWind }

[CreateAssetMenu(fileName = "NewUnitData", menuName = "Autobattler/Unit Data")]
public class UnitData : ScriptableObject
{
    public string unitName;
    public UnitArchetype archetype;

    [Header("Ultimate Ability")]
    public UnitUltimate ultimate = UnitUltimate.ArchetypeDefault;
    public string ultimateName;
    [Min(0f)] public float ultimatePowerMultiplier = 0f; // Zero uses the ability default.
    [Min(0)] public int ultimateRadius = 1;
    [Min(0f)] public float ultimateStunDuration = 1.5f;

    public UnitUltimate UltimateType => ultimate != UnitUltimate.ArchetypeDefault ? ultimate :
        archetype == UnitArchetype.Melee ? UnitUltimate.CleaveStun :
        archetype == UnitArchetype.Ranged ? UnitUltimate.Snipe : UnitUltimate.HealingChorus;

    public float UltimateMultiplier => ultimatePowerMultiplier > 0f ? ultimatePowerMultiplier :
        UltimateType == UnitUltimate.CleaveStun ? 2.2f : UltimateType == UnitUltimate.Snipe ? 2.8f : 2.5f;

    public string UltimateName => !string.IsNullOrWhiteSpace(ultimateName) ? ultimateName :
        UltimateType == UnitUltimate.CleaveStun ? "Earthshaker Cleave" :
        UltimateType == UnitUltimate.Snipe ? "Snipe Barrage" :
        UltimateType == UnitUltimate.SecondWind ? "Second Wind" : "Divine Chorus";

    public string GetUltimateDescription(int attack)
    {
        int power = Mathf.RoundToInt(attack * UltimateMultiplier);
        switch (UltimateType)
        {
            case UnitUltimate.CleaveStun:
                return $"Deal {power} damage to enemies within {ultimateRadius} tile(s), including diagonals, and stun survivors for {ultimateStunDuration:0.#} seconds. Does not hit allies.";
            case UnitUltimate.Snipe:
                return $"Deal {power} damage to the enemy with the lowest health percentage anywhere on the board.";
            case UnitUltimate.SecondWind:
                return $"Restore {power} health to this unit, up to maximum health.";
            default:
                return $"Restore {power} health to all living allies on the board, including this unit, up to maximum health.";
        }
    }

    [Header("Economy")]
    public int goldCost = 1; // Tier 1 = 1g, Tier 2 = 2g, etc.
    
    [Header("Combat Stats")]
    public int maxHealth = 100;
    public int attackDamage = 15;
    public float attackRange = 1.5f; // Small for Melee, larger for Ranged/Support
    public float attackSpeed = 1.0f; // Attacks per second
    public float moveSpeed = 3.0f;
}