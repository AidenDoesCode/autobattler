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
    }

    void Start()
    {
        if (data != null)
        {
            currentHealth = data.maxHealth;
        }

        UpdateHealthUI();
    }

    public void Initialize(int x, int z, BoardGridManager gridManager)
    {
        gridX = x;
        gridZ = z;
        SaveValidPosition(x, z, gridManager.GetWorldPosition(x, z));
        transform.position = lastValidWorldPos;

        UpdateHealthUI();
    }

    public void SaveValidPosition(int x, int z, Vector3 worldPos)
    {
        lastValidX = x;
        lastValidZ = z;
        lastValidWorldPos = worldPos;
        gridX = x;
        gridZ = z;
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

        // Step forward
        while (elapsed < halfDuration)
        {
            transform.position = Vector3.Lerp(startPos, lungePos, elapsed / halfDuration);
            elapsed += Time.deltaTime;
            yield return null;
        }

        elapsed = 0f;
        // Snap back
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

        if (flinchRoutine != null) StopCoroutine(flinchRoutine);
        flinchRoutine = StartCoroutine(FlinchRoutine());
    }

    public void Heal(int amount)
    {
        if (data != null)
        {
            currentHealth = Mathf.Min(data.maxHealth, currentHealth + amount);
            UpdateHealthUI();

            SpawnPopup(amount, isHeal: true);
        }
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
            unitRenderer.material.color = originalColor;
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

    public void ResetToPlanningPosition(BoardGridManager gridManager)
    {
        gameObject.SetActive(true);
        isMoving = false;

        if (data != null)
        {
            currentHealth = data.maxHealth;
        }

        // 1. Force the color back to normal if a flinch was interrupted
        if (flinchRoutine != null) StopCoroutine(flinchRoutine);
        if (unitRenderer != null && unitRenderer.material.HasProperty("_Color"))
        {
            unitRenderer.material.color = originalColor;
        }

        // 2. Clear any stranded text
        if (healthBar != null)
        {
            healthBar.ClearPopups();
        }

        transform.rotation = Quaternion.identity;
        gridX = lastValidX;
        gridZ = lastValidZ;
        transform.position = lastValidWorldPos;

        if (gridManager != null)
        {
            gridManager.RegisterUnit(this, gridX, gridZ);
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