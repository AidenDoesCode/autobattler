using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    public enum GameState { Planning, Combat }
    public GameState currentState;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
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

    public void ReturnToPlanning()
    {
        currentState = GameState.Planning;
        Debug.Log("Round over. Returning to Planning/Shop phase.");
    }
}