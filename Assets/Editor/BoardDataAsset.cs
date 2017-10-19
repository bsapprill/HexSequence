//Credit: http://wiki.unity3d.com/index.php?title=CreateScriptableObjectAsset

using UnityEngine;
using UnityEditor;

public class BoardDataAsset
{
    [MenuItem("Assets/Create/Board Data")]
    public static void CreateAsset()
    {
        ScriptableObjectUtility.CreateAsset<BoardData>();
    }
}
