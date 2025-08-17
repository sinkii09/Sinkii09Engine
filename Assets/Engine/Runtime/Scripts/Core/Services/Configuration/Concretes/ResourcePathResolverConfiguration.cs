using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Sirenix.OdinInspector;

namespace Sinkii09.Engine.Services
{
    /// <summary>
    /// Configuration for the ResourcePathResolver service
    /// Defines path templates, fallback hierarchies, and performance settings
    /// </summary>
    [CreateAssetMenu(fileName = "ResourcePathResolverConfiguration", menuName = "Engine/Services/ResourcePathResolver Configuration")]
    public class ResourcePathResolverConfiguration : ServiceConfigurationBase
    {
        [TabGroup("General")]
        [Title("Basic Settings")]
        [Tooltip("Default resource environment (Development, Production, etc.)")]
        [SerializeField] private ResourceEnvironment _defaultEnvironment = ResourceEnvironment.Development;
        
        [TabGroup("General")]
        [Tooltip("Enable path validation for better error reporting")]
        [SerializeField] private bool _enablePathValidation = true;
        
        [TabGroup("General")]
        [Tooltip("Enable existence checking for resolved paths")]
        [SerializeField] private bool _enableExistenceChecking = false;
        
        [TabGroup("General")]
        [Tooltip("Root directory for all resources")]
        [SerializeField] private string _resourceRoot = "Assets/Resources";
        
        [TabGroup("Templates")]
        [Title("Path Templates")]
        [InfoBox("Define path templates using placeholders like {actorId}, {expression}, etc.")]
        [SerializeField] private PathTemplateEntry[] _pathTemplates = Array.Empty<PathTemplateEntry>();
        
        [TabGroup("Fallbacks")]
        [Title("Fallback Configuration")]
        [InfoBox("Define fallback paths when primary resources are not found")]
        [SerializeField] private FallbackPathEntry[] _fallbackPaths = Array.Empty<FallbackPathEntry>();
        
        [TabGroup("Performance")]
        [Title("Performance Settings")]
        [Range(100, 10000)]
        [Tooltip("Maximum number of paths to cache")]
        [SerializeField] private int _maxCacheSize = 1000;
        
        [TabGroup("Performance")]
        [Range(0.1f, 60f)]
        [Tooltip("Cache entry lifetime in seconds")]
        [SerializeField] private float _cacheEntryLifetime = 300f; // 5 minutes
        
        [TabGroup("Performance")]
        [Tooltip("Enable LRU (Least Recently Used) cache eviction")]
        [SerializeField] private bool _enableLRUEviction = true;
        
        [TabGroup("Performance")]
        [Range(1, 100)]
        [Tooltip("Maximum resolution time in milliseconds before warning")]
        [SerializeField] private float _maxResolutionTimeMs = 5f;
        
        [TabGroup("Performance")]
        [Tooltip("Enable memory pressure response")]
        [SerializeField] private bool _enableMemoryPressureResponse = true;
        
        [TabGroup("Validation")]
        [Title("Validation Settings")]
        [Tooltip("Validate templates at startup")]
        [SerializeField] private bool _validateTemplatesAtStartup = true;
        
        [TabGroup("Validation")]
        [Tooltip("Strict mode - fail on invalid templates")]
        [SerializeField] private bool _strictValidationMode = false;
        
        [TabGroup("Validation")]
        [Tooltip("Log warnings for missing resources")]
        [SerializeField] private bool _logMissingResources = true;
        
        [TabGroup("Environment")]
        [Title("Environment Overrides")]
        [InfoBox("Override settings based on runtime environment")]
        [SerializeField] private EnvironmentOverride[] _environmentOverrides = Array.Empty<EnvironmentOverride>();
        
        #region Properties
        
        public ResourceEnvironment DefaultEnvironment => _defaultEnvironment;
        public bool EnablePathValidation => _enablePathValidation;
        public bool EnableExistenceChecking => _enableExistenceChecking;
        public string ResourceRoot => _resourceRoot;
        public int MaxCacheSize => _maxCacheSize;
        public float CacheEntryLifetime => _cacheEntryLifetime;
        public bool EnableLRUEviction => _enableLRUEviction;
        public float MaxResolutionTimeMs => _maxResolutionTimeMs;
        public bool EnableMemoryPressureResponse => _enableMemoryPressureResponse;
        public bool ValidateTemplatesAtStartup => _validateTemplatesAtStartup;
        public bool StrictValidationMode => _strictValidationMode;
        public bool LogMissingResources => _logMissingResources;
        
        public IReadOnlyList<PathTemplateEntry> PathTemplates => _pathTemplates;
        public IReadOnlyList<FallbackPathEntry> FallbackPaths => _fallbackPaths;
        public IReadOnlyList<EnvironmentOverride> EnvironmentOverrides => _environmentOverrides;
        
        #endregion
        
        #region Template Management
        
        /// <summary>
        /// Gets the path template for a specific resource type and category
        /// </summary>
        public string GetPathTemplate(ResourceType resourceType, ResourceCategory category)
        {
            var template = _pathTemplates.FirstOrDefault(t => 
                t.ResourceType == resourceType && t.Category == category);
            return template?.Template;
        }
        
        /// <summary>
        /// Gets all templates for a specific resource type
        /// </summary>
        public IEnumerable<PathTemplateEntry> GetTemplatesForType(ResourceType resourceType)
        {
            return _pathTemplates.Where(t => t.ResourceType == resourceType);
        }
        
        /// <summary>
        /// Gets fallback paths for a specific resource type and category
        /// </summary>
        public IEnumerable<string> GetFallbackPaths(ResourceType resourceType, ResourceCategory category)
        {
            return _fallbackPaths
                .Where(f => f.ResourceType == resourceType && f.Category == category)
                .OrderByDescending(f => f.Priority)
                .Select(f => f.FallbackTemplate);
        }
        
        #endregion
        
        #region Environment Overrides
        
        /// <summary>
        /// Gets environment-specific overrides
        /// </summary>
        public EnvironmentOverride GetEnvironmentOverride(ResourceEnvironment environment)
        {
            return _environmentOverrides.FirstOrDefault(o => o.Environment == environment);
        }
        
        /// <summary>
        /// Applies environment overrides to get effective configuration
        /// </summary>
        public ResourcePathResolverConfiguration GetEffectiveConfiguration(ResourceEnvironment environment)
        {
            var effectiveConfig = Instantiate(this);
            var environmentOverride = GetEnvironmentOverride(environment);
            
            if (environmentOverride != null)
            {
                effectiveConfig.ApplyEnvironmentOverride(environmentOverride);
            }
            
            return effectiveConfig;
        }
        
        private void ApplyEnvironmentOverride(EnvironmentOverride environmentOverride)
        {
            if (environmentOverride.OverrideResourceRoot)
                _resourceRoot = environmentOverride.ResourceRoot;
            
            if (environmentOverride.OverrideCacheSettings)
            {
                _maxCacheSize = environmentOverride.MaxCacheSize;
                _cacheEntryLifetime = environmentOverride.CacheEntryLifetime;
            }
            
            if (environmentOverride.OverrideValidationSettings)
            {
                _enablePathValidation = environmentOverride.EnablePathValidation;
                _enableExistenceChecking = environmentOverride.EnableExistenceChecking;
                _strictValidationMode = environmentOverride.StrictValidationMode;
            }
        }
        
        #endregion
        
        #region IServiceConfiguration Implementation
        
        public override bool Validate(out List<string> errors)
        {
            errors = new List<string>();
            
            // Validate basic settings
            
            if (_maxCacheSize <= 0)
                errors.Add("Max cache size must be greater than 0");
            
            if (_cacheEntryLifetime <= 0)
                errors.Add("Cache entry lifetime must be greater than 0");
            
            // Validate path templates
            foreach (var template in _pathTemplates)
            {
                if (string.IsNullOrEmpty(template.Template))
                {
                    errors.Add($"Path template for {template.ResourceType}.{template.Category} cannot be empty");
                    continue;
                }
                
                // Basic template validation - check for balanced braces
                var openBraces = template.Template.Count(c => c == '{');
                var closeBraces = template.Template.Count(c => c == '}');
                if (openBraces != closeBraces)
                {
                    errors.Add($"Unbalanced braces in template for {template.ResourceType}.{template.Category}: {template.Template}");
                }
            }
            
            // Validate fallback paths
            foreach (var fallback in _fallbackPaths)
            {
                if (string.IsNullOrEmpty(fallback.FallbackTemplate))
                {
                    errors.Add($"Fallback template for {fallback.ResourceType}.{fallback.Category} cannot be empty");
                }
            }
            
            // Check for duplicate templates
            var duplicateTemplates = _pathTemplates
                .GroupBy(t => new { t.ResourceType, t.Category })
                .Where(g => g.Count() > 1)
                .Select(g => g.Key);
            
            foreach (var duplicate in duplicateTemplates)
            {
                errors.Add($"Duplicate template found for {duplicate.ResourceType}.{duplicate.Category}");
            }
            
            return errors.Count == 0;
        }
        
        public void ApplyDefaults()
        {
            if (_pathTemplates == null || _pathTemplates.Length == 0)
            {
                _pathTemplates = CreateDefaultPathTemplates();
            }
            
            if (_fallbackPaths == null || _fallbackPaths.Length == 0)
            {
                _fallbackPaths = CreateDefaultFallbackPaths();
            }
        }
        
        #endregion
        
        #region Default Templates
        
        private PathTemplateEntry[] CreateDefaultPathTemplates()
        {
            return new[]
            {
                // Actor templates - organized by resource type first, then category
                new PathTemplateEntry(ResourceType.Actor, ResourceCategory.Primary, "Actors/{actorType}/{actorId}", PathPriority.High),
                new PathTemplateEntry(ResourceType.Actor, ResourceCategory.Sprites, "Actors/{actorType}/{actorId}/Sprites/{addressableKey}", PathPriority.High),
                
                // Layered character templates for layer-based loading
                new PathTemplateEntry(ResourceType.Actor, ResourceCategory.Primary, "Actors/Characters/{characterName}/{layerPath}", PathPriority.High),
                new PathTemplateEntry(ResourceType.Actor, ResourceCategory.Animations, "Actors/{actorType}/Animations/{actorId}/{animationType}", PathPriority.High),
                new PathTemplateEntry(ResourceType.Actor, ResourceCategory.Audio, "Actors/{actorType}/Audio/{actorId}/{audioType}", PathPriority.High),
                new PathTemplateEntry(ResourceType.Actor, ResourceCategory.Metadata, "Actors/{actorType}/Metadata/{actorId}", PathPriority.High),
                new PathTemplateEntry(ResourceType.Actor, ResourceCategory.Textures, "Actors/{actorType}/Textures/{actorId}/{appearance}", PathPriority.High),
                new PathTemplateEntry(ResourceType.Actor, ResourceCategory.Effects, "Actors/{actorType}/Effects/{actorId}/{effectType}", PathPriority.High),
                new PathTemplateEntry(ResourceType.Actor, ResourceCategory.Music, "Actors/{actorType}/Music/{actorId}/{trackName}", PathPriority.High),
                
                // Script templates
                new PathTemplateEntry(ResourceType.Script, ResourceCategory.Source, "Scripts/{scriptName}.script", PathPriority.High),
                new PathTemplateEntry(ResourceType.Script, ResourceCategory.Compiled, "Scripts/Compiled/{scriptName}.bytes", PathPriority.High),
                new PathTemplateEntry(ResourceType.Script, ResourceCategory.Metadata, "Scripts/Metadata/{scriptName}.meta", PathPriority.Normal),
                
                // Audio templates
                new PathTemplateEntry(ResourceType.Audio, ResourceCategory.Music, "Audio/Music/{category}/{trackName}", PathPriority.High),
                new PathTemplateEntry(ResourceType.Audio, ResourceCategory.Primary, "Audio/SFX/{category}/{soundName}", PathPriority.High),
                
                // UI templates
                new PathTemplateEntry(ResourceType.UI, ResourceCategory.Primary, "UI/{category}/{elementName}", PathPriority.High),
                
                // Prefab templates (shared per type, not per actor)
                new PathTemplateEntry(ResourceType.Prefab, ResourceCategory.Primary, "Prefabs/{actorType}/{actorType}ActorPrefab", PathPriority.High),
                
                // Config templates
                new PathTemplateEntry(ResourceType.Config, ResourceCategory.Primary, "Configs/{configType}/{configName}", PathPriority.High),
            };
        }
        
        private FallbackPathEntry[] CreateDefaultFallbackPaths()
        {
            return new[]
            {
                // Actor fallbacks - matching resource type structure
                new FallbackPathEntry(ResourceType.Actor, ResourceCategory.Sprites, "Actors/Default/Sprites/MissingSprite", PathPriority.Low),
                new FallbackPathEntry(ResourceType.Actor, ResourceCategory.Audio, "Actors/Default/Audio/MissingAudio", PathPriority.Low),
                new FallbackPathEntry(ResourceType.Actor, ResourceCategory.Textures, "Actors/Default/Textures/MissingTexture", PathPriority.Low),
                new FallbackPathEntry(ResourceType.Actor, ResourceCategory.Effects, "Actors/Default/Effects/MissingEffect", PathPriority.Low),
                new FallbackPathEntry(ResourceType.Actor, ResourceCategory.Animations, "Actors/Default/Animations/MissingAnimation", PathPriority.Low),
                
                // Script fallbacks
                new FallbackPathEntry(ResourceType.Script, ResourceCategory.Source, "Scripts/Default/EmptyScript.script", PathPriority.Low),
                
                // Audio fallbacks
                new FallbackPathEntry(ResourceType.Audio, ResourceCategory.Primary, "Audio/Default/Silence", PathPriority.Low),
                new FallbackPathEntry(ResourceType.Audio, ResourceCategory.Music, "Audio/Default/DefaultMusic", PathPriority.Low),
                
                // UI fallbacks
                new FallbackPathEntry(ResourceType.UI, ResourceCategory.Primary, "UI/Default/MissingUI", PathPriority.Low),
                
                // Prefab fallbacks
                new FallbackPathEntry(ResourceType.Prefab, ResourceCategory.Primary, "Prefabs/Default/DefaultPrefab", PathPriority.Low),
                
                // Config fallbacks
                new FallbackPathEntry(ResourceType.Config, ResourceCategory.Primary, "Configs/Default/DefaultConfig", PathPriority.Low),
            };
        }
        
        #endregion
        
        #region Unity Editor Support
        
        [Button("Generate Default Templates")]
        [TabGroup("Templates")]
        private void GenerateDefaultTemplates()
        {
            _pathTemplates = CreateDefaultPathTemplates();
            _fallbackPaths = CreateDefaultFallbackPaths();
            
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }
        
        [Button("Validate Configuration")]
        [TabGroup("Validation")]
        private void ValidateConfigurationInEditor()
        {
            var isValid = Validate(out var errors);
            var message = isValid ? "Configuration is valid!" : $"Configuration errors:\n{string.Join("\n", errors)}";
            
#if UNITY_EDITOR
            UnityEditor.EditorUtility.DisplayDialog("Configuration Validation", message, "OK");
#endif
        }
        
        #endregion
    }
    
    #region Configuration Data Structures
    
    [Serializable]
    public class PathTemplateEntry
    {
        [HorizontalGroup("Template")]
        [LabelWidth(80)]
        public ResourceType ResourceType;
        
        [HorizontalGroup("Template")]
        [LabelWidth(60)]
        public ResourceCategory Category;
        
        [HorizontalGroup("Template")]
        [LabelWidth(60)]
        public PathPriority Priority;
        
        [HideLabel]
        [MultiLineProperty(2)]
        public string Template;
        
        [TextArea(2, 4)]
        [Tooltip("Description of this template")]
        public string Description;
        
        public PathTemplateEntry() { }
        
        public PathTemplateEntry(ResourceType resourceType, ResourceCategory category, string template, PathPriority priority = PathPriority.Normal)
        {
            ResourceType = resourceType;
            Category = category;
            Template = template;
            Priority = priority;
        }
    }
    
    [Serializable]
    public class FallbackPathEntry
    {
        [HorizontalGroup("Fallback")]
        [LabelWidth(80)]
        public ResourceType ResourceType;
        
        [HorizontalGroup("Fallback")]
        [LabelWidth(60)]
        public ResourceCategory Category;
        
        [HorizontalGroup("Fallback")]
        [LabelWidth(60)]
        public PathPriority Priority;
        
        [HideLabel]
        [MultiLineProperty(2)]
        public string FallbackTemplate;
        
        public FallbackPathEntry() { }
        
        public FallbackPathEntry(ResourceType resourceType, ResourceCategory category, string fallbackTemplate, PathPriority priority = PathPriority.Low)
        {
            ResourceType = resourceType;
            Category = category;
            FallbackTemplate = fallbackTemplate;
            Priority = priority;
        }
    }
    
    [Serializable]
    public class EnvironmentOverride
    {
        [Title("Environment Override")]
        public ResourceEnvironment Environment;
        
        [FoldoutGroup("Resource Root Override")]
        public bool OverrideResourceRoot;
        
        [FoldoutGroup("Resource Root Override")]
        [ShowIf("OverrideResourceRoot")]
        public string ResourceRoot;
        
        [FoldoutGroup("Cache Settings Override")]
        public bool OverrideCacheSettings;
        
        [FoldoutGroup("Cache Settings Override")]
        [ShowIf("OverrideCacheSettings")]
        public int MaxCacheSize = 1000;
        
        [FoldoutGroup("Cache Settings Override")]
        [ShowIf("OverrideCacheSettings")]
        public float CacheEntryLifetime = 300f;
        
        [FoldoutGroup("Validation Settings Override")]
        public bool OverrideValidationSettings;
        
        [FoldoutGroup("Validation Settings Override")]
        [ShowIf("OverrideValidationSettings")]
        public bool EnablePathValidation = true;
        
        [FoldoutGroup("Validation Settings Override")]
        [ShowIf("OverrideValidationSettings")]
        public bool EnableExistenceChecking = false;
        
        [FoldoutGroup("Validation Settings Override")]
        [ShowIf("OverrideValidationSettings")]
        public bool StrictValidationMode = false;
    }
    
    #endregion
}