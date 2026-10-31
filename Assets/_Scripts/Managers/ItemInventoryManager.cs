using UnityEngine;
using UnityEngine.InputSystem;
using System;
using System.Collections.Generic;

public class ItemInventoryManager : MonoBehaviour
{
    public static ItemInventoryManager Instance { get; private set; }

    [Header("Database & Settings")]
    public ItemDatabase itemDatabase;
    public int maxInventorySlots = 8;
    public ItemData[] inventorySlots;

    public event Action OnInventoryChanged;

    private Camera mainCamera;
    private BoardGridManager gridManager;

    public int DraggingSlotIndex { get; private set; } = -1;
    public bool IsDraggingItem => DraggingSlotIndex >= 0;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        inventorySlots = new ItemData[maxInventorySlots];
    }

    void Start()
    {
        mainCamera = Camera.main;
        gridManager = UnityEngine.Object.FindAnyObjectByType<BoardGridManager>();

        // Auto-seed sample items if available
        if (itemDatabase != null && itemDatabase.allItems.Count >= 2)
        {
            AddItem(itemDatabase.allItems[0]); // Sword
            AddItem(itemDatabase.allItems[1]); // Vest
        }
    }

    void Update()
    {
        if (!IsDraggingItem) return;

        // Fallback release detection for Unity Input System
        if (Mouse.current != null && !Mouse.current.leftButton.isPressed)
        {
            FinishItemDrag(Mouse.current.position.ReadValue());
        }
    }

    public bool AddItem(ItemData item)
    {
        if (item == null) return false;

        for (int i = 0; i < maxInventorySlots; i++)
        {
            if (inventorySlots[i] == null)
            {
                inventorySlots[i] = item;
                OnInventoryChanged?.Invoke();
                return true;
            }
        }

        Debug.LogWarning("[Inventory] Item bench is full!");
        return false;
    }

    public void StartDraggingItem(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= maxInventorySlots) return;
        if (inventorySlots[slotIndex] == null) return;

        DraggingSlotIndex = slotIndex;
        Debug.Log($"<color=cyan>[Items]</color> Started dragging: {inventorySlots[slotIndex].itemName}");
    }

    public void CancelItemDrag()
    {
        DraggingSlotIndex = -1;
        OnInventoryChanged?.Invoke();
    }

    public void FinishItemDrag(Vector2 screenPosition)
    {
        if (!IsDraggingItem) return;

        int fromSlot = DraggingSlotIndex;
        ItemData itemToEquip = inventorySlots[fromSlot];
        
        // Immediately mark drag complete to avoid double-execution
        DraggingSlotIndex = -1;

        if (mainCamera == null) mainCamera = Camera.main;
        if (gridManager == null) gridManager = UnityEngine.Object.FindAnyObjectByType<BoardGridManager>();

        if (mainCamera == null || itemToEquip == null)
        {
            OnInventoryChanged?.Invoke();
            return;
        }

        Vector2 actualScreenPos = Mouse.current != null ? Mouse.current.position.ReadValue() : screenPosition;
        Ray ray = mainCamera.ScreenPointToRay(actualScreenPos);

        Unit targetUnit = null;

        // 1. Direct unit collider raycast check (depth-sorted)
        RaycastHit[] hits = Physics.RaycastAll(ray, 300f);
        Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (var hit in hits)
        {
            Unit u = hit.collider.GetComponentInParent<Unit>();
            // Strict condition: Must be player team, alive, and NOT on the bench
            if (u != null && u.isPlayerTeam && u.currentHealth > 0 && !u.isOnBench)
            {
                targetUnit = u;
                break;
            }
        }

        // 2. Fallback: Check if the ray hit the ground plane on an active board tile
        if (targetUnit == null && gridManager != null)
        {
            Plane groundPlane = new Plane(Vector3.up, Vector3.zero);
            if (groundPlane.Raycast(ray, out float enterDist))
            {
                Vector3 worldHit = ray.GetPoint(enterDist);
                float minTileDist = 1.2f; // Snapping radius around tile centers

                for (int x = 0; x < gridManager.width; x++)
                {
                    for (int z = 0; z <= gridManager.maxPlayerRow; z++)
                    {
                        Vector3 tileCenter = gridManager.GetWorldPosition(x, z);
                        float dist = Vector2.Distance(new Vector2(worldHit.x, worldHit.z), new Vector2(tileCenter.x, tileCenter.z));

                        if (dist < minTileDist)
                        {
                            Unit occupant = gridManager.GetUnitAt(x, z);
                            if (occupant != null && occupant.isPlayerTeam && occupant.currentHealth > 0 && !occupant.isOnBench)
                            {
                                targetUnit = occupant;
                                minTileDist = dist;
                            }
                        }
                    }
                }
            }
        }

        // 3. Equip or return to tray
        if (targetUnit != null && !targetUnit.isOnBench)
        {
            bool equipped = targetUnit.TryEquipOrFuseItem(itemToEquip, itemDatabase);
            if (equipped)
            {
                inventorySlots[fromSlot] = null;
                OnInventoryChanged?.Invoke();
                return;
            }
        }

        Debug.Log("[Items] No valid board unit targeted (or target was on bench); item returned to tray.");
        OnInventoryChanged?.Invoke();
    }
}