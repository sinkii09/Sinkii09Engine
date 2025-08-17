namespace Sinkii09.Engine.Services
{
    /// <summary>
    /// Standard path parameter names used across the engine for resource path resolution
    /// </summary>
    public static class PathParameterNames
    {
        // Core parameters
        public const string ACTOR_ID = "actorId";
        public const string ACTOR_TYPE = "actorType";
        public const string RESOURCE_ID = "resourceId";
        public const string ID = "id";
        public const string ENVIRONMENT = "environment";
        public const string RESOURCE_ROOT = "resourceRoot";
        public const string ADDRESSABLES_KEY = "addressableKey";

        // Character-specific parameters
        public const string EXPRESSION = "expression";
        public const string POSE = "pose";
        public const string OUTFIT = "outfit";
        public const string CHARACTER_NAME = "characterName";
        public const string DEFAULT_EXPRESSION = "defaultExpression";
        public const string DEFAULT_POSE = "defaultPose";
        public const string DEFAULT_OUTFIT = "defaultOutfit";
        
        // Layered character parameters
        public const string LAYER_PATH = "layerPath";
        public const string LAYER_TYPE = "layerType";
        public const string LAYER_NAME = "layerName";
        public const string COMPOSITION_EXPRESSION = "compositionExpression";
        
        // Background-specific parameters
        public const string LOCATION = "location";
        public const string TIME_OF_DAY = "timeOfDay";
        public const string WEATHER = "weather";
        public const string VARIANT = "variant";
        public const string BACKGROUND_TYPE = "backgroundType";
        public const string DEFAULT_LOCATION = "defaultLocation";
        public const string DEFAULT_VARIANT = "defaultVariant";
        public const string DEFAULT_TRANSITION = "defaultTransition";
        
        // Generic appearance parameters
        public const string APPEARANCE = "appearance";
        public const string APPEARANCE_ID = "appearanceId";
        public const string DISPLAY_NAME = "displayName";
        public const string LAYER = "layer";
        
        // Script-specific parameters
        public const string SCRIPT_NAME = "scriptName";
        public const string SCRIPT_TYPE = "scriptType";
        
        // Audio-specific parameters
        public const string CATEGORY = "category";
        public const string TRACK_NAME = "trackName";
        public const string SOUND_NAME = "soundName";
        public const string AUDIO_TYPE = "audioType";
        
        // UI-specific parameters
        public const string ELEMENT_NAME = "elementName";
        public const string PREFAB_NAME = "prefabName";
        
        // Config-specific parameters
        public const string CONFIG_TYPE = "configType";
        public const string CONFIG_NAME = "configName";
        
        // Animation-specific parameters
        public const string ANIMATION_TYPE = "animationType";
        public const string CLIP_NAME = "clipName";
    }
}