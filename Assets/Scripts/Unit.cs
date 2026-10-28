using UnityEngine;

public class Unit : MonoBehaviour
{
    [Header("Unit Info Name")]
    public string unitName = "Friend";

    [Header("Grid Coordinates")]
    public int gridX;
    public int gridZ;

    // Function to update the unit's position visually based on grid coordinates
    public void Initialize(int x, int z, BoardGridManager gridManager)
    {
        gridX = x;
        gridZ = z;

        // Snap to the exact world position provided by the BoardGridManager
        transform.position = gridManager.GetWorldPosition(gridX, gridZ);
    }
}