using UnityEngine;
using UnityEditor;
using System.Collections;

[CustomEditor(typeof(GameScript))]
public class SetCellData : Editor {

    enum ToggleState { SetObstacle, RemoveObstace, SetNode, RemoveNode, PlayerOneStart, PlayerTwoStart,
                       RemovePlayerOneStart, RemovePlayerTwoStart, Off }

    enum ChangeType { Obstacle, Node }

    ToggleState toggleState;

    public CellChanger[] cA;
    CellChanger c;
    GameScript g;

    void OnSceneGUI()
    {
        g = (GameScript)target;

        cA = Resources.FindObjectsOfTypeAll<CellChanger>();

        if(c == null)
            c = cA[0];

        SetToggleState();

        if(Event.current.type == EventType.MouseDown)
        {
            RaycastIntoScene();  
        }        
    }

    void RaycastIntoScene()
    {
        Ray ray = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition);
        RaycastHit hit;

        if(Physics.Raycast(ray, out hit))
        {
            if(hit.collider.tag == "Board")
            {
                int hitElement = g.ReturnHexCellElement(hit.point);

                switch (toggleState)
                {
                    case ToggleState.SetObstacle:

                        HandleChange(hitElement, true, Color.black, ChangeType.Obstacle);

                        break;

                    case ToggleState.RemoveObstace:

                        HandleChange(hitElement, false, Color.white, ChangeType.Obstacle);

                        break;

                    case ToggleState.SetNode:

                        HandleChange(hitElement, true, Color.white, ChangeType.Node);
                        
                        break;

                    case ToggleState.RemoveNode:

                        HandleChange(hitElement, false, Color.white, ChangeType.Node);

                        break;

                    default:

                        break;
                }
            }

            if(hit.collider.tag == "Node")
            {
                int nodeElement = g.ReturnHexCellElement(hit.collider.gameObject.transform.position);

                Debug.Log(nodeElement);

                switch (toggleState)
                {
                    case ToggleState.RemoveNode:

                        HandleChange(nodeElement, false, Color.white, ChangeType.Node);

                        DestroyImmediate(hit.collider.gameObject);

                        break;

                    case ToggleState.PlayerOneStart:

                        g.boardData.playerOneStartElement = nodeElement;

                        break;

                    case ToggleState.RemovePlayerOneStart:

                        g.boardData.playerOneStartElement = new int();

                        break;

                    case ToggleState.PlayerTwoStart:

                        g.boardData.playerTwoStartElement = nodeElement;

                        break;

                    case ToggleState.RemovePlayerTwoStart:

                        g.boardData.playerTwoStartElement = new int();

                        break;

                    default:
                        break;
                }         
            }
        }
    }

    void HandleChange(int element, bool change, Color color, ChangeType type) {

        if(type == ChangeType.Obstacle)
        {
            GameScript.HexCell cell = JsonUtility.FromJson<GameScript.HexCell>(g.boardData.JSONData[element]);
            cell.hasObstacle = change;
            g.boardData.JSONData[element] = JsonUtility.ToJson(cell);

            g.ColorSingleCell(element, color);
        }

        else
        {
            GameScript.HexCell cell = JsonUtility.FromJson<GameScript.HexCell>(g.boardData.JSONData[element]);
            cell.hasNode = change;

            if (change == true)
            {
                cell.cellNode = Instantiate(g.nodePrefab, cell.cellPosition, Quaternion.identity, g.transform) as GameObject;
            }

            g.boardData.JSONData[element] = JsonUtility.ToJson(cell);

            g.ColorSingleCell(element, color);            
        }        
    }

    void SetToggleState()
    {
        if(c.cellChangeToggle == false)
        {
            toggleState = ToggleState.Off;
            return;
        }

        for (int i = 0; i < 8; i++)
        {
            if (c.boolStates[i] == false)
                continue;
            else
                toggleState = SetEnum(i);                
        }
    }

    ToggleState SetEnum(int element)
    {
        ToggleState state = new ToggleState();
        
        switch (element)
        {
            case 0:
                state = ToggleState.SetObstacle;
                break;
            case 1:
                state = ToggleState.RemoveObstace;
                break;
            case 2:
                state = ToggleState.SetNode;
                break;
            case 3:
                state = ToggleState.RemoveNode;
                break;
            case 4:
                state = ToggleState.PlayerOneStart;
                break;
            case 5:
                state = ToggleState.PlayerTwoStart;
                break;
            case 6:
                state = ToggleState.RemovePlayerOneStart;
                break;
            case 7:
                state = ToggleState.RemovePlayerTwoStart;
                break;
            default:
                break;
        }

        return state;
    }
}