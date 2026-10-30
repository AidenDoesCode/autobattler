using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class Unit : MonoBehaviour
{
    [Header("Data Configuration")]
    public UnitData data;

    [Header("Grid Coordinates")]
    public int gridX;
    public int gridZ;

    [Header("Bench Coordinates")]
    public bool isOnBench = false;
    public int benchSlotIndex = -1;

    [Header("Team Alignment")]
    public bool isPlayerTeam = true;

    [Header("Star Level Progression")]
    public int starLevel = 1; // 1, 2, or 3 Stars
    public int scaledMaxHealth;
    public int scaledAttackDamage;

    [Header("Visual Components")]
    public UnitHealthBarUI healthBar;

    [HideInInspector] public int currentHealth;
    [HideInInspector] public int lastValidX;
    [HideInInspector] public int lastValidZ;
    [HideInInspector] public Vector3 lastValidWorldPos;
    [HideInInspector] public bool isMoving = false;

    private Renderer unitRenderer;
    private Color originalColor = Color.white;
    private Vector3 basePrefabScale = Vector3.one;
    private Coroutine attackRoutine;
    private Coroutine flinchRoutine;

    void Awake()
    {
        if (healthBar == null)
        {
            healthBar = GetComponentInChildren<UnitHealthBarUI>();
        }

        // Cache the prefab's original natural scale so we don't blow it up
        basePrefabScale = transform.localScale;

        unitRenderer = GetComponentInChildren<Renderer>();
        if (unitRenderer != null && unitRenderer.material.HasProperty("_Color"))
        {
            originalColor = unitRenderer.material.color;
        }
    }

    void Start()
    {
        InitializeStats();
        UpdateHealthUI();
    }

    void OnDisable()
    {
        // Stop any running flinch and restore original color immediately
        if (flinchRoutine != null)
        {
            StopCoroutine(flinchRoutine);
            flinchRoutine = null;
        }
        RestoreOriginalColor();
    }

    public void InitializeStats()
    {
        if (data != null)
        {
            if (scaledMaxHealth <= 0) scaledMaxHealth = data.maxHealth;
            if (scaledAttackDamage <= 0) scaledAttackDamage = data.attackDamage;
            currentHealth = scaledMaxHealth;
        }
    }

    public void Initialize(int x, int z, BoardGridManager gridManager)
    {
        gridX = x;
        gridZ = z;
        SaveValidPosition(x, z, gridManager.GetWorldPosition(x, z), onBench: false, benchIndex: -1);
        transform.position = lastValidWorldPos;

        InitializeStats();
        UpdateHealthUI();
    }

    public void SaveValidPosition(int x, int z, Vector3 worldPos, bool onBench = false, int benchIndex = -1)
    {
        lastValidX = x;
        lastValidZ = z;
        lastValidWorldPos = worldPos;
        gridX = x;
        gridZ = z;
        isOnBench = onBench;
        benchSlotIndex = benchIndex;
    }

    /// <summary>
    /// Upgrades the unit to the next star level, scaling its combat attributes and mesh size relative to its base prefab scale.
    /// </summary>
    public void UpgradeStarLevel(int newStarLevel)
    {
        starLevel = newStarLevel;

        // Multiply stats: 1.8x for 2-Star, 3.2x for 3-Star
        float statMultiplier = (starLevel == 2) ? 1.8f : 3.2f;
        scaledMaxHealth = Mathf.RoundToInt(data.maxHealth * statMultiplier);
        scaledAttackDamage = Mathf.RoundToInt(data.attackDamage * statMultiplier);
        currentHealth = scaledMaxHealth;

        // Subtle scale bump: +8% for 2-Star, +16% for 3-Star relative to original prefab scale
        float visualScaleMultiplier = 1f + (starLevel - 1) * 0.08f;
        transform.localScale = basePrefabScale * visualScaleMultiplier;

        Debug.Log($"<color=yellow>[Star-Up]</color> {data.unitName} upgraded to {starLevel}-Star! (HP: {scaledMaxHealth}, DMG: {scaledAttackDamage})");

        UpdateHealthUI();
    }

    public int GetEffectiveAttackDamage()
    {
        return scaledAttackDamage > 0 ? scaledAttackDamage : (data != null ? data.attackDamage : 10);
    }

    public int GetEffectiveMaxHealth()
    {
        return scaledMaxHealth > 0 ? scaledMaxHealth : (data != null ? data.maxHealth : 100);
    }

    public void PlayAttackAnimation(Vector3 targetPosition)
    {
        if (attackRoutine != null) StopCoroutine(attackRoutine);
        attackRoutine = StartCoroutine(AttackLungeRoutine(targetPosition));
    }

    private IEnumerator AttackLungeRoutine(Vector3 targetPosition)
    {
        Vector3 startPos = transform.position;
        Vector3 lungeDir = (targetPosition - startPos).normalized;
        Vector3 lungePos = startPos + lungeDir * 0.35f;

        float halfDuration = 0.08f;
        float elapsed = 0f;

        while (elapsed < halfDuration)
        {
            transform.position = Vector3.Lerp(startPos, lungePos, elapsed / halfDuration);
            elapsed += Time.deltaTime;
            yield return null;
        }

        elapsed = 0f;
        while (elapsed < halfDuration)
        {
            transform.position = Vector3.Lerp(lungePos, startPos, elapsed / halfDuration);
            elapsed += Time.deltaTime;
            yield return null;
        }

        transform.position = startPos;
    }

    public void TakeDamage(int damage)
    {
        currentHealth = Mathf.Max(0, currentHealth - damage);
        UpdateHealthUI();

        SpawnPopup(damage, isHeal: false);

        // Only flinch if the unit is still alive; if dead, reset immediately
        if (currentHealth > 0)
        {
            if (flinchRoutine != null) StopCoroutine(flinchRoutine);
            flinchRoutine = StartCoroutine(FlinchRoutine());
        }
        else
        {
            RestoreOriginalColor();
        }
    }

    public void Heal(int amount)
    {
        currentHealth = Mathf.Min(GetEffectiveMaxHealth(), currentHealth + amount);
        UpdateHealthUI();

        SpawnPopup(amount, isHeal: true);
    }

    private void SpawnPopup(int amount, bool isHeal)
    {
        if (healthBar != null)
        {
            healthBar.ShowCombatPopup(amount, isHeal);
        }
    }

    private IEnumerator FlinchRoutine()
    {
        if (unitRenderer != null && unitRenderer.material.HasProperty("_Color"))
        {
            unitRenderer.material.color = Color.red;
            yield return new WaitForSeconds(0.1f);
            RestoreOriginalColor();
        }
        flinchRoutine = null;
    }

    private void RestoreOriginalColor()
    {
        if (unitRenderer != null && unitRenderer.material.HasProperty("_Color"))
        {
            unitRenderer.material.color = originalColor;
        }
    }

    public void UpdateHealthUI()
    {
        if (healthBar != null && data != null)
        {
            healthBar.SetTeam(isPlayerTeam);
            healthBar.UpdateHealth(currentHealth, GetEffectiveMaxHealth());
            healthBar.UpdateStarLevel(starLevel); // Updates overhead star badge
        }
    }

    public void ResetToPlanningPosition(BoardGridManager gridManager)
    {
        gameObject.SetActive(true);
        isMoving = false;

        // Ensure clean color and health reset
        RestoreOriginalColor();
        currentHealth = GetEffectiveMaxHealth();

        transform.rotation = Quaternion.identity;
        gridX = lastValidX;
        gridZ = lastValidZ;
        transform.position = lastValidWorldPos;

        if (gridManager != null)
        {
            if (isOnBench)
            {
                gridManager.RegisterBenchUnit(this, benchSlotIndex);
            }
            else
            {
                gridManager.RegisterUnit(this, gridX, gridZ);
            }
        }

        UpdateHealthUI();
    }

    public IEnumerator MoveToTileRoutine(int targetX, int targetZ, BoardGridManager gridManager)
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

    public Unit FindTarget(List<Unit> allUnits)
    {
        Unit bestTarget = null;
        float minDistance = float.MaxValue;

        foreach (var other in allUnits)
        {
            if (other == this || !other.gameObject.activeInHierarchy || other.currentHealth <= 0) continue;

            if (data.archetype == UnitArchetype.Support)
            {
                if (other.isPlayerTeam == this.isPlayerTeam && other.currentHealth < other.GetEffectiveMaxHealth())
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
}