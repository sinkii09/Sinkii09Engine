using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Sinkii09.Engine.Services
{
    /// <summary>
    /// Fluent builder for UI navigation operations
    /// </summary>
    public class UINavigationBuilder : IUINavigationBuilder
    {
        private readonly IUIService _uiService;
        private UIScreenType _targetScreenType = UIScreenType.None;
        private readonly UIScreenContext _context = new();
        private TransitionType _transitionType = TransitionType.None;
        private UIDisplayConfig _displayConfig = UIDisplayConfig.Normal;
        private bool _clearStack = false;
        private bool _replace = false;
        
        internal UINavigationBuilder(IUIService uiService)
        {
            _uiService = uiService ?? throw new ArgumentNullException(nameof(uiService));
        }
        
        public IUINavigationBuilder To(UIScreenType screenType)
        {
            _targetScreenType = screenType;
            return this;
        }
        
        public IUINavigationBuilder WithData<T>(T data)
        {
            _context.Set(data);
            return this;
        }
        
        public IUINavigationBuilder WithContext(UIScreenContext context)
        {
            if (context != null)
            {
                _context.Merge(context, overwrite: true);
            }
            return this;
        }
        
        public IUINavigationBuilder WithTransition(TransitionType transition)
        {
            _transitionType = transition;
            
            // Apply transition to current display config
            _displayConfig.InTransition = transition;
            _displayConfig.OutTransition = transition;
            
            return this;
        }
        
        public IUINavigationBuilder WithInTransition(TransitionType transition)
        {
            _displayConfig.InTransition = transition;
            return this;
        }
        
        public IUINavigationBuilder WithOutTransition(TransitionType transition)
        {
            _displayConfig.OutTransition = transition;
            return this;
        }
        
        public IUINavigationBuilder AsModal()
        {
            _displayConfig = UIDisplayConfig.Modal;
            return this;
        }
        
        public IUINavigationBuilder AsOverlay()
        {
            _displayConfig = UIDisplayConfig.Overlay;
            return this;
        }

        public IUINavigationBuilder AsBlurModal()
        {
            _displayConfig = UIDisplayConfig.BlurredModal;
            return this;
        }

        public IUINavigationBuilder WithDisplayConfig(UIDisplayConfig displayConfig)
        {
            _displayConfig = displayConfig ?? UIDisplayConfig.Normal;
            return this;
        }
        
        public IUINavigationBuilder ClearStack()
        {
            _clearStack = true;
            return this;
        }
        
        public IUINavigationBuilder Replace()
        {
            _replace = true;
            return this;
        }
        
        public async UniTask ExecuteAsync(CancellationToken cancellationToken = default)
        {
            // Validate required parameters
            if (_targetScreenType == UIScreenType.None)
            {
                throw new InvalidOperationException("Target screen type must be set using To() method");
            }
            
            try
            {
                // Clear stack if requested
                if (_clearStack)
                {
                    await _uiService.ClearAsync(cancellationToken);
                }
                
                // Execute the appropriate navigation operation with context data and display config
                if (_replace)
                {
                    await _uiService.ReplaceAsync(_targetScreenType, _context, _displayConfig, cancellationToken);
                }
                else
                {
                    await _uiService.ShowAsync(_targetScreenType, _context, _displayConfig, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to execute navigation to {_targetScreenType}: {ex.Message}", ex);
            }
        }
        
        /// <summary>
        /// Get the context data for a specific type
        /// </summary>
        internal T GetContextData<T>() where T : class
        {
            return _context.Get<T>();
        }
        
        /// <summary>
        /// Check if context data exists for a specific type
        /// </summary>
        internal bool HasContextData<T>()
        {
            return _context.Has<T>();
        }
        
        /// <summary>
        /// Get the complete context (for internal use)
        /// </summary>
        internal UIScreenContext GetContext()
        {
            return _context;
        }
        
        /// <summary>
        /// Get the display configuration (for internal use)
        /// </summary>
        internal UIDisplayConfig GetDisplayConfig()
        {
            return _displayConfig;
        }
    }
}