using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshFilter))]
public class FluidSurfaceController : MonoBehaviour
{
    const float CORNER_WEIGHT = 1f;

    /// <summary>Size of the grid in cells</summary>
    public Vector2Int fluidGridSize = new Vector2Int(4, 4);

    /// <summary>Size of a cell in word units</summary>
    public Vector2 fluidCellSize = new Vector2(1.0f, 1.0f);

    public float fluidHeightMax = 10f;

    public float fluidHeightMin = .1f;

    /// <summary>Current target height of fluid</summary>
    public float fluidHeightTarget = 1.0f;

    /// <summary>Current amount of turbulence at start</summary>
    public float fluidInitialTurbulence = 0.0f;

    /// <summary>Amount the fluid resists flow as a multiple of heigh retained</summary>
    public float fluidResistenceCoefficient = 1.0f;

    public float fluidGravity = 0.03f;

    protected Mesh mesh;
    protected Vector3[] vertices;
    protected int[] triangles;
    protected float[] fluidHeightMap;
    protected float[][] fluidHeightMapVelocities;
    protected uint currentFluidHeightMapVelocityIndex = 0;

    // Start is called before the first frame update
    void Start()
    {
        GetComponent<MeshFilter>().mesh = mesh = new Mesh();

        Reinitialize();
        ApplyFluidTurbulence();
    }

    void Reinitialize()
    {
        if (vertices == null || vertices.Length != (fluidGridSize.x + 1) * (fluidGridSize.y + 1)) {
            vertices = new Vector3[(fluidGridSize.x + 1) * (fluidGridSize.y + 1)];
        }
        if (triangles == null || triangles.Length != fluidGridSize.x * fluidGridSize.y * 6) {
            triangles = new int[fluidGridSize.x * fluidGridSize.y * 6];
            for (int ti = 0, vi = 0, z = 0; z < fluidGridSize.y; z++, vi++) {
                for (int x = 0; x < fluidGridSize.x; x++, ti += 6, vi++) {
                    triangles[ti] = vi;
                    triangles[ti + 3] = triangles[ti + 2] = vi + 1;
                    triangles[ti + 4] = triangles[ti + 1] = vi + fluidGridSize.x + 1;
                    triangles[ti + 5] = vi + fluidGridSize.x + 2;
                }
            }
        }

        if (
            fluidHeightMap == null ||
            fluidHeightMap.Length != (fluidGridSize.x + 1) * (fluidGridSize.y + 1)
        ) {
            fluidHeightMap = new float[(fluidGridSize.x + 1) * (fluidGridSize.y + 1)];
        }

        if (
            fluidHeightMapVelocities == null ||
            fluidHeightMapVelocities.Length != 2 ||
            fluidHeightMapVelocities[0].Length != ((fluidGridSize.x + 1) * (fluidGridSize.y + 1)) ||
            fluidHeightMapVelocities[1].Length != ((fluidGridSize.x + 1) * (fluidGridSize.y + 1))
        ) {
            fluidHeightMapVelocities = new float[][]
            {
                new float[(fluidGridSize.x + 1) * (fluidGridSize.y + 1)],
                new float[(fluidGridSize.x + 1) * (fluidGridSize.y + 1)]
            };
        }
    }

    void ApplyFluidTurbulence()
    {
        for (int i = 0, z = 0; z <= fluidGridSize.y; z++) {
            for (int x = 0; x <= fluidGridSize.y; x++, i++) {
                fluidHeightMap[i] = (
                    fluidHeightTarget +
                    Random.Range(-fluidInitialTurbulence, fluidInitialTurbulence)
                );
            }
        }
    }

    void Update()
    {
        Reinitialize();

        uint prevI = currentFluidHeightMapVelocityIndex;
        uint currI =
            currentFluidHeightMapVelocityIndex =
            currentFluidHeightMapVelocityIndex == 1u ? 0u : 1u;
        for (int i = 0, z = 0; z <= fluidGridSize.y; z++) {
            for (int x = 0; x <= fluidGridSize.y; x++, i++) {
                fluidHeightMapVelocities[currI][i] = fluidResistenceCoefficient * (
                    fluidHeightMapVelocities[prevI][i] +
                    (fluidGravity * Mathf.Log(fluidHeightMap[i] / fluidHeightTarget, Mathf.Epsilon))
                );
                fluidHeightMap[i] += fluidHeightMapVelocities[currI][i] - (
                    (
                        // Right
                        ((x + 1 <= fluidGridSize.x) ?
                            fluidHeightMapVelocities[prevI][i + 1] :
                            0
                        ) +
                        // Right-Up
                        (((x + 1 <= fluidGridSize.x) && (z + 1 <= fluidGridSize.y)) ?
                            fluidHeightMapVelocities[prevI][i + fluidGridSize.x + 2] * CORNER_WEIGHT :
                            0
                        ) +
                        // Up
                        ((z + 1 <= fluidGridSize.y) ?
                            fluidHeightMapVelocities[prevI][i + fluidGridSize.x + 1] :
                            0
                        ) +
                        // Up-Left
                        (((z + 1 <= fluidGridSize.y) && (x - 1 >= 0)) ?
                            fluidHeightMapVelocities[prevI][i + fluidGridSize.x] * CORNER_WEIGHT :
                            0
                        ) +
                        // Left
                        ((x - 1 >= 0) ?
                            fluidHeightMapVelocities[prevI][i - 1] :
                            0
                        ) +
                        // Left-Down
                        (((x - 1 >= 0) && (z - 1 >= 0)) ?
                            fluidHeightMapVelocities[prevI][i - fluidGridSize.x - 2] * CORNER_WEIGHT :
                            0
                        ) +
                        // Down
                        ((z - 1 >= 0) ?
                            fluidHeightMapVelocities[prevI][i - fluidGridSize.x - 1] :
                            0
                        ) +
                        // Down-Right
                        (((z - 1 >= 0) && (x + 1 <= fluidGridSize.x)) ?
                            fluidHeightMapVelocities[prevI][i - fluidGridSize.x] * CORNER_WEIGHT :
                            0
                        )
                    ) / 8f
                );
                if (fluidHeightMap[i] > fluidHeightMax) {
                    fluidHeightMap[i] = fluidHeightMax;
                }
                if (fluidHeightMap[i] < fluidHeightMin) {
                    fluidHeightMap[i] = fluidHeightMin;
                }
            }
        }

        mesh.Clear();

        if (Input.GetKeyDown(KeyCode.Space)) {
            int centerX = (int) Random.Range(0, fluidGridSize.x + 1);
            int centerZ = (int) Random.Range(0, fluidGridSize.y + 1);
            for (int z = -20; z < 20; z ++) {
                for (int x = -20; x < 20; x++) {
                    if (
                        (x + centerX) < 0 ||
                        (x + centerX) > (fluidGridSize.x) ||
                        (z + centerZ) < 0 ||
                        (z + centerZ) > (fluidGridSize.y)
                    ) {
                        continue;
                    }
                    fluidHeightMapVelocities[currentFluidHeightMapVelocityIndex][
                        (x + centerX) + ((z + centerZ) * (fluidGridSize.x + 1))
                    ] = -1f;
                }
            }
        }

        for (int i = 0, z = 0; z <= fluidGridSize.y; z++) {
            for (int x = 0; x <= fluidGridSize.x; x++, i++) {
                vertices[i].x = x * fluidCellSize.x;
                vertices[i].y = fluidHeightMap[i];
                vertices[i].z = z * fluidCellSize.y;
            }
        }

        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
    }
}
