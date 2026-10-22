using UnityEngine;

public class ModularLevelBuilder : MonoBehaviour
{
    [Header("Grid Settings")]
    public float tileSize = 10f; // 각 타일 크기
    public int gridWidth = 10;
    public int gridHeight = 10;

    [Header("Floor Tiles")]
    public GameObject[] floorTiles; // 바닥 타일들
    public GameObject[] pathTiles;  // 길 타일들

    [Header("Structure Prefabs")]
    public GameObject[] wallPrefabs;     // 벽들
    public GameObject[] pillarPrefabs;   // 기둥들
    public GameObject[] platformPrefabs; // 플랫폼들
    public GameObject[] decorPrefabs;    // 장식 오브젝트들

    [Header("Layout")]
    public bool[,] isWalkable; // 걸을 수 있는 타일
    public TileType[,] tileTypes; // 타일 종류

    public enum TileType
    {
        Empty,
        Floor,
        Path,
        Wall,
        Platform,
        Decoration
    }

    [ContextMenu("Generate Level")]
    public void GenerateLevel()
    {
        ClearExisting();
        InitializeGrid();
        CreateBasicLayout();
        PlaceStructures();
        PlaceDecorations();
    }

    // Inspector 버튼용
    public void GenerateLevelButton()
    {
        GenerateLevel();
    }

    void ClearExisting()
    {
        foreach (Transform child in transform)
        {
            if (Application.isEditor)
                DestroyImmediate(child.gameObject);
            else
                Destroy(child.gameObject);
        }
    }

    void InitializeGrid()
    {
        isWalkable = new bool[gridWidth, gridHeight];
        tileTypes = new TileType[gridWidth, gridHeight];

        // 기본적으로 모든 타일을 바닥으로
        for (int x = 0; x < gridWidth; x++)
        {
            for (int z = 0; z < gridHeight; z++)
            {
                isWalkable[x, z] = true;
                tileTypes[x, z] = TileType.Floor;
            }
        }
    }

    void CreateBasicLayout()
    {
        // 1. 중앙 전투 공간 (넓은 평지)
        CreateCombatArea();

        // 2. 가장자리 벽들
        CreateBoundaryWalls();

        // 3. 길 만들기
        CreatePaths();
    }

    void CreateCombatArea()
    {
        int centerX = gridWidth / 2;
        int centerZ = gridHeight / 2;
        int radius = 3;

        for (int x = centerX - radius; x <= centerX + radius; x++)
        {
            for (int z = centerZ - radius; z <= centerZ + radius; z++)
            {
                if (IsValidTile(x, z))
                {
                    PlaceFloorTile(x, z);
                    isWalkable[x, z] = true;
                }
            }
        }
    }

    void CreateBoundaryWalls()
    {
        // 가장자리에 벽 배치
        for (int x = 0; x < gridWidth; x++)
        {
            PlaceWall(x, 0);
            PlaceWall(x, gridHeight - 1);
            isWalkable[x, 0] = false;
            isWalkable[x, gridHeight - 1] = false;
        }

        for (int z = 0; z < gridHeight; z++)
        {
            PlaceWall(0, z);
            PlaceWall(gridWidth - 1, z);
            isWalkable[0, z] = false;
            isWalkable[gridWidth - 1, z] = false;
        }
    }

    void CreatePaths()
    {
        // 중앙에서 각 방향으로 길 만들기
        int centerX = gridWidth / 2;
        int centerZ = gridHeight / 2;

        // 수평 길
        for (int x = 2; x < gridWidth - 2; x++)
        {
            PlacePathTile(x, centerZ);
        }

        // 수직 길
        for (int z = 2; z < gridHeight - 2; z++)
        {
            PlacePathTile(centerX, z);
        }
    }

    void PlaceStructures()
    {
        // 랜덤하게 구조물 배치
        for (int x = 2; x < gridWidth - 2; x++)
        {
            for (int z = 2; z < gridHeight - 2; z++)
            {
                if (!isWalkable[x, z]) continue;
                if (Vector2.Distance(new Vector2(x, z), new Vector2(gridWidth / 2, gridHeight / 2)) < 4f) continue;

                if (Random.value < 0.15f) // 15% 확률로 구조물
                {
                    PlacePlatform(x, z);
                    isWalkable[x, z] = false;
                }
            }
        }
    }

    void PlaceDecorations()
    {
        // 장식 오브젝트들 배치
        for (int x = 1; x < gridWidth - 1; x++)
        {
            for (int z = 1; z < gridHeight - 1; z++)
            {
                if (!isWalkable[x, z]) continue;

                if (Random.value < 0.1f) // 10% 확률로 장식
                {
                    PlaceDecoration(x, z);
                }
            }
        }
    }

    void PlaceFloorTile(int x, int z)
    {
        if (floorTiles.Length == 0) return;

        Vector3 position = GetWorldPosition(x, z);
        GameObject tile = floorTiles[Random.Range(0, floorTiles.Length)];
        Instantiate(tile, position, GetRandomRotation(), transform);
    }

    void PlacePathTile(int x, int z)
    {
        if (pathTiles.Length == 0) return;

        Vector3 position = GetWorldPosition(x, z);
        GameObject tile = pathTiles[Random.Range(0, pathTiles.Length)];
        Instantiate(tile, position, GetRandomRotation(), transform);
    }

    void PlaceWall(int x, int z)
    {
        if (wallPrefabs.Length == 0) return;

        Vector3 position = GetWorldPosition(x, z);
        position.y += 1f; // 벽은 약간 위로
        GameObject wall = wallPrefabs[Random.Range(0, wallPrefabs.Length)];
        Instantiate(wall, position, GetRandomRotation(), transform);
    }

    void PlacePlatform(int x, int z)
    {
        if (platformPrefabs.Length == 0) return;

        Vector3 position = GetWorldPosition(x, z);
        position.y += 0.5f;
        GameObject platform = platformPrefabs[Random.Range(0, platformPrefabs.Length)];
        Instantiate(platform, position, GetRandomRotation(), transform);
    }

    void PlaceDecoration(int x, int z)
    {
        if (decorPrefabs.Length == 0) return;

        Vector3 position = GetWorldPosition(x, z);
        GameObject deco = decorPrefabs[Random.Range(0, decorPrefabs.Length)];
        Instantiate(deco, position, GetRandomRotation(), transform);
    }

    Vector3 GetWorldPosition(int x, int z)
    {
        return new Vector3(x * tileSize, 0, z * tileSize);
    }

    Quaternion GetRandomRotation()
    {
        int rotation = Random.Range(0, 4) * 90;
        return Quaternion.Euler(0, rotation, 0);
    }

    bool IsValidTile(int x, int z)
    {
        return x >= 0 && x < gridWidth && z >= 0 && z < gridHeight;
    }

    void OnDrawGizmosSelected()
    {
        // 그리드 표시
        Gizmos.color = Color.yellow;
        for (int x = 0; x <= gridWidth; x++)
        {
            Vector3 start = new Vector3(x * tileSize, 0, 0);
            Vector3 end = new Vector3(x * tileSize, 0, gridHeight * tileSize);
            Gizmos.DrawLine(start, end);
        }

        for (int z = 0; z <= gridHeight; z++)
        {
            Vector3 start = new Vector3(0, 0, z * tileSize);
            Vector3 end = new Vector3(gridWidth * tileSize, 0, z * tileSize);
            Gizmos.DrawLine(start, end);
        }
    }
}