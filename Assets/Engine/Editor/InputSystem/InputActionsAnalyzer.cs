using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Sinkii09.Engine.Editor.InputSystem
{
    /// <summary>
    /// Analyzes Unity's generated InputSystem_Actions.cs using Roslyn
    /// to extract all action maps and their actions for code generation
    /// </summary>
    public class InputActionsAnalyzer
    {
        /// <summary>
        /// Analyzes the InputSystem_Actions.cs file and extracts all action information
        /// </summary>
        /// <returns>Complete information about all discovered actions</returns>
        public InputActionsInfo AnalyzeInputActions()
        {
            MonoScript inputActionsScript = FindInputActionsScript();
            if (inputActionsScript == null)
            {
                throw new Exception("InputSystem_Actions MonoScript not found in project. Make sure Input Actions asset has generated C# wrapper.");
            }

            string sourceCode = inputActionsScript.text;
            if (string.IsNullOrEmpty(sourceCode))
            {
                throw new Exception("InputSystem_Actions source code is empty or could not be read.");
            }

            return AnalyzeSourceCode(sourceCode);
        }

        /// <summary>
        /// Finds the InputSystem_Actions MonoScript asset using Unity's AssetDatabase
        /// </summary>
        /// <returns>MonoScript asset or null if not found</returns>
        private MonoScript FindInputActionsScript()
        {
            var monoScripts = AssetDatabase.FindAssets("t:MonoScript")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<MonoScript>)
                .Where(script => script != null);

            return monoScripts.FirstOrDefault(script =>
                script.GetClass() == typeof(Services.InputSystem_Actions) ||
                script.name == "InputSystem_Actions");
        }

        /// <summary>
        /// Parses the source code using Roslyn and extracts action information
        /// </summary>
        /// <param name="sourceCode">C# source code of InputSystem_Actions.cs</param>
        /// <returns>Parsed action information</returns>
        private InputActionsInfo AnalyzeSourceCode(string sourceCode)
        {
            SyntaxTree tree = CSharpSyntaxTree.ParseText(sourceCode);
            CompilationUnitSyntax root = (CompilationUnitSyntax)tree.GetRoot();

            var info = new InputActionsInfo();

            // Find the main InputSystem_Actions class
            var mainClass = root.DescendantNodes()
                .OfType<ClassDeclarationSyntax>()
                .FirstOrDefault(c => c.Identifier.ValueText.Contains("InputSystem_Actions"));

            if (mainClass == null)
            {
                throw new Exception("InputSystem_Actions class not found in source code.");
            }

            // Extract action maps and their actions
            ExtractActionMaps(mainClass, info);

            Debug.Log($"[InputActionsAnalyzer] Successfully analyzed {info.ActionMaps.Count} action maps with {info.ActionMaps.Sum(m => m.Actions.Count)} total actions.");

            return info;
        }

        /// <summary>
        /// Extracts action map structures and their InputAction properties
        /// </summary>
        /// <param name="mainClass">The main InputSystem_Actions class syntax node</param>
        /// <param name="info">Info object to populate with discovered actions</param>
        private void ExtractActionMaps(ClassDeclarationSyntax mainClass, InputActionsInfo info)
        {
            // Find struct declarations that represent action maps (e.g., PlayerActions, UIActions)
            var actionStructs = mainClass.DescendantNodes()
                .OfType<StructDeclarationSyntax>()
                .Where(s => s.Identifier.ValueText.EndsWith("Actions"));

            foreach (var actionStruct in actionStructs)
            {
                string mapName = actionStruct.Identifier.ValueText.Replace("Actions", "");
                var actionMap = new ActionMapInfo { Name = mapName };

                Debug.Log($"[InputActionsAnalyzer] Found action map: {mapName}");

                // Find InputAction properties in the struct
                var actionProperties = actionStruct.DescendantNodes()
                    .OfType<PropertyDeclarationSyntax>()
                    .Where(p => IsInputActionProperty(p));

                foreach (var prop in actionProperties)
                {
                    string actionName = prop.Identifier.ValueText.TrimStart('@'); // Remove @ prefix if present
                    var expectedControlType = DetermineExpectedControlType(actionName, mapName);

                    actionMap.Actions.Add(new ActionInfo
                    {
                        Name = actionName,
                        PropertyName = prop.Identifier.ValueText,
                        ExpectedControlType = expectedControlType,
                        ActionMapName = mapName
                    });

                    Debug.Log($"[InputActionsAnalyzer]   - Action: {actionName} (Type: {expectedControlType})");
                }

                if (actionMap.Actions.Count > 0)
                {
                    info.ActionMaps.Add(actionMap);
                }
            }
        }

        /// <summary>
        /// Determines if a property represents an InputAction
        /// </summary>
        /// <param name="property">Property syntax node to check</param>
        /// <returns>True if the property is an InputAction</returns>
        private bool IsInputActionProperty(PropertyDeclarationSyntax property)
        {
            string typeName = property.Type.ToString();
            return typeName.Contains("InputAction") && !typeName.Contains("InputActionMap");
        }

        /// <summary>
        /// Determines the expected control type based on action name and context
        /// </summary>
        /// <param name="actionName">Name of the action</param>
        /// <param name="mapName">Name of the action map</param>
        /// <returns>Expected control type for delegate generation</returns>
        private ExpectedControlType DetermineExpectedControlType(string actionName, string mapName)
        {
            // Vector2 actions
            if (actionName.Equals("Move", StringComparison.OrdinalIgnoreCase) ||
                actionName.Equals("Look", StringComparison.OrdinalIgnoreCase) ||
                actionName.Equals("Navigate", StringComparison.OrdinalIgnoreCase) ||
                actionName.Equals("Point", StringComparison.OrdinalIgnoreCase) ||
                actionName.Equals("ScrollWheel", StringComparison.OrdinalIgnoreCase))
            {
                return ExpectedControlType.Vector2;
            }

            // Vector3 actions
            if (actionName.Equals("TrackedDevicePosition", StringComparison.OrdinalIgnoreCase))
            {
                return ExpectedControlType.Vector3;
            }

            // Quaternion actions  
            if (actionName.Equals("TrackedDeviceOrientation", StringComparison.OrdinalIgnoreCase))
            {
                return ExpectedControlType.Quaternion;
            }

            // Default to Button for all other actions
            return ExpectedControlType.Button;
        }
    }

    #region Data Classes

    /// <summary>
    /// Complete information about all discovered input actions
    /// </summary>
    public class InputActionsInfo
    {
        public List<ActionMapInfo> ActionMaps { get; set; } = new List<ActionMapInfo>();

        /// <summary>
        /// Gets all actions across all action maps
        /// </summary>
        public IEnumerable<ActionInfo> AllActions => ActionMaps.SelectMany(m => m.Actions);
    }

    /// <summary>
    /// Information about a specific action map (e.g., Player, UI)
    /// </summary>
    public class ActionMapInfo
    {
        public string Name { get; set; }
        public List<ActionInfo> Actions { get; set; } = new List<ActionInfo>();
    }

    /// <summary>
    /// Information about a specific input action
    /// </summary>
    public class ActionInfo
    {
        public string Name { get; set; }
        public string PropertyName { get; set; }
        public string ActionMapName { get; set; }
        public ExpectedControlType ExpectedControlType { get; set; }
    }

    /// <summary>
    /// Expected control types for input actions
    /// </summary>
    public enum ExpectedControlType
    {
        Button,
        Vector2,
        Vector3,
        Quaternion,
        Float
    }

    #endregion
}