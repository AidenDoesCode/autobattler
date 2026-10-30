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
        gridManager = UnityEngine.Object.FindAnyObjectByType<BoardGridManager>();
    }

    /// <summary>
    /// Checks all player units on the board and bench for 3-of-a-kind combinations and merges them.
    /// </summary>
    public void CheckForMerges()
    {
        if (gridManager == null) gridManager = UnityEngine.Object.FindAnyObjectByType<BoardGridManager>();

        // 1. Collect all active player units
        Unit[] allUnits = UnityEngine.Object.FindObjectsByType<Unit>();
        List<Unit> playerUnits = new List<Unit>();

        foreach (var u in allUnits)
        {
            if (u != null && u.gameObject.activeInHierarchy && u.isPlayerTeam && u.data != null)
            {
                playerUnits.Add(u);
            }
        }

        // 2. Group units by UnitData name and current StarLevel
        // Key: "Warrior_1" or "Mage_2"
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

        // 3. Evaluate each group for 3 copies
        bool mergedAny = false;

        foreach (var kvp in groups)
        {
            List<Unit> matches = kvp.Value;
            if (matches.Count >= 3)
            {
                // Prioritize keeping a unit on the board over bench
                matches.Sort((a, b) => a.isOnBench.CompareTo(b.isOnBench));

                Unit primaryUnit = matches[0];
                Unit secondaryUnit1 = matches[1];
                Unit secondaryUnit2 = matches[2];

                // Clear and destroy secondary copies
                ConsumeUnit(secondaryUnit1);
                ConsumeUnit(secondaryUnit2);

                // Upgrade the primary survivor
                primaryUnit.UpgradeStarLevel(primaryUnit.starLevel + 1);

                mergedAny = true;
                break; // Break and rerun recursively to catch chain merges (e.g. 1* -> 2* -> 3*)
            }
        }

        if (mergedAny)
        {
            CheckForMerges();
        }
    }

    private void ConsumeUnit(Unit unit)
    {
        if (unit == null) return;

        if (gridManager != null)
        {
            gridManager.UnregisterUnit(unit);
        }

        Destroy(unit.gameObject);
    }
}