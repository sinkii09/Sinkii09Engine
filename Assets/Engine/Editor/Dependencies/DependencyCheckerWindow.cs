using UnityEngine;
using UnityEditor;
using System.Linq;
using System.Collections.Generic;
using Sinkii09.Engine.Editor.Core;

namespace Sinkii09.Engine.Editor.Dependencies
{
    /// <summary>
    /// Simple dependency checker window using the new EditorDependencyChecker
    /// </summary>
    public class DependencyCheckerWindow : EditorWindow
    {
        private EditorDependencyCheckResult _lastCheckResult;
        private Vector2 _scrollPosition;
        private Dictionary<string, bool> _installationInProgress = new Dictionary<string, bool>();
        private static bool _userDismissedDialog;
        private const string DISMISSAL_PREF_KEY = "DependencyChecker_UserDismissed";

        [MenuItem("Engine/Setup/Dependencies/Check Dependencies", false, 10)]
        public static void ShowWindow()
        {
            var window = GetWindow<DependencyCheckerWindow>("Engine Dependencies");
            window.minSize = new Vector2(600, 400);
            _userDismissedDialog = false;
            EditorPrefs.SetBool(DISMISSAL_PREF_KEY, false);
        }

        [MenuItem("Engine/Setup/Dependencies/Quick Install Missing", false, 11)]
        public static void QuickInstallMissing()
        {
            var result = EditorDependencyChecker.CheckAllDependencies();
            var missingDeps = result.DependencyStatuses
                .Where(s => !s.IsInstalled && s.Definition.IsRequired)
                .ToArray();

            if (missingDeps.Length == 0)
            {
                EditorUtility.DisplayDialog("Dependencies", "All required dependencies are installed!", "OK");
                return;
            }

            foreach (var dep in missingDeps)
            {
                if (dep.Definition.ProviderType != EditorPackageProviderType.AssetStore &&
                    dep.Definition.ProviderType != EditorPackageProviderType.NuGet)
                {
                    EditorDependencyChecker.InstallDependency(dep.Definition, (success, message) =>
                    {
                        if (success)
                            Debug.Log($"[Dependencies] Installed: {dep.Definition.DisplayName}");
                        else
                            Debug.LogError($"[Dependencies] Failed to install {dep.Definition.DisplayName}: {message}");
                    });
                }
            }
        }

        [MenuItem("Engine/Setup/Dependencies/Reset Startup Check", false, 12)]
        public static void ResetStartupCheck()
        {
            _userDismissedDialog = false;
            EditorPrefs.DeleteKey(DISMISSAL_PREF_KEY);
            SessionState.SetBool("DependencyChecker_SessionCheck", false);
            EditorUtility.DisplayDialog("Reset", "Dependency check will appear on next startup if dependencies are missing.", "OK");
        }

        private void OnEnable()
        {
            RefreshDependencies();
        }

        private void OnGUI()
        {
            DrawHeader();
            DrawDependencyList();
            DrawActionButtons();
        }

        private void DrawHeader()
        {
            EditorGUILayout.Space(10);
            GUILayout.Label("Sinkii09 Engine Dependencies", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            if (_lastCheckResult == null)
            {
                EditorGUILayout.HelpBox("Checking dependencies...", MessageType.Info);
                return;
            }

            // Summary box
            var messageType = _lastCheckResult.AllSatisfied ? MessageType.Info : MessageType.Warning;
            EditorGUILayout.HelpBox(_lastCheckResult.Summary, messageType);
            EditorGUILayout.Space(10);
        }

        private void DrawDependencyList()
        {
            if (_lastCheckResult == null || _lastCheckResult.DependencyStatuses == null)
                return;

            EditorGUILayout.LabelField("Dependencies:", EditorStyles.boldLabel);
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            foreach (var status in _lastCheckResult.DependencyStatuses)
            {
                DrawDependencyItem(status);
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawDependencyItem(EditorDependencyStatus status)
        {
            using (new EditorGUILayout.HorizontalScope(GUI.skin.box))
            {
                // Status icon
                var isInstalling = _installationInProgress.ContainsKey(status.Definition.PackageId) && 
                                  _installationInProgress[status.Definition.PackageId];
                
                Color statusColor;
                string statusText;
                
                if (isInstalling)
                {
                    statusColor = Color.yellow;
                    statusText = "⏳ Installing";
                }
                else if (status.IsInstalled)
                {
                    statusColor = Color.green;
                    statusText = "✓ Installed";
                }
                else if (!string.IsNullOrEmpty(status.ErrorMessage))
                {
                    statusColor = Color.red;
                    statusText = "⚠ Error";
                }
                else
                {
                    statusColor = status.Definition.IsRequired ? Color.red : Color.yellow;
                    statusText = status.Definition.IsRequired ? "✗ Missing" : "○ Optional";
                }

                var oldColor = GUI.color;
                GUI.color = statusColor;
                GUILayout.Label(statusText, GUILayout.Width(90));
                GUI.color = oldColor;

                // Package info
                using (new EditorGUILayout.VerticalScope())
                {
                    EditorGUILayout.LabelField(status.Definition.DisplayName, EditorStyles.boldLabel);
                    EditorGUILayout.LabelField(status.Definition.Description, EditorStyles.miniLabel);
                    
                    if (!string.IsNullOrEmpty(status.ErrorMessage))
                    {
                        var errorStyle = new GUIStyle(EditorStyles.miniLabel);
                        errorStyle.normal.textColor = Color.red;
                        EditorGUILayout.LabelField($"Error: {status.ErrorMessage}", errorStyle);
                    }
                    else if (status.IsInstalled && !string.IsNullOrEmpty(status.DetectedVersion))
                    {
                        EditorGUILayout.LabelField($"Version: {status.DetectedVersion}", EditorStyles.miniLabel);
                    }
                }

                // Action button
                if (!status.IsInstalled && !isInstalling)
                {
                    DrawInstallButton(status);
                }
                else if (isInstalling)
                {
                    GUI.enabled = false;
                    GUILayout.Button("Installing...", GUILayout.Width(80));
                    GUI.enabled = true;
                }
            }
        }

        private void DrawInstallButton(EditorDependencyStatus status)
        {
            switch (status.Definition.ProviderType)
            {
                case EditorPackageProviderType.AssetStore:
                    if (GUILayout.Button("Asset Store", GUILayout.Width(80)))
                    {
                        if (!string.IsNullOrEmpty(status.Definition.AssetStoreUrl))
                            Application.OpenURL(status.Definition.AssetStoreUrl);
                        else
                            EditorUtility.DisplayDialog("Asset Store", 
                                $"{status.Definition.DisplayName} must be installed from the Unity Asset Store.", "OK");
                    }
                    break;

                case EditorPackageProviderType.NuGet:
                    if (GUILayout.Button("NuGet Info", GUILayout.Width(80)))
                    {
                        EditorUtility.DisplayDialog("NuGet Package", 
                            $"{status.Definition.DisplayName} requires NuGet installation.\n\n" +
                            $"Package: {status.Definition.PackageId}\n" +
                            $"Version: {status.Definition.Version}", "OK");
                    }
                    break;

                default:
                    if (GUILayout.Button("Install", GUILayout.Width(80)))
                    {
                        InstallDependency(status.Definition);
                    }
                    break;
            }
        }

        private void DrawActionButtons()
        {
            EditorGUILayout.Space(10);
            
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Refresh", GUILayout.Height(30)))
                {
                    RefreshDependencies();
                }

                if (GUILayout.Button("Install All Missing", GUILayout.Height(30)))
                {
                    InstallAllMissing();
                }

                if (GUILayout.Button("Package Manager", GUILayout.Height(30)))
                {
                    EditorApplication.ExecuteMenuItem("Window/Package Manager");
                }
            }

            EditorGUILayout.Space(5);
            EditorGUILayout.HelpBox(
                "Tip: Use 'Engine > Dependencies > Quick Install Missing' for automatic installation.", 
                MessageType.Info);
        }

        private void RefreshDependencies()
        {
            _lastCheckResult = EditorDependencyChecker.CheckAllDependencies();
            Repaint();
        }

        private void InstallDependency(EditorDependencyDefinition dependency)
        {
            _installationInProgress[dependency.PackageId] = true;
            Repaint();

            EditorDependencyChecker.InstallDependency(dependency, (success, message) =>
            {
                _installationInProgress[dependency.PackageId] = false;
                
                if (success)
                {
                    Debug.Log($"[Dependencies] Successfully installed: {dependency.DisplayName}");
                    EditorApplication.delayCall += RefreshDependencies;
                }
                else
                {
                    Debug.LogError($"[Dependencies] Failed to install {dependency.DisplayName}: {message}");
                    
                    if (dependency.ProviderType == EditorPackageProviderType.Git || 
                        dependency.ProviderType == EditorPackageProviderType.UPM)
                    {
                        EditorUtility.DisplayDialog("Installation Failed",
                            $"Failed to install {dependency.DisplayName}.\n\n" +
                            $"Error: {message}\n\n" +
                            $"Try manually:\n" +
                            $"1. Open Package Manager\n" +
                            $"2. Click + > Add package from git URL\n" +
                            $"3. Enter: {dependency.GetInstallIdentifier()}", "OK");
                    }
                }
                
                Repaint();
            });
        }

        private void InstallAllMissing()
        {
            if (_lastCheckResult == null)
                return;

            var missingDeps = _lastCheckResult.DependencyStatuses
                .Where(s => !s.IsInstalled && s.Definition.IsRequired && string.IsNullOrEmpty(s.ErrorMessage))
                .ToArray();

            if (missingDeps.Length == 0)
            {
                EditorUtility.DisplayDialog("Dependencies", "All required dependencies are already installed!", "OK");
                return;
            }

            var installable = missingDeps.Where(s => 
                s.Definition.ProviderType != EditorPackageProviderType.AssetStore &&
                s.Definition.ProviderType != EditorPackageProviderType.NuGet).ToArray();
            
            var manual = missingDeps.Where(s => 
                s.Definition.ProviderType == EditorPackageProviderType.AssetStore ||
                s.Definition.ProviderType == EditorPackageProviderType.NuGet).ToArray();

            var message = $"Found {missingDeps.Length} missing dependencies.\n";
            if (installable.Length > 0)
                message += $"\n• {installable.Length} can be installed automatically";
            if (manual.Length > 0)
                message += $"\n• {manual.Length} require manual installation";
            
            message += "\n\nProceed with automatic installation?";

            if (EditorUtility.DisplayDialog("Install Dependencies", message, "Install", "Cancel"))
            {
                foreach (var dep in installable)
                {
                    InstallDependency(dep.Definition);
                }

                if (manual.Length > 0)
                {
                    var manualList = string.Join("\n", manual.Select(d => $"• {d.Definition.DisplayName}"));
                    EditorUtility.DisplayDialog("Manual Installation Required",
                        $"The following packages require manual installation:\n\n{manualList}", "OK");
                }
            }
        }

        [InitializeOnLoadMethod]
        private static void CheckOnStartup()
        {
            // Check if user has dismissed the dialog
            _userDismissedDialog = EditorPrefs.GetBool(DISMISSAL_PREF_KEY, false);
            if (_userDismissedDialog)
                return;

            // Check once per session
            var sessionKey = "DependencyChecker_SessionCheck";
            if (SessionState.GetBool(sessionKey, false))
                return;
            
            SessionState.SetBool(sessionKey, true);

            // Delay to let Unity fully initialize
            EditorApplication.delayCall += () =>
            {
                var result = EditorDependencyChecker.CheckAllDependencies();
                
                if (!result.AllSatisfied)
                {
                    var missingRequired = result.DependencyStatuses
                        .Where(s => !s.IsInstalled && s.Definition.IsRequired)
                        .Select(s => s.Definition.DisplayName)
                        .ToArray();

                    if (missingRequired.Length > 0)
                    {
                        var message = $"Sinkii09 Engine is missing {missingRequired.Length} required dependencies:\n\n";
                        message += string.Join("\n", missingRequired.Select(d => $"• {d}"));
                        message += "\n\nWould you like to open the dependency manager?";

                        var choice = EditorUtility.DisplayDialogComplex("Missing Dependencies", message, 
                            "Open Manager", "Later", "Don't Show Again");

                        switch (choice)
                        {
                            case 0: // Open Manager
                                ShowWindow();
                                break;
                            case 1: // Later
                                // Do nothing, will check again next session
                                break;
                            case 2: // Don't Show Again
                                _userDismissedDialog = true;
                                EditorPrefs.SetBool(DISMISSAL_PREF_KEY, true);
                                break;
                        }
                    }
                }
            };
        }
    }
}