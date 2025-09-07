using System;
using System.Collections.Concurrent;
using System.Reflection;
using UnityEngine;

namespace Sinkii09.Engine.Commands
{
    /// <summary>
    /// Runtime metadata container for command execution behavior.
    /// Cached from CommandMetaAttribute and CommandAliasAttribute for performance.
    /// </summary>
    public class CommandMetadata
    {
        #region Properties
        // Identification
        public Type CommandType { get; }
        public string Alias { get; }
        
        // Timeout Management
        public float Timeout { get; }
        public bool AdaptiveTimeout { get; }
        
        // Retry Policy
        public int MaxRetries { get; }
        public float RetryDelay { get; }
        public RetryStrategy RetryStrategy { get; }
        
        // Command Classification
        public CommandCategory Category { get; }
        public bool Critical { get; }
        public CommandPriority Priority { get; }
        
        // Recovery Behavior
        public FallbackAction Fallback { get; }
        public bool SkippableOnError { get; }
        public bool AllowUserIntervention { get; }
        
        // Performance
        public bool Cacheable { get; }
        public bool TrackPerformance { get; }
        public float ExpectedDuration { get; }
        
        // Validation
        public bool ValidateParameters { get; }
        public bool PreloadResources { get; }
        #endregion

        #region Constructor
        public CommandMetadata(Type commandType, string alias, CommandMetaAttribute meta)
        {
            CommandType = commandType ?? throw new ArgumentNullException(nameof(commandType));
            Alias = alias ?? commandType.Name;
            
            // Use meta attributes if provided, otherwise use defaults
            if (meta != null)
            {
                Timeout = meta.Timeout;
                AdaptiveTimeout = meta.AdaptiveTimeout;
                MaxRetries = meta.MaxRetries;
                RetryDelay = meta.RetryDelay;
                RetryStrategy = meta.RetryStrategy;
                Category = meta.Category;
                Critical = meta.Critical;
                Priority = meta.Priority;
                Fallback = meta.Fallback;
                SkippableOnError = meta.SkippableOnError;
                AllowUserIntervention = meta.AllowUserIntervention;
                Cacheable = meta.Cacheable;
                TrackPerformance = meta.TrackPerformance;
                ExpectedDuration = meta.ExpectedDuration;
                ValidateParameters = meta.ValidateParameters;
                PreloadResources = meta.PreloadResources;
            }
            else
            {
                // Default values when no CommandMeta attribute is present
                Timeout = GetDefaultTimeout(commandType);
                AdaptiveTimeout = true;
                MaxRetries = GetDefaultMaxRetries(commandType);
                RetryDelay = 1f;
                RetryStrategy = RetryStrategy.Linear;
                Category = GetDefaultCategory(commandType);
                Critical = false;
                Priority = CommandPriority.Normal;
                Fallback = FallbackAction.Continue;
                SkippableOnError = true;
                AllowUserIntervention = false;
                Cacheable = true;
                TrackPerformance = true;
                ExpectedDuration = 0f;
                ValidateParameters = true;
                PreloadResources = false;
            }
        }
        #endregion

        #region Default Value Logic
        private static float GetDefaultTimeout(Type commandType)
        {
            // Provide sensible defaults based on command name patterns
            var name = commandType.Name.ToLowerInvariant();
            
            if (name.Contains("load") || name.Contains("resource"))
                return 60f; // Resource loading commands need more time
            
            if (name.Contains("save") || name.Contains("state"))
                return 45f; // Save operations can be slow
            
            if (name.Contains("network") || name.Contains("web"))
                return 30f; // Network operations need time
            
            if (name.Contains("wait") || name.Contains("delay"))
                return 300f; // Wait commands might need long timeouts
            
            return 15f; // Default for most commands
        }
        
        private static int GetDefaultMaxRetries(Type commandType)
        {
            // Provide sensible retry defaults based on command patterns
            var name = commandType.Name.ToLowerInvariant();
            
            if (name.Contains("critical") || name.Contains("save"))
                return 5; // Important operations should retry more
            
            if (name.Contains("network") || name.Contains("load"))
                return 3; // Network/loading operations should retry
            
            if (name.Contains("ui") || name.Contains("animation"))
                return 1; // UI/animation commands rarely benefit from retries
            
            return 2; // Default retry count
        }
        
        private static CommandCategory GetDefaultCategory(Type commandType)
        {
            // Auto-detect category based on command name patterns
            var name = commandType.Name.ToLowerInvariant();
            
            if (name.Contains("load") || name.Contains("resource"))
                return CommandCategory.ResourceLoading;
            
            if (name.Contains("say") || name.Contains("dialog") || name.Contains("think"))
                return CommandCategory.Dialogue;
            
            if (name.Contains("show") || name.Contains("hide") || name.Contains("move") || name.Contains("actor"))
                return CommandCategory.ActorManagement;
            
            if (name.Contains("play") || name.Contains("audio") || name.Contains("sound"))
                return CommandCategory.Audio;
            
            if (name.Contains("animate") || name.Contains("tween") || name.Contains("effect"))
                return CommandCategory.Animation;
            
            if (name.Contains("ui") || name.Contains("menu") || name.Contains("button"))
                return CommandCategory.UI;
            
            if (name.Contains("if") || name.Contains("goto") || name.Contains("jump") || name.Contains("loop"))
                return CommandCategory.FlowControl;
            
            if (name.Contains("wait") || name.Contains("delay"))
                return CommandCategory.Timing;
            
            if (name.Contains("set") || name.Contains("add") || name.Contains("variable"))
                return CommandCategory.Variables;
            
            if (name.Contains("save") || name.Contains("state"))
                return CommandCategory.StateManagement;
            
            if (name.Contains("scene") || name.Contains("background"))
                return CommandCategory.Scene;
            
            return CommandCategory.Generic;
        }
        #endregion

        #region Utility Methods
        /// <summary>
        /// Calculate actual retry delay based on retry strategy and attempt number
        /// </summary>
        public float CalculateRetryDelay(int attemptNumber)
        {
            return RetryStrategy switch
            {
                RetryStrategy.Immediate => 0f,
                RetryStrategy.Linear => RetryDelay,
                RetryStrategy.Exponential => RetryDelay * (float)Math.Pow(2, attemptNumber - 1),
                RetryStrategy.Adaptive => RetryDelay * (1 + attemptNumber * 0.5f), // Gradually increase
                _ => RetryDelay
            };
        }

        /// <summary>
        /// Check if command should be retried based on current attempt and error
        /// </summary>
        public bool ShouldRetry(int currentAttempt, Exception error)
        {
            if (currentAttempt >= MaxRetries)
                return false;

            // Don't retry critical errors unless explicitly configured
            if (error is OperationCanceledException)
                return false;

            // Don't retry validation errors
            if (error is ArgumentException || error is InvalidOperationException)
                return RetryStrategy == RetryStrategy.Adaptive;

            return true;
        }

        /// <summary>
        /// Get summary string for debugging
        /// </summary>
        public override string ToString()
        {
            return $"{CommandType.Name} [{Alias}] - " +
                   $"Timeout:{Timeout}s, Retries:{MaxRetries}, " +
                   $"Category:{Category}, Critical:{Critical}";
        }
        #endregion
    }

    /// <summary>
    /// High-performance cache for command metadata to avoid repeated reflection
    /// </summary>
    public static class CommandMetadataCache
    {
        private static readonly ConcurrentDictionary<Type, CommandMetadata> _cache = 
            new ConcurrentDictionary<Type, CommandMetadata>();

        /// <summary>
        /// Get cached metadata for a command type, creating if necessary
        /// </summary>
        public static CommandMetadata GetMetadata(Type commandType)
        {
            return _cache.GetOrAdd(commandType, CreateMetadata);
        }

        /// <summary>
        /// Get cached metadata for a command type, creating if necessary
        /// </summary>
        public static CommandMetadata GetMetadata<T>() where T : ICommand
        {
            return GetMetadata(typeof(T));
        }

        /// <summary>
        /// Clear the metadata cache (useful for hot reloading in editor)
        /// </summary>
        public static void ClearCache()
        {
            _cache.Clear();
        }

        /// <summary>
        /// Get cache statistics for performance monitoring
        /// </summary>
        public static (int Count, long MemoryEstimate) GetCacheStats()
        {
            var count = _cache.Count;
            var memoryEstimate = count * 200; // Rough estimate of bytes per metadata object
            return (count, memoryEstimate);
        }

        private static CommandMetadata CreateMetadata(Type commandType)
        {
            // Get command alias
            var aliasAttr = commandType.GetCustomAttribute<CommandAliasAttribute>();
            var alias = aliasAttr?.Alias ?? commandType.Name;

            // Get command meta
            var metaAttr = commandType.GetCustomAttribute<CommandMetaAttribute>();

            // Create metadata object
            var metadata = new CommandMetadata(commandType, alias, metaAttr);

            if (Application.isEditor)
            {
                Debug.Log($"[CommandMetadataCache] Cached metadata for {commandType.Name}: {metadata}");
            }

            return metadata;
        }
    }
}