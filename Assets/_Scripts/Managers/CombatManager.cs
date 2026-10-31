using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class CombatManager : MonoBehaviour
{
    public static CombatManager Instance { get; private set; }

    private GameManager gameManager;
    private BoardGridManager gridManager;
    private BattleUIController battleUI;
    private List<Unit> allUnits = new List<Unit>();
    private Dictionary<Unit, float> attackCooldowns = new Dictionary<Unit, float>();

    private bool isRoundEnding = false;

    [Header("Loot Drop Settings")]
    [Tooltip("Prefab containing LootOrb component.")]
    public GameObject lootOrbPrefab;

    [Range(0f, 1f)]
    public float itemDropChance = 0.35f;

    [Range(0f, 1f)]
    public float goldDropChance = 0.40f;

    public List<ItemData> dropItemPool = new List<ItemData>();

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
        gameManager = Object.FindAnyObjectByType<GameManager>();
        gridManager = Object.FindAnyObjectByType<BoardGridManager>();
        battleUI = Object.FindAnyObjectByType<BattleUIController>();

        // Auto-populate drop item pool from database if not set
        if ((dropItemPool == null || dropItemPool.Count == 0) && ItemInventoryManager.Instance != null && ItemInventoryManager.Instance.itemDatabase != null)
        {
            foreach (var it in ItemInventoryManager.Instance.itemDatabase.allItems)
            {
                if (it != null && !it.isCompletedItem)
                {
                    dropItemPool.Add(it);
                }
            }
        }
    }

    void Update()
    {
        if (gameManager == null || gameManager.currentState != GameManager.GameState.Combat)
        {
            isRoundEnding = false;
            return;
        }

        if (isRoundEnding) return;

        allUnits = new List<Unit>(Object.FindObjectsByType<Unit>(FindObjectsInactive.Exclude));

        int activePlayerUnits = 0;
        int activeEnemyUnits = 0;

        foreach (var u in allUnits)
        {
            if (u == null || u.isOnBench || u.currentHealth <= 0) continue;

            if (u.isPlayerTeam) activePlayerUnits++;
            else activeEnemyUnits++;
        }

        if (activePlayerUnits == 0 && activeEnemyUnits == 0)
        {
            return;
        }

        if (activeEnemyUnits == 0)
        {
            StartCoroutine(EndRoundRoutine(playerWon: true));
            return;
        }
        else if (activePlayerUnits == 0)
        {
            StartCoroutine(EndRoundRoutine(playerWon: false));
            return;
        }

        // Execute combat turns
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
            Debug.Log("<color=red>DEFEAT!</color> Your army was eliminated.");
        }

        if (EconomyManager.Instance != null)
        {
            EconomyManager.Instance.ProcessRoundEndIncome(playerWon);
        }

        yield return new WaitForSeconds(1.5f);

        // Revive living and dead player units
        Unit[] units = Object.FindObjectsByType<Unit>(FindObjectsInactive.Include);

        foreach (var u in units)
        {
            if (u == null) continue;

            if (u.isPlayerTeam)
            {
                u.ResetToPlanningPosition(gridManager);
            }
            else
            {
                Destroy(u.gameObject);
            }
        }

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

        if (battleUI != null)
        {
            battleUI.ResetButtonText();
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
                float effectiveAS = unit.data.attackSpeed + unit.GetItemBonusAttackSpeed();
                float attackDelay = effectiveAS > 0 ? (1f / effectiveAS) : 1f;
                attackCooldowns[unit] = Time.time + attackDelay;
            }
        }
    }

    private void PerformCombatAction(Unit attacker, Unit target)
    {
        string attackerName = attacker.data.unitName;
        string targetName = target.data.unitName;
        int actionValue = attacker.GetEffectiveAttackDamage();

        attacker.PlayAttackAnimation(target.transform.position);

        if (attacker.data.archetype == UnitArchetype.Support)
        {
            target.Heal(actionValue);
            Debug.Log($"[HEAL] {attackerName} healed {targetName} for {actionValue} HP!");
        }
        else
        {
            int dr = target.GetItemFlatDamageReduction();
            int finalDmg = Mathf.Max(1, actionValue - dr);
            target.TakeDamage(finalDmg);

            if (target.currentHealth <= 0)
            {
                Debug.Log($"[DEFEATED] {targetName} was eliminated!");

                if (attackCooldowns.ContainsKey(target)) attackCooldowns.Remove(target);
                if (gridManager != null) gridManager.ClearUnitPosition(target.gridX, target.gridZ);

                // If defeated unit was an enemy, roll for loot orb drop!
                if (!target.isPlayerTeam)
                {
                    RollEnemyLootDrop(target.transform.position);
                }

                target.gameObject.SetActive(false);
            }
        }
    }

    private void RollEnemyLootDrop(Vector3 deathWorldPos)
    {
        if (lootOrbPrefab == null) return;

        float roll = Random.value;

        // 1. Roll Item Component Drop
        if (roll <= itemDropChance && dropItemPool != null && dropItemPool.Count > 0)
        {
            ItemData droppedItem = dropItemPool[Random.Range(0, dropItemPool.Count)];
            SpawnLootOrb(deathWorldPos, LootType.ItemComponent, droppedItem, 0);
            return;
        }

        // 2. Roll Gold Drop
        if (roll <= (itemDropChance + goldDropChance))
        {
            int goldAmount = Random.Range(1, 4); // 1g to 3g
            SpawnLootOrb(deathWorldPos, LootType.Gold, null, goldAmount);
        }
    }

    public void SpawnLootOrb(Vector3 originPos, LootType type, ItemData item, int gold)
    {
        Vector2 randomCircle = Random.insideUnitCircle.normalized * Random.Range(1.0f, 1.8f);
        Vector3 landingPos = new Vector3(originPos.x + randomCircle.x, 0.25f, originPos.z + randomCircle.y);

        GameObject orbObj = Instantiate(lootOrbPrefab, originPos + Vector3.up * 0.5f, Quaternion.identity);
        LootOrb orb = orbObj.GetComponent<LootOrb>();

        if (orb != null)
        {
            orb.Launch(originPos + Vector3.up * 0.5f, landingPos, type, item, gold);
        }
    }
}