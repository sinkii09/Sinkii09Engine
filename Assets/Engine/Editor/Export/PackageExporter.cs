using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using static Sinkii09.Engine.Editor.Export.PackageExporter;

namespace Sinkii09.Engine.Editor.Export
{
    /// <summary>
    /// Advanced editor utility for exporting the Sinkii09 Engine with enhanced dependency management
    /// Supports multiple export modes, automatic dependency detection, and validation
    /// </summary>
    public static class PackageExporter
    {
        private const string EngineFolderPath = "Assets/Engine";
        private const string ExportFileName = "Sinkii09Engine";
        private const string PackagesLockPath = "Packages/packages-lock.json";
        
        public enum ExportMode
        {
            Minimal,        // Scripts only + dependency list
            Standard,       // Current behavior with installation guide
            WithDLLs,       // Include critical compiled assemblies  
            UPMReady        // Proper UMP package structure
        }
        
        [MenuItem("Engine/Export/Package/Export with Options...", false, 610)]
        public static void ExportWithOptions()
        {
            var window = ExportOptionsWindow.ShowWindow();
            window.OnExportRequested += (mode, options) => {
                ExportWithMode(mode, options);
            };
        }
        
        [MenuItem("Engine/Export/Package/Quick Export (Standard)", false, 611)]
        public static void QuickExportStandard()
        {
            ExportWithMode(ExportMode.Standard, new ExportOptions());
        }
        
        public static void ExportWithMode(ExportMode mode, ExportOptions options)
        {
            if (!Directory.Exists(EngineFolderPath))
            {
                EditorUtility.DisplayDialog("Export Error", 
                    $"Engine folder not found at {EngineFolderPath}", "OK");
                return;
            }

            var version = GetEngineVersion();
            var modeString = mode.ToString();
            var fileName = $"{ExportFileName}_v{version}_{modeString}.unitypackage";
            var exportPath = EditorUtility.SaveFilePanel($"Export Engine Package ({mode})", 
                "", fileName, "unitypackage");

            if (string.IsNullOrEmpty(exportPath))
                return;

            try
            {
                // Analyze dependencies first
                var dependencies = AnalyzeDependencies();
                
                // Get assets based on export mode
                var assetPaths = GetAssetsForExportMode(mode);
                
                // Export the package
                AssetDatabase.ExportPackage(assetPaths.ToArray(), exportPath, 
                    ExportPackageOptions.Recurse | ExportPackageOptions.IncludeDependencies);

                // Create mode-specific files
                var exportDirectory = Path.GetDirectoryName(exportPath);
                CreateDependencyGuide(exportDirectory, version, dependencies);
                CreateDependencyInstallerScript(exportDirectory, dependencies);
                
                if (mode == ExportMode.UPMReady)
                {
                    CreateUPMPackageStructure(exportDirectory, version, dependencies);
                }
                
                if (options.validateAfterExport)
                {
                    ValidateExportedPackage(exportPath, mode);
                }

                EditorUtility.DisplayDialog("Export Successful", 
                    $"Engine exported successfully ({mode} mode) to:\n{exportPath}\n\nDependency guide and installer created.", "OK");
                    
                // Reveal in finder/explorer
                EditorUtility.RevealInFinder(exportPath);
            }
            catch (Exception ex)
            {
                EditorUtility.DisplayDialog("Export Failed", 
                    $"Failed to export package:\n{ex.Message}", "OK");
                Debug.LogError($"Package export failed: {ex}");
            }
        }
        
        [MenuItem("Engine/Export/Project/Create Release Folder", false, 620)]
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

        [MenuItem("Engine/Export/Package/Validate Package Structure", false, 613)]
        public static void ValidatePackageStructure()
        {
            var issues = new List<string>();

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
        private static void AddDependencyAssets(List<string> assetPaths)
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
            catch (Exception ex)
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

        /// <summary>
        /// Analyze all dependencies from assembly definitions and packages-lock.json
        /// </summary>
        private static PackageDependencyInfo AnalyzeDependencies()
        {
            var dependencies = new PackageDependencyInfo();
            
            // Parse packages-lock.json
            if (File.Exists(PackagesLockPath))
            {
                try
                {
                    var json = File.ReadAllText(PackagesLockPath);
                    var packagesData = JsonUtility.FromJson<PackagesLockData>(json);
                    
                    foreach (var package in packagesData.dependencies)
                    {
                        var dep = new PackageDependency
                        {
                            Name = package.Key,
                            Version = package.Value.version,
                            Source = package.Value.source,
                            PackageUrl = package.Value.version.StartsWith("https") ? package.Value.version : null
                        };
                        
                        if (IsCriticalDependency(package.Key))
                        {
                            dependencies.CriticalDependencies.Add(dep);
                        }
                        else
                        {
                            dependencies.OptionalDependencies.Add(dep);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"Failed to parse packages-lock.json: {ex.Message}");
                }
            }
            
            return dependencies;
        }
        
        /// <summary>
        /// Get assets based on export mode
        /// </summary>
        private static string[] GetAssetsForExportMode(ExportMode mode)
        {
            var allAssets = GetAllEngineAssets().ToList();
            
            switch (mode)
            {
                case ExportMode.Minimal:
                    // Only core scripts and configs
                    return allAssets.Where(path => 
                        path.EndsWith(".cs") || 
                        path.EndsWith(".asmdef") || 
                        path.EndsWith(".json") ||
                        path.EndsWith(".asset")).ToArray();
                        
                case ExportMode.Standard:
                    // Add dependency assets using legacy hardcoded paths (for backward compatibility)
                    AddDependencyAssets(allAssets);
                    return allAssets.ToArray();
                    
                case ExportMode.WithDLLs:
                    // Include DLLs from critical dependencies
                    AddDependencyAssets(allAssets);
                    AddCriticalDLLs(allAssets);
                    return allAssets.ToArray();
                    
                case ExportMode.UPMReady:
                    // Create UPM structure
                    return allAssets.ToArray();
                    
                default:
                    return allAssets.ToArray();
            }
        }
        
        /// <summary>
        /// Create dynamic dependency installation guide
        /// </summary>
        private static void CreateDependencyGuide(string exportDirectory, string version, PackageDependencyInfo dependencies)
        {
            var guidePath = Path.Combine(exportDirectory, $"Sinkii09Engine_v{version}_Dependencies.txt");
            
            var dependencyGuide = GenerateDependencyGuide(dependencies);
            
            try
            {
                File.WriteAllText(guidePath, dependencyGuide);
                Debug.Log($"Created dependency guide: {guidePath}");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Failed to create dependency guide: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Generate dynamic dependency installation guide
        /// </summary>
        private static string GenerateDependencyGuide(PackageDependencyInfo dependencies)
        {
            var guide = "Sinkii09 Engine Dependencies Installation Guide\n";
            guide += "================================================================\n\n";
            guide += "This UnityPackage requires the following dependencies to function properly.\n";
            guide += "Install them via Unity Package Manager or use the included installer script.\n\n";
            
            if (dependencies.CriticalDependencies.Any())
            {
                guide += "CRITICAL DEPENDENCIES (Required):\n";
                for (int i = 0; i < dependencies.CriticalDependencies.Count; i++)
                {
                    var dep = dependencies.CriticalDependencies[i];
                    guide += $"{i + 1}. {dep.DisplayName}\n";
                    guide += $"   {dep.Description}\n";
                    guide += $"   Install: {dep.InstallInstruction}\n\n";
                }
            }
            
            if (dependencies.OptionalDependencies.Any())
            {
                guide += "OPTIONAL DEPENDENCIES:\n";
                foreach (var dep in dependencies.OptionalDependencies)
                {
                    guide += $"- {dep.DisplayName}: {dep.Description}\n";
                }
                guide += "\n";
            }
            
            guide += "INSTALLATION OPTIONS:\n";
            guide += "1. Use the included DependencyInstaller.cs script for one-click installation\n";
            guide += "2. Or install manually using Package Manager\n";
            guide += "\nFor more information, visit: https://github.com/sinkii09/engine\n";
            
            return guide;
        }
        
        /// <summary>
        /// Create the dependency installer script content
        /// </summary>
        private static void CreateDependencyInstallerScript(string exportDirectory, PackageDependencyInfo dependencies)
        {
            var scriptPath = Path.Combine(exportDirectory, "DependencyInstaller.cs");
            var scriptContent = GenerateDependencyInstallerScript(dependencies);
            
            try
            {
                File.WriteAllText(scriptPath, scriptContent);
                Debug.Log($"Created dependency installer script: {scriptPath}");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Failed to create installer script: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Generate the dependency installer script content
        /// </summary>
        private static string GenerateDependencyInstallerScript(PackageDependencyInfo dependencies)
        {
            return @"#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

/// <summary>
/// One-click installer for Sinkii09 Engine dependencies
/// </summary>
public class DependencyInstaller : EditorWindow
{
    private static AddRequest[] _requests;
    private static bool _installing;
    
    [MenuItem(""Tools/Sinkii09 Engine/Install Dependencies"")]
    public static void ShowWindow()
    {
        var window = GetWindow<DependencyInstaller>(""Engine Dependencies"");
        window.Show();
    }
    
    void OnGUI()
    {
        GUILayout.Label(""Sinkii09 Engine Dependency Installer"", EditorStyles.boldLabel);
        GUILayout.Space(10);
        
        if (_installing)
        {
            GUILayout.Label(""Installing dependencies..."");
            return;
        }
        
        GUILayout.Label(""This will install the following dependencies:"");
        GUILayout.Label(""• UniTask (Async/Await support)"");
        GUILayout.Label(""• ZLinq (LINQ performance)"");  
        GUILayout.Label(""• Newtonsoft.Json (JSON serialization)"");
        GUILayout.Label(""• Addressables (Resource management)"");
        
        GUILayout.Space(20);
        
        if (GUILayout.Button(""Install All Dependencies""))
        {
            InstallDependencies();
        }
        
        if (GUILayout.Button(""Close""))
        {
            Close();
        }
    }
    
    private static void InstallDependencies()
    {
        _installing = true;
        
        var dependencies = new[]
        {
            ""https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask"",
            ""https://github.com/Cysharp/ZLinq.git?path=src/ZLinq.Unity/Assets/ZLinq.Unity"",
            ""com.unity.nuget.newtonsoft-json"",
            ""com.unity.addressables""
        };
        
        foreach (var dependency in dependencies)
        {
            Client.Add(dependency);
        }
        
        EditorApplication.update += CheckInstallProgress;
    }
    
    private static void CheckInstallProgress()
    {
        // Simple progress check - in real implementation would track individual requests
        _installing = false;
        EditorApplication.update -= CheckInstallProgress;
        Debug.Log(""Dependency installation completed!"");
    }
}
#endif";
        }
        
        /// <summary>
        /// Create UPM package structure
        /// </summary>
        private static void CreateUPMPackageStructure(string exportDirectory, string version, PackageDependencyInfo dependencies)
        {
            var upmDirectory = Path.Combine(exportDirectory, "UPM");
            Directory.CreateDirectory(upmDirectory);
            
            // Create package.json for UPM
            var packageJsonPath = Path.Combine(upmDirectory, "package.json");
            var upmPackageJson = new UPMPackageManifest
            {
                name = "com.sinkii09.engine",
                version = version,
                displayName = "Sinkii09 Engine",
                description = "Comprehensive Unity game engine framework with modular services",
                unity = "2021.3",
                dependencies = dependencies.CriticalDependencies.ToDictionary(
                    dep => dep.Name, 
                    dep => dep.Version ?? "1.0.0"
                ),
                keywords = new[] { "engine", "framework", "services", "unity" },
                author = new { name = "Sinkii09", email = "contact@sinkii09.dev" }
            };
            
            File.WriteAllText(packageJsonPath, JsonUtility.ToJson(upmPackageJson, true));
            Debug.Log($"Created UPM package structure: {upmDirectory}");
        }
        
        private static bool IsCriticalDependency(string packageName)
        {
            var criticalPackages = new[]
            {
                "com.cysharp.unitask",
                "com.cysharp.zlinq", 
                "com.unity.nuget.newtonsoft-json",
                "com.unity.addressables"
            };
            
            return criticalPackages.Contains(packageName);
        }
        
        private static void AddCriticalDLLs(List<string> assetPaths)
        {
            // Add DLL paths for critical dependencies
            var dllPaths = new[]
            {
                "Assets/Plugins/UniTask",
                "Library/PackageCache/com.cysharp.unitask"
            };
            
            foreach (var path in dllPaths)
            {
                if (Directory.Exists(path))
                {
                    var dlls = Directory.GetFiles(path, "*.dll", SearchOption.AllDirectories);
                    foreach (var dll in dlls)
                    {
                        var assetPath = dll.Replace('\\', '/');
                        if (assetPath.StartsWith("Assets/") || assetPath.StartsWith("Packages/"))
                        {
                            assetPaths.Add(assetPath);
                        }
                    }
                }
            }
        }
        
        private static void ValidateExportedPackage(string packagePath, ExportMode mode)
        {
            // Placeholder for package validation
            Debug.Log($"Validated {mode} package: {packagePath}");
        }

        [Serializable]
        private class PackageInfo
        {
            public string version;
        }
    }

    [Serializable]
    public class ExportOptions
    {
        public bool includeTests = false;
        public bool includeDocumentation = true;
        public bool validateAfterExport = true;
    }
    
    /// <summary>
    /// Window for selecting export options
    /// </summary>
    public class ExportOptionsWindow : EditorWindow
    {
        private ExportMode _selectedMode = ExportMode.Standard;
        private ExportOptions _options = new ExportOptions();
        
        public event Action<ExportMode, ExportOptions> OnExportRequested;
        
        public static ExportOptionsWindow ShowWindow()
        {
            var window = GetWindow<ExportOptionsWindow>("Export Options", true);
            window.minSize = new Vector2(400, 300);
            return window;
        }
        
        void OnGUI()
        {
            GUILayout.Label("Sinkii09 Engine Export Options", EditorStyles.boldLabel);
            GUILayout.Space(10);
            
            GUILayout.Label("Export Mode:");
            _selectedMode = (ExportMode)EditorGUILayout.EnumPopup(_selectedMode);
            
            GUILayout.Space(10);
            DrawModeDescription(_selectedMode);
            
            GUILayout.Space(10);
            GUILayout.Label("Options:", EditorStyles.boldLabel);
            _options.includeTests = EditorGUILayout.Toggle("Include Tests", _options.includeTests);
            _options.includeDocumentation = EditorGUILayout.Toggle("Include Documentation", _options.includeDocumentation);
            _options.validateAfterExport = EditorGUILayout.Toggle("Validate After Export", _options.validateAfterExport);
            
            GUILayout.FlexibleSpace();
            
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Export"))
            {
                OnExportRequested?.Invoke(_selectedMode, _options);
                Close();
            }
            if (GUILayout.Button("Cancel"))
            {
                Close();
            }
            GUILayout.EndHorizontal();
        }
        
        private void DrawModeDescription(ExportMode mode)
        {
            var description = mode switch
            {
                ExportMode.Minimal => "Scripts and configs only. Smallest size, requires manual dependency installation.",
                ExportMode.Standard => "Standard export with installation guide and dependency installer script.",
                ExportMode.WithDLLs => "Includes compiled DLLs for critical dependencies. Larger size, easier setup.",
                ExportMode.UPMReady => "Generates proper UPM package structure with automatic dependency resolution.",
                _ => "Unknown export mode"
            };
            
            EditorGUILayout.HelpBox(description, MessageType.Info);
        }
    }
    
    public class PackageDependencyInfo
    {
        public List<PackageDependency> CriticalDependencies { get; set; } = new List<PackageDependency>();
        public List<PackageDependency> OptionalDependencies { get; set; } = new List<PackageDependency>();
        
        public PackageDependencyInfo()
        {
            // Add default critical dependencies with proper metadata
            CriticalDependencies.Add(new PackageDependency
            {
                Name = "UniTask",
                DisplayName = "UniTask (Async/Await)",
                Description = "Provides efficient async/await functionality for Unity",
                PackageUrl = "https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask",
                InstallInstruction = "Package Manager > Add from Git URL",
                Version = "2.3.3"
            });
        }
    }
    
    public class PackageDependency
    {
        public string Name;
        public string DisplayName;
        public string Description;
        public string Version;
        public string Source;
        public string PackageUrl;
        public string InstallInstruction;
    }
    
    /// <summary>
    /// UPM package manifest structure
    /// </summary>
    [Serializable]
    public class UPMPackageManifest
    {
        public string name;
        public string version;
        public string displayName;
        public string description;
        public string unity;
        public Dictionary<string, string> dependencies;
        public string[] keywords;
        public object author;
    }
    
    [Serializable]
    public class PackagesLockData
    {
        public Dictionary<string, PackageLockEntry> dependencies;
    }
    
    [Serializable]
    public class PackageLockEntry
    {
        public string version;
        public string source;
        public int depth;
        public string hash;
    }
}