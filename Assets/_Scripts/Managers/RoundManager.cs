using UnityEngine;
using System;

public class RoundManager : MonoBehaviour
{
    public static RoundManager Instance { get; private set; }
    
    [Header("Round Tracking")]
    public int currentRound = 1;
    
    [Header("Wave Prefabs")]
    [Tooltip("The neutral beast/monster prefab for PvE rounds.")]
    public GameObject creepPrefab; 
    [Tooltip("The standard enemy unit prefab for PvP mirror rounds.")]
    public GameObject pvpEnemyPrefab; 

    public event Action<int, bool> OnRoundStarted; // (roundNumber, isPvE)

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
        // Delay the initial round 1 spawn slightly to allow the grid to finish generating
        Invoke(nameof(SpawnCurrentWave), 0.2f);
    }

    public void AdvanceRound()
    {
        currentRound++;
        SpawnCurrentWave();
    }

    private void SpawnCurrentWave()
    {
        BoardGridManager grid = UnityEngine.Object.FindAnyObjectByType<BoardGridManager>();
        if (grid == null) return;

        bool isPvE = IsPvERound(currentRound);
        
        if (isPvE)
        {
            // Scale PvE difficulty (Round 1: 2 creeps, Round 2: 3 creeps, Round 3: 4 creeps)
            int creepCount = Mathf.Min(6, currentRound + 1);
            grid.SpawnEnemyWave(creepPrefab, creepCount);
            Debug.Log($"<color=orange>[RoundManager]</color> Started Round {currentRound} (PvE Creep Wave)");
        }
        else
        {
            // Scale PvP opponent count roughly to the player's level capacity
            int pvpCount = EconomyManager.Instance != null ? EconomyManager.Instance.currentLevel : 3;
            grid.SpawnEnemyWave(pvpEnemyPrefab, pvpCount);
            Debug.Log($"<color=orange>[RoundManager]</color> Started Round {currentRound} (PvP Opponent)");
        }

        OnRoundStarted?.Invoke(currentRound, isPvE);
    }

    public bool IsPvERound(int round)
    {
        // Rounds 1, 2, 3 and every 5th boss round (10, 15, 20) are PvE drops
        return round <= 3 || round % 5 == 0;
    }
}