using UnityEngine;
using UnityEditor;
using Sinkii09.Engine.Services;

namespace Sinkii09.Engine.Editor
{
    /// <summary>
    /// Custom editor for UIScreenAsset to improve workflow
    /// </summary>
    [CustomEditor(typeof(UIScreenAsset))]
    public class UIScreenAssetEditor : UnityEditor.Editor
    {
        private SerializedProperty _screenType;
        private SerializedProperty _displayName;
        private SerializedProperty _category;
        private SerializedProperty _addressableReference;
        private SerializedProperty _sortingOrder;
        private SerializedProperty _cacheScreen;
        private SerializedProperty _allowMultipleInstances;
        
        private void OnEnable()
        {
            _screenType = serializedObject.FindProperty("_screenType");
            _displayName = serializedObject.FindProperty("_displayName");
            _category = serializedObject.FindProperty("_category");
            _addressableReference = serializedObject.FindProperty("_addressableReference");
            _sortingOrder = serializedObject.FindProperty("_sortingOrder");
            _cacheScreen = serializedObject.FindProperty("_cacheScreen");
            _allowMultipleInstances = serializedObject.FindProperty("_allowMultipleInstances");
        }
        
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            
            UIScreenAsset asset = (UIScreenAsset)target;
            
            // Header info
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                "UI Screen Asset Configuration\n\n" +
                "• Set Screen Type to identify this screen\n" +
                "• Drag your screen prefab to Addressable Reference\n",
                MessageType.Info
            );
            EditorGUILayout.Space();
            
            // Screen Type (Most Important)
            EditorGUILayout.LabelField("Screen Identity", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(_screenType);
            if (EditorGUI.EndChangeCheck())
            {
                // Auto-generate display name if empty
                if (string.IsNullOrEmpty(_displayName.stringValue))
                {
                    _displayName.stringValue = _screenType.enumNames[_screenType.enumValueIndex]
                        .Replace("Screen", "")
                        .Replace("UI", "");
                }
            }
            
            EditorGUILayout.PropertyField(_displayName);
            EditorGUILayout.PropertyField(_category);
            EditorGUILayout.Space();
            
            // Addressable Reference (Required)
            EditorGUILayout.LabelField("Prefab Reference", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_addressableReference, new GUIContent("Addressable Reference"));

            // Check if AssetReference is valid (can't use objectReferenceValue on AssetReference)
            var assetRef = asset.AddressableReference;
            if (assetRef == null || !assetRef.RuntimeKeyIsValid())
            {
                EditorGUILayout.HelpBox("⚠️ Addressable reference is required!", MessageType.None);
            }
            else
            {
                EditorGUILayout.HelpBox("✅ Prefab linked", MessageType.None);
            }
            EditorGUILayout.Space();
            
            // Display Settings
            EditorGUILayout.LabelField("Display Settings", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_sortingOrder);
            EditorGUILayout.PropertyField(_cacheScreen);
            EditorGUILayout.PropertyField(_allowMultipleInstances);
            EditorGUILayout.Space();
            
            // Validation
            if (asset.ScreenType == UIScreenType.None)
            {
                EditorGUILayout.HelpBox("Screen Type must be set!", MessageType.Error);
            }
            
            // Quick Actions
            EditorGUILayout.LabelField("Quick Actions", EditorStyles.boldLabel);
            
            if (GUILayout.Button("Validate Configuration"))
            {
                if (asset.Validate(out var errors))
                {
                    EditorUtility.DisplayDialog("Validation Success", "Screen asset is properly configured!", "OK");
                }
                else
                {
                    string errorMessage = string.Join("\n• ", errors);
                    EditorUtility.DisplayDialog("Validation Failed", $"Issues found:\n• {errorMessage}", "OK");
                }
            }
            
            if (GUILayout.Button("Select in Registry"))
            {
                // Find and select the registry asset
                var registryGUIDs = AssetDatabase.FindAssets("t:UIScreenRegistry");
                if (registryGUIDs.Length > 0)
                {
                    var registryPath = AssetDatabase.GUIDToAssetPath(registryGUIDs[0]);
                    var registry = AssetDatabase.LoadAssetAtPath<UIScreenRegistry>(registryPath);
                    if (registry != null)
                    {
                        Selection.activeObject = registry;
                        EditorGUIUtility.PingObject(registry);
                    }
                }
            }
            
            serializedObject.ApplyModifiedProperties();
        }
    }
}