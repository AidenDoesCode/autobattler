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
        isDragging = true;
        boardYLevel = transform.position.y;

        // Clear old position slot when picking up
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

            // Check if another unit is already on this tile
            bool isOccupied = gridManager.IsTileOccupied(closestX, closestZ);
            Color highlightColor = isOccupied ? Color.red : Color.green;

            gridManager.HighlightTile(closestX, closestZ, highlightColor);

            currentHoverX = closestX;
            currentHoverZ = closestZ;
        }
    }

    void SnapToClosestTile()
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

        // Optional rule: If space is occupied, you could snap back to original position, 
        // but for now it will update coordinates and register itself
        transform.position = gridManager.GetWorldPosition(closestX, closestZ);
        
        if (unitScript != null)
        {
            unitScript.gridX = closestX;
            unitScript.gridZ = closestZ;
            gridManager.RegisterUnit(unitScript, closestX, closestZ);
        }
        
        currentHoverX = -1;
        currentHoverZ = -1;
    }
}