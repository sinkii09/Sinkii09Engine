using System;
using System.Collections.Generic;

namespace Sinkii09.Engine.Services
{
    /// <summary>
    /// Generic action filter for input context management
    /// Handles filtering logic for any enum-based action type
    /// </summary>
    /// <typeparam name="T">Enum type representing actions (PlayerAction or UIAction)</typeparam>
    [Serializable]
    public class ActionFilter<T> where T : Enum
    {
        #region Properties
        
        /// <summary>
        /// Filter mode determining how Actions set is interpreted
        /// </summary>
        public InputFilterMode Mode { get; set; } = InputFilterMode.PassThrough;
        
        /// <summary>
        /// Set of actions for filtering - meaning depends on Mode
        /// </summary>
        public HashSet<T> Actions { get; set; } = new HashSet<T>();
        
        #endregion
        
        #region Public Methods
        
        /// <summary>
        /// Check if the specified action is allowed by this filter
        /// </summary>
        /// <param name="action">Action to check</param>
        /// <returns>True if action is allowed, false if blocked</returns>
        public bool IsAllowed(T action)
        {
            return Mode switch
            {
                InputFilterMode.PassThrough => true,
                InputFilterMode.AllowOnly => Actions.Contains(action),
                InputFilterMode.BlockExcept => Actions.Contains(action),
                _ => true
            };
        }
        
        /// <summary>
        /// Add an action to the filter set
        /// </summary>
        /// <param name="action">Action to add</param>
        /// <returns>This filter for method chaining</returns>
        public ActionFilter<T> Add(T action)
        {
            Actions.Add(action);
            return this;
        }
        
        /// <summary>
        /// Add multiple actions to the filter set
        /// </summary>
        /// <param name="actions">Actions to add</param>
        /// <returns>This filter for method chaining</returns>
        public ActionFilter<T> Add(params T[] actions)
        {
            foreach (var action in actions)
            {
                Actions.Add(action);
            }
            return this;
        }
        
        /// <summary>
        /// Remove an action from the filter set
        /// </summary>
        /// <param name="action">Action to remove</param>
        /// <returns>This filter for method chaining</returns>
        public ActionFilter<T> Remove(T action)
        {
            Actions.Remove(action);
            return this;
        }
        
        /// <summary>
        /// Clear all actions from the filter set
        /// </summary>
        /// <returns>This filter for method chaining</returns>
        public ActionFilter<T> Clear()
        {
            Actions.Clear();
            return this;
        }
        
        /// <summary>
        /// Set the filter mode
        /// </summary>
        /// <param name="mode">New filter mode</param>
        /// <returns>This filter for method chaining</returns>
        public ActionFilter<T> WithMode(InputFilterMode mode)
        {
            Mode = mode;
            return this;
        }
        
        #endregion
        
        #region Static Factory Methods
        
        /// <summary>
        /// Create a filter that allows everything (pass through)
        /// </summary>
        /// <returns>New PassThrough filter</returns>
        public static ActionFilter<T> AllowAll()
        {
            return new ActionFilter<T>
            {
                Mode = InputFilterMode.PassThrough
            };
        }
        
        /// <summary>
        /// Create a filter that only allows specified actions
        /// </summary>
        /// <param name="actions">Actions to allow</param>
        /// <returns>New AllowOnly filter</returns>
        public static ActionFilter<T> AllowOnly(params T[] actions)
        {
            return new ActionFilter<T>
            {
                Mode = InputFilterMode.AllowOnly,
                Actions = new HashSet<T>(actions)
            };
        }
        
        /// <summary>
        /// Create a filter that blocks all except specified actions
        /// </summary>
        /// <param name="actions">Actions to allow (exceptions)</param>
        /// <returns>New BlockExcept filter</returns>
        public static ActionFilter<T> BlockExcept(params T[] actions)
        {
            return new ActionFilter<T>
            {
                Mode = InputFilterMode.BlockExcept,
                Actions = new HashSet<T>(actions)
            };
        }
        
        #endregion
        
        #region Debugging
        
        /// <summary>
        /// Get debug information about this filter
        /// </summary>
        /// <returns>Human-readable filter description</returns>
        public override string ToString()
        {
            return Mode switch
            {
                InputFilterMode.PassThrough => "PassThrough (all allowed)",
                InputFilterMode.AllowOnly => $"AllowOnly ({Actions.Count} actions)",
                InputFilterMode.BlockExcept => $"BlockExcept ({Actions.Count} exceptions)",
                _ => "Unknown mode"
            };
        }
        
        #endregion
    }
}