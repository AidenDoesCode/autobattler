using UnityEngine;
using UnityEngine.InputSystem;

public class UnitDragHandler : MonoBehaviour
{
    private Camera mainCamera;
    private BoardGridManager gridManager;
    private Unit unitScript;
    
    private bool isDragging = false;
    private float boardYLevel;
    
    private int currentHoverX = -1;
    private int currentHoverZ = -1;

    void Start()
    {
        mainCamera = Camera.main;
        gridManager = FindAnyObjectByType<BoardGridManager>();
        unitScript = GetComponent<Unit>();

        // Register initial position
        if (gridManager != null)
        {
            gridManager.RegisterUnit(unitScript, unitScript.gridX, unitScript.gridZ);
        }
    }

    void OnMouseDown()
    {
        if (GameManager.Instance != null && GameManager.Instance.currentState != GameManager.GameState.Planning)
        {
            return; // Block dragging during combat
        }

        isDragging = true;
        boardYLevel = transform.position.y;

        if (gridManager != null)
        {
            gridManager.ClearUnitPosition(unitScript.gridX, unitScript.gridZ);
        }
    }

    void Update()
    {
        if (isDragging)
        {
            if (Mouse.current != null)
            {
                Vector2 mouseScreenPos = Mouse.current.position.ReadValue();
                Ray ray = mainCamera.ScreenPointToRay(mouseScreenPos);
                Plane groundPlane = new Plane(Vector3.up, new Vector3(0, boardYLevel, 0));

                if (groundPlane.Raycast(ray, out float distance))
                {
                    Vector3 targetPoint = ray.GetPoint(distance);
                    transform.position = new Vector3(targetPoint.x, boardYLevel, targetPoint.z);

                    UpdateHoverTile();
                }
            }

            if (Mouse.current != null && !Mouse.current.leftButton.isPressed)
            {
                isDragging = false;
                
                if (gridManager != null)
                {
                    gridManager.ResetAllTileColors(Color.white);
                }

                SnapToClosestTile();
            }
        }
    }

    void UpdateHoverTile()
    {
        if (gridManager == null) return;

        int closestX = 0;
        int closestZ = 0;
        float minDistance = float.MaxValue;

        for (int x = 0; x < gridManager.width; x++)
        {
            for (int z = 0; z < gridManager.height; z++)
            {
                Vector3 tilePos = gridManager.GetWorldPosition(x, z);
                float dist = Vector3.Distance(new Vector3(transform.position.x, 0, transform.position.z), 
                                              new Vector3(tilePos.x, 0, tilePos.z));

                if (dist < minDistance)
                {
                    minDistance = dist;
                    closestX = x;
                    closestZ = z;
                }
            }
        }

        if (closestX != currentHoverX || closestZ != currentHoverZ)
        {
            gridManager.ResetAllTileColors(Color.white);

            // Rule 1: Must be in player's deployment zone (rows 0-1)
            // Rule 2: Cannot be occupied by another unit
            bool isInBounds = gridManager.IsInPlayerZone(closestX, closestZ);
            bool isOccupied = gridManager.IsTileOccupied(closestX, closestZ);

            Color highlightColor = (!isInBounds || isOccupied) ? Color.red : Color.green;

            gridManager.HighlightTile(closestX, closestZ, highlightColor);

            currentHoverX = closestX;
            currentHoverZ = closestZ;
        }
    }

    void SnapToClosestTile()
    {
        if (gridManager == null || unitScript == null) return;

        int closestX = 0;
        int closestZ = 0;
        float minDistance = float.MaxValue;

        for (int x = 0; x < gridManager.width; x++)
        {
            for (int z = 0; z < gridManager.height; z++)
            {
                Vector3 tilePos = gridManager.GetWorldPosition(x, z);
                float dist = Vector3.Distance(new Vector3(transform.position.x, 0, transform.position.z), 
                                              new Vector3(tilePos.x, 0, tilePos.z));

                if (dist < minDistance)
                {
                    minDistance = dist;
                    closestX = x;
                    closestZ = z;
                }
            }
        }

        // Validate both occupancy and player zone restrictions
        bool isInBounds = gridManager.IsInPlayerZone(closestX, closestZ);
        bool isOccupied = gridManager.IsTileOccupied(closestX, closestZ);

        if (!isInBounds || isOccupied)
        {
            // REJECT MOVE: Snap back to last valid position
            transform.position = unitScript.lastValidWorldPos;
            gridManager.RegisterUnit(unitScript, unitScript.lastValidX, unitScript.lastValidZ);
            Debug.Log("Invalid placement! Must stay in your zone and unoccupied.");
        }
        else
        {
            // ACCEPT MOVE
            Vector3 newPos = gridManager.GetWorldPosition(closestX, closestZ);
            transform.position = newPos;
            
            unitScript.SaveValidPosition(closestX, closestZ, newPos);
            gridManager.RegisterUnit(unitScript, closestX, closestZ);
        }
        
        currentHoverX = -1;
        currentHoverZ = -1;
    }
}