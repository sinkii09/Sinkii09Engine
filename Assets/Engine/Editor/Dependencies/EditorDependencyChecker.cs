using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

namespace Sinkii09.Engine.Editor.Dependencies
{
    /// <summary>
    /// Static editor-only dependency checker that works without runtime services
    /// </summary>
    public static class EditorDependencyChecker
    {
        private static readonly EditorDependencyDefinition[] DefaultDependencies = new[]
        {
            new EditorDependencyDefinition(
                "com.cysharp.unitask", 
                "UniTask", 
                "Modern async/await patterns",
                EditorPackageProviderType.Git,
                gitUrl: "https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask",
                assemblyNames: new[] { "UniTask" },
                folderPaths: new[] { "Assets/Plugins/UniTask", "Packages/com.cysharp.unitask" }
            ),
            new EditorDependencyDefinition(
                "com.unity.addressables",
                "Addressables",
                "Advanced resource management",
                EditorPackageProviderType.UPM,
                version: "2.2.2",
                assemblyNames: new[] { "Unity.Addressables" },
                folderPaths: new[] { "Packages/com.unity.addressables" }
            ),
            new EditorDependencyDefinition(
                "com.unity.nuget.newtonsoft-json",
                "Newtonsoft.Json",
                "JSON serialization",
                EditorPackageProviderType.UPM,
                version: "3.2.1",
                assemblyNames: new[] { "Newtonsoft.Json" },
                folderPaths: new[] { "Packages/com.unity.nuget.newtonsoft-json" }
            ),
            new EditorDependencyDefinition(
                "com.demigiant.dotween",
                "DOTween",
                "Animation and tweening system",
                EditorPackageProviderType.AssetStore,
                assetStoreUrl: "https://assetstore.unity.com/packages/tools/animation/dotween-hotween-v2-27676",
                assemblyNames: new[] { "DOTween" },
                folderPaths: new[] { "Assets/Plugins/Demigiant/DOTween", "Assets/DOTween", "Assets/Plugins/DOTween" }
            ),
            new EditorDependencyDefinition(
                "com.cysharp.r3",
                "R3 (Reactive Extensions)",
                "Reactive Extensions for Unity",
                EditorPackageProviderType.Git,
                gitUrl: "https://github.com/Cysharp/R3.git?path=src/R3.Unity/Assets/R3.Unity",
                assemblyNames: new[] { "R3" },
                folderPaths: new[] { "Assets/Plugins/R3", "Packages/com.cysharp.r3" },
                isRequired: false
            )
        };

        public static EditorDependencyCheckResult CheckAllDependencies()
        {
            var result = new EditorDependencyCheckResult();
            var statuses = new List<EditorDependencyStatus>();

            foreach (var dependency in DefaultDependencies)
            {
                var status = CheckSingleDependency(dependency);
                statuses.Add(status);

                if (status.IsInstalled)
                {
                    result.InstalledCount++;
                }
                else
                {
                    result.MissingCount++;
                    if (dependency.IsRequired)
                    {
                        result.RequiredMissingCount++;
                    }
                }
            }

            result.TotalCount = DefaultDependencies.Length;
            result.DependencyStatuses = statuses.ToArray();
            result.AllSatisfied = result.RequiredMissingCount == 0;
            result.Summary = GenerateSummary(result);

            return result;
        }

        public static EditorDependencyStatus CheckSingleDependency(EditorDependencyDefinition dependency)
        {
            var status = new EditorDependencyStatus
            {
                Definition = dependency,
                IsInstalled = false,
                ErrorMessage = null,
                DetectedVersion = null,
                InstallationPath = null
            };

            try
            {
                // Check assemblies first (fastest)
                if (CheckAssemblyInstallation(dependency, out var detectedVersion, out var installPath))
                {
                    status.IsInstalled = true;
                    status.DetectedVersion = detectedVersion;
                    status.InstallationPath = installPath;
                    return status;
                }

                // Check folder paths
                if (CheckFolderInstallation(dependency, out installPath))
                {
                    status.IsInstalled = true;
                    status.InstallationPath = installPath;
                    return status;
                }

                // Check specific files
                if (CheckFileInstallation(dependency, out installPath))
                {
                    status.IsInstalled = true;
                    status.InstallationPath = installPath;
                    return status;
                }

                // For UPM packages, also check Package Manager
                if (dependency.ProviderType == EditorPackageProviderType.UPM || 
                    dependency.ProviderType == EditorPackageProviderType.Git)
                {
                    if (CheckUpmInstallation(dependency, out detectedVersion))
                    {
                        status.IsInstalled = true;
                        status.DetectedVersion = detectedVersion;
                        status.InstallationPath = "Package Manager";
                        return status;
                    }
                }
            }
            catch (Exception ex)
            {
                status.ErrorMessage = $"Check failed: {ex.Message}";
                Debug.LogError($"[EditorDependencyChecker] Error checking {dependency.PackageId}: {ex.Message}");
            }

            return status;
        }

        public static void InstallDependency(EditorDependencyDefinition dependency, Action<bool, string> onComplete = null)
        {
            try
            {
                switch (dependency.ProviderType)
                {
                    case EditorPackageProviderType.UPM:
                    case EditorPackageProviderType.Git:
                        InstallUpmPackage(dependency, onComplete);
                        break;

                    case EditorPackageProviderType.AssetStore:
                        InstallAssetStorePackage(dependency, onComplete);
                        break;

                    case EditorPackageProviderType.NuGet:
                        InstallNuGetPackage(dependency, onComplete);
                        break;

                    default:
                        onComplete?.Invoke(false, $"Unknown provider type: {dependency.ProviderType}");
                        break;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[EditorDependencyChecker] Failed to install {dependency.PackageId}: {ex.Message}");
                onComplete?.Invoke(false, ex.Message);
            }
        }

        private static bool CheckAssemblyInstallation(EditorDependencyDefinition dependency, out string version, out string path)
        {
            version = null;
            path = null;

            if (dependency.AssemblyNames == null || dependency.AssemblyNames.Length == 0)
                return false;

            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            foreach (var assemblyName in dependency.AssemblyNames)
            {
                var assembly = assemblies.FirstOrDefault(a => 
                    a.GetName().Name.Equals(assemblyName, StringComparison.OrdinalIgnoreCase) ||
                    a.GetName().Name.Contains(assemblyName));

                if (assembly != null)
                {
                    version = assembly.GetName().Version?.ToString();
                    path = "Assembly";
                    return true;
                }
            }

            return false;
        }

        private static bool CheckFolderInstallation(EditorDependencyDefinition dependency, out string path)
        {
            path = null;

            if (dependency.FolderPaths == null || dependency.FolderPaths.Length == 0)
                return false;

            foreach (var folderPath in dependency.FolderPaths)
            {
                if (Directory.Exists(folderPath))
                {
                    path = folderPath;
                    return true;
                }
            }

            return false;
        }

        private static bool CheckFileInstallation(EditorDependencyDefinition dependency, out string path)
        {
            path = null;

            if (dependency.FilePaths == null || dependency.FilePaths.Length == 0)
                return false;

            foreach (var filePath in dependency.FilePaths)
            {
                if (File.Exists(filePath))
                {
                    path = filePath;
                    return true;
                }
            }

            return false;
        }

        private static bool CheckUpmInstallation(EditorDependencyDefinition dependency, out string version)
        {
            version = null;

            try
            {
                var listRequest = Client.List(true); // Include local packages
                // Note: This is synchronous for simplicity in editor context
                while (!listRequest.IsCompleted)
                {
                    System.Threading.Thread.Sleep(10);
                }

                if (listRequest.Status == StatusCode.Success)
                {
                    var package = listRequest.Result.FirstOrDefault(p => p.name == dependency.PackageId);
                    if (package != null)
                    {
                        version = package.version;
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[EditorDependencyChecker] UMP check failed for {dependency.PackageId}: {ex.Message}");
            }

            return false;
        }

        private static void InstallUpmPackage(EditorDependencyDefinition dependency, Action<bool, string> onComplete)
        {
            var identifier = dependency.GetInstallIdentifier();
            Debug.Log($"[EditorDependencyChecker] Installing UPM package: {identifier}");

            var addRequest = Client.Add(identifier);
            
            EditorApplication.CallbackFunction checkProgress = null;
            checkProgress = () =>
            {
                if (addRequest.IsCompleted)
                {
                    EditorApplication.update -= checkProgress;
                    
                    if (addRequest.Status == StatusCode.Success)
                    {
                        Debug.Log($"[EditorDependencyChecker] Successfully installed: {dependency.PackageId}");
                        onComplete?.Invoke(true, "Installation successful");
                    }
                    else
                    {
                        var error = $"Installation failed: {addRequest.Error?.message ?? "Unknown error"}";
                        Debug.LogError($"[EditorDependencyChecker] {error}");
                        onComplete?.Invoke(false, error);
                    }
                }
            };
            
            EditorApplication.update += checkProgress;
        }

        private static void InstallAssetStorePackage(EditorDependencyDefinition dependency, Action<bool, string> onComplete)
        {
            Debug.LogWarning($"[EditorDependencyChecker] {dependency.PackageId} requires manual Asset Store installation");
            
            if (!string.IsNullOrEmpty(dependency.AssetStoreUrl))
            {
                Application.OpenURL(dependency.AssetStoreUrl);
            }
            
            onComplete?.Invoke(false, "Manual Asset Store installation required");
        }

        private static void InstallNuGetPackage(EditorDependencyDefinition dependency, Action<bool, string> onComplete)
        {
            Debug.LogWarning($"[EditorDependencyChecker] {dependency.PackageId} requires manual NuGet installation");
            onComplete?.Invoke(false, "Manual NuGet installation required");
        }

        private static string GenerateSummary(EditorDependencyCheckResult result)
        {
            if (result.AllSatisfied)
            {
                return $"All {result.TotalCount} dependencies satisfied";
            }
            else
            {
                return $"{result.InstalledCount}/{result.TotalCount} dependencies installed, {result.RequiredMissingCount} required missing";
            }
        }

        public static EditorDependencyDefinition[] GetDefaultDependencies()
        {
            return DefaultDependencies;
        }
    }

    [Serializable]
    public class EditorDependencyCheckResult
    {
        public bool AllSatisfied;
        public int TotalCount;
        public int InstalledCount;
        public int MissingCount;
        public int RequiredMissingCount;
        public string Summary;
        public EditorDependencyStatus[] DependencyStatuses;
    }

    [Serializable]
    public class EditorDependencyStatus
    {
        public EditorDependencyDefinition Definition;
        public bool IsInstalled;
        public string ErrorMessage;
        public string DetectedVersion;
        public string InstallationPath;
    }
}