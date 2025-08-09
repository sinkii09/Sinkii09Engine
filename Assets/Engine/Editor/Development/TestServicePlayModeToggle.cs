using UnityEditor;
using UnityEngine;
using Sinkii09.Engine.Services;

namespace Sinkii09.Engine.Editor.Development
{
    /// <summary>
    /// Provides runtime controls for test services during play mode
    /// </summary>
    public static class TestServicePlayModeToggle
    {
        private const string RUNTIME_ENABLE_KEY = "EngineTestServices_RuntimeEnabled";
        
        /// <summary>
        /// Runtime toggle for test services (persists during play session)
        /// </summary>
        public static bool RuntimeTestServicesEnabled
        {
            get => SessionState.GetBool(RUNTIME_ENABLE_KEY, false);
            set => SessionState.SetBool(RUNTIME_ENABLE_KEY, value);
        }
        
        [MenuItem("Engine/Development/Runtime/Toggle Test Services", false, 100)]
        public static void ToggleRuntimeTestServices()
        {
            RuntimeTestServicesEnabled = !RuntimeTestServicesEnabled;
            
            var status = RuntimeTestServicesEnabled ? "ENABLED" : "DISABLED";
            var icon = RuntimeTestServicesEnabled ? "🟢" : "🔴";
            
            Debug.Log($"{icon} Runtime Test Services: {status}");
            
            if (Application.isPlaying)
            {
                Debug.LogWarning("⚠️ Test service changes require engine restart to take effect");
                ShowRestartEngineDialog();
            }
        }
        
        [MenuItem("Engine/Development/Runtime/Toggle Test Services", true)]
        public static bool ToggleRuntimeTestServicesValidate()
        {
            Menu.SetChecked("Engine/Development/Runtime/Toggle Test Services", RuntimeTestServicesEnabled);
            return true;
        }
        
        [MenuItem("Engine/Development/Runtime/Restart Engine with Test Services", false, 101)]
        public static void RestartEngineWithTestServices()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("Engine is not running");
                return;
            }
            
            RuntimeTestServicesEnabled = true;
            RestartEngine();
        }
        
        [MenuItem("Engine/Development/Runtime/Restart Engine without Test Services", false, 102)]
        public static void RestartEngineWithoutTestServices()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("Engine is not running");
                return;
            }
            
            RuntimeTestServicesEnabled = false;
            RestartEngine();
        }
        
        [MenuItem("Engine/Development/Runtime/Show Test Service Status", false, 103)]
        public static void ShowTestServiceStatus()
        {
            if (!Application.isPlaying)
            {
                Debug.Log("📊 Test Service Status: Engine not running");
                return;
            }
            
            Debug.Log("📊 Test Service Status:");
            Debug.Log($"   Runtime Enabled: {RuntimeTestServicesEnabled}");
            Debug.Log($"   Should Include: {ServiceTestUtils.ShouldIncludeTestServices()}");
            Debug.Log($"   Status: {ServiceTestUtils.GetTestServiceStatus()}");
            
            // Show registered test services
            if (Engine.Initialized)
            {
                var testServiceCount = CountRegisteredTestServices();
                Debug.Log($"   Registered Test Services: {testServiceCount}");
            }
        }
        
        private static void ShowRestartEngineDialog()
        {
            if (EditorUtility.DisplayDialog(
                "Restart Required", 
                "Test service changes require engine restart to take effect.\n\nRestart now?", 
                "Restart Engine", 
                "Later"))
            {
                RestartEngine();
            }
        }
        
        private static void RestartEngine()
        {
            if (!Application.isPlaying)
                return;
                
            Debug.Log("🔄 Restarting Engine with new test service settings...");
            
            // Stop play mode and restart
            EditorApplication.isPlaying = false;
            EditorApplication.delayCall += () =>
            {
                EditorApplication.isPlaying = true;
            };
        }
        
        private static int CountRegisteredTestServices()
        {
            // This would require access to the service container
            // For now, return a placeholder
            return 0; // TODO: Implement actual counting
        }
    }
    
    /// <summary>
    /// Custom inspector window for test service management
    /// </summary>
    public class TestServiceManagerWindow : EditorWindow
    {
        private Vector2 scrollPosition;
        private bool showAdvancedOptions = false;
        
        [MenuItem("Engine/Development/Test Service Manager", false, 200)]
        public static void ShowWindow()
        {
            var window = GetWindow<TestServiceManagerWindow>("Test Services");
            window.Show();
        }
        
        private void OnGUI()
        {
            GUILayout.Label("Test Service Manager", EditorStyles.boldLabel);
            EditorGUILayout.Space();
            
            // Runtime toggle
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Runtime Test Services:", GUILayout.Width(150));
            
            var currentState = TestServicePlayModeToggle.RuntimeTestServicesEnabled;
            var newState = EditorGUILayout.Toggle(currentState);
            
            if (newState != currentState)
            {
                TestServicePlayModeToggle.RuntimeTestServicesEnabled = newState;
                if (Application.isPlaying)
                {
                    EditorUtility.DisplayDialog("Restart Required", 
                        "Changes will take effect after engine restart", "OK");
                }
            }
            EditorGUILayout.EndHorizontal();
            
            // Status display
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Status:", EditorStyles.boldLabel);
            
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.TextField("Engine State", Application.isPlaying ? "Playing" : "Stopped");
                
                if (Application.isPlaying)
                {
                    EditorGUILayout.TextField("Should Include Tests", 
                        ServiceTestUtils.ShouldIncludeTestServices().ToString());
                    EditorGUILayout.TextField("Current Status", 
                        ServiceTestUtils.GetTestServiceStatus());
                }
            }
            
            EditorGUILayout.Space();
            
            // Action buttons
            EditorGUILayout.LabelField("Actions:", EditorStyles.boldLabel);
            
            using (new EditorGUI.DisabledScope(!Application.isPlaying))
            {
                if (GUILayout.Button("🔄 Restart Engine with Test Services"))
                {
                    TestServicePlayModeToggle.RestartEngineWithTestServices();
                }
                
                if (GUILayout.Button("🔄 Restart Engine without Test Services"))
                {
                    TestServicePlayModeToggle.RestartEngineWithoutTestServices();
                }
                
                if (GUILayout.Button("📊 Show Test Service Status"))
                {
                    TestServicePlayModeToggle.ShowTestServiceStatus();
                }
            }
            
            EditorGUILayout.Space();
            
            // Advanced options
            showAdvancedOptions = EditorGUILayout.Foldout(showAdvancedOptions, "Advanced Options");
            if (showAdvancedOptions)
            {
                EditorGUILayout.BeginVertical("box");
                
                if (GUILayout.Button("🔧 Toggle Scripting Define Symbol"))
                {
                    TestToggle.ToggleTestServices();
                }
                
                if (GUILayout.Button("🧪 Run All Engine Tests"))
                {
                    TestToggle.RunAllEngineTests();
                }
                
                if (GUILayout.Button("📋 Open Test Runner"))
                {
                    EditorApplication.ExecuteMenuItem("Window/General/Test Runner");
                }
                
                EditorGUILayout.EndVertical();
            }
            
            // Footer
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("💡 Tip: Use runtime toggles for quick testing during play mode", 
                EditorStyles.helpBox);
        }
        
        private void OnInspectorUpdate()
        {
            // Refresh the window periodically
            Repaint();
        }
    }
}