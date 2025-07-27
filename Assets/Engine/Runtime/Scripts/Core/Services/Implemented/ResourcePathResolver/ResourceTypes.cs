using System;

namespace Sinkii09.Engine.Services
{
    /// <summary>
    /// Defines the major resource types supported by the ResourcePathResolver
    /// </summary>
    public enum ResourceType
    {
        /// <summary>
        /// Actor resources (characters, backgrounds, etc.)
        /// </summary>
        Actor = 0,
        
        /// <summary>
        /// Script resources (.script files, compiled scripts, etc.)
        /// </summary>
        Script = 1,
        
        /// <summary>
        /// Audio resources (music, sound effects, voice)
        /// </summary>
        Audio = 2,
        
        /// <summary>
        /// User interface resources (prefabs, textures, layouts)
        /// </summary>
        UI = 3,
        
        /// <summary>
        /// Texture resources (sprites, backgrounds, effects)
        /// </summary>
        Texture = 4,
        
        /// <summary>
        /// Prefab resources (GameObjects, components)
        /// </summary>
        Prefab = 5,
        
        /// <summary>
        /// Animation resources (clips, controllers, sequences)
        /// </summary>
        Animation = 6,
        
        /// <summary>
        /// Shader and material resources
        /// </summary>
        Shader = 7,
        
        /// <summary>
        /// Material resources
        /// </summary>
        Material = 8,
        
        /// <summary>
        /// Configuration and data resources
        /// </summary>
        Config = 9,
        
        /// <summary>
        /// Custom resource type for extensions
        /// </summary>
        Custom = 999
    }
    
    /// <summary>
    /// Defines resource categories for organizing related assets
    /// </summary>
    public enum ResourceCategory
    {
        /// <summary>
        /// Primary/default category for the resource type
        /// </summary>
        Primary = 0,
        
        /// <summary>
        /// Fallback resources when primary is unavailable
        /// </summary>
        Fallback = 1,
        
        /// <summary>
        /// Development-specific resources
        /// </summary>
        Development = 2,
        
        /// <summary>
        /// Production-optimized resources
        /// </summary>
        Production = 3,
        
        /// <summary>
        /// Sprite and image resources
        /// </summary>
        Sprites = 10,
        
        /// <summary>
        /// Animation-related resources
        /// </summary>
        Animations = 11,
        
        /// <summary>
        /// Audio files and sound resources
        /// </summary>
        Audio = 12,
        
        /// <summary>
        /// Metadata and configuration files
        /// </summary>
        Metadata = 13,
        
        /// <summary>
        /// Effect and particle resources
        /// </summary>
        Effects = 14,
        
        /// <summary>
        /// Music and background audio
        /// </summary>
        Music = 15,
        
        /// <summary>
        /// Texture and material resources
        /// </summary>
        Textures = 16,
        
        /// <summary>
        /// Compiled or processed resources
        /// </summary>
        Compiled = 17,
        
        /// <summary>
        /// Source files and raw resources
        /// </summary>
        Source = 18,
        
        /// <summary>
        /// Localization and translation resources
        /// </summary>
        Localization = 19,

        Prefabs = 20,

        Voice = 21,

        /// <summary>
        /// Custom category for extensions
        /// </summary>
        Custom = 999
    }
    
    /// <summary>
    /// Represents the environment context for resource resolution
    /// </summary>
    public enum ResourceEnvironment
    {
        /// <summary>
        /// Development environment with debug resources
        /// </summary>
        Development = 0,
        
        /// <summary>
        /// Production environment with optimized resources
        /// </summary>
        Production = 1,
        
        /// <summary>
        /// Testing environment with mock resources
        /// </summary>
        Testing = 2,
        
        /// <summary>
        /// Editor environment for Unity editor tools
        /// </summary>
        Editor = 3
    }
    
    /// <summary>
    /// Defines the priority level for path resolution fallbacks
    /// </summary>
    public enum PathPriority
    {
        /// <summary>
        /// Lowest priority, used only as last resort
        /// </summary>
        Low = 0,
        
        /// <summary>
        /// Normal priority for standard resources
        /// </summary>
        Normal = 1,
        
        /// <summary>
        /// High priority for important resources
        /// </summary>
        High = 2,
        
        /// <summary>
        /// Critical priority, always tried first
        /// </summary>
        Critical = 3
    }
    
    /// <summary>
    /// Result information from path resolution operations
    /// </summary>
    public readonly struct PathResolutionResult
    {
        public readonly string ResolvedPath;
        public readonly bool IsSuccess;
        public readonly string ErrorMessage;
        public readonly PathPriority UsedPriority;
        public readonly bool FromCache;
        public readonly TimeSpan ResolutionTime;
        
        public PathResolutionResult(string resolvedPath, bool isSuccess, string errorMessage = null, 
            PathPriority usedPriority = PathPriority.Normal, bool fromCache = false, TimeSpan resolutionTime = default)
        {
            ResolvedPath = resolvedPath;
            IsSuccess = isSuccess;
            ErrorMessage = errorMessage;
            UsedPriority = usedPriority;
            FromCache = fromCache;
            ResolutionTime = resolutionTime;
        }
        
        public static PathResolutionResult Success(string path, PathPriority priority = PathPriority.Normal, 
            bool fromCache = false, TimeSpan resolutionTime = default)
        {
            return new PathResolutionResult(path, true, null, priority, fromCache, resolutionTime);
        }
        
        public static PathResolutionResult Failure(string errorMessage, TimeSpan resolutionTime = default)
        {
            return new PathResolutionResult(null, false, errorMessage, PathPriority.Low, false, resolutionTime);
        }
    }
    
    /// <summary>
    /// Template parameter for path substitution
    /// </summary>
    public readonly struct PathParameter
    {
        public readonly string Name;
        public readonly object Value;
        public readonly Type ValueType;
        
        public PathParameter(string name, object value)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Value = value;
            ValueType = value?.GetType() ?? typeof(object);
        }
        
        public override string ToString()
        {
            return Value?.ToString() ?? string.Empty;
        }
        
        public string ToString(string format)
        {
            if (Value is IFormattable formattable && !string.IsNullOrEmpty(format))
            {
                return formattable.ToString(format, null);
            }
            return ToString();
        }
    }
}