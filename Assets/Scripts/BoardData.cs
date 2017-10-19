using UnityEngine;
using System;

public class BoardData : ScriptableObject {

    public string[] JSONData;

    [HideInInspector]
    public bool usePlayerOneStartElement;

    [HideInInspector]
    public int playerOneStartElement;

    [HideInInspector]
    public bool usePlayerTwoStartElement;

    [HideInInspector]
    public int playerTwoStartElement;
}