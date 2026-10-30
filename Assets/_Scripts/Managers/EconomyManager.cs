using UnityEngine;
using System;
using System.Collections.Generic;

public class EconomyManager : MonoBehaviour
{
    public static EconomyManager Instance { get; private set; }

    [Header("Gold Settings")]
    public int currentGold = 10;
    public int baseRoundIncome = 5;
    public int interestPerTenGold = 1;
    public int maxInterest = 5;

    [Header("Level & XP Settings")]
    public int currentLevel = 1;
    public int maxLevel = 9;
    public int currentXP = 0;
    public int xpCost = 4;
    public int xpPerPurchase = 4;
    public int passiveRoundXP = 2;

    // XP required to reach the NEXT level: Index represents current level
    // Level 1 -> 2: 2 XP, Level 2 -> 3: 4 XP, etc.
    private readonly int[] xpToNextLevel = new int[] { 0, 2, 4, 8, 14, 24, 36, 54, 76 };

    [Header("Streaks")]
    public int winStreak = 0;
    public int lossStreak = 0;

    // Events for UI Toolkit
    public event Action<int> OnGoldChanged;
    public event Action<int, int, int> OnXPChanged; // (currentXP, maxXPForLevel, currentLevel)
    public event Action<int> OnLevelUp;             // (newLevel)

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // Fallback safeguard: if Unity's Inspector serialized 0, reset to 10
        if (currentGold <= 0)
        {
            currentGold = 10;
        }
    }

    void Start()
    {
        OnGoldChanged?.Invoke(currentGold);
        NotifyXP();
        ApplyLevelToBoardCap();
    }

    public bool CanAfford(int amount) => currentGold >= amount;

    public bool SpendGold(int amount)
    {
        if (!CanAfford(amount)) return false;

        currentGold -= amount;
        OnGoldChanged?.Invoke(currentGold);
        return true;
    }

    public void AddGold(int amount)
    {
        currentGold += amount;
        OnGoldChanged?.Invoke(currentGold);
    }

    public void SellUnit(UnitData data)
    {
        if (data == null) return;

        int refund = data.goldCost;
        currentGold += refund;
        OnGoldChanged?.Invoke(currentGold);

        Debug.Log($"<color=yellow>[Economy]</color> Sold {data.unitName} for {refund}g. Current Gold: {currentGold}g");
    }

    // --- XP & Leveling Logic ---

    public bool BuyXP()
    {
        if (currentLevel >= maxLevel) return false;
        if (!SpendGold(xpCost)) return false;

        AddXP(xpPerPurchase);
        return true;
    }

    public void AddXP(int amount)
    {
        if (currentLevel >= maxLevel) return;

        currentXP += amount;

        while (currentLevel < maxLevel && currentXP >= GetRequiredXPForCurrentLevel())
        {
            currentXP -= GetRequiredXPForCurrentLevel();
            currentLevel++;

            Debug.Log($"<color=cyan>[Level Up!]</color> Player reached Level {currentLevel}!");
            ApplyLevelToBoardCap();
            OnLevelUp?.Invoke(currentLevel);
        }

        if (currentLevel >= maxLevel)
        {
            currentXP = 0;
        }

        NotifyXP();
    }

    public int GetRequiredXPForCurrentLevel()
    {
        if (currentLevel >= maxLevel) return 0;
        if (currentLevel < xpToNextLevel.Length) return xpToNextLevel[currentLevel];
        return 50;
    }

    private void NotifyXP()
    {
        OnXPChanged?.Invoke(currentXP, GetRequiredXPForCurrentLevel(), currentLevel);
    }

    private void ApplyLevelToBoardCap()
    {
        BoardGridManager grid = UnityEngine.Object.FindAnyObjectByType<BoardGridManager>();
        if (grid != null)
        {
            // Direct 1:1 scaling: Level 1 = 1 unit, Level 2 = 2 units, etc.
            grid.maxBoardUnits = currentLevel;
        }
    }

    public void ProcessRoundEndIncome(bool playerWon)
    {
        // 1. Passive Round XP (+2 XP each round)
        AddXP(passiveRoundXP);

        // 2. Win / Loss Streak calculation
        if (playerWon)
        {
            winStreak++;
            lossStreak = 0;
        }
        else
        {
            lossStreak++;
            winStreak = 0;
        }

        int streakBonus = CalculateStreakBonus();
        int interest = Mathf.Min(currentGold / 10 * interestPerTenGold, maxInterest);
        int winGold = playerWon ? 1 : 0;
        int totalIncome = baseRoundIncome + interest + streakBonus + winGold;

        AddGold(totalIncome);

        Debug.Log($"<color=yellow>[Income]</color> Base: {baseRoundIncome}g | Interest: {interest}g | Streak: {streakBonus}g | Total: +{totalIncome}g");
    }

    private int CalculateStreakBonus()
    {
        int activeStreak = Mathf.Max(winStreak, lossStreak);
        if (activeStreak >= 5) return 3;
        if (activeStreak >= 4) return 2;
        if (activeStreak >= 2) return 1;
        return 0;
    }

    /// <summary>
    /// Returns probability weights [Tier1, Tier2, Tier3] based on player level.
    /// </summary>
    public float[] GetTierOdds()
    {
        switch (currentLevel)
        {
            case 1: return new float[] { 1.00f, 0.00f, 0.00f };
            case 2: return new float[] { 1.00f, 0.00f, 0.00f };
            case 3: return new float[] { 0.75f, 0.25f, 0.00f };
            case 4: return new float[] { 0.55f, 0.30f, 0.15f };
            case 5: return new float[] { 0.45f, 0.33f, 0.22f };
            case 6: return new float[] { 0.30f, 0.40f, 0.30f };
            case 7: return new float[] { 0.19f, 0.45f, 0.36f };
            case 8: return new float[] { 0.15f, 0.40f, 0.45f };
            default: return new float[] { 0.10f, 0.35f, 0.55f };
        }
    }
}