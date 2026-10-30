using UnityEngine;

public class BoardGridManager : MonoBehaviour
{
    [Header("Grid Dimensions")]
    public int width = 6;   // Columns (X axis)
    public int height = 7;  // Rows / Depth (Z axis)
    public float tileSize = 1f;

    [Header("Visuals")]
    public GameObject tilePrefab; //drag tile prefab to spawn that tile

    [Header("Grid Tracking")]
    private Unit[,] unitGrid; // Tracks which unit is on which tile
    private GameObject[,] gridTiles;


    [Header("Units")]
    public GameObject unitPrefab; // Drag your character prefab here in the Inspector

    [Header("Team Setup Limits")]
    public int playerMaxRow = 1; // Rows 0 and 1 belong to the player during planning
    public int enemyMinRow = 5;  // Rows 5 and 6 belong to the enemy


    void Start()
    {
        GenerateGrid();
        
        // Spawn Player Friend units on the bottom rows (e.g., Row 0)
        SpawnUnitAt(1, 0);
        SpawnUnitAt(3, 0);

        // Spawn Enemy units on the top rows (e.g., Row 6)
        SpawnUnitAt(1, 6);
        SpawnUnitAt(3, 6);
    }


    // Call this method to spawn and place a unit at a specific grid coordinate
    public GameObject SpawnUnitAt(int x, int z)
    {
        if (unitPrefab == null)
        {
            Debug.LogWarning("No Unit Prefab assigned to BoardGridManager!");
            return null;
        }

        // Validate that coordinates are within your 6x7 bounds
        if (x < 0 || x >= width || z < 0 || z >= height)
        {
            Debug.LogError($"Coordinates ({x}, {z}) are outside the grid bounds!");
            return null;
        }

        // Get the exact world position for this tile
        Vector3 spawnPos = GetWorldPosition(x, z);

        // Instantiate the character unit
        GameObject newUnitObj = Instantiate(unitPrefab, spawnPos, Quaternion.identity);
        
        // Initialize its grid tracking data
        Unit unitScript = newUnitObj.GetComponent<Unit>();
        if (unitScript != null)
        {
            unitScript.Initialize(x, z, this);
        }

        return newUnitObj;
    }
    void GenerateGrid()
    {
        //initialize grids
        unitGrid = new Unit[width, height];
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

    // Highlights a specific tile with a given color
    public void HighlightTile(int x, int z, Color color)
    {
        if (gridTiles == null || x < 0 || x >= width || z < 0 || z >= height) return;
        
        GameObject tile = gridTiles[x, z];
        if (tile != null)
        {
            Renderer rend = tile.GetComponent<Renderer>();
            if (rend != null)
            {
                rend.material.color = color;
            }
        }
    }

    // Resets all tiles back to their default color
    public void ResetAllTileColors(Color defaultColor)
    {
        if (gridTiles == null) return;

        for (int x = 0; x < width; x++)
        {
            for (int z = 0; z < height; z++)
            {
                GameObject tile = gridTiles[x, z];
                if (tile != null)
                {
                    Renderer rend = tile.GetComponent<Renderer>();
                    if (rend != null)
                    {
                        rend.material.color = defaultColor;
                    }
                }
            }
        }
    }

    public void RegisterUnit(Unit unit, int x, int z)
    {
        if (unitGrid == null) unitGrid = new Unit[width, height];
        unitGrid[x, z] = unit;
    }

    public void ClearUnitPosition(int x, int z)
    {
        if (unitGrid != null && x >= 0 && x < width && z >= 0 && z < height)
        {
            unitGrid[x, z] = null;
        }
    }

    public bool IsTileOccupied(int x, int z)
    {
        if (unitGrid == null) unitGrid = new Unit[width, height];
        return unitGrid[x, z] != null && unitGrid[x, z] != GetComponent<Unit>(); // Ignores the unit currently being dragged
    }

    // Check if a coordinate is within the player's deployment zone
    public bool IsInPlayerZone(int x, int z)
    {
        return z >= 0 && z <= playerMaxRow && x >= 0 && x < width;
    }

    // Check if a coordinate is within the enemy zone
    public bool IsInEnemyZone(int x, int z)
    {
        return z >= enemyMinRow && z < height && x >= 0 && x < width;
    }

}