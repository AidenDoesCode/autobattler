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

    public void RecalculateSynergies()
    {
        Unit[] allUnits = UnityEngine.Object.FindObjectsByType<Unit>(FindObjectsInactive.Exclude);
        HashSet<string> uniqueArchetypesCounted = new HashSet<string>();

        int melee = 0;
        int ranged = 0;
        int support = 0;

        foreach (var u in allUnits)
        {
            if (u != null && u.gameObject.activeInHierarchy && u.isPlayerTeam && !u.isOnBench && u.data != null)
            {
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

        meleeTier = meleeCount >= 4 ? 2 : (meleeCount >= 2 ? 1 : 0);
        rangedTier = rangedCount >= 4 ? 2 : (rangedCount >= 2 ? 1 : 0);
        supportTier = supportCount >= 2 ? 1 : 0;

        OnSynergiesUpdated?.Invoke();
    }

    public void ApplyCombatSynergies(List<Unit> playerUnits)
    {
        int bonusHp = 0;
        if (meleeTier == 1) bonusHp = 200;
        else if (meleeTier == 2) bonusHp = 450;

        if (bonusHp > 0)
        {
            foreach (var u in playerUnits)
            {
                if (u != null && u.data != null && u.data.archetype == UnitArchetype.Melee)
                {
                    u.bonusSynergyHealth = bonusHp;
                    u.currentHealth += bonusHp;
                    u.UpdateHealthUI();
                }
            }
        }

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

    public float GetRangedAttackInterval(Unit attacker, float baseInterval)
    {
        if (attacker == null || !attacker.isPlayerTeam || attacker.data == null || attacker.data.archetype != UnitArchetype.Ranged)
        {
            return baseInterval;
        }

        if (rangedTier == 1) return baseInterval / 1.25f;
        if (rangedTier == 2) return baseInterval / 1.55f;
        return baseInterval;
    }

    private IEnumerator SupportPulseHealRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(4f);

            Unit[] allUnits = UnityEngine.Object.FindObjectsByType<Unit>(FindObjectsInactive.Exclude);
            foreach (var supportUnit in allUnits)
            {
                if (supportUnit == null || !supportUnit.gameObject.activeInHierarchy || !supportUnit.isPlayerTeam ||
                    supportUnit.data == null || supportUnit.data.archetype != UnitArchetype.Support || supportUnit.currentHealth <= 0)
                {
                    continue;
                }

                foreach (var ally in allUnits)
                {
                    if (ally == null || !ally.gameObject.activeInHierarchy || !ally.isPlayerTeam || ally.currentHealth <= 0) continue;

                    float dist = Vector3.Distance(supportUnit.transform.position, ally.transform.position);
                    if (dist <= 1.6f)
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