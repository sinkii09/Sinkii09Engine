using UnityEngine;
using UnityEditor;

namespace Sinkii09.Engine.Editor.InputSystem
{
    /// <summary>
    /// Test script to verify the InputActionCodeGenerator is working correctly
    /// </summary>
    public class InputActionCodeGeneratorTest
    {
        [MenuItem("Engine/Test/Test Code Generator")]
        public static void TestInputActionCodeGenerator()
        {
            Debug.Log("[InputActionCodeGeneratorTest] Starting code generation test...");

            try
            {
                // Step 1: Analyze input actions
                var analyzer = new InputActionsAnalyzer();
                var actionsInfo = analyzer.AnalyzeInputActions();

                Debug.Log($"[InputActionCodeGeneratorTest] Analysis completed. Found {actionsInfo.ActionMaps.Count} action maps.");

                // Step 2: Generate delegate cache code
                var codeGenerator = new InputActionCodeGenerator();
                codeGenerator.GenerateDelegateCache(actionsInfo);

                Debug.Log("[InputActionCodeGeneratorTest] Code generation completed successfully! ✓");
                Debug.Log("[InputActionCodeGeneratorTest] Check the generated file at: Assets/Engine/Runtime/Scripts/Core/Services/Implemented/InputService/Generated/InputActionMappings.generated.cs");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[InputActionCodeGeneratorTest] Test failed with error: {ex.Message}");
                Debug.LogError($"[InputActionCodeGeneratorTest] Stack trace: {ex.StackTrace}");
            }
        }

    }
}