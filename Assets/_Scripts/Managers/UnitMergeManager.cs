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

    public void CheckForMerges()
    {
        if (gridManager == null) gridManager = UnityEngine.Object.FindAnyObjectByType<BoardGridManager>();

        Unit[] allUnits = UnityEngine.Object.FindObjectsByType<Unit>();
        List<Unit> playerUnits = new List<Unit>();

        foreach (var u in allUnits)
        {
            if (u != null && u.gameObject.activeInHierarchy && u.isPlayerTeam && u.data != null)
            {
                playerUnits.Add(u);
            }
        }

        Dictionary<string, List<Unit>> groups = new Dictionary<string, List<Unit>>();

        foreach (var u in playerUnits)
        {
            if (u.starLevel >= 3) continue;

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
                matches.Sort((a, b) => a.isOnBench.CompareTo(b.isOnBench));

                Unit primaryUnit = matches[0];
                Unit secondaryUnit1 = matches[1];
                Unit secondaryUnit2 = matches[2];

                ConsumeUnit(secondaryUnit1);
                ConsumeUnit(secondaryUnit2);

                primaryUnit.UpgradeStarLevel(primaryUnit.starLevel + 1);

                mergedAny = true;
                break;
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

        if (gridManager != null)
        {
            gridManager.UnregisterUnit(unit);
        }

        Destroy(unit.gameObject);
    }
}