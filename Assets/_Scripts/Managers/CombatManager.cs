using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class CombatManager : MonoBehaviour
{
    public static CombatManager Instance { get; private set; }

    private BoardGridManager gridManager;
    private Dictionary<Unit, float> attackCooldowns = new Dictionary<Unit, float>();
    private bool inCombat = false;

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
        gridManager = UnityEngine.Object.FindAnyObjectByType<BoardGridManager>();
    }

    public void StartCombat()
    {
        if (inCombat) return;
        inCombat = true;
        attackCooldowns.Clear();

        List<Unit> playerUnits = GetUnitsByTeam(isPlayer: true);

        // Apply Synergy Buffs at round start
        if (SynergyManager.Instance != null)
        {
            SynergyManager.Instance.ApplyCombatSynergies(playerUnits);
        }

        StartCoroutine(CombatLoopRoutine());
    }

    public void EndCombat()
    {
        inCombat = false;
        StopAllCoroutines();

        // Stop recurring synergy coroutines
        if (SynergyManager.Instance != null)
        {
            SynergyManager.Instance.StopCombatSynergies();
        }

        attackCooldowns.Clear();
    }

    private IEnumerator CombatLoopRoutine()
    {
        while (inCombat)
        {
            Unit[] allUnits = UnityEngine.Object.FindObjectsByType<Unit>();
            List<Unit> activeUnits = new List<Unit>();

            foreach (var u in allUnits)
            {
                if (u != null && u.gameObject.activeInHierarchy && u.currentHealth > 0 && !u.isOnBench)
                {
                    activeUnits.Add(u);
                }
            }

            // Check win/loss condition
            bool hasPlayer = activeUnits.Exists(u => u.isPlayerTeam);
            bool hasEnemy = activeUnits.Exists(u => !u.isPlayerTeam);

            if (!hasPlayer || !hasEnemy)
            {
                EndCombat();
                GameManager.Instance?.ReturnToPlanning();
                yield break;
            }

            foreach (var unit in activeUnits)
            {
                if (unit == null || unit.currentHealth <= 0 || unit.isMoving) continue;

                if (!attackCooldowns.ContainsKey(unit))
                {
                    attackCooldowns[unit] = 0f;
                }

                attackCooldowns[unit] -= Time.deltaTime;

                if (attackCooldowns[unit] <= 0f)
                {
                    Unit target = unit.FindTarget(activeUnits);
                    if (target != null)
                    {
                        float dist = Vector3.Distance(unit.transform.position, target.transform.position);
                        float attackRange = unit.data != null ? unit.data.attackRange : 1.5f;

                        if (dist <= attackRange)
                        {
                            unit.PlayAttackAnimation(target.transform.position);
                            PerformCombatAction(unit, target);
                        }
                    }
                }
            }

            yield return null;
        }
    }

    private void PerformCombatAction(Unit attacker, Unit target)
    {
        string attackerName = attacker.data.unitName;
        string targetName = target.data.unitName;
        int actionValue = attacker.GetEffectiveAttackDamage();

        if (attacker.data.archetype == UnitArchetype.Support)
        {
            target.Heal(actionValue);
            Debug.Log($"[HEAL] {attackerName} healed {targetName} for {actionValue} HP!");
        }
        else
        {
            // Apply Melee flat damage reduction
            int reduction = SynergyManager.Instance != null ? SynergyManager.Instance.GetMeleeDamageReduction(target) : 0;
            int finalDamage = Mathf.Max(1, actionValue - reduction);

            target.TakeDamage(finalDamage);
            Debug.Log($"[ATTACK] {attackerName} attacked {targetName} for {finalDamage} DMG! (Blocked: {reduction})");

            if (target.currentHealth <= 0)
            {
                Debug.Log($"[DEFEATED] {targetName} was eliminated!");
                if (attackCooldowns.ContainsKey(target)) attackCooldowns.Remove(target);
                gridManager.ClearUnitPosition(target.gridX, target.gridZ);
                target.gameObject.SetActive(false);
            }
        }

        // Apply Ranged Synergy Attack Speed to cooldown
        float baseCooldown = 1f / Mathf.Max(attacker.data != null ? attacker.data.attackSpeed : 1f, 0.1f);
        float modifiedCooldown = SynergyManager.Instance != null 
            ? SynergyManager.Instance.GetRangedAttackInterval(attacker, baseCooldown) 
            : baseCooldown;

        attackCooldowns[attacker] = modifiedCooldown;
    }

    public List<Unit> GetUnitsByTeam(bool isPlayer)
    {
        Unit[] allUnits = UnityEngine.Object.FindObjectsByType<Unit>();
        List<Unit> list = new List<Unit>();
        foreach (var u in allUnits)
        {
            if (u != null && u.gameObject.activeInHierarchy && u.isPlayerTeam == isPlayer && !u.isOnBench)
            {
                list.Add(u);
            }
        }
        return list;
    }
}