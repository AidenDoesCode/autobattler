using UnityEngine;

public class BoardGridManager : MonoBehaviour
{
    [Header("Grid Dimensions")]
    public int width = 6;   // Columns (X axis)
    public int height = 7;  // Rows / Depth (Z axis)
    public float tileSize = 1f;

    [Header("Visuals")]
    public GameObject tilePrefab; // Assign your flattened cube prefab here

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
        
        // Get half the height of the cube so it rests flat on the board surface
        float tileHalfHeight = tilePrefab != null ? tilePrefab.transform.localScale.y / 2f : 0.05f;

        for (int x = 0; x < width; x++)
        {
            for (int z = 0; z < height; z++)
            {
                // Calculate position relative to the board center
                Vector3 localPos = new Vector3((x * tileSize) - xOffset, tileHalfHeight, (z * tileSize) - zOffset);
                Vector3 worldPos = transform.position + localPos;

                if (tilePrefab != null)
                {
                    // Instantiate and parent the visual tile
                    GameObject tile = Instantiate(tilePrefab, worldPos, Quaternion.identity, transform);
                    tile.name = $"Tile_{x}_{z}";
                    
                    // If you didn't scale the prefab manually, you can enforce it here:
                    // tile.transform.localScale = new Vector3(tileSize * 0.95f, 0.05f, tileSize * 0.95f);

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
        float tileHeight = tilePrefab != null ? tilePrefab.transform.localScale.y : 0.05f;

        return new Vector3(
            transform.position.x + (x * tileSize) - xOffset,
            transform.position.y + tileHeight, // Spawns units right on top of the tile surface
            transform.position.z + (z * tileSize) - zOffset
        );
    }
}