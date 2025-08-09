using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace Sinkii09.Engine.Editor.Development
{
    /// <summary>  
    /// Editor utilities for toggling test-related features during development  
    /// </summary>  
    public static class TestToggle
    {
        private const string ENABLE_ENGINE_TESTS = "ENABLE_ENGINE_TESTS";
        private const string MENU_PATH = "Engine/Development/";

        [MenuItem(MENU_PATH + "Toggle Test Services")]
        public static void ToggleTestServices()
        {
            var buildTarget = EditorUserBuildSettings.selectedBuildTargetGroup;
            var namedBuildTarget = NamedBuildTarget.FromBuildTargetGroup(buildTarget);
            var symbols = PlayerSettings.GetScriptingDefineSymbols(namedBuildTarget);

            if (symbols.Contains(ENABLE_ENGINE_TESTS))
            {
                symbols = symbols.Replace($";{ENABLE_ENGINE_TESTS}", "")
                                .Replace(ENABLE_ENGINE_TESTS, "");
                Debug.Log("🔴 Engine Test Services: DISABLED");
            }
            else
            {
                symbols += $";{ENABLE_ENGINE_TESTS}";
                Debug.Log("🟢 Engine Test Services: ENABLED");
            }

            PlayerSettings.SetScriptingDefineSymbols(namedBuildTarget, symbols);
        }

        [MenuItem(MENU_PATH + "Toggle Test Services", true)]
        public static bool ToggleTestServicesValidate()
        {
            var buildTarget = EditorUserBuildSettings.selectedBuildTargetGroup;
            var namedBuildTarget = NamedBuildTarget.FromBuildTargetGroup(buildTarget);
            var symbols = PlayerSettings.GetScriptingDefineSymbols(namedBuildTarget);

            Menu.SetChecked(MENU_PATH + "Toggle Test Services", symbols.Contains(ENABLE_ENGINE_TESTS));
            return true;
        }

        [MenuItem(MENU_PATH + "Run All Engine Tests")]
        public static void RunAllEngineTests()
        {
            // This will run tests using Unity Test Runner  
            EditorApplication.ExecuteMenuItem("Window/General/Test Runner");
        }

        /// <summary>  
        /// Check if test services should be included at runtime  
        /// </summary>  
        public static bool ShouldIncludeTestServices()
        {
#if !UNITY_INCLUDE_TESTS
                return false; // Test assembly not available  
#endif

#if !DEVELOPMENT_BUILD && !UNITY_EDITOR
                return false; // Production build - never include  
#endif

#if ENABLE_ENGINE_TESTS
                return true; // Explicitly enabled  
#endif

            return false; // Default: disabled  
        }
    }
}