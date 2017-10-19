using UnityEngine;
using UnityEditor;

public class CreateProjectFolders : MonoBehaviour {
    
    [MenuItem("Assets/Create Standard Folders")]
    static void CreateStandardFolders()
    {
        NewFolders();
    }

    static void NewFolders()
    {
        NewFolder("Editor");
        NewFolder("GameData");
        NewFolder("Materials");
        NewFolder("Prefabs");
        NewFolder("Scenes");
        NewFolder("Scripts");
        NewFolder("Shaders");
        NewFolder("Textures");
    }

    static void NewFolder(string folderName)
    {
        AssetDatabase.CreateFolder("Assets", folderName);
    }
}
