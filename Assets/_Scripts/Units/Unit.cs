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
    public int starLevel = 1;
    public int scaledMaxHealth;
    public int scaledAttackDamage;

    [Header("Equipped Items (Max 3)")]
    public List<ItemData> equippedItems = new List<ItemData>();
    public const int MaxItems = 3;

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
            currentHealth = GetEffectiveMaxHealth();
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

    public void UpgradeStarLevel(int newStarLevel)
    {
        starLevel = newStarLevel;

        float statMultiplier = (starLevel == 2) ? 1.8f : 3.2f;
        scaledMaxHealth = Mathf.RoundToInt(data.maxHealth * statMultiplier);
        scaledAttackDamage = Mathf.RoundToInt(data.attackDamage * statMultiplier);
        currentHealth = GetEffectiveMaxHealth();

        float visualScaleMultiplier = 1f + (starLevel - 1) * 0.08f;
        transform.localScale = basePrefabScale * visualScaleMultiplier;

        Debug.Log($"<color=yellow>[Star-Up]</color> {data.unitName} upgraded to {starLevel}-Star! (HP: {scaledMaxHealth}, DMG: {scaledAttackDamage})");

        UpdateHealthUI();
    }

    // --- Item Stat Bonuses ---

    public int GetItemBonusHealth()
    {
        int sum = 0;
        foreach (var it in equippedItems) if (it != null) sum += it.bonusHealth;
        return sum;
    }

    public int GetItemBonusDamage()
    {
        int sum = 0;
        foreach (var it in equippedItems) if (it != null) sum += it.bonusAttackDamage;
        return sum;
    }

    public int GetItemFlatDamageReduction()
    {
        int sum = 0;
        foreach (var it in equippedItems) if (it != null) sum += it.flatDamageReduction;
        return sum;
    }

    public float GetItemBonusAttackSpeed()
    {
        float sum = 0f;
        foreach (var it in equippedItems) if (it != null) sum += it.bonusAttackSpeed;
        return sum;
    }

    public int GetEffectiveAttackDamage()
    {
        int baseVal = scaledAttackDamage > 0 ? scaledAttackDamage : (data != null ? data.attackDamage : 10);
        return baseVal + GetItemBonusDamage();
    }

    public int GetEffectiveMaxHealth()
    {
        int baseVal = scaledMaxHealth > 0 ? scaledMaxHealth : (data != null ? data.maxHealth : 100);
        return baseVal + GetItemBonusHealth();
    }

    /// <summary>
    /// Attempts to equip or fuse an item onto this unit.
    /// </summary>
    public bool TryEquipOrFuseItem(ItemData newItem, ItemDatabase db)
    {
        if (newItem == null) return false;

        // 1. Check if an uncompleted component can fuse with newItem
        if (!newItem.isCompletedItem && db != null)
        {
            for (int i = 0; i < equippedItems.Count; i++)
            {
                if (!equippedItems[i].isCompletedItem)
                {
                    ItemData combined = db.CombineItems(equippedItems[i], newItem);
                    if (combined != null)
                    {
                        Debug.Log($"<color=cyan>[Item Fuse]</color> {equippedItems[i].itemName} + {newItem.itemName} -> <b>{combined.itemName}</b> on {data.unitName}!");
                        equippedItems[i] = combined;
                        currentHealth += combined.bonusHealth;
                        UpdateHealthUI();
                        return true;
                    }
                }
            }
        }

        // 2. Otherwise, equip into an empty slot if under capacity
        if (equippedItems.Count < MaxItems)
        {
            equippedItems.Add(newItem);
            currentHealth += newItem.bonusHealth;
            Debug.Log($"<color=yellow>[Equip]</color> Equipped {newItem.itemName} onto {data.unitName} ({equippedItems.Count}/{MaxItems})");
            UpdateHealthUI();
            return true;
        }

        Debug.LogWarning($"[Items] {data.unitName} already has max items ({MaxItems})!");
        return false;
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
            healthBar.UpdateStarLevel(starLevel);
            healthBar.UpdateEquippedItems(equippedItems);
        }
    }

    public void ResetToPlanningPosition(BoardGridManager gridManager)
    {
        gameObject.SetActive(true);
        isMoving = false;

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