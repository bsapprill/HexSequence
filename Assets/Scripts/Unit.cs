using UnityEngine;
using System.Collections;
using System.Collections.Generic;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider))]
public class Unit : GameScript.Combatable
{
    #region Variables
 
    public float hexSizeDivisor;

    public int[] flankArmor;

    //This is an int that represents the hex direction enum in GameScript
    public int unitDirection;

    #region Mesh Data

    Mesh modelMesh;

    #region Mesh Lists

    List<Vector3> meshVertices;
    List<Vector2> meshUVs;
    List<Vector3> meshNormals;
    List<int> triangleIndices;

    #endregion

    #endregion

    #endregion Variables

    #region Defaults

    void Awake()
    {
        GetComponent<MeshFilter>().mesh = modelMesh = new Mesh();

        flankPositions = new Dictionary<Flank, Vector2>();

        meshVertices = new List<Vector3>();
        meshUVs = new List<Vector2>();
        meshNormals = new List<Vector3>();
        triangleIndices = new List<int>();

        BuildHexModel();        
    }
    
    #endregion

    #region Constructors

    #endregion Constructors

    #region Functions

    int ArmorDeducedDamage()
    {
        return 0;
    }
    
    #region Model Generation

    //Function for generation of a 3D hexagon model using the Unity Mesh class
    void BuildHexModel()
    {
        //Generates the top "hexagon" of the mesh
        for (int i = 0; i < 7; i++)
        {
            meshVertices.Add(GameScript.HexData.corners[i] / hexSizeDivisor);

            SetUVsForUnitTop(i, currentTier);

            if (i == 6)
                continue;

            triangleIndices.Add(0);
            triangleIndices.Add(i + 1);
            triangleIndices.Add((i == 5) ? 1 : (i + 2));//The final triangle nees to use the second vertex of the set. All the others use the i + 2 vertex for the third spot
        }

        for (int i = 1; i < 7; i++)
        {
            BuildRectangle(i);
        }

        modelMesh.vertices = meshVertices.ToArray();
        modelMesh.uv = meshUVs.ToArray();
        modelMesh.triangles = triangleIndices.ToArray();
        modelMesh.normals = meshNormals.ToArray();
        modelMesh.RecalculateNormals();
        GetComponent<MeshCollider>().sharedMesh = modelMesh;
    }

    void BuildRectangle(int rectNumber)
    {
        if (rectNumber != 6)
        {
            meshVertices.Add(GameScript.HexData.corners[rectNumber] / hexSizeDivisor);
            meshVertices.Add(GameScript.HexData.corners[rectNumber + 1] / hexSizeDivisor);
            meshVertices.Add((GameScript.HexData.corners[rectNumber + 1] + Vector3.down ) / hexSizeDivisor);
            meshVertices.Add((GameScript.HexData.corners[rectNumber] + Vector3.down) / hexSizeDivisor);
        }
        else
        {
            meshVertices.Add(GameScript.HexData.corners[rectNumber] / hexSizeDivisor);
            meshVertices.Add(GameScript.HexData.corners[1] / hexSizeDivisor);
            meshVertices.Add((GameScript.HexData.corners[1] + Vector3.down ) / hexSizeDivisor);
            meshVertices.Add((GameScript.HexData.corners[rectNumber] + Vector3.down ) / hexSizeDivisor);
        }

        //AddRectNormals();

        meshUVs.Add(new Vector2(0.6f, 1f));
        meshUVs.Add(new Vector2(0.5f, 1f));
        meshUVs.Add(new Vector2(0.5f, 0f));
        meshUVs.Add(new Vector2(0.6f, 0f));

        AddRectTriangles();
    }

    void AddRectNormals()
    {
        int baseIndex = meshVertices.Count - 4;

        meshNormals.Add(ReturnNormal(baseIndex, baseIndex + 2, baseIndex + 1));
        meshNormals.Add(ReturnNormal(baseIndex + 1, baseIndex, baseIndex + 2));
        meshNormals.Add(ReturnNormal(baseIndex + 2, baseIndex, baseIndex + 3));
        meshNormals.Add(ReturnNormal(baseIndex + 3, baseIndex + 2, baseIndex));
    }

    void AddRectTriangles()
    {
        int baseIndex = meshVertices.Count - 4;

        triangleIndices.Add(baseIndex);
        triangleIndices.Add(baseIndex + 2);
        triangleIndices.Add(baseIndex + 1);

        triangleIndices.Add(baseIndex);
        triangleIndices.Add(baseIndex + 3);
        triangleIndices.Add(baseIndex + 2);
    }

    void SetUVsForUnitTop(int currentLoopElement, int unitTier)
    {
        Vector2 adjustedUV = GameScript.HexData.UVs[currentLoopElement];
        adjustedUV.x = ( adjustedUV.x / 10f ) + (( unitTier / 10f ) - 0.1f);

        meshUVs.Add(adjustedUV);
    }

    public void SetUVsForUnitTopFromUnitTier(int unitTier)
    {
        for (int i = 0; i < 7; i++)
        {
            Vector2 adjustedUV = GameScript.HexData.UVs[i];
            adjustedUV.x = (adjustedUV.x / 10f) + ((unitTier / 10f) - 0.1f);

            meshUVs[i] = adjustedUV;
        }

        modelMesh.uv = meshUVs.ToArray();
    }

    //a is the vector of the normal. b and c are the other corners of the triangle
    Vector3 ReturnNormal(int a, int b, int c)
    {
        Vector3 sideOne = meshVertices[b] - meshVertices[a];
        Vector3 sideTwo = meshVertices[c] - meshVertices[a];

        Vector3 normal = Vector3.Cross(sideOne, sideTwo);

        normal.Normalize();

        return normal;
    }

    #endregion

    public void InitializeUnit(int level)
    {
        currentTier = level;

        hitPoints = level;

        flankArmor = new int[6];

        Debug.Log(flankArmor.Length);

        flankArmor[0] = currentTier - 2;
        flankArmor[1] = flankArmor[5] = currentTier - 3;
        flankArmor[2] = flankArmor[4] = currentTier - 4;
        flankArmor[3] = currentTier - 5;

        for (int i = 0; i < 6; i++)
        {
            if (flankArmor[i] < 0)
            {
                flankArmor[i] = 0;
            }
        }
    }

    #endregion Functions
}
