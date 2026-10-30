using UnityEngine;

public class Unit : MonoBehaviour
{
    [Header("Unit Info Name")]
    public string unitName = "Friend";

    [Header("Grid Coordinates")]
    public int gridX;
    public int gridZ;

    // Track the last valid locked-in position
    [HideInInspector] public int lastValidX;
    [HideInInspector] public int lastValidZ;
    [HideInInspector] public Vector3 lastValidWorldPos;

    public void Initialize(int x, int z, BoardGridManager gridManager)
    {
        gridX = x;
        gridZ = z;
        
        SaveValidPosition(x, z, gridManager.GetWorldPosition(x, z));

        // Snap to the exact world position provided by the BoardGridManager
        transform.position = lastValidWorldPos;
    }

    public void SaveValidPosition(int x, int z, Vector3 worldPos)
    {
        lastValidX = x;
        lastValidZ = z;
        lastValidWorldPos = worldPos;
        gridX = x;
        gridZ = z;
    }
}