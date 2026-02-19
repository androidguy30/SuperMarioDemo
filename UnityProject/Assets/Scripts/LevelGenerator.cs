using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Procedurally generates the World 1-1 inspired level using prefabs.
/// Attach to an empty GameObject in the scene.
/// </summary>
public class LevelGenerator : MonoBehaviour
{
    [Header("Tile Prefabs")]
    [SerializeField] private GameObject groundPrefab;
    [SerializeField] private GameObject brickPrefab;
    [SerializeField] private GameObject questionBlockPrefab;
    [SerializeField] private GameObject usedBlockPrefab;
    [SerializeField] private GameObject pipePrefab;         // 2-tile wide pipe segment
    [SerializeField] private GameObject pipeTopPrefab;      // 2-tile wide pipe top

    [Header("Entity Prefabs")]
    [SerializeField] private GameObject goombaPrefab;
    [SerializeField] private GameObject koopaPrefab;
    [SerializeField] private GameObject coinPrefab;
    [SerializeField] private GameObject mushroomPrefab;

    [Header("Decoration Prefabs")]
    [SerializeField] private GameObject hillPrefab;
    [SerializeField] private GameObject bushPrefab;
    [SerializeField] private GameObject cloudPrefab;
    [SerializeField] private GameObject flagpolePrefab;
    [SerializeField] private GameObject castlePrefab;

    [Header("Settings")]
    [SerializeField] private float tileSize = 1f;
    [SerializeField] private int levelWidth = 210;
    [SerializeField] private int levelHeight = 15;

    private Transform tilesParent;
    private Transform entitiesParent;
    private Transform decorParent;

    private void Awake()
    {
        tilesParent = new GameObject("Tiles").transform;
        entitiesParent = new GameObject("Entities").transform;
        decorParent = new GameObject("Decorations").transform;
    }

    private void Start()
    {
        GenerateLevel();
    }

    public void GenerateLevel()
    {
        ClearLevel();
        GenerateGround();
        GenerateBlocks();
        GeneratePipes();
        GenerateStaircases();
        GenerateEnemies();
        GenerateDecorations();
        GenerateFlag();
        GenerateCastle();
    }

    private void ClearLevel()
    {
        foreach (Transform child in tilesParent) Destroy(child.gameObject);
        foreach (Transform child in entitiesParent) Destroy(child.gameObject);
        foreach (Transform child in decorParent) Destroy(child.gameObject);
    }

    private void GenerateGround()
    {
        // Ground with gaps
        int[][] groundSegments = new int[][]
        {
            new int[] { 0, 68 },
            new int[] { 71, 86 },
            new int[] { 89, 152 },
            new int[] { 156, levelWidth }
        };

        foreach (var seg in groundSegments)
        {
            for (int x = seg[0]; x < seg[1]; x++)
            {
                for (int row = 0; row < 2; row++)
                {
                    PlaceTile(groundPrefab, x, -(13 + row));
                }
            }
        }
    }

    private void GenerateBlocks()
    {
        // Question blocks: (x, y) positions
        int[][] questionBlocks = new int[][]
        {
            new int[] { 16, 9 }, new int[] { 21, 9 }, new int[] { 23, 5 },
            new int[] { 22, 9 }, new int[] { 24, 9 }, new int[] { 78, 9 },
            new int[] { 94, 5 }, new int[] { 106, 9 }, new int[] { 109, 5 },
            new int[] { 112, 9 }, new int[] { 129, 9 }, new int[] { 130, 9 }
        };

        foreach (var pos in questionBlocks)
        {
            PlaceTile(questionBlockPrefab, pos[0], -pos[1]);
        }

        // Brick blocks
        int[][] brickBlocks = new int[][]
        {
            new int[] { 20, 9 }, new int[] { 24, 9 },
            new int[] { 77, 9 }, new int[] { 79, 9 },
            new int[] { 80, 5 }, new int[] { 81, 5 }, new int[] { 82, 5 },
            new int[] { 83, 5 }, new int[] { 84, 5 }, new int[] { 85, 5 },
            new int[] { 86, 5 }, new int[] { 87, 5 },
            new int[] { 91, 5 }, new int[] { 92, 5 }, new int[] { 93, 5 },
            new int[] { 95, 5 },
            new int[] { 100, 9 }, new int[] { 101, 9 },
            new int[] { 107, 9 }, new int[] { 108, 9 },
            new int[] { 110, 9 }, new int[] { 111, 9 },
            new int[] { 118, 9 }, new int[] { 119, 9 }, new int[] { 120, 9 },
            new int[] { 128, 9 }, new int[] { 131, 9 },
            new int[] { 129, 5 }, new int[] { 130, 5 }, new int[] { 131, 5 },
            new int[] { 132, 5 }, new int[] { 133, 5 }, new int[] { 134, 5 },
            new int[] { 168, 9 }, new int[] { 169, 9 }
        };

        foreach (var pos in brickBlocks)
        {
            PlaceTile(brickPrefab, pos[0], -pos[1]);
        }
    }

    private void GeneratePipes()
    {
        // Pipes: (x position, top y, height in tiles)
        int[][] pipes = new int[][]
        {
            new int[] { 28, 11, 2 },
            new int[] { 38, 10, 3 },
            new int[] { 46, 9, 4 },
            new int[] { 57, 10, 3 },
            new int[] { 163, 11, 2 },
            new int[] { 179, 10, 3 }
        };

        foreach (var p in pipes)
        {
            int px = p[0];
            int topY = p[1];
            int height = p[2];

            // Place pipe top
            PlaceTile(pipeTopPrefab, px, -topY);

            // Place pipe body segments
            for (int row = 1; row < height; row++)
            {
                PlaceTile(pipePrefab, px, -(topY + row));
            }
        }
    }

    private void GenerateStaircases()
    {
        // First ascending staircase
        for (int step = 0; step < 8; step++)
        {
            for (int row = 0; row <= step; row++)
            {
                PlaceTile(groundPrefab, 134 + step, -(12 - row));
            }
        }

        // First descending staircase
        for (int step = 0; step < 8; step++)
        {
            for (int row = 0; row <= step; row++)
            {
                PlaceTile(groundPrefab, 152 - step, -(12 - row));
            }
        }

        // Second ascending staircase
        for (int step = 0; step < 9; step++)
        {
            for (int row = 0; row <= step; row++)
            {
                PlaceTile(groundPrefab, 156 + step, -(12 - row));
            }
        }
    }

    private void GenerateEnemies()
    {
        // Goombas
        int[] goombaPositions = { 22, 40, 51, 52, 80, 82, 97, 98, 114, 115, 124, 125, 174, 175 };
        foreach (int gx in goombaPositions)
        {
            PlaceEntity(goombaPrefab, gx, -12);
        }

        // Koopas
        int[] koopaPositions = { 107, 170 };
        foreach (int kx in koopaPositions)
        {
            PlaceEntity(koopaPrefab, kx, -11);
        }
    }

    private void GenerateDecorations()
    {
        // Hills (background parallax layer)
        float[] hillPositions = { 4, 18, 36, 56, 74, 100, 130, 160 };
        foreach (float hx in hillPositions)
        {
            PlaceDecor(hillPrefab, hx, -13, 0.5f);
        }

        // Bushes
        float[] bushPositions = { 10, 26, 44, 68, 90, 118, 148 };
        foreach (float bx in bushPositions)
        {
            PlaceDecor(bushPrefab, bx, -13, 0.5f);
        }

        // Clouds (parallax layer at different depth)
        float[][] cloudData = new float[][]
        {
            new float[] { 8, -2, 1f },
            new float[] { 20, -1, 0.7f },
            new float[] { 36, -2.5f, 1.1f },
            new float[] { 52, -1.5f, 0.8f },
            new float[] { 70, -2, 1f },
            new float[] { 88, -1, 0.9f },
            new float[] { 108, -2.5f, 1.2f },
            new float[] { 128, -1, 0.7f },
            new float[] { 150, -2, 1f },
            new float[] { 172, -1.5f, 0.8f },
        };

        foreach (var cd in cloudData)
        {
            PlaceDecor(cloudPrefab, cd[0], cd[1], cd[2]);
        }
    }

    private void GenerateFlag()
    {
        if (flagpolePrefab != null)
        {
            Vector3 pos = new Vector3(198 * tileSize, -4 * tileSize, 0);
            Instantiate(flagpolePrefab, pos, Quaternion.identity, entitiesParent);
        }
    }

    private void GenerateCastle()
    {
        if (castlePrefab != null)
        {
            Vector3 pos = new Vector3(202 * tileSize, -10 * tileSize, 0);
            Instantiate(castlePrefab, pos, Quaternion.identity, decorParent);
        }
    }

    private void PlaceTile(GameObject prefab, int x, int y)
    {
        if (prefab == null) return;
        Vector3 pos = new Vector3(x * tileSize, y * tileSize, 0);
        Instantiate(prefab, pos, Quaternion.identity, tilesParent);
    }

    private void PlaceEntity(GameObject prefab, int x, int y)
    {
        if (prefab == null) return;
        Vector3 pos = new Vector3(x * tileSize + 0.5f, y * tileSize + 0.5f, 0);
        Instantiate(prefab, pos, Quaternion.identity, entitiesParent);
    }

    private void PlaceDecor(GameObject prefab, float x, float y, float parallaxFactor)
    {
        if (prefab == null) return;
        Vector3 pos = new Vector3(x * tileSize, y * tileSize, parallaxFactor * 10f);
        Instantiate(prefab, pos, Quaternion.identity, decorParent);
    }
}
