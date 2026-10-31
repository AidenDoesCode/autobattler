using UnityEngine;
using UnityEngine.InputSystem;
using System;

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

        // Pure Grid-Based Detection (Ignores 3D Colliders to prevent perspective mismatches)
        Plane groundPlane = new Plane(Vector3.up, Vector3.zero);
        if (groundPlane.Raycast(ray, out float enterDist))
        {
            Vector3 worldHit = ray.GetPoint(enterDist);
            
            float closestDist = float.MaxValue;
            int closestX = -1;
            int closestZ = -1;

            // Find the absolute closest tile center to the drop point
            for (int x = 0; x < gridManager.width; x++)
            {
                // Only loop through player rows (items cannot be dropped on enemies)
                for (int z = 0; z <= gridManager.maxPlayerRow; z++) 
                {
                    Vector3 tileCenter = gridManager.GetWorldPosition(x, z);
                    float dist = Vector2.Distance(new Vector2(worldHit.x, worldHit.z), new Vector2(tileCenter.x, tileCenter.z));

                    if (dist < closestDist)
                    {
                        closestDist = dist;
                        closestX = x;
                        closestZ = z;
                    }
                }
            }

            // Check if dropped directly over a specific tile's boundaries
            if (closestDist <= (gridManager.tileSize * 0.65f) && closestX != -1 && closestZ != -1)
            {
                Unit occupant = gridManager.GetUnitAt(closestX, closestZ);
                
                // Verify the occupant is a player unit, alive, and NOT on the bench
                if (occupant != null && occupant.isPlayerTeam && occupant.currentHealth > 0 && !occupant.isOnBench)
                {
                    targetUnit = occupant;
                }
            }
        }

        // Equip the item or return it to the tray
        if (targetUnit != null)
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