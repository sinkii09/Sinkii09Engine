using System;
using UnityEngine;

namespace Sinkii09.Engine.Editor.Dependencies
{
    /// <summary>
    /// Editor-only dependency definition for package management
    /// </summary>
    [Serializable]
    public class EditorDependencyDefinition
    {
        [SerializeField] private string _packageId;
        [SerializeField] private string _displayName;
        [SerializeField] private string _description;
        [SerializeField] private bool _isRequired = true;
        [SerializeField] private EditorPackageProviderType _providerType;
        [SerializeField] private string _version;
        [SerializeField] private string _gitUrl;
        [SerializeField] private string _assetStoreUrl;
        [SerializeField] private string[] _assemblyNames = new string[0];
        [SerializeField] private string[] _folderPaths = new string[0];
        [SerializeField] private string[] _filePaths = new string[0];

        public string PackageId => _packageId;
        public string DisplayName => string.IsNullOrEmpty(_displayName) ? _packageId : _displayName;
        public string Description => _description;
        public bool IsRequired => _isRequired;
        public EditorPackageProviderType ProviderType => _providerType;
        public string Version => _version;
        public string GitUrl => _gitUrl;
        public string AssetStoreUrl => _assetStoreUrl;
        public string[] AssemblyNames => _assemblyNames;
        public string[] FolderPaths => _folderPaths;
        public string[] FilePaths => _filePaths;

        public EditorDependencyDefinition(string packageId, string displayName, string description,
            EditorPackageProviderType providerType, string version = null, string gitUrl = null, string assetStoreUrl = null,
            string[] assemblyNames = null, string[] folderPaths = null, string[] filePaths = null, bool isRequired = true)
        {
            _packageId = packageId;
            _displayName = displayName;
            _description = description;
            _providerType = providerType;
            _version = version;
            _gitUrl = gitUrl;
            _assetStoreUrl = assetStoreUrl;
            _assemblyNames = assemblyNames ?? new string[0];
            _folderPaths = folderPaths ?? new string[0];
            _filePaths = filePaths ?? new string[0];
            _isRequired = isRequired;
        }

        public bool IsValid()
        {
            if (string.IsNullOrEmpty(_packageId)) return false;
            
            switch (_providerType)
            {
                case EditorPackageProviderType.UPM:
                    return !string.IsNullOrEmpty(_version);
                case EditorPackageProviderType.Git:
                    return !string.IsNullOrEmpty(_gitUrl);
                case EditorPackageProviderType.AssetStore:
                    return true; // Asset Store packages don't require specific validation
                case EditorPackageProviderType.NuGet:
                    return !string.IsNullOrEmpty(_version);
                default:
                    return true;
            }
        }

        public string GetInstallIdentifier()
        {
            switch (_providerType)
            {
                case EditorPackageProviderType.Git:
                    return _gitUrl;
                case EditorPackageProviderType.UPM:
                case EditorPackageProviderType.NuGet:
                    return $"{_packageId}@{_version}";
                default:
                    return _packageId;
            }
        }
    }

    public enum EditorPackageProviderType
    {
        UPM,
        Git,
        AssetStore,
        NuGet,
        Manual
    }
}