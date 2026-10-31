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

    [Header("Team Alignment")]
    public bool isPlayerTeam = true;

    [Header("Star Progression")]
    public int starLevel = 1;

    [Header("Mana & Ultimate")]
    public int currentMana = 0;
    public int maxMana = 100;
    public int manaGainOnAttack = 20;
    public int manaGainOnDamaged = 10;
    public bool isStunned = false;

    [Header("Items")]
    public List<ItemData> equippedItems = new List<ItemData>();
    public const int MaxItems = 3;

    [Header("Synergy Stat Buffs")]
    public int bonusSynergyHealth = 0;
    public int bonusSynergyDamage = 0;

    // Backward-compatible alias for SynergyManager
    public int scaledMaxHealth
    {
        get => GetEffectiveMaxHealth();
        set => bonusSynergyHealth = Mathf.Max(0, value - (data != null ? data.maxHealth : 100));
    }

    [Header("Bench Placement")]
    public bool isOnBench = false;
    public int benchSlotIndex = -1;

    [Header("Visual Components")]
    public UnitHealthBarUI healthBar;

    [HideInInspector] public int currentHealth;
    [HideInInspector] public int lastValidX;
    [HideInInspector] public int lastValidZ;
    [HideInInspector] public Vector3 lastValidWorldPos;
    [HideInInspector] public bool isMoving = false;

    private Renderer unitRenderer;
    private Color originalColor = Color.white;
    private Coroutine attackRoutine;
    private Coroutine flinchRoutine;
    private Coroutine stunRoutine;
    private Vector3 baseScale = Vector3.one;

    void Awake()
    {
        if (healthBar == null)
        {
            healthBar = GetComponentInChildren<UnitHealthBarUI>();
        }

        unitRenderer = GetComponentInChildren<Renderer>();
        if (unitRenderer != null && unitRenderer.material.HasProperty("_Color"))
        {
            originalColor = unitRenderer.material.color;
        }

        baseScale = transform.localScale;
    }

    void Start()
    {
        if (data != null)
        {
            currentHealth = GetEffectiveMaxHealth();
        }

        currentMana = 0;
        UpdateHealthUI();
    }

    public void Initialize(int x, int z, BoardGridManager gridManager)
    {
        gridX = x;
        gridZ = z;
        SaveValidPosition(x, z, gridManager.GetWorldPosition(x, z), onBench: false, benchIndex: -1);
        transform.position = lastValidWorldPos;

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

    // --- Mana & Ultimates ---

    public void GainMana(int amount)
    {
        if (currentMana >= maxMana) return;

        currentMana = Mathf.Min(maxMana, currentMana + amount);
        UpdateHealthUI();

        if (currentMana >= maxMana)
        {
            Debug.Log($"<color=cyan>[Ultimate Ready]</color> {data.unitName} is fully charged!");
        }
    }

    public bool CanCastUltimate() => currentMana >= maxMana && !isStunned && !isMoving;

    public void CastUltimate(List<Unit> allUnits, BoardGridManager gridManager)
    {
        if (!CanCastUltimate()) return;

        currentMana = 0;
        UpdateHealthUI();

        if (healthBar != null)
        {
            healthBar.ShowCombatPopup(0, false, data.UltimateName, new Color(0.2f, 0.8f, 1f));
        }

        StartCoroutine(UltimateCastPulseRoutine());

        switch (data.UltimateType)
        {
            case UnitUltimate.CleaveStun:
                ExecuteMeleeCleaveStun(allUnits, gridManager);
                break;

            case UnitUltimate.Snipe:
                ExecuteRangedBarrage(allUnits);
                break;

            case UnitUltimate.HealingChorus:
                ExecuteSupportAoeChorus(allUnits);
                break;

            case UnitUltimate.SecondWind:
                Heal(Mathf.RoundToInt(GetEffectiveAttackDamage() * data.UltimateMultiplier));
                break;
        }
    }

    private void ExecuteMeleeCleaveStun(List<Unit> allUnits, BoardGridManager gridManager)
    {
        int ultDamage = Mathf.RoundToInt(GetEffectiveAttackDamage() * data.UltimateMultiplier);
        Debug.Log($"<color=red>[ULTIMATE: {data.UltimateName}]</color> {data.unitName} smashes the ground for {ultDamage} DMG!");

        foreach (var u in allUnits)
        {
            if (!IsValidEnemyTarget(u)) continue;

            int dist = Mathf.Max(Mathf.Abs(u.gridX - gridX), Mathf.Abs(u.gridZ - gridZ));
            if (dist <= data.ultimateRadius)
            {
                u.TakeDamage(ultDamage, this);
                if (u.currentHealth > 0) u.ApplyStun(data.ultimateStunDuration);
            }
        }
    }

    private void ExecuteRangedBarrage(List<Unit> allUnits)
    {
        Unit weakestEnemy = null;
        float lowestHpRatio = float.MaxValue;

        foreach (var u in allUnits)
        {
            if (!IsValidEnemyTarget(u)) continue;

            float ratio = (float)u.currentHealth / u.GetEffectiveMaxHealth();
            if (ratio < lowestHpRatio)
            {
                lowestHpRatio = ratio;
                weakestEnemy = u;
            }
        }

        if (weakestEnemy != null)
        {
            int barrageDmg = Mathf.RoundToInt(GetEffectiveAttackDamage() * data.UltimateMultiplier);
            Debug.Log($"<color=orange>[ULTIMATE: {data.UltimateName}]</color> {data.unitName} snipes {weakestEnemy.data.unitName} for {barrageDmg} DMG!");
            weakestEnemy.TakeDamage(barrageDmg, this);
        }
    }

    private void ExecuteSupportAoeChorus(List<Unit> allUnits)
    {
        int healAmount = Mathf.RoundToInt(GetEffectiveAttackDamage() * data.UltimateMultiplier);
        Debug.Log($"<color=green>[ULTIMATE: {data.UltimateName}]</color> {data.unitName} heals all allies for {healAmount} HP!");

        foreach (var u in allUnits)
        {
            if (u == null || !u.gameObject.activeInHierarchy || u.isPlayerTeam != isPlayerTeam || u.isOnBench || u.currentHealth <= 0) continue;
            u.Heal(healAmount);
        }
    }

    public void ApplyStun(float duration)
    {
        if (stunRoutine != null) StopCoroutine(stunRoutine);
        stunRoutine = StartCoroutine(StunRoutine(duration));
    }

    private IEnumerator StunRoutine(float duration)
    {
        isStunned = true;
        if (healthBar != null) healthBar.ShowCombatPopup(0, false, "STUNNED!", Color.yellow);

        yield return new WaitForSeconds(duration);
        isStunned = false;
    }

    private IEnumerator UltimateCastPulseRoutine()
    {
        Vector3 origScale = transform.localScale;
        transform.localScale = origScale * 1.25f;
        yield return new WaitForSeconds(0.18f);
        transform.localScale = origScale;
    }

    // --- Stats & Combat ---

    public int GetEffectiveAttackDamage()
    {
        if (data == null) return 10;
        float starMult = Mathf.Pow(1.8f, starLevel - 1);
        int baseVal = Mathf.RoundToInt(data.attackDamage * starMult);
        return baseVal + GetItemBonusAttackDamage() + bonusSynergyDamage;
    }

    public int GetEffectiveMaxHealth()
    {
        if (data == null) return 100;
        float starMult = Mathf.Pow(1.8f, starLevel - 1);
        int baseVal = Mathf.RoundToInt(data.maxHealth * starMult);
        return baseVal + GetItemBonusHealth() + bonusSynergyHealth;
    }

    public bool IsValidEnemyTarget(Unit target)
    {
        return target != null && target != this &&
            target.isPlayerTeam != isPlayerTeam &&
            target.gameObject.activeInHierarchy && !target.isOnBench &&
            target.currentHealth > 0;
    }

    public void TakeDamage(int damage, Unit attacker)
    {
        // Reject friendly fire before health, mana, or hit effects can change.
        if (attacker == null || !attacker.IsValidEnemyTarget(this)) return;

        TakeDamage(damage);
    }

    public void TakeDamage(int damage)
    {
        currentHealth = Mathf.Max(0, currentHealth - damage);
        GainMana(manaGainOnDamaged);

        UpdateHealthUI();

        if (healthBar != null)
        {
            healthBar.ShowCombatPopup(damage, isHeal: false);
        }

        if (flinchRoutine != null) StopCoroutine(flinchRoutine);
        flinchRoutine = StartCoroutine(FlinchRoutine());
    }

    public void Heal(int amount)
    {
        currentHealth = Mathf.Min(GetEffectiveMaxHealth(), currentHealth + amount);
        UpdateHealthUI();

        if (healthBar != null)
        {
            healthBar.ShowCombatPopup(amount, isHeal: true);
        }
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

    private IEnumerator FlinchRoutine()
    {
        if (unitRenderer != null && unitRenderer.material.HasProperty("_Color"))
        {
            unitRenderer.material.color = Color.red;
            yield return new WaitForSeconds(0.1f);
            unitRenderer.material.color = originalColor;
        }
    }

    public void UpdateHealthUI()
    {
        if (healthBar != null && data != null)
        {
            healthBar.SetTeam(isPlayerTeam);
            healthBar.UpdateHealth(currentHealth, GetEffectiveMaxHealth());
            healthBar.UpdateMana(currentMana, maxMana);
            healthBar.UpdateStarLevel(starLevel);
            healthBar.UpdateEquippedItems(equippedItems);
        }
    }

    public void UpgradeStarLevel(int newLevel)
    {
        starLevel = Mathf.Clamp(newLevel, 1, 3);
        transform.localScale = baseScale * (1f + (starLevel - 1) * 0.15f);

        currentHealth = GetEffectiveMaxHealth();
        UpdateHealthUI();
    }

    public void ResetToPlanningPosition(BoardGridManager gridManager)
    {
        gameObject.SetActive(true);
        isMoving = false;
        isStunned = false;
        currentMana = 0;
        bonusSynergyHealth = 0;
        bonusSynergyDamage = 0;

        if (flinchRoutine != null) StopCoroutine(flinchRoutine);
        if (attackRoutine != null) StopCoroutine(attackRoutine);
        if (stunRoutine != null) StopCoroutine(stunRoutine);

        if (unitRenderer != null && unitRenderer.material.HasProperty("_Color"))
        {
            unitRenderer.material.color = originalColor;
        }

        currentHealth = GetEffectiveMaxHealth();
        transform.rotation = Quaternion.identity;
        transform.position = lastValidWorldPos;

        if (gridManager != null)
        {
            if (isOnBench) gridManager.RegisterBenchUnit(this, benchSlotIndex);
            else gridManager.RegisterUnit(this, lastValidX, lastValidZ);
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
            if (other == this || !other.gameObject.activeInHierarchy || other.currentHealth <= 0 || other.isOnBench) continue;

            if (data.archetype == UnitArchetype.Support)
            {
                if (other.isPlayerTeam == isPlayerTeam && other.currentHealth < other.GetEffectiveMaxHealth())
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
                if (other.isPlayerTeam != isPlayerTeam)
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

    // --- Items ---

    public bool TryEquipOrFuseItem(ItemData newItem, ItemDatabase database)
    {
        if (newItem == null || isOnBench) return false;

        // 1. Check if there is an uncompleted component on this unit that can fuse with newItem
        if (!newItem.isCompletedItem && database != null)
        {
            for (int i = 0; i < equippedItems.Count; i++)
            {
                ItemData existing = equippedItems[i];
                if (existing != null && !existing.isCompletedItem)
                {
                    // Fixed: Calling CombineItems instead of GetCombination
                    ItemData recipeResult = database.CombineItems(existing, newItem);
                    if (recipeResult != null)
                    {
                        equippedItems.RemoveAt(i);
                        equippedItems.Add(recipeResult);
                        UpdateHealthUI();
                        return true;
                    }
                }
            }
        }

        if (equippedItems.Count < MaxItems)
        {
            equippedItems.Add(newItem);
            UpdateHealthUI();
            return true;
        }

        return false;
    }

    public int GetItemBonusAttackDamage()
    {
        int bonus = 0;
        foreach (var item in equippedItems) if (item != null) bonus += item.bonusAttackDamage;
        return bonus;
    }

    public int GetItemBonusHealth()
    {
        int bonus = 0;
        foreach (var item in equippedItems) if (item != null) bonus += item.bonusHealth;
        return bonus;
    }

    public float GetItemBonusAttackSpeed()
    {
        float bonus = 0f;
        foreach (var item in equippedItems) if (item != null) bonus += item.bonusAttackSpeed;
        return bonus;
    }

    public int GetItemFlatDamageReduction()
    {
        int reduction = 0;
        foreach (var item in equippedItems) if (item != null) reduction += item.flatDamageReduction;
        return reduction;
    }
}
