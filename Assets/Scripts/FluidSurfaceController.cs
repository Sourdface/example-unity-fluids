using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


/// <summary>Creates and manages a 2D planar mesh as if it were the surface of a fluid volume</summary>
///
/// A fluid volume grid is created in memory. Each position represents a fluid
/// volume and that position is rendered as the y component of the corresponding
/// mesh vertex.
[RequireComponent(typeof(MeshFilter))]
public class FluidSurfaceController : MonoBehaviour
{
    /// <summary>Number of grid points along the x and z axes. (Z is mapped to the Y component of the vector.)</summary>
    public Vector2Int fluidGridSize = new Vector2Int(26, 26);

    /// <summary> Maximum allowed volume of any fluid point</summary>
    public double fluidVolumeMax = 20;

    /// <summary> Minimum allowed volume of any fluid point</summary>
    public double fluidVolumeMin = 10;

    /// <summary>Current target height of every volume point</summary>
    public double fluidVolumeTarget = 15;

    /// <summary>Amount of turbulence at start</summary>
    public double fluidVolumeTurbulence = 1.0;

    /// <summary>Amount the fluid resists flow as a multiple of height retained</summary>
    public double fluidResistenceCoefficient = .998;

    /// <summary>Force applied to fluid volume per point per step</summary>
    public double fluidGravity = -10;

    public double fluidImpactPower = 2;

    /// <summary>Current fluid volume points</summary>
    protected double[] flVolC;

    /// <summary>Previous fluid volume points</summary>
    protected double[] flVolP;

    /// <summary>Temporary storage used to calculate volume point velocities</summary>
    protected double[] flVolV;

    /// <summary>Coefficients of distribution of volumes</summary>
    protected double[] flVolM;

    /// <summary>Mesh used to display the volumes</summary>
    protected Mesh mesh;

    /// <summary>Vertices of the mesh</summary>
    protected Vector3[] vertices;

    /// <summary>Triangles of the mesh</summary>
    protected int[] triangles;

    void Start()
    {
        GetComponent<MeshFilter>().mesh = mesh = new Mesh();

        Reinitialize();
        ApplyFluidTurbulence();
    }

    void Update()
    {
        Reinitialize();
        UserInput();
        ApplyFluidForces();
        RedrawMesh();
    }

    /// Update all arrays to the correct sizes
    void Reinitialize()
    {
        if (vertices == null || vertices.Length != flGridArea) {
            vertices = new Vector3[flGridArea];
        }
        // TODO: See if we can smooth out the triangles somehow
        if (triangles == null || triangles.Length != ((fluidGridSize.x - 1) * (fluidGridSize.y - 1) * 6)) {
            triangles = new int[(fluidGridSize.x - 1) * (fluidGridSize.y - 1) * 6];
            for (int ti = 0, vi = 0, z = 0; z < (fluidGridSize.y - 1); z++, vi++) {
                for (int x = 0; x < (fluidGridSize.x - 1); x++, ti += 6, vi++) {
                    triangles[ti] = vi;
                    triangles[ti + 3] = triangles[ti + 2] = vi + 1;
                    triangles[ti + 4] = triangles[ti + 1] = vi + fluidGridSize.x;
                    triangles[ti + 5] = vi + fluidGridSize.x + 1;
                }
            }
        }

        if (flVolC == null || flVolC.Length != flGridArea) {
            flVolC = new double[flGridArea];
            for (int i = 0; i < flVolC.Length; i++) {
                flVolC[i] = fluidVolumeTarget;
            }
        }

        if (flVolP == null || flVolP.Length != flGridArea) {
            flVolP = (double[]) flVolC.Clone();
        }

        if (flVolV == null || flVolV.Length != flGridArea) {
            flVolV = new double[flGridArea];
        }

        if (flVolM == null || flVolM.Length != flGridArea) {
            flVolM = new double[flGridArea];

            Vector2 center = fluidGridSize / 2;
            double centerD = Vector2.Distance(Vector2.zero, center);

            // TODO: Build a proper distribution matrix
            double sum = 0;
            for (double z = 0; z < fluidGridSize.y; z++) {
                for (double x = 0; x < fluidGridSize.x; x++) {
                    Vector2 v = new Vector2((float) x, (float) z);
                    double vDist = Vector2.Distance(v, center);
                    double dRatio = vDist / centerD;
                    int i = GetFlVolI((int) x, (int) z);
                    flVolM[i] = 1 + (
                        // TODO: The real formula is more complex than this,
                        //       and may require some form of recursion
                        dRatio / -1
                    );
                    sum += flVolM[i];
                }
            }

            for (double z = 0; z < fluidGridSize.y; z++) {
                for (double x = 0; x < fluidGridSize.x; x++) {
                    flVolM[GetFlVolI((int) x, (int) z)] /= sum * Math.E;
                }
            }
        }
    }

    void UserInput()
    {
         if (Input.GetKeyDown(KeyCode.Space)) {
            int centerX = (int) UnityEngine.Random.Range(0, fluidGridSize.x);
            int centerZ = (int) UnityEngine.Random.Range(0, fluidGridSize.y);
            for (int z = -20; z < 20; z ++) {
                for (int x = -20; x < 20; x++) {
                    if (
                        (x + centerX) < 0 ||
                        (x + centerX) >= (fluidGridSize.x) ||
                        (z + centerZ) < 0 ||
                        (z + centerZ) >= (fluidGridSize.y)
                    ) {
                        continue;
                    }
                    flVolC[
                        (x + centerX) + ((z + centerZ) * (fluidGridSize.x))
                    ] += fluidImpactPower;
                }
            }
        }

        if (Input.GetKeyDown(KeyCode.Return)) {
            int centerX = (int) UnityEngine.Random.Range(0, fluidGridSize.x);
            int centerZ = (int) UnityEngine.Random.Range(0, fluidGridSize.y);
            flVolC[
                (centerX) + ((centerZ) * (fluidGridSize.x))
            ] += fluidImpactPower;
        }
    }

    void ApplyFluidTurbulence()
    {
        for (int i = 0, z = 0; z < fluidGridSize.y; z++) {
            for (int x = 0; x < fluidGridSize.y; x++, i++) {
                flVolC[i] = (
                    fluidVolumeTarget +
                    UnityEngine.Random.Range((float) -fluidVolumeTurbulence, (float) fluidVolumeTurbulence)
                );
            }
        }
    }

    void ApplyFluidForces()
    {
        for (int z = 0; z < fluidGridSize.y; z++) {
            for (int x = 0; x < fluidGridSize.x; x++) {
                int i = GetFlVolI(x, z);
                flVolV[i] = (flVolC[i] - flVolP[i] + (fluidGravity * Math.Pow(flVolC[i]/fluidVolumeTarget, Math.E))) * fluidResistenceCoefficient;
                flVolP[i] = flVolC[i];
            }
        }

        for (int z = 0; z < fluidGridSize.y; z++) {
            for (int x = 0; x < fluidGridSize.x; x++) {
                int i = GetFlVolI(x, z);
                flVolC[i] += flVolV[i];
                for (int zV = 0; zV < fluidGridSize.y; zV++) {
                    for (int xV = 0; xV < fluidGridSize.x; xV++) {
                        int iV = GetFlVolI(xV, zV);
                        int iM = GetFlVolI(xV - x, zV - z);
                        flVolC[i] -= (flVolV[iV] * flVolM[iM]);
                    }
                }
                flVolC[i] = Math.Max(Math.Min(flVolC[i], fluidVolumeMax), fluidVolumeMin);
            }
        }
    }

    void RedrawMesh()
    {
        mesh.Clear();

        for (int z = 0; z < fluidGridSize.y; z++) {
            for (int x = 0; x < fluidGridSize.x; x++) {
                int i = GetFlVolI(x, z);
                vertices[i].x = ((float) x) - (fluidGridSize.x * 0.5f);
                vertices[i].y = (float) flVolC[i];
                vertices[i].z = ((float) z) - (fluidGridSize.y * 0.5f);
            }
        }

        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
    }

    protected int flGridArea
    {
        get {
            return fluidGridSize.x * fluidGridSize.y;
        }
    }

    /// <summary>Get the index of a given x,z coordinate, or -1 if the coordinate is not valid</summary>
    protected int GetFlVolI(int x, int z)
    {
        int x2 = (
            x < 0 ? - x :
            x >= fluidGridSize.x ? (fluidGridSize.x * 2) - x - 2 :
            x
        );
        int z2 = (
            z < 0 ? - z :
            z >= fluidGridSize.y ? (fluidGridSize.y * 2) - z - 2 :
            z
        );
        int i = x2 + (z2 * fluidGridSize.x);
        return i;
    }
}
