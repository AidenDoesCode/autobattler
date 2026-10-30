using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class CombatManager : MonoBehaviour
{
    private GameManager gameManager;
    private BoardGridManager gridManager;
    private BattleUIController battleUI;
    private List<Unit> allUnits = new List<Unit>();
    private Dictionary<Unit, float> attackCooldowns = new Dictionary<Unit, float>();

    private bool isRoundEnding = false;

    void Start()
    {
        gameManager = Object.FindAnyObjectByType<GameManager>();
        gridManager = Object.FindAnyObjectByType<BoardGridManager>();
        battleUI = Object.FindAnyObjectByType<BattleUIController>();
    }

    void Update()
    {
        if (gameManager == null || gameManager.currentState != GameManager.GameState.Combat)
        {
            isRoundEnding = false;
            return;
        }

        if (isRoundEnding) return;

        // Fetch living active units on the board
        allUnits = new List<Unit>(Object.FindObjectsByType<Unit>(FindObjectsInactive.Exclude));

        // 1. Check if either team has been eliminated
        int playerCount = 0;
        int enemyCount = 0;

        for (int i = 0; i < allUnits.Count; i++)
        {
            Unit u = allUnits[i];
            if (u != null && u.currentHealth > 0)
            {
                if (u.isPlayerTeam) playerCount++;
                else enemyCount++;
            }
        }

        if (playerCount == 0 || enemyCount == 0)
        {
            StartCoroutine(EndRoundRoutine(playerCount > 0));
            return;
        }

        // 2. Execute unit combat turns
        for (int i = 0; i < allUnits.Count; i++)
        {
            Unit currentUnit = allUnits[i];
            if (currentUnit != null && currentUnit.gameObject.activeInHierarchy)
            {
                ExecuteUnitTurn(currentUnit);
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
            Debug.Log("<color=red>DEFEAT!</color> Your team was wiped out.");
        }

        yield return new WaitForSeconds(1.5f);

        if (gameManager != null)
        {
            gameManager.ReturnToPlanning();
        }

        if (battleUI != null)
        {
            battleUI.ResetButtonText();
        }

        ResetUnitsAfterRound();
    }

    private void ResetUnitsAfterRound()
    {
        attackCooldowns.Clear();

        // Clear the entire board grid so surviving and dead units can re-register cleanly
        if (gridManager != null)
        {
            for (int x = 0; x < gridManager.width; x++)
            {
                for (int z = 0; z < gridManager.height; z++)
                {
                    gridManager.ClearUnitPosition(x, z);
                }
            }
        }

        // Find ALL units including disabled/defeated ones
        Unit[] units = Object.FindObjectsByType<Unit>(FindObjectsInactive.Include);

        foreach (var unit in units)
        {
            if (unit == null) continue;

            if (unit.isPlayerTeam)
            {
                // Revive, reactivate, and reset to pre-battle positions
                unit.ResetToPlanningPosition(gridManager);
            }
            else
            {
                // Clean up enemy units from the previous wave
                Destroy(unit.gameObject);
            }
        }

        // Spawn a fresh wave of enemies for the next round
        if (gridManager != null)
        {
            gridManager.SpawnEnemyWave();
        }
    }

    private void ExecuteUnitTurn(Unit unit)
    {
        if (unit == null || unit.data == null || unit.currentHealth <= 0 || unit.isMoving) return;

        Unit target = unit.FindTarget(allUnits);
        if (target == null) return;

        int rangeInTiles = Mathf.Max(1, Mathf.FloorToInt(unit.data.attackRange));
        int tileDistance = Mathf.Max(Mathf.Abs(unit.gridX - target.gridX), Mathf.Abs(unit.gridZ - target.gridZ));

        Vector3 lookDir = target.transform.position - unit.transform.position;
        lookDir.y = 0;
        if (lookDir != Vector3.zero) unit.transform.rotation = Quaternion.LookRotation(lookDir);

        if (tileDistance > rangeInTiles)
        {
            GridPoint start = new GridPoint(unit.gridX, unit.gridZ);
            GridPoint goal = new GridPoint(target.gridX, target.gridZ);

            List<GridPoint> path = gridManager.FindPath(start, goal, rangeInTiles);

            if (path != null && path.Count > 0)
            {
                GridPoint nextStep = path[0];
                gridManager.MoveUnitOnGrid(unit, unit.gridX, unit.gridZ, nextStep.x, nextStep.z);
                StartCoroutine(unit.MoveToTileRoutine(nextStep.x, nextStep.z, gridManager));
            }
        }
        else
        {
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

        if (attacker.data.archetype == UnitArchetype.Support)
        {
            target.currentHealth = Mathf.Min(target.data.maxHealth, target.currentHealth + attacker.data.attackDamage);
            Debug.Log($"[HEAL] {attackerName} healed {targetName} for {attacker.data.attackDamage} HP!");
        }
        else
        {
            target.currentHealth -= attacker.data.attackDamage;
            Debug.Log($"[ATTACK] {attackerName} attacked {targetName} for {attacker.data.attackDamage} DMG! ({target.currentHealth}/{target.data.maxHealth})");

            if (target.currentHealth <= 0)
            {
                Debug.Log($"[DEFEATED] {targetName} was eliminated!");

                if (attackCooldowns.ContainsKey(target)) attackCooldowns.Remove(target);
                gridManager.ClearUnitPosition(target.gridX, target.gridZ);

                // Disable instead of destroying so the unit can reset next round
                target.gameObject.SetActive(false);
            }
        }
    }
}