using UnityEngine;
using UnityEditor;
using System.IO;

namespace Sinkii09.Engine.Editor
{
    /// <summary>
    /// Editor utility for exporting the Sinkii09 Engine as a UnityPackage
    /// </summary>
    public static class PackageExporter
    {
        private const string EngineFolderPath = "Assets/Engine";
        private const string ExportFileName = "Sinkii09Engine";
        
        [MenuItem("Engine/Export/Export as UnityPackage")]
        public static void ExportAsUnityPackage()
        {
            if (!Directory.Exists(EngineFolderPath))
            {
                EditorUtility.DisplayDialog("Export Error", 
                    $"Engine folder not found at {EngineFolderPath}", "OK");
                return;
            }

            var version = GetEngineVersion();
            var fileName = $"{ExportFileName}_v{version}.unitypackage";
            var exportPath = EditorUtility.SaveFilePanel("Export Engine Package", 
                "", fileName, "unitypackage");

            if (string.IsNullOrEmpty(exportPath))
                return;

            try
            {
                // Get all assets in the Engine folder
                var assetPaths = GetAllEngineAssets();
                
                // Add dependency packages if they exist in the project
                var allAssets = new System.Collections.Generic.List<string>(assetPaths);
                AddDependencyAssets(allAssets);
                
                // Export the package
                AssetDatabase.ExportPackage(allAssets.ToArray(), exportPath, 
                    ExportPackageOptions.Recurse | ExportPackageOptions.IncludeDependencies);

                EditorUtility.DisplayDialog("Export Successful", 
                    $"Engine exported successfully to:\n{exportPath}\n\nNote: Users may need to install dependencies manually via Package Manager.", "OK");
                    
                // Create dependency installation guide
                CreateDependencyInstallationGuide(Path.GetDirectoryName(exportPath), version);
                    
                // Reveal in finder/explorer
                EditorUtility.RevealInFinder(exportPath);
            }
            catch (System.Exception ex)
            {
                EditorUtility.DisplayDialog("Export Failed", 
                    $"Failed to export package:\n{ex.Message}", "OK");
                Debug.LogError($"Package export failed: {ex}");
            }
        }
        
        [MenuItem("Engine/Export/Create Release Folder")]
        public static void CreateReleaseFolder()
        {
            var version = GetEngineVersion();
            var releasePath = $"Releases/v{version}";
            
            if (!Directory.Exists("Releases"))
                Directory.CreateDirectory("Releases");
                
            if (Directory.Exists(releasePath))
            {
                if (!EditorUtility.DisplayDialog("Release Exists", 
                    $"Release folder v{version} already exists. Overwrite?", "Yes", "Cancel"))
                    return;
                    
                Directory.Delete(releasePath, true);
            }
            
            Directory.CreateDirectory(releasePath);
            
            // Copy Engine folder to release
            CopyDirectory(EngineFolderPath, $"{releasePath}/Engine");
            
            // Create UnityPackage
            var packagePath = $"{releasePath}/{ExportFileName}_v{version}.unitypackage";
            var assetPaths = GetAllEngineAssets();
            AssetDatabase.ExportPackage(assetPaths, packagePath, 
                ExportPackageOptions.Recurse | ExportPackageOptions.IncludeDependencies);
            
            // Copy documentation
            if (File.Exists("README.md"))
                File.Copy("README.md", $"{releasePath}/README.md", true);
            if (File.Exists("LICENSE"))
                File.Copy("LICENSE", $"{releasePath}/LICENSE", true);
            
            EditorUtility.DisplayDialog("Release Created", 
                $"Release folder created at:\n{Path.GetFullPath(releasePath)}", "OK");
            EditorUtility.RevealInFinder(Path.GetFullPath(releasePath));
        }

        [MenuItem("Engine/Export/Validate Package Structure")]
        public static void ValidatePackageStructure()
        {
            var issues = new System.Collections.Generic.List<string>();

            // Check required files
            if (!File.Exists($"{EngineFolderPath}/package.json"))
                issues.Add("Missing package.json");
            if (!File.Exists($"{EngineFolderPath}/README.md"))
                issues.Add("Missing README.md");
            if (!File.Exists($"{EngineFolderPath}/CHANGELOG.md"))
                issues.Add("Missing CHANGELOG.md");

            // Check assembly definitions
            var runtimeAsmdef = $"{EngineFolderPath}/Runtime/Scripts/Sinkii09.Engine.asmdef";
            var editorAsmdef = $"{EngineFolderPath}/Editor/Sinkii09.Engine.Editor.asmdef";
            var testAsmdef = $"{EngineFolderPath}/Tests/Sinkii09.Engine.Test.asmdef";

            if (!File.Exists(runtimeAsmdef))
                issues.Add("Missing Runtime assembly definition");
            if (!File.Exists(editorAsmdef))
                issues.Add("Missing Editor assembly definition");
            if (!File.Exists(testAsmdef))
                issues.Add("Missing Test assembly definition");

            // Check for meta files
            var criticalPaths = new[] {
                EngineFolderPath,
                $"{EngineFolderPath}/Runtime",
                $"{EngineFolderPath}/Runtime/Scripts",
                $"{EngineFolderPath}/Editor",
                $"{EngineFolderPath}/Tests"
            };

            foreach (var path in criticalPaths)
            {
                if (Directory.Exists(path) && !File.Exists($"{path}.meta"))
                    issues.Add($"Missing .meta file for {path}");
            }

            if (issues.Count == 0)
            {
                EditorUtility.DisplayDialog("Validation Passed", 
                    "Package structure is valid and ready for export!", "OK");
            }
            else
            {
                var message = "Package validation issues found:\n\n" + string.Join("\n", issues);
                EditorUtility.DisplayDialog("Validation Issues", message, "OK");
            }
        }

        private static string[] GetAllEngineAssets()
        {
            var allAssets = AssetDatabase.FindAssets("", new[] { EngineFolderPath });
            var assetPaths = new string[allAssets.Length];
            
            for (int i = 0; i < allAssets.Length; i++)
            {
                assetPaths[i] = AssetDatabase.GUIDToAssetPath(allAssets[i]);
            }
            
            return assetPaths;
        }

        private static string GetEngineVersion()
        {
            var packageJsonPath = $"{EngineFolderPath}/package.json";
            if (!File.Exists(packageJsonPath))
                return "0.0.0";

            try
            {
                var json = File.ReadAllText(packageJsonPath);
                var packageData = JsonUtility.FromJson<PackageInfo>(json);
                return packageData.version ?? "0.0.0";
            }
            catch
            {
                return "0.0.0";
            }
        }

        /// <summary>
        /// Add dependency assets to the export if they exist in the project
        /// </summary>
        private static void AddDependencyAssets(System.Collections.Generic.List<string> assetPaths)
        {
            var dependencyPaths = new[]
            {
                "Assets/Plugins/UniTask",
                "Assets/Plugins/ZLinq", 
                "Assets/Plugins/R3",
                "Packages/com.cysharp.unitask",
                "Packages/com.cysharp.zlinq",
                "Packages/com.cysharp.r3"
            };

            foreach (var path in dependencyPaths)
            {
                if (Directory.Exists(path))
                {
                    var dependencyAssets = AssetDatabase.FindAssets("", new[] { path });
                    foreach (var asset in dependencyAssets)
                    {
                        var assetPath = AssetDatabase.GUIDToAssetPath(asset);
                        if (!assetPaths.Contains(assetPath))
                        {
                            assetPaths.Add(assetPath);
                        }
                    }
                    Debug.Log($"Added dependency assets from: {path}");
                }
            }
        }

        /// <summary>
        /// Create a dependency installation guide for UnityPackage users
        /// </summary>
        private static void CreateDependencyInstallationGuide(string exportDirectory, string version)
        {
            var guidePath = Path.Combine(exportDirectory, $"Sinkii09Engine_v{version}_Dependencies.txt");
            
            var dependencyGuide = @"Sinkii09 Engine Dependencies Installation Guide
================================================================

This UnityPackage requires the following dependencies to function properly.
Install them via Unity Package Manager (Window > Package Manager > + > Add package from git URL):

REQUIRED DEPENDENCIES:
1. UniTask (Async/Await support)
   Git URL: https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask

2. ZLinq (LINQ performance)  
   Git URL: https://github.com/Cysharp/ZLinq.git?path=src/ZLinq.Unity/Assets/ZLinq.Unity

3. R3 (Reactive Extensions)
   Git URL: https://github.com/Cysharp/R3.git?path=src/R3.Unity/Assets/R3.Unity

4. Newtonsoft.Json (JSON serialization)
   Package Manager: Search for ""Newtonsoft Json"" or use:
   com.unity.nuget.newtonsoft-json

5. Addressables (Resource management)  
   Package Manager: Search for ""Addressables"" or use:
   com.unity.addressables

6. DOTween (Animation system) - REQUIRED
   Unity Asset Store: https://assetstore.unity.com/packages/tools/animation/dotween-hotween-v2-27676 (Free)
   Unity Asset Store: https://assetstore.unity.com/packages/tools/animation/dotween-pro-32416 (Pro)

INSTALLATION STEPS:
1. Open Unity Package Manager (Window > Package Manager)
2. Click the ""+"" button in top-left
3. Select ""Add package from git URL""
4. Paste each Git URL above one by one
5. For Newtonsoft.Json and Addressables, search in Package Manager
6. For DOTween, visit the Unity Asset Store links above

OPTIONAL DEPENDENCIES:
- Odin Inspector (Inspector enhancement) - Available from Unity Asset Store

ALTERNATIVE: Use Unity Package Manager (Recommended)
Instead of this UnityPackage, install via UPM for automatic dependency resolution:
Git URL: https://github.com/sinkii09/engine.git

For more information, visit: https://github.com/sinkii09/engine
";

            try
            {
                File.WriteAllText(guidePath, dependencyGuide);
                Debug.Log($"Created dependency guide: {guidePath}");
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"Failed to create dependency guide: {ex.Message}");
            }
        }

        private static void CopyDirectory(string sourceDir, string destinationDir)
        {
            var dir = new DirectoryInfo(sourceDir);
            if (!dir.Exists)
                return;

            DirectoryInfo[] dirs = dir.GetDirectories();
            Directory.CreateDirectory(destinationDir);

            FileInfo[] files = dir.GetFiles();
            foreach (FileInfo file in files)
            {
                // Skip .meta files in source control
                if (file.Extension == ".meta") continue;
                
                string tempPath = Path.Combine(destinationDir, file.Name);
                file.CopyTo(tempPath, true);
            }

            foreach (DirectoryInfo subDir in dirs)
            {
                string tempPath = Path.Combine(destinationDir, subDir.Name);
                CopyDirectory(subDir.FullName, tempPath);
            }
        }

        [System.Serializable]
        private class PackageInfo
        {
            public string version;
        }
    }
}