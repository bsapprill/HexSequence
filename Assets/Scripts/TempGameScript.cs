/*
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using System;
using System.Collections;
using System.Collections.Generic;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class TempGameScript : MonoBehaviour
{

    #region Defaults

    void Awake()
    {
        playerOneToggles = UI.playerOneColorPanel.GetComponentsInChildren<Toggle>();
        playerTwoToggles = UI.playerTwoColorPanel.GetComponentsInChildren<Toggle>();

        UI.playerTwoColorPanel.SetActive(false);

        BuildBoard();

        PlayerOne = new Player(playerOneMaterial, boardData.playerOneStartElement, hexCells, playerOneColor, playerOneLayer);

        PlayerTwo = new Player(playerTwoMaterial, boardData.playerTwoStartElement, hexCells, playerTwoColor, playerTwoLayer);

        fsm.Add(PlayerState.PLAYER_ONE_INITIALIZE, new Action(PlayerOneInitialize));
        fsm.Add(PlayerState.PLAYER_TWO_INITIALIZE, new Action(PlayerTwoInitialize));
        fsm.Add(PlayerState.PLAYER_ONE_ACTIVE, new Action(PlayerOneActive));
        fsm.Add(PlayerState.PLAYER_TWO_ACTIVE, new Action(PlayerTwoActive));
        fsm.Add(PlayerState.PLAYER_ONE_EXIT, new Action(PlayerOneExit));
        fsm.Add(PlayerState.PLAYER_TWO_EXIT, new Action(PlayerTwoExit));

        osm.Add(ObjectHandler.Idle, new Action(Idle));
        osm.Add(ObjectHandler.InitiateUnitRotate, new Action(InitiateUnitRotate));
        osm.Add(ObjectHandler.InitiateUnitSpawn, new Action(InitiateUnitSpawn));
        osm.Add(ObjectHandler.RotateUnit, new Action(RotateUnit));
        osm.Add(ObjectHandler.SpawnUnit, new Action(SpawnUnit));
        osm.Add(ObjectHandler.ExitRotateUnit, new Action(ExitRotateUnit));
        osm.Add(ObjectHandler.ExitUnitSpawn, new Action(ExitUnitSpawn));
        osm.Add(ObjectHandler.PathUnit, new Action(PathUnit));
        osm.Add(ObjectHandler.MoveUnit, new Action(MoveUnit));
        osm.Add(ObjectHandler.InitializeCombat, new Action(InitalizeCombat));
        osm.Add(ObjectHandler.Combat, new Action(Combat));
        osm.Add(ObjectHandler.ConfirmCounterAttack, new Action(ConfirmCounterAttack));
        osm.Add(ObjectHandler.UpgradeNode, new Action(UpgradeNode));

        currentPlayerState = PlayerState.PLAYER_ONE_INITIALIZE;
        currentObjectState = ObjectHandler.Idle;
    }

    void Start()
    {

    }

    void Update()
    {
        fsm[currentPlayerState].Invoke();



        if (Input.GetKeyDown(KeyCode.F))
        {

            Debug.Log(currentPathElements.Length);
        }
    }

    #endregion Defaults

    #region State Machines



    #endregion

    #region Generate Game Board

    public void BuildBoard()
    {
        #region Initializations

        GetComponent<MeshFilter>().mesh = hexBoardMesh = new Mesh();
        hexBoardMesh.name = "Hex Board";
        hexBoardCollider = gameObject.AddComponent<MeshCollider>();

        boardColors = new List<Color>();
        boardTriangles = new List<int>();
        boardUVs = new List<Vector2>();
        boardVertices = new List<Vector3>();

        hexCells = new HexCell[boardSizeX * boardSizeY];

        for (int i = 0; i < hexCells.Length; i++)
        {
            hexCells[i] = JsonUtility.FromJson<HexCell>(boardData.JSONData[i]);
        }

        #endregion List Initializations

        #region Clear Data

        hexBoardMesh.Clear();
        boardColors.Clear();
        boardTriangles.Clear();
        boardUVs.Clear();
        boardVertices.Clear();

        #endregion

        #region The Build Loop

        for (int i = 0; i < hexCells.Length; i++)
        {
            int vertexIndex = boardVertices.Count;

            if (hexCells[i].hasNode)
            {
                hexCells[i].cellNode = Instantiate(nodePrefab, hexCells[i].cellPosition, Quaternion.identity, transform) as GameObject;
                hexCells[i].cellNode.gameObject.GetComponent<Node>().nodeCellElement = ReturnHexCellElement(hexCells[i]);
            }

            for (int j = 0; j < HexData.corners.Length; j++)
            {
                boardVertices.Add(HexData.corners[j] + hexCells[i].cellPosition);
                boardUVs.Add(new Vector2(HexData.UVs[j].x / 2, HexData.UVs[j].y));

                if (hexCells[i].hasObstacle && hexCells[i].hasNode != true)
                    boardColors.Add(Color.black);
                else
                    boardColors.Add(Color.white);

                if (j == 0)
                    continue;

                boardTriangles.Add(vertexIndex);
                boardTriangles.Add(vertexIndex + j);
                if (j == 6)
                    boardTriangles.Add(vertexIndex + 1);
                else
                    boardTriangles.Add(vertexIndex + j + 1);
            }
        }

        #endregion

        #region Set Mesh Values

        hexBoardMesh.vertices = boardVertices.ToArray();
        hexBoardMesh.colors = boardColors.ToArray();
        hexBoardMesh.uv = boardUVs.ToArray();
        hexBoardMesh.triangles = boardTriangles.ToArray();
        hexBoardMesh.RecalculateNormals();

        hexBoardCollider.sharedMesh = hexBoardMesh;

        #endregion        

        EditorUtility.SetDirty(boardData);
    }

    #region Variables

    Mesh hexBoardMesh;
    MeshCollider hexBoardCollider;

    List<Color> boardColors;
    List<int> boardTriangles;
    List<Vector2> boardUVs;
    List<Vector3> boardVertices;

    public BoardData boardData;

    HexCell[] hexCells;

    public int boardSizeX;
    public int boardSizeY;

    public GameObject nodePrefab;

    #endregion

    #endregion

    public class HexCell
    {
        #region Variables

        #region Cube Coordinates

        public int cubeX;
        public int cubeY;
        public int cubeZ;

        #endregion

        public bool hasNode;
        public bool hasObstacle;
        public bool hasUnit;

        public GameObject cellNode;
        public GameObject cellUnit;

        #region Pathfinding values

        public int gCost;
        public int hCost;
        public int fCost
        {
            get { return gCost + hCost; }
        }

        public int parentElement;

        #endregion


        public Vector3 cellPosition;

        #endregion

        #region Constructors

        public HexCell(Vector3 position)
        {
            cellPosition = position;

            #region Constructs Cube Coordinates

            float x = position.x / (HexData.innerRadius * 2f);
            float y = -x;

            float offset = position.z / (HexData.outerRadius * 3f);
            x -= offset;
            y -= offset;

            int iX = Mathf.RoundToInt(x);
            int iY = Mathf.RoundToInt(y);
            int iZ = Mathf.RoundToInt(-x - y);

            if (iX + iY + iZ != 0)
            {
                float dX = Mathf.Abs(x - iX);
                float dY = Mathf.Abs(y - iY);
                float dZ = Mathf.Abs(-x - y - iZ);

                if (dX > dY && dX > dZ)
                {
                    iX = -iY - iZ;
                }
                else if (dZ > dY)
                {
                    iZ = -iX - iY;
                }
            }

            #endregion

            cubeX = iX;
            cubeY = iY;
            cubeZ = iZ;
        }

        public HexCell(Vector3 position, GameObject node)
        {
            cellPosition = position;

            cellNode = node;

            #region Constructs Cube Coordinates

            float x = position.x / (HexData.innerRadius * 2f);
            float y = -x;

            float offset = position.z / (HexData.outerRadius * 3f);
            x -= offset;
            y -= offset;

            int iX = Mathf.RoundToInt(x);
            int iY = Mathf.RoundToInt(y);
            int iZ = Mathf.RoundToInt(-x - y);

            if (iX + iY + iZ != 0)
            {
                float dX = Mathf.Abs(x - iX);
                float dY = Mathf.Abs(y - iY);
                float dZ = Mathf.Abs(-x - y - iZ);

                if (dX > dY && dX > dZ)
                {
                    iX = -iY - iZ;
                }
                else if (dZ > dY)
                {
                    iZ = -iX - iY;
                }
            }

            #endregion

            cubeX = iX;
            cubeY = iY;
            cubeZ = iZ;
        }

        #endregion

        #region Functions

        #endregion
    }

    public static class HexData
    {
        public const float outerRadius = 10f;
        public const float innerRadius = outerRadius * 0.866f;

        public static Vector3[] corners =
        {
        new Vector3(0f,0f,0f),
        new Vector3(0f,0f,outerRadius),
        new Vector3(innerRadius, 0f, 0.5f*outerRadius),
        new Vector3(innerRadius, 0f, -0.5f*outerRadius),
        new Vector3(0f,0f,-outerRadius),
        new Vector3(-innerRadius, 0f, -0.5f*outerRadius),
        new Vector3(-innerRadius, 0f, 0.5f*outerRadius)
        };

        public static Vector2[] UVs =
        {
        new Vector2(0.5f, 0.5f),
        new Vector2(0.5f, 1.0f),
        new Vector2(1.0f, 0.75f),
        new Vector2(1.0f, 0.25f),
        new Vector2(0.5f, 0f),
        new Vector2(0f, 0.25f),
        new Vector2(0f, 0.75f)
        };
    }
}
//I am going to organize the code with the first functionality being put at the bottom

*/