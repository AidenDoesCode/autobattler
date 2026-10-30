using UnityEngine;
using UnityEngine.InputSystem;
public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    public enum GameState
    {
        Planning,
        Combat,
        Auction // New state between rounds
    }
    public GameState currentState;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }
    public void StartAuctionPhase()
    {
        currentState = GameState.Auction;
        Debug.Log("<color=purple>[GameManager]</color> Starting Blind Auction Round!");
        if (AuctionManager.Instance != null)
        {
            AuctionManager.Instance.BeginAuction();
        }
    }

    void Start()
    {
        // Start the game in the Planning phase
        currentState = GameState.Planning;
    }

    // Call this from your UI Button
    public void StartBattle()
    {
        if (currentState == GameState.Planning)
        {
            currentState = GameState.Combat;
            Debug.Log("Battle Started! Drag-and-drop disabled. Combat begins...");

            // Here is where you will trigger your turn-based combat loop later!
        }
    }

    // Inside GameManager.cs
    // Inside GameManager.cs
    [Header("Round & Auction Flow")]
    public int currentRound = 0;
    public int auctionInterval = 3; // Triggers every 3 rounds

    public void ReturnToPlanning()
    {
        currentRound++;
        Debug.Log($"<color=white>[Round {currentRound}]</color> Ending combat.");

        // 1. Check if this round should be an Auction round
        if (currentRound > 0 && currentRound % auctionInterval == 0)
        {
            StartAuctionPhase();
            return; // Exit early so Auction state is preserved
        }

        // 2. Standard Round: Return to Planning & Shop
        currentState = GameState.Planning;
        Debug.Log("<color=green>[Phase]</color> Returning to Planning/Shop phase.");

        // Automatically give a fresh shop (or retain current offers if locked)
        if (ShopManager.Instance != null)
        {
            ShopManager.Instance.OnRoundPlanningStarted();
        }
    }

    void Update()
    {
        // 2. Check key press via New Input System
        if (Keyboard.current != null && Keyboard.current.bKey.wasPressedThisFrame)
        {
            StartAuctionPhase();
        }
    }
}