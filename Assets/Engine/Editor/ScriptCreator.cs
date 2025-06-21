using System.IO;
using UnityEditor;
using UnityEditor.ProjectWindowCallback;
using UnityEngine;

namespace Sinkii09.Engine.Editor
{
    public static class ScriptCreator
    {
        [MenuItem("Assets/Create/Engine/Script", false, 1000)]
        public static void CreateScript()
        {
            FocusProjectWindow();
            // Delay call to ensure Unity updates the selection context
            EditorApplication.delayCall += () =>
            {
                string folderPath = GetActiveFolderPath();
                if (string.IsNullOrEmpty(folderPath))
                {
                    Debug.LogWarning("Script creation cancelled.");
                    return;
                }
                string defaultName = "NewScript.script";
                string path = Path.Combine(folderPath, defaultName);
                ProjectWindowUtil.StartNameEditingIfProjectWindowExists(
                    0,
                    ScriptableObject.CreateInstance<DoCreateScriptAsset>(),
                    path,
                    null,
                    null
                );
            };
        }

        private class DoCreateScriptAsset : EndNameEditAction
        {
            public override void Action(int instanceId, string pathName, string resourceFile)
            {
                File.WriteAllText(pathName, string.Empty);
                AssetDatabase.ImportAsset(pathName);
                ProjectWindowUtil.ShowCreatedAsset(AssetDatabase.LoadAssetAtPath<Object>(pathName));
            }
        }

        private static string GetActiveFolderPath()
        {
            string folderPath = "Assets";
            if (Selection.activeObject != null)
            {
                string assetPath = AssetDatabase.GetAssetPath(Selection.activeObject);
                if (AssetDatabase.IsValidFolder(assetPath))
                {
                    folderPath = assetPath;
                }
                else
                {
                    // If a file is selected, get its containing folder
                    string parent = Path.GetDirectoryName(assetPath);
                    if (!string.IsNullOrEmpty(parent) && AssetDatabase.IsValidFolder(parent))
                        folderPath = parent;
                }
            }

            return folderPath;
        }

        private static void FocusProjectWindow()
        {
            EditorApplication.ExecuteMenuItem("Window/General/Project");
        }
    }
}
