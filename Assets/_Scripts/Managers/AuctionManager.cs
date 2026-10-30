using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;

public class AuctionManager : MonoBehaviour
{
    public static AuctionManager Instance { get; private set; }

    [Header("Auction Pool & Rules")]
    public List<UnitData> auctionUnitPool = new List<UnitData>();
    public GameObject unitBasePrefab;
    public float auctionDuration = 10f;

    [Header("State")]
    public UnitData currentLot;
    public float timeRemaining;
    public bool isAuctionActive = false;

    // Secret locked bids
    private int playerBid = 0;
    private int aiBid = 0;
    private bool playerLockedIn = false;

    // AI simulation profile
    public int aiGold = 15; // Simulates enemy banked economy

    // Events for UI Toolkit
    public event Action<UnitData, float> OnAuctionStarted;        // (unit, duration)
    public event Action<float> OnTimerTick;                      // (timeRemaining)
    public event Action<int> OnPlayerBidChanged;                 // (currentBid)
    public event Action<bool, string, int, int> OnAuctionEnded;  // (playerWon, winnerName, pBid, aiBid)

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public void BeginAuction()
    {
        if (auctionUnitPool == null || auctionUnitPool.Count == 0)
        {
            // Fallback to ShopManager pool if available
            if (ShopManager.Instance != null && ShopManager.Instance.unitPool.Count > 0)
            {
                auctionUnitPool = new List<UnitData>(ShopManager.Instance.unitPool);
            }
            else
            {
                Debug.LogWarning("[AuctionManager] No units configured in auction pool!");
                EndAuctionEarly();
                return;
            }
        }

        // 1. Pick a random prize unit
        int randomIndex = UnityEngine.Random.Range(0, auctionUnitPool.Count);
        currentLot = auctionUnitPool[randomIndex];

        // 2. Reset Bids & Flags
        playerBid = 0;
        playerLockedIn = false;
        timeRemaining = auctionDuration;
        isAuctionActive = true;

        // 3. Calculate simulated AI secret bid
        SimulateAIBid();

        OnAuctionStarted?.Invoke(currentLot, auctionDuration);
        OnPlayerBidChanged?.Invoke(playerBid);
    }

    void Update()
    {
        if (!isAuctionActive) return;

        timeRemaining -= Time.deltaTime;
        OnTimerTick?.Invoke(Mathf.Max(0, timeRemaining));

        if (timeRemaining <= 0)
        {
            ResolveAuction();
        }
    }

    public void SetPlayerBid(int amount)
    {
        if (!isAuctionActive || playerLockedIn) return;

        int playerMaxGold = EconomyManager.Instance != null ? EconomyManager.Instance.currentGold : 0;
        playerBid = Mathf.Clamp(amount, 0, playerMaxGold);
        OnPlayerBidChanged?.Invoke(playerBid);
    }

    public void LockInPlayerBid()
    {
        if (!isAuctionActive) return;
        playerLockedIn = true;
    }

    private void SimulateAIBid()
    {
        int minBid = currentLot != null ? currentLot.goldCost : 2;
        int maxBid = Mathf.Min(aiGold, minBid + UnityEngine.Random.Range(1, 6));

        aiBid = UnityEngine.Random.Range(minBid, Mathf.Max(minBid + 1, maxBid + 1));
    }

    private void ResolveAuction()
    {
        isAuctionActive = false;

        bool playerWon = playerBid > aiBid;
        string winnerName = playerWon ? "Player" : (aiBid > playerBid ? "Opponent" : "Tie (House Wins)");

        Debug.Log($"<color=yellow>[Auction Result]</color> Winner: {winnerName} | Player Bid: {playerBid}g vs Opponent Bid: {aiBid}g");

        if (playerWon)
        {
            if (EconomyManager.Instance != null)
            {
                EconomyManager.Instance.SpendGold(playerBid);
            }

            AwardUnitToPlayer(currentLot);
        }
        else if (aiBid > playerBid)
        {
            aiGold = Mathf.Max(0, aiGold - aiBid);
        }

        OnAuctionEnded?.Invoke(playerWon, winnerName, playerBid, aiBid);

        StartCoroutine(ConcludeAuctionRoutine());
    }

    private void AwardUnitToPlayer(UnitData prize)
    {
        BoardGridManager grid = UnityEngine.Object.FindAnyObjectByType<BoardGridManager>();
        if (grid == null || prize == null) return;

        int benchSlot = grid.GetFirstEmptyBenchSlot();
        if (benchSlot == -1)
        {
            Debug.LogWarning("[AuctionManager] Player won the unit, but bench is full!");
            return;
        }

        GameObject prefab = unitBasePrefab != null ? unitBasePrefab : (ShopManager.Instance != null ? ShopManager.Instance.unitBasePrefab : null);
        if (prefab == null) return;

        Vector3 spawnPos = grid.GetBenchWorldPosition(benchSlot);
        GameObject newUnit = Instantiate(prefab, spawnPos, Quaternion.identity);
        newUnit.name = $"{prize.unitName}_AuctionWin";

        Unit unitComp = newUnit.GetComponent<Unit>();
        if (unitComp != null)
        {
            unitComp.data = prize;
            unitComp.isPlayerTeam = true;
            unitComp.currentHealth = prize.maxHealth;
            unitComp.SaveValidPosition(-1, -1, spawnPos, onBench: true, benchIndex: benchSlot);
            grid.RegisterBenchUnit(unitComp, benchSlot);
            unitComp.UpdateHealthUI();
        }
    }

    private IEnumerator ConcludeAuctionRoutine()
    {
        yield return new WaitForSeconds(3.5f);

        GameManager gm = UnityEngine.Object.FindAnyObjectByType<GameManager>();
        if (gm != null)
        {
            gm.ReturnToPlanning();
        }
    }

    private void EndAuctionEarly()
    {
        isAuctionActive = false;
        GameManager gm = UnityEngine.Object.FindAnyObjectByType<GameManager>();
        if (gm != null) gm.ReturnToPlanning();
    }
}