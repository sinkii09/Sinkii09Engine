using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Sinkii09.Engine.Services
{
    /// <summary>
    /// Layered character appearance system inspired by Naninovel
    /// Allows runtime composition of base pose, expressions, outfits, and accessories
    /// </summary>
    [Serializable]
    public struct LayeredCharacterAppearance : IAppearance, IEquatable<LayeredCharacterAppearance>
    {
        [Header("Base Layer (Required)")]
        [SerializeField] private CharacterPose _basePose;
        
        [Header("Expression Layers")]
        [SerializeField] private CharacterEmotion[] _expressionLayers;
        
        [Header("Outfit Layers")]
        [SerializeField] private int[] _outfitLayers;
        
        [Header("Accessory Layers")]
        [SerializeField] private string[] _accessoryLayers;
        
        [Header("Layer Control")]
        [SerializeField] private bool _useExpressionOverlay;
        [SerializeField] private float _layerOpacity;
        
        #region Properties
        
        public CharacterPose BasePose
        {
            get => _basePose;
            set => _basePose = value;
        }
        
        public CharacterEmotion[] ExpressionLayers
        {
            get => _expressionLayers ?? new CharacterEmotion[0];
            set => _expressionLayers = value ?? new CharacterEmotion[0];
        }
        
        public int[] OutfitLayers
        {
            get => _outfitLayers ?? new int[0];
            set => _outfitLayers = value ?? new int[0];
        }
        
        public string[] AccessoryLayers
        {
            get => _accessoryLayers ?? new string[0];
            set => _accessoryLayers = value ?? new string[0];
        }
        
        public bool UseExpressionOverlay
        {
            get => _useExpressionOverlay;
            set => _useExpressionOverlay = value;
        }
        
        public float LayerOpacity
        {
            get => _layerOpacity;
            set => _layerOpacity = Mathf.Clamp01(value);
        }
        
        /// <summary>
        /// Primary expression (first in expression layers)
        /// </summary>
        public CharacterEmotion PrimaryExpression =>
            ExpressionLayers.Length > 0 ? ExpressionLayers[0] : CharacterEmotion.Neutral;
            
        /// <summary>
        /// Primary outfit (first in outfit layers)
        /// </summary>
        public int PrimaryOutfit =>
            OutfitLayers.Length > 0 ? OutfitLayers[0] : 0;
        
        #endregion
        
        #region Constructors
        
        public LayeredCharacterAppearance(CharacterPose basePose)
        {
            _basePose = basePose;
            _expressionLayers = new CharacterEmotion[] { CharacterEmotion.Neutral };
            _outfitLayers = new int[] { 0 };
            _accessoryLayers = new string[0];
            _useExpressionOverlay = true;
            _layerOpacity = 1f;
        }
        
        public LayeredCharacterAppearance(CharacterPose basePose, CharacterEmotion expression, int outfit = 0)
        {
            _basePose = basePose;
            _expressionLayers = new CharacterEmotion[] { expression };
            _outfitLayers = new int[] { outfit };
            _accessoryLayers = new string[0];
            _useExpressionOverlay = true;
            _layerOpacity = 1f;
        }
        
        public LayeredCharacterAppearance(CharacterPose basePose, CharacterEmotion[] expressions, int[] outfits, string[] accessories = null)
        {
            _basePose = basePose;
            _expressionLayers = expressions ?? new CharacterEmotion[] { CharacterEmotion.Neutral };
            _outfitLayers = outfits ?? new int[] { 0 };
            _accessoryLayers = accessories ?? new string[0];
            _useExpressionOverlay = true;
            _layerOpacity = 1f;
        }
        
        #endregion
        
        #region Layer Management
        
        /// <summary>
        /// Gets all layer paths for loading individual sprites with comprehensive pose compatibility
        /// </summary>
        public string[] GetLayerPaths(string actorId)
        {
            var paths = new List<string>();
            
            // Base layer (always required)
            paths.Add($"base/{_basePose.ToString().ToLower()}");
            
            // Expression layers with pose-specific fallbacks
            if (_useExpressionOverlay)
            {
                foreach (var expression in ExpressionLayers)
                {
                    var expressionPaths = GetLayerPaths("expressions", expression.ToString().ToLower());
                    paths.AddRange(expressionPaths);
                }
            }
            
            // Outfit layers with pose-specific fallbacks
            foreach (var outfit in OutfitLayers)
            {
                var outfitPaths = GetLayerPaths("outfits", $"outfit_{outfit:D2}");
                paths.AddRange(outfitPaths);
            }
            
            // Accessory layers with pose-specific fallbacks
            foreach (var accessory in AccessoryLayers)
            {
                if (!string.IsNullOrEmpty(accessory))
                {
                    var accessoryPaths = GetLayerPaths("accessories", accessory.ToLower());
                    paths.AddRange(accessoryPaths);
                }
            }
            
            return paths.ToArray();
        }
        
        /// <summary>
        /// Gets layer paths with pose-specific and fallback options for any layer type
        /// </summary>
        private string[] GetLayerPaths(string layerType, string layerName)
        {
            var paths = new List<string>();
            var poseName = _basePose.ToString().ToLower();
            
            // Priority 1: Pose-specific layer
            paths.Add($"{layerType}/{poseName}/{layerName}");
            
            // Priority 2: Universal layer (works with any pose)
            paths.Add($"{layerType}/universal/{layerName}");
            
            // Priority 3: Generic layer (backward compatibility)
            paths.Add($"{layerType}/{layerName}");
            
            return paths.ToArray();
        }
        
        /// <summary>
        /// Gets layer paths with fallback priorities for resource loading
        /// Returns array of path groups, each group contains fallback options in priority order
        /// </summary>
        public string[][] GetLayerPathsWithFallbacks(string actorId)
        {
            var pathGroups = new List<string[]>();
            
            // Base layer (no fallbacks needed - must exist)
            pathGroups.Add(new[] { $"base/{_basePose.ToString().ToLower()}" });
            
            // Expression layers with comprehensive fallbacks
            if (_useExpressionOverlay)
            {
                foreach (var expression in ExpressionLayers)
                {
                    var fallbacks = GetLayerPaths("expressions", expression.ToString().ToLower());
                    pathGroups.Add(fallbacks);
                }
            }
            
            // Outfit layers with pose-specific fallbacks
            foreach (var outfit in OutfitLayers)
            {
                var fallbacks = GetLayerPaths("outfits", $"outfit_{outfit:D2}");
                pathGroups.Add(fallbacks);
            }
            
            // Accessory layers with pose-specific fallbacks
            foreach (var accessory in AccessoryLayers)
            {
                if (!string.IsNullOrEmpty(accessory))
                {
                    var fallbacks = GetLayerPaths("accessories", accessory.ToLower());
                    pathGroups.Add(fallbacks);
                }
            }
            
            return pathGroups.ToArray();
        }
        
        /// <summary>
        /// Gets pose-specific compatibility info for debugging
        /// </summary>
        public string GetCompatibilityInfo()
        {
            var info = new List<string>();
            var poseName = _basePose.ToString().ToLower();
            
            info.Add($"Pose: {poseName}");
            
            if (_useExpressionOverlay && ExpressionLayers.Length > 0)
            {
                info.Add($"Expressions: {string.Join(", ", ExpressionLayers.Select(e => $"{poseName}/{e.ToString().ToLower()}"))}");
            }
            
            if (OutfitLayers.Length > 0)
            {
                info.Add($"Outfits: {string.Join(", ", OutfitLayers.Select(o => $"{poseName}/outfit_{o:D2}"))}");
            }
            
            if (AccessoryLayers.Length > 0)
            {
                info.Add($"Accessories: {string.Join(", ", AccessoryLayers.Select(a => $"{poseName}/{a}"))}");
            }
            
            return string.Join(" | ", info);
        }
        
        /// <summary>
        /// Gets addressable keys for each layer
        /// </summary>
        public string[] GetLayerAddressableKeys(string actorId)
        {
            var keys = new List<string>();
            var layerPaths = GetLayerPaths(actorId);
            
            foreach (var path in layerPaths)
            {
                keys.Add($"{actorId}_{path.Replace("/", "_")}");
            }
            
            return keys.ToArray();
        }
        
        /// <summary>
        /// Creates a layer composition expression (Naninovel-style)
        /// </summary>
        public string GetCompositionExpression()
        {
            var parts = new List<string>();
            
            // Base pose
            parts.Add($"base>{_basePose}");
            
            // Expressions
            if (_useExpressionOverlay && ExpressionLayers.Length > 0)
            {
                parts.Add($"expressions>{ExpressionLayers[0]}");
                
                // Additional expressions as overlays
                for (int i = 1; i < ExpressionLayers.Length; i++)
                {
                    parts.Add($"expressions+{ExpressionLayers[i]}");
                }
            }
            
            // Outfits
            foreach (var outfit in OutfitLayers)
            {
                parts.Add($"outfits+outfit_{outfit:D2}");
            }
            
            // Accessories
            foreach (var accessory in AccessoryLayers)
            {
                if (!string.IsNullOrEmpty(accessory))
                {
                    parts.Add($"accessories+{accessory}");
                }
            }
            
            return string.Join(",", parts);
        }
        
        #endregion
        
        #region IAppearance Implementation
        
        public string GetAddressableKey(string actorId)
        {
            // For backward compatibility, generate a compound key
            var expression = ExpressionLayers.Length > 0 ? ExpressionLayers[0].ToString() : "Neutral";
            var outfit = OutfitLayers.Length > 0 ? OutfitLayers[0] : 0;
            return $"layered_{actorId.ToLower()}_{expression.ToLower()}_{_basePose.ToString().ToLower()}_{outfit:D2}";
        }
        
        public string[] GetAddressableLabels()
        {
            var labels = new List<string>();
            
            // Base labels
            labels.Add($"pose_{_basePose.ToString().ToLower()}");
            labels.Add("layered_character");
            
            // Expression labels
            foreach (var expression in ExpressionLayers)
            {
                labels.Add($"expression_{expression.ToString().ToLower()}");
            }
            
            // Outfit labels
            foreach (var outfit in OutfitLayers)
            {
                labels.Add($"outfit_{outfit:D2}");
            }
            
            // Accessory labels
            foreach (var accessory in AccessoryLayers)
            {
                if (!string.IsNullOrEmpty(accessory))
                {
                    labels.Add($"accessory_{accessory.ToLower()}");
                }
            }
            
            return labels.ToArray();
        }
        
        public PathParameter[] GetPathParameters(string actorId)
        {
            var parameters = new List<PathParameter>
            {
                new(PathParameterNames.ACTOR_TYPE, "LayeredCharacter"),
                new(PathParameterNames.ACTOR_ID, actorId),
                new(PathParameterNames.CHARACTER_NAME, actorId),
                new(PathParameterNames.POSE, _basePose.ToString()),
                new(PathParameterNames.APPEARANCE, GetCompositionExpression()),
                new("layerCount", GetLayerPaths(actorId).Length.ToString()),
                new("hasExpressions", (ExpressionLayers.Length > 0).ToString()),
                new("hasOutfits", (OutfitLayers.Length > 0).ToString()),
                new("hasAccessories", (AccessoryLayers.Length > 0).ToString())
            };
            
            // Add primary expression and outfit
            if (ExpressionLayers.Length > 0)
            {
                parameters.Add(new(PathParameterNames.EXPRESSION, ExpressionLayers[0].ToString()));
            }
            
            if (OutfitLayers.Length > 0)
            {
                parameters.Add(new(PathParameterNames.OUTFIT, OutfitLayers[0].ToString("D2")));
            }
            
            return parameters.ToArray();
        }
        
        #endregion
        
        #region Layer Manipulation Methods
        
        /// <summary>
        /// Adds an expression layer
        /// </summary>
        public LayeredCharacterAppearance WithExpression(CharacterEmotion expression)
        {
            var newExpressions = new List<CharacterEmotion>(ExpressionLayers) { expression };
            return new LayeredCharacterAppearance(_basePose, newExpressions.ToArray(), OutfitLayers, AccessoryLayers);
        }
        
        /// <summary>
        /// Sets the primary expression (replaces all expressions)
        /// </summary>
        public LayeredCharacterAppearance WithPrimaryExpression(CharacterEmotion expression)
        {
            return new LayeredCharacterAppearance(_basePose, new[] { expression }, OutfitLayers, AccessoryLayers);
        }
        
        /// <summary>
        /// Changes the base pose
        /// </summary>
        public LayeredCharacterAppearance WithPose(CharacterPose pose)
        {
            return new LayeredCharacterAppearance(pose, ExpressionLayers, OutfitLayers, AccessoryLayers);
        }
        
        /// <summary>
        /// Adds an outfit layer
        /// </summary>
        public LayeredCharacterAppearance WithOutfit(int outfitId)
        {
            var newOutfits = new List<int>(OutfitLayers) { outfitId };
            return new LayeredCharacterAppearance(_basePose, ExpressionLayers, newOutfits.ToArray(), AccessoryLayers);
        }
        
        /// <summary>
        /// Adds an accessory layer
        /// </summary>
        public LayeredCharacterAppearance WithAccessory(string accessory)
        {
            var newAccessories = new List<string>(AccessoryLayers) { accessory };
            return new LayeredCharacterAppearance(_basePose, ExpressionLayers, OutfitLayers, newAccessories.ToArray());
        }
        
        /// <summary>
        /// Removes all layers of a specific type
        /// </summary>
        public LayeredCharacterAppearance WithoutExpressions()
        {
            return new LayeredCharacterAppearance(_basePose, new CharacterEmotion[0], OutfitLayers, AccessoryLayers);
        }
        
        #endregion
        
        #region Equality and Conversion
        
        public bool Equals(LayeredCharacterAppearance other)
        {
            return _basePose == other._basePose &&
                   ExpressionLayers.SequenceEqual(other.ExpressionLayers) &&
                   OutfitLayers.SequenceEqual(other.OutfitLayers) &&
                   AccessoryLayers.SequenceEqual(other.AccessoryLayers) &&
                   _useExpressionOverlay == other._useExpressionOverlay;
        }
        
        public override bool Equals(object obj)
            => obj is LayeredCharacterAppearance other && Equals(other);
        
        public override int GetHashCode()
        {
            var hash = _basePose.GetHashCode();
            foreach (var expr in ExpressionLayers)
                hash ^= expr.GetHashCode();
            foreach (var outfit in OutfitLayers)
                hash ^= outfit.GetHashCode();
            foreach (var accessory in AccessoryLayers)
                hash ^= accessory?.GetHashCode() ?? 0;
            return hash;
        }
        
        public static bool operator ==(LayeredCharacterAppearance left, LayeredCharacterAppearance right)
            => left.Equals(right);
        
        public static bool operator !=(LayeredCharacterAppearance left, LayeredCharacterAppearance right)
            => !left.Equals(right);
        
        public override string ToString()
            => GetCompositionExpression();
        
        /// <summary>
        /// Converts from simple CharacterAppearance to LayeredCharacterAppearance
        /// </summary>
        public static implicit operator LayeredCharacterAppearance(CharacterAppearance appearance)
        {
            return new LayeredCharacterAppearance(
                appearance.Pose,
                new[] { appearance.Expression },
                new[] { appearance.OutfitId }
            );
        }
        
        #endregion
        
        #region Predefined Layered Appearances
        
        public static readonly LayeredCharacterAppearance Default = 
            new(CharacterPose.Standing, CharacterEmotion.Neutral, 0);
            
        public static readonly LayeredCharacterAppearance HappyStanding = 
            new(CharacterPose.Standing, CharacterEmotion.Happy, 0);
            
        public static readonly LayeredCharacterAppearance SadSitting = 
            new(CharacterPose.Sitting, CharacterEmotion.Sad, 0);
            
        public static readonly LayeredCharacterAppearance SchoolUniform = 
            new(CharacterPose.Standing, CharacterEmotion.Neutral, 1);
        
        #endregion
    }
}