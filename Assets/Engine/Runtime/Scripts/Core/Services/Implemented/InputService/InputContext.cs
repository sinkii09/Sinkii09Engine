using System;

namespace Sinkii09.Engine.Services
{
    /// <summary>
    /// Defines an input context with separate filtering for player and UI actions
    /// Modern priority-based system using handles instead of stack management
    /// </summary>
    [Serializable]
    public class InputContext
    {
        #region Properties
        
        /// <summary>
        /// Unique identifier for this context (automatically generated)
        /// </summary>
        public Guid Id { get; } = Guid.NewGuid();
        
        /// <summary>
        /// Semantic layer for priority management
        /// Higher layers process input before lower layers
        /// </summary>
        public InputLayer Layer { get; set; } = InputLayer.Gameplay;
        
        /// <summary>
        /// When this context was created (for priority ordering within same layer)
        /// </summary>
        public DateTime CreatedAt { get; } = DateTime.UtcNow;
        
        /// <summary>
        /// Filter configuration for PlayerAction inputs
        /// </summary>
        public ActionFilter<PlayerAction> PlayerFilter { get; set; } = ActionFilter<PlayerAction>.AllowAll();
        
        /// <summary>
        /// Filter configuration for UIAction inputs
        /// </summary>
        public ActionFilter<UIAction> UIFilter { get; set; } = ActionFilter<UIAction>.AllowAll();
        
        /// <summary>
        /// Whether this context consumes input (prevents lower priority contexts from processing)
        /// If false, input continues to lower priority contexts even if handled
        /// </summary>
        public bool ConsumeInput { get; set; } = true;
        
        /// <summary>
        /// Computed effective priority for processing order
        /// Higher values are processed first
        /// </summary>
        public long EffectivePriority => ((long)Layer * 1_000_000_000) + CreatedAt.Ticks;
        
        #endregion
        
        #region Optional Debug Info
        
#if DEBUG
        /// <summary>
        /// Optional debug information for logging and debugging
        /// Not used for any logic - purely for development visibility
        /// </summary>
        public string DebugInfo { get; set; }
        
        /// <summary>
        /// Stack trace of where this context was created (debug builds only)
        /// </summary>
        public System.Diagnostics.StackTrace CreationStack { get; } = new System.Diagnostics.StackTrace(1, true);
#endif
        
        #endregion
        
        #region Constructor
        
        /// <summary>
        /// Create a new input context with default settings
        /// </summary>
        public InputContext()
        {
        }
        
        /// <summary>
        /// Create a new input context with specified layer
        /// </summary>
        /// <param name="layer">Input layer for priority</param>
        public InputContext(InputLayer layer)
        {
            Layer = layer;
        }
        
        #endregion
        
        #region Input Checking
        
        /// <summary>
        /// Check if a PlayerAction is allowed by this context
        /// </summary>
        /// <param name="action">PlayerAction to check</param>
        /// <returns>True if allowed, false if blocked</returns>
        public bool IsAllowed(PlayerAction action)
        {
            return PlayerFilter.IsAllowed(action);
        }
        
        /// <summary>
        /// Check if a UIAction is allowed by this context
        /// </summary>
        /// <param name="action">UIAction to check</param>
        /// <returns>True if allowed, false if blocked</returns>
        public bool IsAllowed(UIAction action)
        {
            return UIFilter.IsAllowed(action);
        }
        
        #endregion
        
        #region Fluent Builder Methods
        
        /// <summary>
        /// Set the layer for this context (fluent API)
        /// </summary>
        /// <param name="layer">Layer to set</param>
        /// <returns>This context for method chaining</returns>
        public InputContext WithLayer(InputLayer layer)
        {
            Layer = layer;
            return this;
        }
        
        /// <summary>
        /// Set input consumption behavior (fluent API)
        /// </summary>
        /// <param name="consume">Whether to consume input</param>
        /// <returns>This context for method chaining</returns>
        public InputContext WithInputConsumption(bool consume)
        {
            ConsumeInput = consume;
            return this;
        }
        
        /// <summary>
        /// Configure PlayerAction filtering (fluent API)
        /// </summary>
        /// <param name="filter">Configured ActionFilter for PlayerActions</param>
        /// <returns>This context for method chaining</returns>
        public InputContext WithPlayerFilter(ActionFilter<PlayerAction> filter)
        {
            PlayerFilter = filter ?? ActionFilter<PlayerAction>.AllowAll();
            return this;
        }
        
        /// <summary>
        /// Configure UIAction filtering (fluent API)
        /// </summary>
        /// <param name="filter">Configured ActionFilter for UIActions</param>
        /// <returns>This context for method chaining</returns>
        public InputContext WithUIFilter(ActionFilter<UIAction> filter)
        {
            UIFilter = filter ?? ActionFilter<UIAction>.AllowAll();
            return this;
        }
        
#if DEBUG
        /// <summary>
        /// Set debug information (fluent API, debug builds only)
        /// </summary>
        /// <param name="info">Debug information string</param>
        /// <returns>This context for method chaining</returns>
        public InputContext WithDebugInfo(string info)
        {
            DebugInfo = info;
            return this;
        }
#endif
        
        #endregion
        
        #region Static Factory Methods
        
        /// <summary>
        /// Create a gameplay context that allows all actions
        /// </summary>
        /// <returns>New gameplay context</returns>
        public static InputContext CreateGameplay()
        {
            return new InputContext(InputLayer.Gameplay)
                .WithInputConsumption(false); // Allow other contexts to layer on top
        }
        
        /// <summary>
        /// Create a menu context that blocks player actions but allows UI
        /// </summary>
        /// <returns>New menu context</returns>
        public static InputContext CreateMenu()
        {
            return new InputContext(InputLayer.Menus)
                .WithPlayerFilter(ActionFilter<PlayerAction>.AllowOnly()) // Block all player actions
                .WithUIFilter(ActionFilter<UIAction>.AllowAll()); // Allow all UI
        }
        
        /// <summary>
        /// Create a modal context for dialogs and confirmations
        /// </summary>
        /// <param name="allowedUIActions">UI actions to allow</param>
        /// <returns>New modal context</returns>
        public static InputContext CreateModal(params UIAction[] allowedUIActions)
        {
            return new InputContext(InputLayer.Modal)
                .WithPlayerFilter(ActionFilter<PlayerAction>.AllowOnly()) // Block all player actions
                .WithUIFilter(ActionFilter<UIAction>.AllowOnly(allowedUIActions));
        }
        
        /// <summary>
        /// Create a system context for critical overlays
        /// </summary>
        /// <param name="allowedActions">Actions to allow (if any)</param>
        /// <returns>New system context</returns>
        public static InputContext CreateSystem(params UIAction[] allowedActions)
        {
            return new InputContext(InputLayer.System)
                .WithPlayerFilter(ActionFilter<PlayerAction>.AllowOnly()) // Block all player actions
                .WithUIFilter(ActionFilter<UIAction>.AllowOnly(allowedActions));
        }
        
        #endregion
        
        #region Debugging
        
        public override string ToString()
        {
#if DEBUG
            var debugPart = !string.IsNullOrEmpty(DebugInfo) ? $" ({DebugInfo})" : "";
            return $"InputContext[{Id:D}] Layer={Layer} Priority={EffectivePriority} Player={PlayerFilter} UI={UIFilter} Consume={ConsumeInput}{debugPart}";
#else
            return $"InputContext[{Id:D}] Layer={Layer} Priority={EffectivePriority} Consume={ConsumeInput}";
#endif
        }
        
        #endregion
    }
}