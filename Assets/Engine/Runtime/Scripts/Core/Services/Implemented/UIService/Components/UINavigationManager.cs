using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using R3;

namespace Sinkii09.Engine.Services
{
    /// <summary>
    /// Component responsible for screen navigation, stack management, and screen visibility
    /// </summary>
    public class UINavigationManager : IUIComponent
    {
        private IUIServiceContext _context;
        private UIScreenStack _screenStack;
        private bool _isInitialized;

        // R3 reactive events
        private readonly Subject<UIScreenAsset> _screenShown = new();
        private readonly Subject<UIScreenAsset> _screenHidden = new();

        public string ComponentName => "NavigationManager";
        public bool IsInitialized => _isInitialized;

        public Observable<UIScreenAsset> ScreenShown => _screenShown.AsObservable();
        public Observable<UIScreenAsset> ScreenHidden => _screenHidden.AsObservable();

        public int StackDepth => _screenStack?.Count ?? 0;
        public int MaxStackDepth => _screenStack?.MaxDepth ?? 0;
        public bool IsStackFull => _screenStack?.IsFull ?? false;
        public UIScreen CurrentScreen => _screenStack?.Current;

        public async UniTask<bool> InitializeAsync(IUIServiceContext context, CancellationToken cancellationToken = default)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _screenStack = new UIScreenStack(_context.Configuration.MaxStackDepth);
            _isInitialized = true;
            await UniTask.CompletedTask;
            return true;
        }

        public async UniTask ShutdownAsync(CancellationToken cancellationToken = default)
        {
            if (_screenStack != null)
            {
                var allEntries = _screenStack.Clear();
                foreach (var entry in allEntries)
                {
                    await HideScreenInternalAsync(entry.Screen, entry.Asset, cancellationToken);
                }
                _screenStack.Dispose();
            }

            _screenShown?.Dispose();
            _screenHidden?.Dispose();
            _isInitialized = false;
        }

        public async UniTask<ComponentHealthStatus> HealthCheckAsync(CancellationToken cancellationToken = default)
        {
            await UniTask.Yield();
            
            if (!_isInitialized)
                return new ComponentHealthStatus(false, "NavigationManager not initialized");

            if (_screenStack == null)
                return new ComponentHealthStatus(false, "Screen stack is null");

            var stackIssues = _screenStack.ValidateStack();
            if (stackIssues.Count > 0)
                return new ComponentHealthStatus(false, $"Stack validation issues: {string.Join(", ", stackIssues)}");

            return new ComponentHealthStatus(true, $"NavigationManager healthy - stack depth: {_screenStack.Count}/{_screenStack.MaxDepth}");
        }

        public void RespondToMemoryPressure(float pressureLevel)
        {
            // Navigation manager doesn't hold resources directly
            // Pressure is handled by individual screen components
        }

        /// <summary>
        /// Show a screen with full navigation logic
        /// </summary>
        public async UniTask ShowScreenAsync(UIScreenAsset screenAsset, UIScreenContext context, UIDisplayConfig displayConfig, CancellationToken cancellationToken = default)
        {
            if (!_isInitialized)
                throw new InvalidOperationException("NavigationManager not initialized");

            if (screenAsset == null)
                throw new ArgumentNullException(nameof(screenAsset));

            // Load screen via ScreenManager
            var screenManager = _context.GetComponent<UIScreenManager>();
            var screen = await screenManager.LoadScreenAsync(screenAsset, cancellationToken);

            // Initialize screen with context and config
            screen.InternalInitialize(context, displayConfig);

            // Handle modal backdrop creation
            if (displayConfig?.IsModal == true)
            {
                var modalManager = _context.GetComponent<UIModalManager>();
                if (modalManager != null)
                    await modalManager.CreateModalBackdropAsync(screen, displayConfig, cancellationToken);
            }

            // Handle existing screens based on display mode
            await HandleExistingScreensAsync(displayConfig, cancellationToken);

            // Try to push to stack with overflow protection
            if (!_screenStack.Push(screen, screenAsset))
            {
                // Stack overflow - cleanup and throw
                await CleanupFailedScreenAsync(screen, screenAsset);
                throw new InvalidOperationException($"Cannot show screen '{screenAsset.ScreenType}' - UI stack is full (max depth: {_screenStack.MaxDepth})");
            }

            // For blur modals: Show backdrop first (captures screen), then show modal
            if (displayConfig?.BackdropBehavior == ModalBackdropBehavior.Blurred)
            {
                Debug.Log($"[NavigationManager] Showing blur backdrop for {screenAsset.ScreenType} (screen hidden)");
                var modalManager2 = _context.GetComponent<UIModalManager>();
                modalManager2?.ShowModalBackdrop(screen);
                
                // Wait a frame for blur capture to complete
                await UniTask.DelayFrame(2, cancellationToken: cancellationToken);
                
                Debug.Log($"[NavigationManager] Now showing modal {screenAsset.ScreenType} on top of blur backdrop");
                // Then show the modal on top
                screen.InternalShow();
                await ApplyInTransitionAsync(screen, displayConfig, cancellationToken);
            }
            else
            {
                // For non-blur modals: Standard order
                var modalManager2 = _context.GetComponent<UIModalManager>();
                modalManager2?.ShowModalBackdrop(screen);
                
                screen.InternalShow();
                await ApplyInTransitionAsync(screen, displayConfig, cancellationToken);
            }

            // Notify observers
            _screenShown.OnNext(screenAsset);
        }

        /// <summary>
        /// Hide a specific screen
        /// </summary>
        public async UniTask HideScreenAsync(UIScreenType screenType, CancellationToken cancellationToken = default)
        {
            if (!_isInitialized) return;

            var screenAsset = _context.ScreenRegistry.GetAsset(screenType);
            if (screenAsset == null) return;

            if (screenAsset.AllowMultipleInstances)
            {
                await HideAllInstancesAsync(screenType, cancellationToken);
            }
            else
            {
                var entry = _screenStack.Find(screenType);
                if (entry == null) return;

                await HideScreenInternalAsync(entry.Screen, entry.Asset, cancellationToken);
                _screenStack.Remove(entry.Screen);
            }
        }

        /// <summary>
        /// Hide all instances of a screen type
        /// </summary>
        public async UniTask HideAllInstancesAsync(UIScreenType screenType, CancellationToken cancellationToken = default)
        {
            var entries = _screenStack.FindAll(screenType);
            if (entries.Count == 0) return;

            Debug.Log($"[NavigationManager] Hiding {entries.Count} instances of {screenType}");

            foreach (var entry in entries)
            {
                await HideScreenInternalAsync(entry.Screen, entry.Asset, cancellationToken);
                _screenStack.Remove(entry.Screen);
            }
        }

        /// <summary>
        /// Pop the top screen from the stack
        /// </summary>
        public async UniTask PopAsync(CancellationToken cancellationToken = default)
        {
            if (!_isInitialized || _screenStack.IsEmpty) return;

            var entry = _screenStack.Pop();
            if (entry != null)
            {
                await HideScreenInternalAsync(entry.Screen, entry.Asset, cancellationToken);
            }
        }

        /// <summary>
        /// Check if a screen is currently active
        /// </summary>
        public bool IsScreenActive(UIScreenType screenType)
        {
            return _isInitialized && _screenStack?.Contains(screenType) == true;
        }

        /// <summary>
        /// Get current active screen type
        /// </summary>
        public UIScreenType GetActiveScreenType()
        {
            if (!_isInitialized) return UIScreenType.None;

            var currentEntry = _screenStack?.Peek();
            return currentEntry?.Asset?.ScreenType ?? UIScreenType.None;
        }

        private async UniTask HandleExistingScreensAsync(UIDisplayConfig displayConfig, CancellationToken cancellationToken)
        {
            if (_screenStack.Count == 0) return;

            if (displayConfig?.IsModal == true || displayConfig?.IsOverlay == true)
            {
                // For modals/overlays, keep underlying screens active
                return;
            }

            if (!_context.Configuration.AllowStacking)
            {
                var currentEntry = _screenStack.Peek();
                
                // Apply out transition
                await ApplyOutTransitionAsync(currentEntry.Screen, cancellationToken);
                
                // Hide and dispose properly
                await HideScreenInternalAsync(currentEntry.Screen, currentEntry.Asset, cancellationToken);
                _screenStack.Remove(currentEntry.Screen);
            }
        }

        private async UniTask ApplyInTransitionAsync(UIScreen screen, UIDisplayConfig displayConfig, CancellationToken cancellationToken)
        {
            var transitionManager = _context.GetComponent<UITransitionManagerComponent>();
            if (transitionManager != null && displayConfig?.InTransition != TransitionType.None)
            {
                await transitionManager.AnimateScreenInAsync(screen, displayConfig.InTransition, cancellationToken);
            }
        }

        private async UniTask ApplyOutTransitionAsync(UIScreen screen, CancellationToken cancellationToken)
        {
            var transitionManager = _context.GetComponent<UITransitionManagerComponent>();
            if (transitionManager != null && 
                screen.DisplayConfig?.OutTransition != TransitionType.None &&
                screen.DisplayConfig?.OutTransition != null)
            {
                await transitionManager.AnimateScreenOutAsync(screen, screen.DisplayConfig.OutTransition, cancellationToken);
            }
        }

        private async UniTask HideScreenInternalAsync(UIScreen screen, UIScreenAsset screenAsset, CancellationToken cancellationToken)
        {
            if (screen == null) return;

            // Hide modal backdrop
            var modalManager = _context.GetComponent<UIModalManager>();
            modalManager?.HideModalBackdrop(screen);

            // Apply out transition
            await ApplyOutTransitionAsync(screen, cancellationToken);

            // Hide screen
            screen.InternalHide();

            // Handle cleanup based on screen type
            if (screenAsset.AllowMultipleInstances)
            {
                var poolManager = _context.GetComponent<UIInstancePoolManager>();
                poolManager?.ReturnInstance(screenAsset.ScreenType, screen);
            }
            else if (!IsScreenCacheable(screenAsset))
            {
                var cacheManager = _context.GetComponent<UIScreenCacheManager>();
                cacheManager?.Remove(screenAsset.ScreenType);
                UnityEngine.Object.Destroy(screen.gameObject);
            }
            else
            {
                screen.gameObject.SetActive(false);
            }

            // Show previous screen if exists
            if (_screenStack.Current != null)
            {
                _screenStack.Current.gameObject.SetActive(true);
            }

            // Notify observers
            _screenHidden.OnNext(screenAsset);
        }

        private async UniTask CleanupFailedScreenAsync(UIScreen screen, UIScreenAsset screenAsset)
        {
            // Cleanup modal backdrop
            var modalManager = _context.GetComponent<UIModalManager>();
            modalManager?.CleanupFailedScreen(screen);

            // Return to appropriate pool or destroy
            if (screenAsset.AllowMultipleInstances)
            {
                var poolManager = _context.GetComponent<UIInstancePoolManager>();
                poolManager?.ReturnInstance(screenAsset.ScreenType, screen);
            }
            else
            {
                UnityEngine.Object.Destroy(screen.gameObject);
            }

            await UniTask.CompletedTask;
        }

        private bool IsScreenCacheable(UIScreenAsset screenAsset)
        {
            return screenAsset?.CacheScreen == true;
        }

        /// <summary>
        /// Pop screens until reaching the specified screen type
        /// </summary>
        public async UniTask PopToAsync(UIScreenType targetScreenType, CancellationToken cancellationToken = default)
        {
            if (!_isInitialized) return;
            
            var targetEntry = _screenStack.Find(targetScreenType);
            if (targetEntry == null)
            {
                Debug.LogWarning($"[NavigationManager] PopToAsync: Target screen '{targetScreenType}' not found in stack");
                return;
            }
            
            // Pop screens until we reach the target
            var screensToRemove = new List<UIScreenStackEntry>();
            var allEntries = _screenStack.GetAll();
            
            // Iterate from top to bottom (index 0 = top)
            for (int i = 0; i < allEntries.Count; i++)
            {
                var entry = allEntries[i];
                if (entry.Asset.ScreenType == targetScreenType)
                    break;
                    
                screensToRemove.Add(entry);
            }
            
            // Hide screens in reverse order
            foreach (var entry in screensToRemove)
            {
                await HideScreenInternalAsync(entry.Screen, entry.Asset, cancellationToken);
                _screenStack.Remove(entry.Screen);
            }
            
            // Ensure target screen is active
            var currentEntry = _screenStack.Peek();
            if (currentEntry?.Asset?.ScreenType == targetScreenType)
            {
                currentEntry.Screen.gameObject.SetActive(true);
            }
        }
        
        /// <summary>
        /// Replace the top screen with a new one
        /// </summary>
        public async UniTask ReplaceAsync(UIScreenAsset newScreenAsset, UIScreenContext context, UIDisplayConfig displayConfig, CancellationToken cancellationToken = default)
        {
            if (!_isInitialized)
                throw new InvalidOperationException("NavigationManager not initialized");
            
            if (newScreenAsset == null)
                throw new ArgumentNullException(nameof(newScreenAsset));
            
            UIScreenStackEntry oldEntry = null;
            if (!_screenStack.IsEmpty)
            {
                oldEntry = _screenStack.Pop();
            }
            
            try
            {
                // Load new screen
                var screenManager = _context.GetComponent<UIScreenManager>();
                var newScreen = await screenManager.LoadScreenAsync(newScreenAsset, cancellationToken);
                
                // Initialize new screen
                newScreen.InternalInitialize(context, displayConfig);
                
                // Handle modal backdrop creation
                if (displayConfig?.IsModal == true)
                {
                    var modalManager = _context.GetComponent<UIModalManager>();
                    if (modalManager != null)
                        await modalManager.CreateModalBackdropAsync(newScreen, displayConfig, cancellationToken);
                }
                
                // Handle replacement transition (old screen out, new screen in)
                if (oldEntry != null)
                {
                    var transitionManager = _context.GetComponent<UITransitionManagerComponent>();
                    if (transitionManager != null)
                    {
                        await transitionManager.AnimateScreenReplaceAsync(newScreen, oldEntry.Screen, displayConfig?.InTransition ?? TransitionType.None, cancellationToken);
                    }
                    
                    // Cleanup old screen
                    await HideScreenInternalAsync(oldEntry.Screen, oldEntry.Asset, cancellationToken);
                }
                else
                {
                    // No old screen, just show new one
                    await ApplyInTransitionAsync(newScreen, displayConfig, cancellationToken);
                }
                
                // Add new screen to stack
                if (!_screenStack.Push(newScreen, newScreenAsset))
                {
                    await CleanupFailedScreenAsync(newScreen, newScreenAsset);
                    throw new InvalidOperationException($"Cannot replace with screen '{newScreenAsset.ScreenType}' - UI stack is full");
                }
                
                // Show modal backdrop and screen
                var modalManager2 = _context.GetComponent<UIModalManager>();
                modalManager2?.ShowModalBackdrop(newScreen);
                
                newScreen.InternalShow();
                
                // Notify observers
                if (oldEntry != null)
                    _screenHidden.OnNext(oldEntry.Asset);
                _screenShown.OnNext(newScreenAsset);
            }
            catch
            {
                // If replacement failed, restore old screen
                if (oldEntry != null)
                {
                    _screenStack.Push(oldEntry.Screen, oldEntry.Asset);
                    oldEntry.Screen.gameObject.SetActive(true);
                }
                throw;
            }
        }
        
        /// <summary>
        /// Get navigation breadcrumbs (screen type names from bottom to top of stack)
        /// </summary>
        public IReadOnlyList<string> GetNavigationBreadcrumbs()
        {
            if (!_isInitialized || _screenStack == null)
                return new List<string>();
            
            return _screenStack.GetBreadcrumbs();
        }

        public void Dispose()
        {
            _screenShown?.Dispose();
            _screenHidden?.Dispose();
            _screenStack?.Dispose();
            _isInitialized = false;
        }
    }
}