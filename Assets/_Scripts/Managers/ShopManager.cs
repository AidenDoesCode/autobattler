using UnityEngine;
using System;
using System.Collections.Generic;

public class ShopManager : MonoBehaviour
{
    public static ShopManager Instance { get; private set; }

    [Header("Shop Pool & Settings")]
    public List<UnitData> unitPool = new List<UnitData>();
    public GameObject unitBasePrefab;
    public int shopSize = 5;
    public int rerollCost = 2;

    [Header("Shop Lock State")]
    public bool isLocked = false;

    [Header("Current Shop Cards")]
    public UnitData[] currentShopUnits;

    // Events for UI Toolkit
    public event Action<UnitData[]> OnShopGenerated;
    public event Action<int> OnUnitPurchased; // Index of purchased card
    public event Action<bool> OnLockStateChanged; // (isLocked)

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
        currentShopUnits = new UnitData[shopSize];
        RerollShop(isFree: true);
    }

    /// <summary>
    /// Toggles the lock state and notifies the UI.
    /// </summary>
    public void ToggleLock()
    {
        isLocked = !isLocked;
        Debug.Log($"<color={(isLocked ? "yellow" : "white")}>[Shop]</color> Shop is now {(isLocked ? "LOCKED" : "UNLOCKED")}.");
        OnLockStateChanged?.Invoke(isLocked);
    }

    /// <summary>
    /// Called when transitioning to the Planning phase to grant a round-start refresh.
    /// If locked, skips rerolling and consumes the lock for the following round.
    /// </summary>
    public void OnRoundPlanningStarted()
    {
        if (isLocked)
        {
            Debug.Log("[Shop] Shop was locked! Retaining current offers.");
            // Consume the lock for the upcoming round
            isLocked = false;
            OnLockStateChanged?.Invoke(false);
            return;
        }

        RerollShop(isFree: true);
    }

    public void RerollShop(bool isFree = false)
    {
        if (!isFree)
        {
            if (EconomyManager.Instance == null || !EconomyManager.Instance.SpendGold(rerollCost))
            {
                Debug.LogWarning("[ShopManager] Not enough gold to reroll!");
                return;
            }
        }

        GenerateShop();
    }

    public void GenerateShop()
    {
        if (unitPool == null || unitPool.Count == 0)
        {
            Debug.LogWarning("[ShopManager] Unit pool is empty!");
            return;
        }

        if (currentShopUnits == null || currentShopUnits.Length != shopSize)
        {
            currentShopUnits = new UnitData[shopSize];
        }

        for (int i = 0; i < shopSize; i++)
        {
            currentShopUnits[i] = GetRandomUnitByOdds();
        }

        OnShopGenerated?.Invoke(currentShopUnits);
    }

    private UnitData GetRandomUnitByOdds()
    {
        if (unitPool == null || unitPool.Count == 0) return null;

        float[] odds = EconomyManager.Instance != null 
            ? EconomyManager.Instance.GetTierOdds() 
            : new float[] { 0.6f, 0.3f, 0.1f };

        float roll = UnityEngine.Random.value;
        int targetCost = 1;
        float cumulative = 0f;

        for (int i = 0; i < odds.Length; i++)
        {
            cumulative += odds[i];
            if (roll <= cumulative)
            {
                targetCost = i + 1;
                break;
            }
        }

        List<UnitData> tierUnits = unitPool.FindAll(u => u != null && u.goldCost == targetCost);
        if (tierUnits.Count > 0)
        {
            return tierUnits[UnityEngine.Random.Range(0, tierUnits.Count)];
        }

        return unitPool[UnityEngine.Random.Range(0, unitPool.Count)];
    }

    public bool BuyUnit(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= shopSize) return false;
        UnitData unitToBuy = currentShopUnits[slotIndex];
        if (unitToBuy == null) return false;

        if (gridManager == null) gridManager = UnityEngine.Object.FindAnyObjectByType<BoardGridManager>();
        if (gridManager == null) return false;

        int emptyBenchSlot = gridManager.GetFirstEmptyBenchSlot();
        if (emptyBenchSlot == -1)
        {
            Debug.LogWarning("[ShopManager] Bench is full! Cannot purchase unit.");
            return false;
        }

        if (EconomyManager.Instance == null || !EconomyManager.Instance.SpendGold(unitToBuy.goldCost))
        {
            Debug.LogWarning("[ShopManager] Not enough gold to buy this unit!");
            return false;
        }

        Vector3 spawnPos = gridManager.GetBenchWorldPosition(emptyBenchSlot);
        GameObject newUnitObj = Instantiate(unitBasePrefab, spawnPos, Quaternion.identity);
        newUnitObj.name = $"{unitToBuy.unitName}_Bench";

        Unit unitComp = newUnitObj.GetComponent<Unit>();
        if (unitComp != null)
        {
            unitComp.data = unitToBuy;
            unitComp.isPlayerTeam = true;
            unitComp.currentHealth = unitToBuy.maxHealth;
            unitComp.SaveValidPosition(-1, -1, spawnPos, onBench: true, benchIndex: emptyBenchSlot);
            gridManager.RegisterBenchUnit(unitComp, emptyBenchSlot);
            unitComp.UpdateHealthUI();
        }

        // Clear offer slot from shop
        currentShopUnits[slotIndex] = null;
        OnUnitPurchased?.Invoke(slotIndex);

        // Check for 3-of-a-kind star merges across board and bench
        if (UnitMergeManager.Instance != null)
        {
            UnitMergeManager.Instance.CheckForMerges();
        }

        return true;
    }
}