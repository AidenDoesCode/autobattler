using UnityEngine;
using System.Collections.Generic;

// Defines coordinates for pathfinding on the grid
public struct GridPoint
{
    public int x;
    public int z;
    public GridPoint(int x, int z) { this.x = x; this.z = z; }
    public override bool Equals(object obj) => obj is GridPoint p && p.x == x && p.z == z;
    public override int GetHashCode() => (x * 397) ^ z;
}

public class BoardGridManager : MonoBehaviour
{
    [Header("Grid Dimensions")]
    public int width = 6;   // Columns (X axis)
    public int height = 7;  // Rows / Depth (Z axis)
    public float tileSize = 1f;

    [Header("Visuals")]
    public GameObject tilePrefab; 

    [Header("Units")]
    public GameObject unitPrefab; 

    [Header("Team Setup Limits")]
    public int playerMaxRow = 1; // Rows 0 and 1 belong to the player during planning
    public int enemyMinRow = 5;  // Rows 5 and 6 belong to the enemy

    [Header("Grid Tracking")]
    private Unit[,] unitGrid;
    private GameObject[,] gridTiles;

    void Start()
    {
        GenerateGrid();

        // Spawn Player Friend units on the bottom rows
        SpawnUnitAt(1, 0);
        SpawnUnitAt(3, 0);

        // Spawn Enemy units on the top rows
        SpawnUnitAt(1, 6);
        SpawnUnitAt(3, 6);
    }

    public GameObject SpawnUnitAt(int x, int z)
    {
        if (unitPrefab == null)
        {
            Debug.LogWarning("No Unit Prefab assigned to BoardGridManager!");
            return null;
        }

        if (x < 0 || x >= width || z < 0 || z >= height)
        {
            Debug.LogError($"Coordinates ({x}, {z}) are outside the grid bounds!");
            return null;
        }

        Vector3 spawnPos = GetWorldPosition(x, z);

        // Player units face +Z (identity), Enemy units face -Z (180 degrees)
        bool isEnemy = IsInEnemyZone(x, z);
        Quaternion initialRotation = isEnemy ? Quaternion.Euler(0f, 180f, 0f) : Quaternion.identity;

        GameObject newUnitObj = Instantiate(unitPrefab, spawnPos, initialRotation);

        Unit unitScript = newUnitObj.GetComponent<Unit>();
        if (unitScript != null)
        {
            unitScript.Initialize(x, z, this);

            if (isEnemy)
            {
                unitScript.isPlayerTeam = false;
            }

            RegisterUnit(unitScript, x, z);
        }

        return newUnitObj;
    }

    void GenerateGrid()
    {
        unitGrid = new Unit[width, height];
        gridTiles = new GameObject[width, height];

        float xOffset = (width * tileSize) / 2f - (tileSize / 2f);
        float zOffset = (height * tileSize) / 2f - (tileSize / 2f);
        float tileHalfHeight = tilePrefab != null ? tilePrefab.transform.localScale.y / 2f : 0.05f;

        for (int x = 0; x < width; x++)
        {
            for (int z = 0; z < height; z++)
            {
                Vector3 localPos = new Vector3((x * tileSize) - xOffset, tileHalfHeight, (z * tileSize) - zOffset);
                Vector3 worldPos = transform.position + localPos;

                if (tilePrefab != null)
                {
                    GameObject tile = Instantiate(tilePrefab, worldPos, Quaternion.identity, transform);
                    tile.name = $"Tile_{x}_{z}";
                    gridTiles[x, z] = tile;
                }
            }
        }
    }

    public Vector3 GetWorldPosition(int x, int z)
    {
        float xOffset = (width * tileSize) / 2f - (tileSize / 2f);
        float zOffset = (height * tileSize) / 2f - (tileSize / 2f);
        float tileHeight = tilePrefab != null ? tilePrefab.transform.localScale.y : 0.05f;

        return new Vector3(
            transform.position.x + (x * tileSize) - xOffset,
            transform.position.y + tileHeight,
            transform.position.z + (z * tileSize) - zOffset
        );
    }

    public bool IsInPlayerZone(int x, int z)
    {
        return z >= 0 && z <= playerMaxRow && x >= 0 && x < width;
    }

    public bool IsInEnemyZone(int x, int z)
    {
        return z >= enemyMinRow && z < height && x >= 0 && x < width;
    }

    public bool IsTileOccupied(int x, int z)
    {
        if (unitGrid == null || x < 0 || x >= width || z < 0 || z >= height) return false;
        return unitGrid[x, z] != null;
    }

    public void RegisterUnit(Unit unit, int x, int z)
    {
        if (unitGrid == null) unitGrid = new Unit[width, height];

        if (x >= 0 && x < width && z >= 0 && z < height)
        {
            unitGrid[x, z] = unit;
        }
    }

    public void ClearUnitPosition(int x, int z)
    {
        if (unitGrid != null && x >= 0 && x < width && z >= 0 && z < height)
        {
            unitGrid[x, z] = null;
        }
    }

    public void MoveUnitOnGrid(Unit unit, int fromX, int fromZ, int toX, int toZ)
    {
        ClearUnitPosition(fromX, fromZ);
        RegisterUnit(unit, toX, toZ);

        unit.gridX = toX;
        unit.gridZ = toZ;
    }

    public List<GridPoint> FindPath(GridPoint start, GridPoint target, int rangeInTiles)
    {
        if (unitGrid == null) unitGrid = new Unit[width, height];

        Queue<GridPoint> queue = new Queue<GridPoint>();
        Dictionary<GridPoint, GridPoint> cameFrom = new Dictionary<GridPoint, GridPoint>();
        HashSet<GridPoint> visited = new HashSet<GridPoint>();

        queue.Enqueue(start);
        visited.Add(start);

        GridPoint[] directions = new GridPoint[]
        {
            new GridPoint(0, 1),
            new GridPoint(0, -1),
            new GridPoint(1, 0),
            new GridPoint(-1, 0)
        };

        GridPoint goal = start;
        bool foundGoal = false;

        while (queue.Count > 0)
        {
            GridPoint current = queue.Dequeue();

            int distToTarget = Mathf.Max(Mathf.Abs(current.x - target.x), Mathf.Abs(current.z - target.z));
            if (distToTarget <= rangeInTiles)
            {
                goal = current;
                foundGoal = true;
                break;
            }

            foreach (var dir in directions)
            {
                GridPoint next = new GridPoint(current.x + dir.x, current.z + dir.z);

                if (next.x >= 0 && next.x < width && next.z >= 0 && next.z < height)
                {
                    if (!visited.Contains(next) && (unitGrid[next.x, next.z] == null || next.Equals(start)))
                    {
                        visited.Add(next);
                        cameFrom[next] = current;
                        queue.Enqueue(next);
                    }
                }
            }
        }

        if (!foundGoal || goal.Equals(start)) return null;

        List<GridPoint> path = new List<GridPoint>();
        GridPoint curr = goal;
        while (!curr.Equals(start))
        {
            path.Add(curr);
            curr = cameFrom[curr];
        }
        path.Reverse();
        return path;
    }

    public void HighlightTile(int x, int z, Color color)
    {
        if (gridTiles == null || x < 0 || x >= width || z < 0 || z >= height) return;

        GameObject tile = gridTiles[x, z];
        if (tile != null)
        {
            Renderer rend = tile.GetComponent<Renderer>();
            if (rend != null) rend.material.color = color;
        }
    }

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
                    if (rend != null) rend.material.color = defaultColor;
                }
            }
        }
    }

    public void SpawnEnemyWave()
    {
        // Clear dead enemies from upper rows and spawn fresh ones
        SpawnUnitAt(1, 6);
        SpawnUnitAt(3, 6);
    }
}