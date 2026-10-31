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

    [Header("Visual Prefabs")]
    public GameObject tilePrefab; 
    public GameObject benchTilePrefab;

    [Header("Unit Configuration")]
    public GameObject unitPrefab; 
    public GameObject defaultEnemyPrefab;

    [Header("Team Setup Limits")]
    public int maxPlayerRow = 3;
    public int enemyMinRow = 5;
    public int defaultMaxUnits = 1;

    [Header("Bench Configuration")]
    public int benchSlotCount = 8;
    public float benchZDistance = 1.4f;

    public int maxBoardUnits
    {
        get => GetMaxBoardUnits();
        set => defaultMaxUnits = value;
    }

    [Header("Grid Tracking")]
    private GameObject[,] gridTiles;
    private Unit[,] unitGrid;

    private GameObject[] benchTiles;
    private Unit[] benchUnits;

    void Awake()
    {
        unitGrid = new Unit[width, height];
        gridTiles = new GameObject[width, height];
        benchUnits = new Unit[benchSlotCount];
        benchTiles = new GameObject[benchSlotCount];
    }

    void Start()
    {
        GenerateGrid();
        GenerateBench();
        // Let RoundManager handle the initial SpawnEnemyWave() call!
    }

    public int GetMaxBoardUnits()
    {
        if (EconomyManager.Instance != null)
        {
            return EconomyManager.Instance.currentLevel;
        }
        return defaultMaxUnits;
    }

    private void GenerateGrid()
    {
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

    private void GenerateBench()
    {
        GameObject prefab = benchTilePrefab != null ? benchTilePrefab : tilePrefab;
        float tileHalfHeight = prefab != null ? prefab.transform.localScale.y / 2f : 0.05f;

        float benchXOffset = (benchSlotCount * tileSize) / 2f - (tileSize / 2f);
        float zOffset = (height * tileSize) / 2f - (tileSize / 2f);
        float benchZPos = -zOffset - benchZDistance;

        for (int i = 0; i < benchSlotCount; i++)
        {
            Vector3 localPos = new Vector3((i * tileSize) - benchXOffset, tileHalfHeight, benchZPos);
            Vector3 worldPos = transform.position + localPos;

            if (prefab != null)
            {
                GameObject bTile = Instantiate(prefab, worldPos, Quaternion.identity, transform);
                bTile.name = $"BenchTile_{i}";

                Renderer r = bTile.GetComponent<Renderer>();
                if (r != null) r.material.color = new Color(0.85f, 0.85f, 0.90f);

                benchTiles[i] = bTile;
            }
        }
    }

    public GameObject SpawnUnitAt(int x, int z, bool isPlayer = true)
    {
        GameObject prefabToSpawn = isPlayer ? unitPrefab : (defaultEnemyPrefab != null ? defaultEnemyPrefab : unitPrefab);
        if (prefabToSpawn == null || x < 0 || x >= width || z < 0 || z >= height) return null;

        Vector3 spawnPos = GetWorldPosition(x, z);
        GameObject newUnitObj = Instantiate(prefabToSpawn, spawnPos, Quaternion.identity);

        Unit unitScript = newUnitObj.GetComponent<Unit>();
        if (unitScript != null)
        {
            unitScript.isPlayerTeam = isPlayer;
            unitScript.Initialize(x, z, this);
            RegisterUnit(unitScript, x, z);
        }

        return newUnitObj;
    }

    public void SpawnEnemyWave()
    {
        SpawnEnemyWave(defaultEnemyPrefab != null ? defaultEnemyPrefab : unitPrefab, 2);
    }

    public void SpawnEnemyWave(GameObject enemyPrefab, int count)
    {
        GameObject prefabToSpawn = enemyPrefab != null ? enemyPrefab : (defaultEnemyPrefab != null ? defaultEnemyPrefab : unitPrefab);
        if (prefabToSpawn == null) return;

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

    public bool IsTileOccupied(int x, int z)
    {
        if (unitGrid == null || x < 0 || x >= width || z < 0 || z >= height) return false;
        return unitGrid[x, z] != null && unitGrid[x, z].gameObject.activeInHierarchy;
    }

    public Unit GetUnitAt(int x, int z)
    {
        if (unitGrid == null || x < 0 || x >= width || z < 0 || z >= height) return null;
        Unit u = unitGrid[x, z];
        return (u != null && u.gameObject.activeInHierarchy) ? u : null;
    }

    public void RegisterUnit(Unit unit, int x, int z)
    {
        if (unitGrid == null) unitGrid = new Unit[width, height];

        for (int ix = 0; ix < width; ix++)
        {
            for (int iz = 0; iz < height; iz++)
            {
                if (unitGrid[ix, iz] == unit)
                {
                    unitGrid[ix, iz] = null;
                }
            }
        }

        if (x >= 0 && x < width && z >= 0 && z < height)
        {
            unitGrid[x, z] = unit;
        }

        NotifySynergiesChanged();
    }

    public void ClearUnitPosition(int x, int z)
    {
        if (unitGrid != null && x >= 0 && x < width && z >= 0 && z < height)
        {
            unitGrid[x, z] = null;
        }

        NotifySynergiesChanged();
    }

    public void UnregisterUnit(Unit unit)
    {
        if (unit == null) return;

        if (unit.isOnBench && unit.benchSlotIndex >= 0)
        {
            ClearBenchPosition(unit.benchSlotIndex);
        }

        if (unitGrid != null)
        {
            for (int x = 0; x < width; x++)
            {
                for (int z = 0; z < height; z++)
                {
                    if (unitGrid[x, z] == unit)
                    {
                        unitGrid[x, z] = null;
                    }
                }
            }
        }

        NotifySynergiesChanged();
    }

    public void MoveUnitOnGrid(Unit unit, int fromX, int fromZ, int toX, int toZ)
    {
        ClearUnitPosition(fromX, fromZ);
        RegisterUnit(unit, toX, toZ);

        unit.gridX = toX;
        unit.gridZ = toZ;
    }

    public int GetCurrentBoardUnitCount()
    {
        Unit[] allSceneUnits = Object.FindObjectsByType<Unit>(FindObjectsInactive.Exclude);
        int count = 0;

        foreach (var u in allSceneUnits)
        {
            if (u != null && u.isPlayerTeam && !u.isOnBench && u.currentHealth > 0)
            {
                count++;
            }
        }
        return count;
    }

    public int GetFirstEmptyBenchSlot()
    {
        if (benchUnits == null) benchUnits = new Unit[benchSlotCount];

        for (int i = 0; i < benchSlotCount; i++)
        {
            if (benchUnits[i] == null || !benchUnits[i].gameObject.activeInHierarchy)
            {
                return i;
            }
        }
        return -1;
    }

    public Unit GetBenchUnitAt(int slotIndex)
    {
        if (benchUnits == null || slotIndex < 0 || slotIndex >= benchSlotCount) return null;
        Unit u = benchUnits[slotIndex];
        return (u != null && u.gameObject.activeInHierarchy) ? u : null;
    }

    public void RegisterBenchUnit(Unit unit, int slotIndex)
    {
        if (benchUnits == null) benchUnits = new Unit[benchSlotCount];

        for (int i = 0; i < benchSlotCount; i++)
        {
            if (benchUnits[i] == unit) benchUnits[i] = null;
        }

        if (slotIndex >= 0 && slotIndex < benchSlotCount)
        {
            benchUnits[slotIndex] = unit;
        }

        NotifySynergiesChanged();
    }

    public void ClearBenchPosition(int slotIndex)
    {
        if (benchUnits != null && slotIndex >= 0 && slotIndex < benchSlotCount)
        {
            benchUnits[slotIndex] = null;
        }

        NotifySynergiesChanged();
    }

    public bool IsInPlayerZone(int x, int z)
    {
        return z >= 0 && z <= maxPlayerRow && x >= 0 && x < width;
    }

    public bool IsInEnemyZone(int x, int z)
    {
        return z >= enemyMinRow && z < height && x >= 0 && x < width;
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
                    GameObject tile = gridTiles[x, z];
                    if (tile != null)
                    {
                        Renderer rend = tile.GetComponent<Renderer>();
                        if (rend != null) rend.material.color = defaultBoardColor;
                    }
                }
            }
        }

        if (benchTiles != null)
        {
            Color benchColor = new Color(0.85f, 0.85f, 0.90f);
            for (int i = 0; i < benchSlotCount; i++)
            {
                GameObject tile = benchTiles[i];
                if (tile != null)
                {
                    Renderer rend = tile.GetComponent<Renderer>();
                    if (rend != null) rend.material.color = benchColor;
                }
            }
        }
    }

    private void NotifySynergiesChanged()
    {
        if (SynergyManager.Instance != null)
        {
            SynergyManager.Instance.RecalculateSynergies();
        }
    }
}