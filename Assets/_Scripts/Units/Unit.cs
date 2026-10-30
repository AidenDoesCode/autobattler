using UnityEngine;
using System.Collections.Generic;

public class Unit : MonoBehaviour
{
    [Header("Data Configuration")]
    public UnitData data;

    [Header("Grid Coordinates")]
    public int gridX;
    public int gridZ;

    [Header("Team Alignment")]
    public bool isPlayerTeam = true; // True for player units, false for enemy units

    // Runtime state
    [HideInInspector] public int currentHealth;
    [HideInInspector] public int lastValidX;
    [HideInInspector] public int lastValidZ;
    [HideInInspector] public Vector3 lastValidWorldPos;

    [Header("Visual Components")]
    public UnitHealthBarUI healthBar;


    [HideInInspector] public bool isMoving = false;

    void Awake()
    {
        if (healthBar == null)
        {
            healthBar = GetComponentInChildren<UnitHealthBarUI>();
        }
    }

    public void TakeDamage(int damage)
    {
        currentHealth = Mathf.Max(0, currentHealth - damage);
        UpdateHealthUI();
    }

    public void Heal(int amount)
    {
        if (data != null)
        {
            currentHealth = Mathf.Min(data.maxHealth, currentHealth + amount);
            UpdateHealthUI();
        }
    }

    public void UpdateHealthUI()
    {
        if (healthBar != null && data != null)
        {
            healthBar.SetTeam(isPlayerTeam);
            healthBar.UpdateHealth(currentHealth, data.maxHealth);
        }
    }

    void Start()
    {
        if (data != null)
        {
            currentHealth = data.maxHealth;
        }
    }


    public System.Collections.IEnumerator MoveToTileRoutine(int targetX, int targetZ, BoardGridManager gridManager)
    {
        isMoving = true;
        Vector3 startPos = transform.position;
        Vector3 endPos = gridManager.GetWorldPosition(targetX, targetZ);

        float duration = 1f / Mathf.Max(data.moveSpeed, 1f);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            transform.position = Vector3.Lerp(startPos, endPos, elapsed / duration);
            elapsed += Time.deltaTime;
            yield return null;
        }

        transform.position = endPos;
        isMoving = false;
    }

    

    public void Initialize(int x, int z, BoardGridManager gridManager)
    {
        gridX = x;
        gridZ = z;
        
        SaveValidPosition(x, z, gridManager.GetWorldPosition(x, z));
        transform.position = lastValidWorldPos;
    }

    public void SaveValidPosition(int x, int z, Vector3 worldPos)
    {
        lastValidX = x;
        lastValidZ = z;
        lastValidWorldPos = worldPos;
        gridX = x;
        gridZ = z;
    }

    // Handles targeting for Melee, Ranged, and Support archetypes
    public Unit FindTarget(List<Unit> allUnits)
    {
        Unit bestTarget = null;
        float minDistance = float.MaxValue;

        foreach (var other in allUnits)
        {
            if (other == this) continue;

            if (data.archetype == UnitArchetype.Support)
            {
                // Support units look for injured friendly units to heal/buff
                if (other.isPlayerTeam == this.isPlayerTeam && other.currentHealth < other.data.maxHealth)
                {
                    float dist = Vector3.Distance(transform.position, other.transform.position);
                    if (dist < minDistance)
                    {
                        minDistance = dist;
                        bestTarget = other;
                    }
                }
            }
            else
            {
                // Melee and Ranged units look for the closest enemy
                if (other.isPlayerTeam != this.isPlayerTeam)
                {
                    float dist = Vector3.Distance(transform.position, other.transform.position);
                    if (dist < minDistance)
                    {
                        minDistance = dist;
                        bestTarget = other;
                    }
                }
            }
        }

        return bestTarget;
    }


    public void ResetToPlanningPosition(BoardGridManager gridManager)
    {
        // Revive and reactivate
        gameObject.SetActive(true);
        isMoving = false;
        
        if (data != null)
        {
            currentHealth = data.maxHealth;
        }

        // Reset rotation to default forward (facing the enemy side)
        transform.rotation = Quaternion.identity;

        // Restore to initial planning coordinates and world position
        gridX = lastValidX;
        gridZ = lastValidZ;
        transform.position = lastValidWorldPos;

        // Re-register position with the board manager
        if (gridManager != null)
        {
            gridManager.RegisterUnit(this, gridX, gridZ);
        }
    }

}