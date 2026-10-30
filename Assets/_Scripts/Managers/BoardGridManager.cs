using UnityEngine;
using System.Collections.Generic;

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
    [Header("Board Grid Dimensions")]
    public int width = 6;
    public int height = 7;
    public float tileSize = 1f;

    [Header("Visuals")]
    public GameObject tilePrefab;

    [Header("Player Zone")]
    public int maxPlayerRow = 1;

    [Header("Bench Configuration")]
    public int benchSlotCount = 8;
    [Tooltip("Offset distance behind row 0 where the bench sits")]
    public float benchZDistance = 1.35f; 

    [Header("Team Board Limits")]
    public int maxBoardUnits = 3; // Active units allowed on combat grid

    private GameObject[,] gridTiles;
    private Unit[,] unitGrid;

    // Bench Data
    private GameObject[] benchTiles;
    private Unit[] benchUnits;

    [Header("Enemy Spawning")]
    public GameObject defaultEnemyPrefab; // Drag your Enemy Unit Prefab here in the Inspector
    public int enemyMinRow = 5;

    void Start()
    {
        GenerateGrid();
        GenerateBench();
    }

    void GenerateGrid()
    {
        gridTiles = new GameObject[width, height];
        unitGrid = new Unit[width, height];

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

    /// <summary>
    /// Deregisters and clears a unit from whichever slot it occupies (board or bench).
    /// </summary>
    public void UnregisterUnit(Unit unit)
    {
        if (unit == null) return;

        if (unit.isOnBench)
        {
            if (benchUnits != null && unit.benchSlotIndex >= 0 && unit.benchSlotIndex < benchSlotCount)
            {
                benchUnits[unit.benchSlotIndex] = null;
            }
        }
        else
        {
            if (unitGrid != null && unit.gridX >= 0 && unit.gridX < width && unit.gridZ >= 0 && unit.gridZ < height)
            {
                if (unitGrid[unit.gridX, unit.gridZ] == unit)
                {
                    unitGrid[unit.gridX, unit.gridZ] = null;
                }
            }
        }
    }

    void GenerateBench()
    {
        benchTiles = new GameObject[benchSlotCount];
        benchUnits = new Unit[benchSlotCount];

        float tileHalfHeight = tilePrefab != null ? tilePrefab.transform.localScale.y / 2f : 0.05f;
        float benchXOffset = (benchSlotCount * tileSize) / 2f - (tileSize / 2f);
        float zOffset = (height * tileSize) / 2f - (tileSize / 2f);
        float benchZPos = -zOffset - benchZDistance;

        for (int i = 0; i < benchSlotCount; i++)
        {
            Vector3 localPos = new Vector3((i * tileSize) - benchXOffset, tileHalfHeight, benchZPos);
            Vector3 worldPos = transform.position + localPos;

            if (tilePrefab != null)
            {
                GameObject tile = Instantiate(tilePrefab, worldPos, Quaternion.identity, transform);
                tile.name = $"BenchTile_{i}";
                
                // Optional visual tint to distinguish bench from combat board
                Renderer r = tile.GetComponent<Renderer>();
                if (r != null) r.material.color = new Color(0.75f, 0.75f, 0.85f);

                benchTiles[i] = tile;
            }
        }
    }

    // World position of combat tile
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

    // World position of bench slot
    public Vector3 GetBenchWorldPosition(int index)
    {
        if (benchTiles != null && index >= 0 && index < benchSlotCount && benchTiles[index] != null)
        {
            float tileHeight = tilePrefab != null ? tilePrefab.transform.localScale.y : 0.05f;
            return benchTiles[index].transform.position + Vector3.up * tileHeight;
        }

        float benchXOffset = (benchSlotCount * tileSize) / 2f - (tileSize / 2f);
        float zOffset = (height * tileSize) / 2f - (tileSize / 2f);
        float benchZPos = -zOffset - benchZDistance;
        float th = tilePrefab != null ? tilePrefab.transform.localScale.y : 0.05f;

        return new Vector3(
            transform.position.x + (index * tileSize) - benchXOffset,
            transform.position.y + th,
            transform.position.z + benchZPos
        );
    }

    // --- Capacity & Query Helpers ---

    public int GetCurrentBoardUnitCount()
    {
        if (unitGrid == null) return 0;
        int count = 0;
        for (int x = 0; x < width; x++)
        {
            for (int z = 0; z <= maxPlayerRow; z++)
            {
                if (unitGrid[x, z] != null && unitGrid[x, z].isPlayerTeam)
                {
                    count++;
                }
            }
        }
        return count;
    }

    public bool IsInPlayerZone(int x, int z)
    {
        return z >= 0 && z <= maxPlayerRow && x >= 0 && x < width;
    }

    public Unit GetUnitAt(int x, int z)
    {
        if (unitGrid == null || x < 0 || x >= width || z < 0 || z >= height) return null;
        return unitGrid[x, z];
    }

    public Unit GetBenchUnitAt(int slotIndex)
    {
        if (benchUnits == null || slotIndex < 0 || slotIndex >= benchSlotCount) return null;
        return benchUnits[slotIndex];
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

    public void RegisterBenchUnit(Unit unit, int slotIndex)
    {
        if (benchUnits == null) benchUnits = new Unit[benchSlotCount];
        if (slotIndex >= 0 && slotIndex < benchSlotCount)
        {
            benchUnits[slotIndex] = unit;
        }
    }

    public void ClearBenchPosition(int slotIndex)
    {
        if (benchUnits != null && slotIndex >= 0 && slotIndex < benchSlotCount)
        {
            benchUnits[slotIndex] = null;
        }
    }

    // --- Tile Highlighting ---

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

    public void HighlightBenchTile(int index, Color color)
    {
        if (benchTiles == null || index < 0 || index >= benchSlotCount) return;
        GameObject tile = benchTiles[index];
        if (tile != null)
        {
            Renderer rend = tile.GetComponent<Renderer>();
            if (rend != null) rend.material.color = color;
        }
    }

    public void ResetAllTileColors(Color defaultBoardColor)
    {
        if (gridTiles != null)
        {
            for (int x = 0; x < width; x++)
            {
                for (int z = 0; z < height; z++)
                {
                    if (gridTiles[x, z] != null)
                    {
                        Renderer rend = gridTiles[x, z].GetComponent<Renderer>();
                        if (rend != null) rend.material.color = defaultBoardColor;
                    }
                }
            }
        }

        if (benchTiles != null)
        {
            Color benchColor = new Color(0.75f, 0.75f, 0.85f);
            for (int i = 0; i < benchSlotCount; i++)
            {
                if (benchTiles[i] != null)
                {
                    Renderer rend = benchTiles[i].GetComponent<Renderer>();
                    if (rend != null) rend.material.color = benchColor;
                }
            }
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
            new GridPoint(0, 1), new GridPoint(0, -1),
            new GridPoint(1, 0), new GridPoint(-1, 0)
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

    public void SpawnEnemyWave()
    {
        SpawnEnemyWave(defaultEnemyPrefab, 2);
    }

    public void SpawnEnemyWave(GameObject enemyPrefab, int count)
    {
        GameObject prefabToSpawn = enemyPrefab != null ? enemyPrefab : defaultEnemyPrefab;
        if (prefabToSpawn == null)
        {
            Debug.LogWarning("[BoardGridManager] Cannot spawn enemy wave: No enemy prefab assigned!");
            return;
        }

        int spawned = 0;
        for (int z = height - 1; z >= enemyMinRow && spawned < count; z--)
        {
            for (int x = 0; x < width && spawned < count; x++)
            {
                if (!IsTileOccupied(x, z))
                {
                    Vector3 spawnPos = GetWorldPosition(x, z);
                    GameObject enemyObj = Instantiate(prefabToSpawn, spawnPos, Quaternion.identity);
                    Unit enemyUnit = enemyObj.GetComponent<Unit>();

                    if (enemyUnit != null)
                    {
                        enemyUnit.isPlayerTeam = false;
                        enemyUnit.Initialize(x, z, this);
                        RegisterUnit(enemyUnit, x, z);
                    }

                    spawned++;
                }
            }
        }
    }

    public bool IsTileOccupied(int x, int z)
    {
        if (unitGrid == null || x < 0 || x >= width || z < 0 || z >= height) return false;
        return unitGrid[x, z] != null;
    }

    /// <summary>
    /// Finds the index of the first unoccupied bench slot. Returns -1 if the bench is full.
    /// </summary>
    public int GetFirstEmptyBenchSlot()
    {
        if (benchUnits == null) return -1;

        for (int i = 0; i < benchSlotCount; i++)
        {
            if (benchUnits[i] == null)
            {
                return i;
            }
        }
        return -1;
    }

    


}