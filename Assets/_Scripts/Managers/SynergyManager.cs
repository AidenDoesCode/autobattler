using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;

public class SynergyManager : MonoBehaviour
{
    public static SynergyManager Instance { get; private set; }

    [Header("Active Synergy Counts (Unique Units On Board)")]
    public int meleeCount = 0;
    public int rangedCount = 0;
    public int supportCount = 0;

    [Header("Active Synergy Tiers")]
    public int meleeTier = 0;   // 0, 1 (2 units), 2 (4 units)
    public int rangedTier = 0;  // 0, 1 (2 units), 2 (4 units)
    public int supportTier = 0; // 0, 1 (2 units)

    // Events for UI Toolkit
    public event Action OnSynergiesUpdated;

    private Coroutine supportPulseRoutine;
    private BoardGridManager gridManager;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    void Start()
    {
        gridManager = UnityEngine.Object.FindAnyObjectByType<BoardGridManager>();
        RecalculateSynergies();
    }

    /// <summary>
    /// Scans the board for unique living player units and counts archetypes.
    /// Call whenever units are placed, dragged, sold, or merged.
    /// </summary>
    public void RecalculateSynergies()
    {
        Unit[] allUnits = UnityEngine.Object.FindObjectsByType<Unit>();
        HashSet<string> uniqueArchetypesCounted = new HashSet<string>();

        int melee = 0;
        int ranged = 0;
        int support = 0;

        foreach (var u in allUnits)
        {
            // Only count active player units on the board (NOT on bench)
            if (u != null && u.gameObject.activeInHierarchy && u.isPlayerTeam && !u.isOnBench && u.data != null)
            {
                // Count unique units only (two 1-star Warriors count as 1 towards synergy)
                string unitIdentifier = u.data.unitName;
                if (!uniqueArchetypesCounted.Contains(unitIdentifier))
                {
                    uniqueArchetypesCounted.Add(unitIdentifier);

                    switch (u.data.archetype)
                    {
                        case UnitArchetype.Melee:
                            melee++;
                            break;
                        case UnitArchetype.Ranged:
                            ranged++;
                            break;
                        case UnitArchetype.Support:
                            support++;
                            break;
                    }
                }
            }
        }

        meleeCount = melee;
        rangedCount = ranged;
        supportCount = support;

        // Evaluate Tiers
        meleeTier = meleeCount >= 4 ? 2 : (meleeCount >= 2 ? 1 : 0);
        rangedTier = rangedCount >= 4 ? 2 : (rangedCount >= 2 ? 1 : 0);
        supportTier = supportCount >= 2 ? 1 : 0;

        OnSynergiesUpdated?.Invoke();
    }

    /// <summary>
    /// Applies HP buffs and starts synergy-specific combat behaviors.
    /// Call from CombatManager at the beginning of combat.
    /// </summary>
    public void ApplyCombatSynergies(List<Unit> playerUnits)
    {
        // 1. Melee Synergy: Flat bonus max HP (+200 at Tier 1, +450 at Tier 2)
        int bonusHp = 0;
        if (meleeTier == 1) bonusHp = 200;
        else if (meleeTier == 2) bonusHp = 450;

        if (bonusHp > 0)
        {
            foreach (var u in playerUnits)
            {
                if (u != null && u.data != null && u.data.archetype == UnitArchetype.Melee)
                {
                    u.scaledMaxHealth += bonusHp;
                    u.currentHealth += bonusHp;
                    u.UpdateHealthUI();
                }
            }
        }

        // 2. Support Synergy: Start 4-second pulse healing routine if Tier 1 is active
        if (supportTier >= 1)
        {
            if (supportPulseRoutine != null) StopCoroutine(supportPulseRoutine);
            supportPulseRoutine = StartCoroutine(SupportPulseHealRoutine());
        }
    }

    public void StopCombatSynergies()
    {
        if (supportPulseRoutine != null)
        {
            StopCoroutine(supportPulseRoutine);
            supportPulseRoutine = null;
        }
    }

    /// <summary>
    /// Calculates flat damage reduction for Melee units.
    /// </summary>
    public int GetMeleeDamageReduction(Unit target)
    {
        if (target == null || !target.isPlayerTeam || target.data == null || target.data.archetype != UnitArchetype.Melee)
        {
            return 0;
        }

        if (meleeTier == 1) return 5;
        if (meleeTier == 2) return 12;
        return 0;
    }

    /// <summary>
    /// Calculates attack cooldown duration modified by Ranged Attack Speed bonus.
    /// </summary>
    public float GetRangedAttackInterval(Unit attacker, float baseInterval)
    {
        if (attacker == null || !attacker.isPlayerTeam || attacker.data == null || attacker.data.archetype != UnitArchetype.Ranged)
        {
            return baseInterval;
        }

        // Tier 1: +25% AS -> duration / 1.25
        // Tier 2: +55% AS -> duration / 1.55
        if (rangedTier == 1) return baseInterval / 1.25f;
        if (rangedTier == 2) return baseInterval / 1.55f;
        return baseInterval;
    }

    /// <summary>
    /// Support Synergy (2): Every 4 seconds, pulses an area heal to all adjacent allies for 15% of Max HP.
    /// </summary>
    private IEnumerator SupportPulseHealRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(4f);

            if (gridManager == null) gridManager = UnityEngine.Object.FindAnyObjectByType<BoardGridManager>();

            Unit[] allUnits = UnityEngine.Object.FindObjectsByType<Unit>();
            foreach (var supportUnit in allUnits)
            {
                if (supportUnit == null || !supportUnit.gameObject.activeInHierarchy || !supportUnit.isPlayerTeam ||
                    supportUnit.data == null || supportUnit.data.archetype != UnitArchetype.Support || supportUnit.currentHealth <= 0)
                {
                    continue;
                }

                // Find adjacent friendly units on the board (within 1.5 tiles)
                foreach (var ally in allUnits)
                {
                    if (ally == null || !ally.gameObject.activeInHierarchy || !ally.isPlayerTeam || ally.currentHealth <= 0) continue;

                    float dist = Vector3.Distance(supportUnit.transform.position, ally.transform.position);
                    if (dist <= 1.6f) // Adjacent tile distance
                    {
                        int healAmount = Mathf.RoundToInt(ally.GetEffectiveMaxHealth() * 0.15f);
                        ally.Heal(healAmount);
                        Debug.Log($"<color=green>[Synergy Pulse]</color> {supportUnit.data.unitName} pulse-healed {ally.data.unitName} for {healAmount} HP!");
                    }
                }
            }
        }
    }
}