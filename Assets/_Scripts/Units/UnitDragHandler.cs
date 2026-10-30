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

    // Track hovered location
    private bool hoverIsBench = false;
    private int hoverBoardX = -1;
    private int hoverBoardZ = -1;
    private int hoverBenchIndex = -1;
    private bool isHoveringSellZone = false;

    [Header("Sell Settings")]
    [Tooltip("Z-distance threshold behind the bench where releasing sells the unit")]
    public float sellZOffset = 1.2f;

    void Awake()
    {
        unit = GetComponent<Unit>();
        unitCollider = GetComponent<Collider>();
    }

    void Start()
    {
        mainCamera = Camera.main;
        gridManager = UnityEngine.Object.FindAnyObjectByType<BoardGridManager>();
        gameManager = UnityEngine.Object.FindAnyObjectByType<GameManager>();
    }

    void Update()
    {
        if (Mouse.current == null) return;
        if (mainCamera == null) mainCamera = Camera.main;
        if (gridManager == null) gridManager = UnityEngine.Object.FindAnyObjectByType<BoardGridManager>();
        if (gameManager == null) gameManager = UnityEngine.Object.FindAnyObjectByType<GameManager>();

        // 1. Pick up unit
        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            TryStartDrag();
        }

        // 2. Active drag & hotkey listener
        if (isDragging)
        {
            // Hotkey 'E': Sell while dragging
            if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
            {
                SellCurrentUnit();
                return;
            }

            Vector2 mousePos = Mouse.current.position.ReadValue();
            Ray ray = mainCamera.ScreenPointToRay(mousePos);
            Plane groundPlane = new Plane(Vector3.up, new Vector3(0, dragYLevel, 0));

            if (groundPlane.Raycast(ray, out float distance))
            {
                Vector3 targetPoint = ray.GetPoint(distance);
                transform.position = new Vector3(targetPoint.x, dragYLevel, targetPoint.z);

                UpdateHoverTile();
            }

            // 3. Drop / Release
            if (Mouse.current.leftButton.wasReleasedThisFrame || !Mouse.current.leftButton.isPressed)
            {
                isDragging = false;
                if (gridManager != null)
                {
                    gridManager.ResetAllTileColors(Color.white);
                }

                if (isHoveringSellZone)
                {
                    SellCurrentUnit();
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
        // Only allow dragging in Planning phase and for living player units
        if (gameManager != null && gameManager.currentState != GameManager.GameState.Planning) return;
        if (unit != null && (!unit.isPlayerTeam || unit.currentHealth <= 0)) return;

        Vector2 mousePos = Mouse.current.position.ReadValue();
        Ray ray = mainCamera.ScreenPointToRay(mousePos);

        if (unitCollider != null && unitCollider.Raycast(ray, out RaycastHit hit, 200f))
        {
            isDragging = true;
            dragYLevel = transform.position.y + 0.35f;
        }
    }

    void UpdateHoverTile()
    {
        if (gridManager == null) return;

        Vector3 currentFlatPos = new Vector3(transform.position.x, 0, transform.position.z);
        Vector3 bench0Pos = gridManager.GetBenchWorldPosition(0);

        // Check if dragged backwards past the bench row (into the sell zone)
        if (currentFlatPos.z < bench0Pos.z - sellZOffset)
        {
            if (!isHoveringSellZone)
            {
                gridManager.ResetAllTileColors(Color.white);
                isHoveringSellZone = true;
            }
            return;
        }
        else
        {
            isHoveringSellZone = false;
        }

        float minDistance = float.MaxValue;
        bool nearestIsBench = false;
        int nearestBX = -1, nearestBZ = -1, nearestBenchIdx = -1;

        // Check player deployable board tiles
        for (int x = 0; x < gridManager.width; x++)
        {
            for (int z = 0; z <= gridManager.maxPlayerRow; z++)
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

        // Check bench tiles
        for (int i = 0; i < gridManager.benchSlotCount; i++)
        {
            Vector3 benchPos = gridManager.GetBenchWorldPosition(i);
            float dist = Vector3.Distance(currentFlatPos, new Vector3(benchPos.x, 0, benchPos.z)) * 0.85f;
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
                gridManager.HighlightBenchTile(nearestBenchIdx, Color.cyan);
            }
            else
            {
                bool willExceedCap = unit.isOnBench && gridManager.GetUnitAt(nearestBX, nearestBZ) == null &&
                                     gridManager.GetCurrentBoardUnitCount() >= gridManager.maxBoardUnits;

                gridManager.HighlightTile(nearestBX, nearestBZ, willExceedCap ? Color.red : Color.green);
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
        isHoveringSellZone = false;
    }

    void HandleBoardDrop(int targetX, int targetZ)
    {
        if (!gridManager.IsInPlayerZone(targetX, targetZ))
        {
            SnapBack();
            return;
        }

        Unit occupant = gridManager.GetUnitAt(targetX, targetZ);

        if (unit.isOnBench && occupant == null)
        {
            if (gridManager.GetCurrentBoardUnitCount() >= gridManager.maxBoardUnits)
            {
                Debug.LogWarning($"[Board Cap] Cannot deploy unit! Maximum board limit reached.");
                SnapBack();
                return;
            }

            gridManager.ClearBenchPosition(unit.benchSlotIndex);
            PlaceUnitOnBoard(unit, targetX, targetZ);
            return;
        }

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
            return;
        }

        if (!unit.isOnBench)
        {
            gridManager.ClearUnitPosition(unit.gridX, unit.gridZ);
            PlaceUnitOnBoard(unit, targetX, targetZ);
        }
    }

    void HandleBenchDrop(int targetBenchIndex)
    {
        Unit occupant = gridManager.GetBenchUnitAt(targetBenchIndex);

        if (occupant != null && occupant != unit)
        {
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
            return;
        }

        if (!unit.isOnBench)
        {
            gridManager.ClearUnitPosition(unit.gridX, unit.gridZ);
        }
        else
        {
            gridManager.ClearBenchPosition(unit.benchSlotIndex);
        }

        PlaceUnitOnBench(unit, targetBenchIndex);
    }

    private void SellCurrentUnit()
    {
        isDragging = false;

        if (gridManager != null)
        {
            gridManager.ResetAllTileColors(Color.white);
            gridManager.UnregisterUnit(unit);
        }

        if (EconomyManager.Instance != null && unit != null)
        {
            EconomyManager.Instance.SellUnit(unit.data);
        }

        Destroy(gameObject);
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