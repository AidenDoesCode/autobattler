using UnityEngine;
using UnityEngine.InputSystem;

public class UnitDragHandler : MonoBehaviour
{
    private Camera mainCamera;
    private BoardGridManager gridManager;
    private GameManager gameManager;
    private Unit unit;
    private Collider unitCollider;

    private bool isDragging = false;
    private float dragYLevel;
    private Vector2 clickStartPosition;
    private const float DragThresholdPixels = 8f;

    // Track hovered location
    private bool hoverIsBench = false;
    private int hoverBoardX = -1;
    private int hoverBoardZ = -1;
    private int hoverBenchIndex = -1;

    void Awake()
    {
        unit = GetComponent<Unit>();
        unitCollider = GetComponent<Collider>();
    }

    void Start()
    {
        mainCamera = Camera.main;
        gridManager = Object.FindAnyObjectByType<BoardGridManager>();
        gameManager = Object.FindAnyObjectByType<GameManager>();
    }

    void Update()
    {
        if (Mouse.current == null) return;
        if (mainCamera == null) mainCamera = Camera.main;
        if (gridManager == null) gridManager = Object.FindAnyObjectByType<BoardGridManager>();
        if (gameManager == null) gameManager = Object.FindAnyObjectByType<GameManager>();

        // 1. Right-Click on Unit to inspect directly
        if (Mouse.current.rightButton.wasPressedThisFrame)
        {
            Vector2 mousePos = Mouse.current.position.ReadValue();
            Ray ray = mainCamera.ScreenPointToRay(mousePos);

            if (unitCollider != null && unitCollider.Raycast(ray, out RaycastHit hit, 200f))
            {
                if (ShopUIController.Instance != null && unit != null)
                {
                    ShopUIController.Instance.InspectUnit(unit);
                }
            }
        }

        // 2. Left-Click Press
        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            TryStartDrag();
        }

        // 3. Active Dragging
        if (isDragging)
        {
            Vector2 mousePos = Mouse.current.position.ReadValue();
            Ray ray = mainCamera.ScreenPointToRay(mousePos);
            Plane groundPlane = new Plane(Vector3.up, new Vector3(0, dragYLevel, 0));

            if (groundPlane.Raycast(ray, out float distance))
            {
                Vector3 targetPoint = ray.GetPoint(distance);
                transform.position = new Vector3(targetPoint.x, dragYLevel, targetPoint.z);

                UpdateHoverTile();
            }

            // 4. Drop / Release or Quick-Click Inspect
            if (Mouse.current.leftButton.wasReleasedThisFrame || !Mouse.current.leftButton.isPressed)
            {
                isDragging = false;
                if (gridManager != null)
                {
                    gridManager.ResetAllTileColors(Color.white);
                }

                float dragDistance = Vector2.Distance(mousePos, clickStartPosition);

                if (dragDistance < DragThresholdPixels)
                {
                    SnapBack();
                    if (ShopUIController.Instance != null && unit != null)
                    {
                        ShopUIController.Instance.InspectUnit(unit);
                    }
                }
                else
                {
                    ExecuteDrop();
                }
            }
        }
    }

    private void TryStartDrag()
    {
        if (gameManager != null && gameManager.currentState != GameManager.GameState.Planning) return;
        if (unit != null && (!unit.isPlayerTeam || unit.currentHealth <= 0)) return;

        Vector2 mousePos = Mouse.current.position.ReadValue();
        Ray ray = mainCamera.ScreenPointToRay(mousePos);

        if (unitCollider != null && unitCollider.Raycast(ray, out RaycastHit hit, 200f))
        {
            isDragging = true;
            clickStartPosition = mousePos;
            dragYLevel = transform.position.y + 0.35f;

            // Clear temporary highlight state
            hoverBoardX = -1;
            hoverBoardZ = -1;
            hoverBenchIndex = -1;
        }
    }

    void UpdateHoverTile()
    {
        if (gridManager == null) return;

        float minDistance = float.MaxValue;
        bool nearestIsBench = false;
        int nearestBX = -1, nearestBZ = -1, nearestBenchIdx = -1;

        Vector3 currentFlatPos = new Vector3(transform.position.x, 0, transform.position.z);

        // Scan board tiles
        for (int x = 0; x < gridManager.width; x++)
        {
            for (int z = 0; z < gridManager.height; z++)
            {
                Vector3 tilePos = gridManager.GetWorldPosition(x, z);
                float dist = Vector3.Distance(currentFlatPos, new Vector3(tilePos.x, 0, tilePos.z));
                if (dist < minDistance)
                {
                    minDistance = dist;
                    nearestIsBench = false;
                    nearestBX = x;
                    nearestBZ = z;
                }
            }
        }

        // Scan bench tiles
        for (int i = 0; i < gridManager.benchSlotCount; i++)
        {
            Vector3 benchPos = gridManager.GetBenchWorldPosition(i);
            float dist = Vector3.Distance(currentFlatPos, new Vector3(benchPos.x, 0, benchPos.z));
            if (dist < minDistance)
            {
                minDistance = dist;
                nearestIsBench = true;
                nearestBenchIdx = i;
            }
        }

        if (nearestIsBench != hoverIsBench || nearestBX != hoverBoardX || nearestBZ != hoverBoardZ || nearestBenchIdx != hoverBenchIndex)
        {
            gridManager.ResetAllTileColors(Color.white);

            if (nearestIsBench)
            {
                // Bench slot hover
                Unit benchOccupant = gridManager.GetBenchUnitAt(nearestBenchIdx);
                bool canDropOnBench = (benchOccupant == null || benchOccupant == unit || unit.isOnBench || !unit.isOnBench);
                gridManager.HighlightBenchTile(nearestBenchIdx, canDropOnBench ? Color.cyan : Color.red);
            }
            else
            {
                // Board tile hover
                bool inPlayerZone = gridManager.IsInPlayerZone(nearestBX, nearestBZ);
                Unit occupant = gridManager.GetUnitAt(nearestBX, nearestBZ);
                bool isOccupiedByOther = (occupant != null && occupant != unit);

                bool willExceedCap = unit.isOnBench && occupant == null &&
                                     gridManager.GetCurrentBoardUnitCount() >= gridManager.GetMaxBoardUnits();

                // Turn RED if out of player zone, tile is occupied by another unit, or moving to an empty tile exceeds cap
                bool isInvalid = !inPlayerZone || isOccupiedByOther || willExceedCap;

                gridManager.HighlightTile(nearestBX, nearestBZ, isInvalid ? Color.red : Color.green);
            }

            hoverIsBench = nearestIsBench;
            hoverBoardX = nearestBX;
            hoverBoardZ = nearestBZ;
            hoverBenchIndex = nearestBenchIdx;
        }
    }

    void ExecuteDrop()
    {
        if (gridManager == null || unit == null) return;

        if (hoverIsBench)
        {
            HandleBenchDrop(hoverBenchIndex);
        }
        else
        {
            HandleBoardDrop(hoverBoardX, hoverBoardZ);
        }

        hoverIsBench = false;
        hoverBoardX = -1;
        hoverBoardZ = -1;
        hoverBenchIndex = -1;
    }

    void HandleBoardDrop(int targetX, int targetZ)
    {
        if (!gridManager.IsInPlayerZone(targetX, targetZ))
        {
            SnapBack();
            return;
        }

        Unit occupant = gridManager.GetUnitAt(targetX, targetZ);

        // Move to empty board tile
        if (occupant == null)
        {
            if (unit.isOnBench && gridManager.GetCurrentBoardUnitCount() >= gridManager.GetMaxBoardUnits())
            {
                Debug.LogWarning("[Board Cap] Board is full!");
                SnapBack();
                return;
            }

            if (unit.isOnBench)
            {
                gridManager.ClearBenchPosition(unit.benchSlotIndex);
            }
            else
            {
                gridManager.ClearUnitPosition(unit.gridX, unit.gridZ);
            }

            PlaceUnitOnBoard(unit, targetX, targetZ);
            return;
        }

        // Drop on same spot
        if (occupant == unit)
        {
            SnapBack();
            return;
        }

        // Swap with occupant
        if (occupant != null && occupant != unit)
        {
            if (unit.isOnBench)
            {
                int oldBenchSlot = unit.benchSlotIndex;
                gridManager.ClearBenchPosition(oldBenchSlot);
                gridManager.ClearUnitPosition(targetX, targetZ);

                PlaceUnitOnBench(occupant, oldBenchSlot);
                PlaceUnitOnBoard(unit, targetX, targetZ);
            }
            else
            {
                int oldX = unit.gridX;
                int oldZ = unit.gridZ;
                gridManager.ClearUnitPosition(oldX, oldZ);
                gridManager.ClearUnitPosition(targetX, targetZ);

                PlaceUnitOnBoard(occupant, oldX, oldZ);
                PlaceUnitOnBoard(unit, targetX, targetZ);
            }
        }
    }

    void HandleBenchDrop(int targetBenchIndex)
    {
        if (targetBenchIndex < 0 || targetBenchIndex >= gridManager.benchSlotCount)
        {
            SnapBack();
            return;
        }

        Unit occupant = gridManager.GetBenchUnitAt(targetBenchIndex);

        if (occupant == null)
        {
            if (unit.isOnBench)
            {
                gridManager.ClearBenchPosition(unit.benchSlotIndex);
            }
            else
            {
                gridManager.ClearUnitPosition(unit.gridX, unit.gridZ);
            }

            PlaceUnitOnBench(unit, targetBenchIndex);
            return;
        }

        if (occupant == unit)
        {
            SnapBack();
            return;
        }

        // Swap
        if (unit.isOnBench)
        {
            int oldBenchIndex = unit.benchSlotIndex;
            gridManager.ClearBenchPosition(oldBenchIndex);
            gridManager.ClearBenchPosition(targetBenchIndex);

            PlaceUnitOnBench(occupant, oldBenchIndex);
            PlaceUnitOnBench(unit, targetBenchIndex);
        }
        else
        {
            int oldX = unit.gridX;
            int oldZ = unit.gridZ;
            gridManager.ClearUnitPosition(oldX, oldZ);
            gridManager.ClearBenchPosition(targetBenchIndex);

            PlaceUnitOnBoard(occupant, oldX, oldZ);
            PlaceUnitOnBench(unit, targetBenchIndex);
        }
    }

    void PlaceUnitOnBoard(Unit targetUnit, int x, int z)
    {
        Vector3 worldPos = gridManager.GetWorldPosition(x, z);
        targetUnit.transform.position = worldPos;
        targetUnit.SaveValidPosition(x, z, worldPos, onBench: false, benchIndex: -1);
        gridManager.RegisterUnit(targetUnit, x, z);
    }

    void PlaceUnitOnBench(Unit targetUnit, int benchIndex)
    {
        Vector3 worldPos = gridManager.GetBenchWorldPosition(benchIndex);
        targetUnit.transform.position = worldPos;
        targetUnit.SaveValidPosition(-1, -1, worldPos, onBench: true, benchIndex: benchIndex);
        gridManager.RegisterBenchUnit(targetUnit, benchIndex);
    }

    void SnapBack()
    {
        transform.position = unit.lastValidWorldPos;
    }
}