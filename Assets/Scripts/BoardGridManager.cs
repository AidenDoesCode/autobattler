using UnityEngine;

public class BoardGridManager : MonoBehaviour
{
    [Header("Grid Dimensions")]
    public int width = 6;   // Columns (X axis)
    public int height = 7;  // Rows / Depth (Z axis)
    public float tileSize = 1f;

    [Header("Visuals")]
    public GameObject tilePrefab; // Use a Plane or flat Cube so it lays flat horizontally

    private GameObject[,] gridTiles;

    void Start()
    {
        GenerateGrid();
    }

    void GenerateGrid()
    {
        gridTiles = new GameObject[width, height];

        // Center the grid relative to the BoardManager's position
        float xOffset = (width * tileSize) / 2f - (tileSize / 2f);
        float zOffset = (height * tileSize) / 2f - (tileSize / 2f);

        for (int x = 0; x < width; x++)
        {
            for (int z = 0; z < height; z++)
            {
                // Calculate position relative to the board center
                Vector3 localPos = new Vector3((x * tileSize) - xOffset, 0.01f, (z * tileSize) - zOffset);
                Vector3 worldPos = transform.position + localPos;

                if (tilePrefab != null)
                {
                    // Instantiate and parent the visual tile
                    GameObject tile = Instantiate(tilePrefab, worldPos, Quaternion.identity, transform);
                    tile.name = $"Tile_{x}_{z}";
                    
                    // Scale it to match tile size (leave a tiny gap for grid lines)
                    tile.transform.localScale = new Vector3(tileSize * 0.095f, 1f, tileSize * 0.095f); // Note: 0.095 is used if using a default Unity Plane (which is 10x10 by default). If your prefab is scaled to 1x1, use 0.95f instead.

                    gridTiles[x, z] = tile;
                }
            }
        }
    }

    // Converts a grid coordinate (e.g., [2, 3]) into an exact 3D world position for units
    public Vector3 GetWorldPosition(int x, int z)
    {
        float xOffset = (width * tileSize) / 2f - (tileSize / 2f);
        float zOffset = (height * tileSize) / 2f - (tileSize / 2f);

        return new Vector3(
            transform.position.x + (x * tileSize) - xOffset,
            transform.position.y + 0.1f, // Slightly above the board surface
            transform.position.z + (z * tileSize) - zOffset
        );
    }
}