using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

public enum LootType
{
    Gold,
    ItemComponent
}

[RequireComponent(typeof(SphereCollider))]
public class LootOrb : MonoBehaviour
{
    [Header("Loot Configuration")]
    public LootType lootType = LootType.ItemComponent;
    public ItemData rewardItem;
    public int rewardGold = 2;

    [Header("Visual Effects")]
    public Color itemOrbColor = new Color(0.2f, 0.7f, 1f, 1f); // Cyan / Blue
    public Color goldOrbColor = new Color(1f, 0.85f, 0.1f, 1f); // Shiny Gold
    public float collectionRadius = 1.25f;

    private Renderer orbRenderer;
    private SphereCollider col;
    private Camera mainCamera;
    private bool isCollected = false;
    private bool isLanded = false;
    private Vector3 targetGroundPos;

    void Awake()
    {
        orbRenderer = GetComponentInChildren<Renderer>();
        col = GetComponent<SphereCollider>();
        col.isTrigger = true;
        mainCamera = Camera.main;
    }

    /// <summary>
    /// Initializes and tosses the orb toward a ground position.
    /// </summary>
    public void Launch(Vector3 spawnPos, Vector3 landPos, LootType type, ItemData item = null, int gold = 0)
    {
        transform.position = spawnPos;
        targetGroundPos = landPos;
        lootType = type;
        rewardItem = item;
        rewardGold = gold;

        ApplyOrbColor();
        StartCoroutine(BounceArcRoutine(spawnPos, landPos));
    }

    private void ApplyOrbColor()
    {
        if (orbRenderer == null) return;

        Color chosenColor = (lootType == LootType.Gold) ? goldOrbColor : itemOrbColor;

        if (orbRenderer.material.HasProperty("_Color"))
        {
            orbRenderer.material.color = chosenColor;
        }

        if (orbRenderer.material.HasProperty("_EmissionColor"))
        {
            orbRenderer.material.EnableKeyword("_EMISSION");
            orbRenderer.material.SetColor("_EmissionColor", chosenColor * 0.75f);
        }
    }

    private IEnumerator BounceArcRoutine(Vector3 start, Vector3 end)
    {
        float duration = 0.65f;
        float elapsed = 0f;
        float arcHeight = 1.8f;

        while (elapsed < duration)
        {
            float t = elapsed / duration;
            Vector3 linear = Vector3.Lerp(start, end, t);
            float heightOffset = Mathf.Sin(t * Mathf.PI) * arcHeight;
            transform.position = new Vector3(linear.x, linear.y + heightOffset, linear.z);

            elapsed += Time.deltaTime;
            yield return null;
        }

        // Secondary small bounce
        elapsed = 0f;
        float bounceDuration = 0.25f;
        while (elapsed < bounceDuration)
        {
            float t = elapsed / bounceDuration;
            float bounceHeight = Mathf.Sin(t * Mathf.PI) * 0.4f;
            transform.position = new Vector3(end.x, end.y + bounceHeight, end.z);

            elapsed += Time.deltaTime;
            yield return null;
        }

        transform.position = end;
        isLanded = true;
    }

    void Update()
    {
        if (!isLanded || isCollected) return;
        if (mainCamera == null) mainCamera = Camera.main;

        // Gentle hover bobbing and rotation
        transform.Rotate(Vector3.up, 90f * Time.deltaTime, Space.World);
        float hoverOffset = Mathf.Sin(Time.time * 3f) * 0.08f;
        transform.position = new Vector3(targetGroundPos.x, targetGroundPos.y + hoverOffset, targetGroundPos.z);

        // 1. Click to Collect
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            Ray ray = mainCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
            if (col != null && col.Raycast(ray, out RaycastHit hit, 200f))
            {
                TryCollect();
                return;
            }
        }

        // 2. Proximity Collect: Check if any active player unit walked near
        Collider[] nearby = Physics.OverlapSphere(transform.position, collectionRadius);
        foreach (var c in nearby)
        {
            Unit u = c.GetComponentInParent<Unit>();
            if (u != null && u.isPlayerTeam && u.currentHealth > 0)
            {
                TryCollect();
                return;
            }
        }
    }

    public void TryCollect()
    {
        if (isCollected) return;

        if (lootType == LootType.ItemComponent)
        {
            if (rewardItem != null && ItemInventoryManager.Instance != null)
            {
                bool added = ItemInventoryManager.Instance.AddItem(rewardItem);
                if (added)
                {
                    Debug.Log($"<color=cyan>[Loot Orb]</color> Collected item: <b>{rewardItem.itemName}</b>!");
                    StartCoroutine(CollectAnimationRoutine());
                }
                else
                {
                    Debug.LogWarning("[Loot Orb] Cannot collect: Item Tray is full! Make room to pick up.");
                }
            }
        }
        else if (lootType == LootType.Gold)
        {
            if (EconomyManager.Instance != null)
            {
                EconomyManager.Instance.AddGold(rewardGold);
                Debug.Log($"<color=yellow>[Loot Orb]</color> Collected +{rewardGold} Gold!");
            }
            StartCoroutine(CollectAnimationRoutine());
        }
    }

    private IEnumerator CollectAnimationRoutine()
    {
        isCollected = true;
        col.enabled = false;

        float duration = 0.2f;
        float elapsed = 0f;
        Vector3 startScale = transform.localScale;

        while (elapsed < duration)
        {
            float t = elapsed / duration;
            transform.localScale = Vector3.Lerp(startScale, Vector3.zero, t);
            transform.position += Vector3.up * (2f * Time.deltaTime);
            elapsed += Time.deltaTime;
            yield return null;
        }

        Destroy(gameObject);
    }
}