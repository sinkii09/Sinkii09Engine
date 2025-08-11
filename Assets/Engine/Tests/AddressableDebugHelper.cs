using UnityEngine;
using UnityEngine.AddressableAssets;
using System.Collections.Generic;

namespace Sinkii09.Engine.Services
{
    /// <summary>
    /// Helper utility to debug Addressable loading issues
    /// </summary>
    public static class AddressableDebugHelper
    {
        /// <summary>
        /// Validate and debug an AssetReference
        /// </summary>
        public static void ValidateAssetReference(AssetReference reference, string context = "")
        {
            if (reference == null)
            {
                Debug.LogError($"[Addressable Debug] {context}: AssetReference is NULL");
                return;
            }

            Debug.Log($"[Addressable Debug] {context}:");
            Debug.Log($"  - Asset GUID: {reference.AssetGUID}");
            Debug.Log($"  - Runtime Key Valid: {reference.RuntimeKeyIsValid()}");
            Debug.Log($"  - SubObject Name: {reference.SubObjectName}");

#if UNITY_EDITOR
            var assetPath = UnityEditor.AssetDatabase.GUIDToAssetPath(reference.AssetGUID);
            var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<Object>(assetPath);
            
            Debug.Log($"  - Asset Path: {assetPath}");
            Debug.Log($"  - Asset Type: {(asset != null ? asset.GetType().Name : "NULL")}");
            Debug.Log($"  - Asset Name: {(asset != null ? asset.name : "NULL")}");
            
            if (asset is GameObject go)
            {
                var uiScreen = go.GetComponent<UIScreen>();
                Debug.Log($"  - Has UIScreen Component: {uiScreen != null}");
            }
            else
            {
                Debug.LogWarning($"  - WARNING: Asset is not a GameObject! Type: {asset?.GetType().Name}");
            }
#endif
        }

        /// <summary>
        /// Debug all UIScreenAssets in a registry
        /// </summary>
        public static void DebugScreenRegistry(UIScreenRegistry registry)
        {
            if (registry == null)
            {
                Debug.LogError("[Addressable Debug] UIScreenRegistry is NULL");
                return;
            }

            Debug.Log($"[Addressable Debug] Debugging UIScreenRegistry: {registry.name}");
            
            var assets = registry.GetAllScreenAssets();
            Debug.Log($"  - Total Screen Assets: {assets.Length}");

            for (int i = 0; i < assets.Length; i++)
            {
                var asset = assets[i];
                if (asset == null)
                {
                    Debug.LogError($"  - Asset [{i}]: NULL");
                    continue;
                }

                Debug.Log($"  - Asset [{i}]: {asset.DisplayName} (Type: {asset.ScreenType})");
                ValidateAssetReference(asset.AddressableReference, $"    Screen '{asset.DisplayName}'");
            }
        }

        /// <summary>
        /// Get detailed error information for loading failures
        /// </summary>
        public static string GetLoadingErrorDetails(AssetReference reference, System.Exception exception)
        {
            var details = new List<string>
            {
                "=== ADDRESSABLE LOADING ERROR DETAILS ===",
                $"AssetReference GUID: {reference?.AssetGUID ?? "NULL"}",
                $"Runtime Key Valid: {reference?.RuntimeKeyIsValid() ?? false}",
                $"Exception Type: {exception?.GetType().Name}",
                $"Exception Message: {exception?.Message}",
            };

#if UNITY_EDITOR
            if (reference != null)
            {
                var assetPath = UnityEditor.AssetDatabase.GUIDToAssetPath(reference.AssetGUID);
                var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<Object>(assetPath);
                
                details.Add($"Asset Path: {assetPath}");
                details.Add($"Asset Type: {asset?.GetType().Name ?? "NOT FOUND"}");
                details.Add($"Asset Exists: {asset != null}");
                
                if (asset != null && !(asset is GameObject))
                {
                    details.Add("❌ PROBLEM: Asset is not a GameObject!");
                    details.Add("✅ SOLUTION: AssetReference should point to a .prefab file, not a .asset file");
                }
            }
#endif

            details.Add("===========================================");
            return string.Join("\n", details);
        }
    }
}