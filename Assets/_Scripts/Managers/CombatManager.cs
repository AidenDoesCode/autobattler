using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class CombatManager : MonoBehaviour
{
    private GameManager gameManager;
    private BoardGridManager gridManager;
    private List<Unit> allUnits = new List<Unit>();

    // Keyed directly by the Unit instance to avoid deprecated GetInstanceID in Unity 6
    private Dictionary<Unit, float> attackCooldowns = new Dictionary<Unit, float>();

    private bool isRoundEnding = false;

    void Start()
    {
        gameManager = UnityEngine.Object.FindAnyObjectByType<GameManager>();
        gridManager = UnityEngine.Object.FindAnyObjectByType<BoardGridManager>();
    }

    void Update()
    {
        // Only run combat logic while in the Combat state
        if (gameManager == null || gameManager.currentState != GameManager.GameState.Combat)
        {
            return;
        }

        if (isRoundEnding) return;

        // Fetch active units on the board (ignoring bench units)
        CollectCombatUnits();

        // Check victory/defeat conditions
        if (CheckRoundCompletion())
        {
            return;
        }

        // Execute turn for each unit
        for (int i = 0; i < allUnits.Count; i++)
        {
            if (i < allUnits.Count && allUnits[i] != null)
            {
                ExecuteUnitCombat(allUnits[i]);
            }
        }
    }

    private void CollectCombatUnits()
    {
        allUnits.Clear();
        // Uses FindObjectsByType with only FindObjectsInactive to avoid deprecated FindObjectsSortMode
        Unit[] foundUnits = UnityEngine.Object.FindObjectsByType<Unit>(FindObjectsInactive.Exclude);

        foreach (var u in foundUnits)
        {
            // Bench units are excluded from combat
            if (u.isOnBench) continue;

            if (u.gameObject.activeInHierarchy && u.currentHealth > 0)
            {
                allUnits.Add(u);
            }
        }
    }

    private bool CheckRoundCompletion()
    {
        int activePlayerUnits = 0;
        int activeEnemyUnits = 0;

        foreach (var u in allUnits)
        {
            if (u == null || u.isOnBench || u.currentHealth <= 0) continue;

            if (u.isPlayerTeam) activePlayerUnits++;
            else activeEnemyUnits++;
        }

        // Safety: If nobody is deployed on either side, cleanly bail out
        if (activePlayerUnits == 0 && activeEnemyUnits == 0)
        {
            Debug.LogWarning("[CombatManager] No units deployed on the board. Returning to planning.");
            if (gameManager != null)
            {
                gameManager.ReturnToPlanning();
            }
            return true;
        }

        // Enemy army eliminated
        if (activeEnemyUnits == 0)
        {
            StartCoroutine(EndRoundRoutine(playerWon: true));
            return true;
        }

        // Player army eliminated
        if (activePlayerUnits == 0)
        {
            StartCoroutine(EndRoundRoutine(playerWon: false));
            return true;
        }

        return false;
    }

    private void ExecuteUnitCombat(Unit unit)
    {
        if (unit == null || unit.data == null || unit.currentHealth <= 0) return;

        // 1. Find valid target (Enemies for Melee/Ranged, Allies for Support)
        Unit target = unit.FindTarget(allUnits);
        if (target == null) return;

        // 2. Measure distance
        float distance = Vector3.Distance(unit.transform.position, target.transform.position);

        // Turn to smoothly face target
        Vector3 targetDirection = (target.transform.position - unit.transform.position);
        targetDirection.y = 0;
        if (targetDirection != Vector3.zero)
        {
            unit.transform.rotation = Quaternion.LookRotation(targetDirection);
        }

        // 3. Movement or Attack
        if (distance > unit.data.attackRange)
        {
            float step = unit.data.moveSpeed * Time.deltaTime;
            unit.transform.position = Vector3.MoveTowards(unit.transform.position, target.transform.position, step);
        }
        else
        {
            // In range: check attack cooldown before executing
            float nextAvailableTime = attackCooldowns.ContainsKey(unit) ? attackCooldowns[unit] : 0f;

            if (Time.time >= nextAvailableTime)
            {
                PerformCombatAction(unit, target);

                float attackDelay = unit.data.attackSpeed > 0 ? (1f / unit.data.attackSpeed) : 1f;
                attackCooldowns[unit] = Time.time + attackDelay;
            }
        }
    }

    private void PerformCombatAction(Unit attacker, Unit target)
    {
        string attackerName = attacker.data.unitName;
        string targetName = target.data.unitName;
        int actionValue = attacker.GetEffectiveAttackDamage();

        if (attacker.data.archetype == UnitArchetype.Support)
        {
            target.Heal(actionValue);
            Debug.Log($"[HEAL] {attackerName} healed {targetName} for {actionValue} HP!");
        }
        else
        {
            target.TakeDamage(actionValue);
            Debug.Log($"[ATTACK] {attackerName} attacked {targetName} for {actionValue} DMG! ({target.currentHealth}/{target.GetEffectiveMaxHealth()})");

            if (target.currentHealth <= 0)
            {
                Debug.Log($"[DEFEATED] {targetName} was eliminated!");

                if (attackCooldowns.ContainsKey(target)) attackCooldowns.Remove(target);
                gridManager.ClearUnitPosition(target.gridX, target.gridZ);

                target.gameObject.SetActive(false);
            }
        }
    }

    private IEnumerator EndRoundRoutine(bool playerWon)
    {
        isRoundEnding = true;

        if (playerWon)
        {
            Debug.Log("<color=green>VICTORY!</color> All enemies have been defeated.");
        }
        else
        {
            Debug.Log("<color=red>DEFEAT!</color> Your army was eliminated.");
        }

        // Payout gold, streak bonuses, and interest via EconomyManager
        if (EconomyManager.Instance != null)
        {
            EconomyManager.Instance.ProcessRoundEndIncome(playerWon);
        }

        yield return new WaitForSeconds(1.5f);

        // Include inactive units without passing SortMode
        Unit[] units = UnityEngine.Object.FindObjectsByType<Unit>(FindObjectsInactive.Include);
        
        foreach (var u in units)
        {
            if (u.isPlayerTeam)
            {
                // Defeated player units are re-enabled, healed, and snapped back
                u.ResetToPlanningPosition(gridManager);
            }
            else
            {
                // Clean up remaining enemy units
                Destroy(u.gameObject);
            }
        }

        // Spawn next wave of enemies
        if (gridManager != null)
        {
            gridManager.SpawnEnemyWave();
        }

        attackCooldowns.Clear();
        isRoundEnding = false;

        if (gameManager != null)
        {
            gameManager.ReturnToPlanning();
        }
    }
}