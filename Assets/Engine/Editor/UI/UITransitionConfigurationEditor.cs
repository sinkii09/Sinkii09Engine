using UnityEngine;
using UnityEditor;
using Sinkii09.Engine.Services;

namespace Sinkii09.Engine.Editor
{
    /// <summary>
    /// Custom editor for UITransitionConfiguration with reset functionality
    /// </summary>
    [CustomEditor(typeof(UITransitionConfiguration))]
    public class UITransitionConfigurationEditor : UnityEditor.Editor
    {
        private UITransitionConfiguration _config;

        private void OnEnable()
        {
            _config = (UITransitionConfiguration)target;
        }

        public override void OnInspectorGUI()
        {
            // Draw default inspector
            DrawDefaultInspector();

            EditorGUILayout.Space(10);

            // Add reset section
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Reset Configuration", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Reset all transition settings to their default values. This action cannot be undone.", MessageType.Warning);

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            // Reset button with confirmation
            GUI.backgroundColor = Color.yellow;
            if (GUILayout.Button("Reset to Defaults", GUILayout.Width(150), GUILayout.Height(25)))
            {
                if (EditorUtility.DisplayDialog(
                    "Reset Transition Configuration", 
                    "Are you sure you want to reset all transition settings to their default values?\n\nThis action cannot be undone.", 
                    "Reset", 
                    "Cancel"))
                {
                    ResetToDefaults();
                }
            }
            GUI.backgroundColor = Color.white;

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();

            // Add configuration summary
            EditorGUILayout.Space(10);
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Configuration Summary", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(_config.GetConfigurationSummary(), EditorStyles.wordWrappedLabel);
            EditorGUILayout.EndVertical();
        }

        private void ResetToDefaults()
        {
            // Record undo operation
            Undo.RecordObject(_config, "Reset UI Transition Configuration");

            // Use the built-in reset method
            _config.ResetToDefaults();

            // Save changes
            AssetDatabase.SaveAssets();
            
            Debug.Log("UITransitionConfiguration has been reset to default values.", _config);
        }
    }
}