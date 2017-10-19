using UnityEngine;
using UnityEditor;
using System.Collections;
using System.Collections.Generic;

public class CellChanger : EditorWindow
{
    GameScript gS;

    const string GenerateHexCellDataTooltip = "Resets hexCell array. \n" +
                                              "Resets JSON array. \n\n" +
                                              "Creates new Hex Cells for board size. \n" +
                                              "Assigns each cells data to a JSON string. \n" +
                                              "Saves the new JSON data to BoardData.";

    public bool cellChangeToggle = false;
    public bool[] boolStates = new bool[8] { false, false, false, false, false, false, false, false };
    string[] toggleNames = new string[8] { "Set Obstacle", "Remove Obstacle", "Set Node", "Remove Node",
                                            "Player One Start", "Player Two Start", "Remove Player One Start", "Remove Player Two Start" };

    [MenuItem("Window/Cell Changer")]
    static void Init()
    {
        CellChanger window = (CellChanger)GetWindow(typeof(CellChanger));
        window.Show();
    }

    void OnGUI()
    {
        if (gS == null)
            gS = GameObject.Find("GO_Main").GetComponent<GameScript>();

        EditorGUILayout.BeginVertical();

        if (GUILayout.Button(new GUIContent("Generate Hex Cell Data", GenerateHexCellDataTooltip), GUILayout.MaxWidth(300)))
        {
            gS.GenerateHexCellData();
        }

        if (GUILayout.Button("Remove Hex Board Data", GUILayout.MaxWidth(300)))
        {
            gS.RemoveHexBoardData();
        }

        if (GUILayout.Button("Build Hex Board", GUILayout.MaxWidth(300)))
        {
            gS.BuildBoard();
        }

        cellChangeToggle = EditorGUILayout.BeginToggleGroup("Change Cells", cellChangeToggle);
        ToggleMethod(toggleNames);
        EditorGUILayout.EndToggleGroup();

        EditorGUILayout.EndVertical();        
    }

    #region Toggle Group Functions

    void ToggleMethod(string[] names)
    {
        for (int i = 0; i < names.Length; i++)
        {
            HandleToggleChange(names[i], i);
        }
    }

    void HandleToggleChange(string toggleName, int i)
    {
        EditorGUI.BeginChangeCheck();
        boolStates[i] = EditorGUILayout.Toggle(toggleName, boolStates[i]);
        if (EditorGUI.EndChangeCheck())
        {
            SetBoolStates(i);
        }
    }

    void SetBoolStates(int element)
    {
        for (int i = 0; i < 8; i++)
        {
            if (i == element)
                continue;
            else
                boolStates[i] = false;
        }
    }

    #endregion
}
