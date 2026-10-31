using UnityEngine;
using System.Collections.Generic;

public class UnitMergeManager : MonoBehaviour
{
    public static UnitMergeManager Instance { get; private set; }

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
        gridManager = Object.FindAnyObjectByType<BoardGridManager>();
    }

    /// <summary>
    /// Checks all player units on the board and bench for 3-of-a-kind combinations and merges them.
    /// </summary>
    public void CheckForMerges()
    {
        if (gridManager == null) gridManager = Object.FindAnyObjectByType<BoardGridManager>();

        // Query only active units in the hierarchy
        Unit[] allUnits = Object.FindObjectsByType<Unit>(FindObjectsInactive.Exclude);
        List<Unit> playerUnits = new List<Unit>();

        foreach (var u in allUnits)
        {
            if (u != null && u.gameObject.activeInHierarchy && u.isPlayerTeam && u.data != null)
            {
                playerUnits.Add(u);
            }
        }

        // Group units by UnitData name and current star level
        Dictionary<string, List<Unit>> groups = new Dictionary<string, List<Unit>>();

        foreach (var u in playerUnits)
        {
            if (u.starLevel >= 3) continue; // 3-Star is maximum

            string key = $"{u.data.unitName}_{u.starLevel}";
            if (!groups.ContainsKey(key))
            {
                groups[key] = new List<Unit>();
            }
            groups[key].Add(u);
        }

        bool mergedAny = false;

        foreach (var kvp in groups)
        {
            List<Unit> matches = kvp.Value;
            if (matches.Count >= 3)
            {
                // Prioritize keeping a unit currently deployed on the board over bench
                matches.Sort((a, b) => a.isOnBench.CompareTo(b.isOnBench));

                Unit primaryUnit = matches[0];
                Unit secondaryUnit1 = matches[1];
                Unit secondaryUnit2 = matches[2];

                // Instantly consume and clear the two extra copies
                ConsumeUnit(secondaryUnit1);
                ConsumeUnit(secondaryUnit2);

                // Promote the surviving unit
                primaryUnit.UpgradeStarLevel(primaryUnit.starLevel + 1);

                mergedAny = true;
                break; // Rerun recursively to resolve cascade merges (e.g., three 2-stars -> one 3-star)
            }
        }

        if (mergedAny)
        {
            CheckForMerges();
        }
        else
        {
            if (SynergyManager.Instance != null)
            {
                SynergyManager.Instance.RecalculateSynergies();
            }
        }
    }

    private void ConsumeUnit(Unit unit)
    {
        if (unit == null) return;

        // 1. Immediately clear its tile/bench registration
        if (gridManager != null)
        {
            gridManager.UnregisterUnit(unit);
        }

        // 2. Deactivate immediately so FindObjects & capacity checks exclude it on this frame
        unit.gameObject.SetActive(false);

        // 3. Destroy from memory
        Destroy(unit.gameObject);
    }
}