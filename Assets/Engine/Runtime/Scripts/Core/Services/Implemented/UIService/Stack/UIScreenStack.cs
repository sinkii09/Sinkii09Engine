using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using R3;

namespace Sinkii09.Engine.Services
{
    /// <summary>
    /// Enhanced UI screen stack with depth protection and advanced navigation capabilities.
    /// Provides stack overflow prevention and navigation history tracking.
    /// </summary>
    public class UIScreenStack
    {
        private readonly Stack<UIScreenStackEntry> _stack;
        private readonly int _maxDepth;
        private readonly ReactiveProperty<int> _currentDepth;
        private readonly Subject<UIScreenStackEntry> _screenPushed;
        private readonly Subject<UIScreenStackEntry> _screenPopped;

        public ReadOnlyReactiveProperty<int> CurrentDepth => _currentDepth;
        public Observable<UIScreenStackEntry> ScreenPushed => _screenPushed.AsObservable();
        public Observable<UIScreenStackEntry> ScreenPopped => _screenPopped.AsObservable();

        public int MaxDepth => _maxDepth;
        public int Count => _stack.Count;
        public bool IsEmpty => _stack.Count == 0;
        public bool IsFull => _stack.Count >= _maxDepth;

        public UIScreenStack(int maxDepth = 10)
        {
            if (maxDepth <= 0)
                throw new ArgumentException("Max depth must be greater than 0", nameof(maxDepth));

            _maxDepth = maxDepth;
            _stack = new Stack<UIScreenStackEntry>();
            _currentDepth = new ReactiveProperty<int>(0);
            _screenPushed = new Subject<UIScreenStackEntry>();
            _screenPopped = new Subject<UIScreenStackEntry>();
        }

        #region Stack Operations

        /// <summary>
        /// Push a screen onto the stack with overflow protection
        /// </summary>
        public bool Push(UIScreen screen, UIScreenAsset asset)
        {
            if (screen == null || asset == null)
                return false;

            if (_stack.Count >= _maxDepth)
            {
                Debug.LogError($"UI Stack overflow: Cannot push '{asset.ScreenType}' - max depth {_maxDepth} reached");
                return false;
            }

            var entry = new UIScreenStackEntry(screen, asset, DateTime.UtcNow);
            _stack.Push(entry);
            
            _currentDepth.Value = _stack.Count;
            _screenPushed.OnNext(entry);

            return true;
        }

        /// <summary>
        /// Pop the top screen from the stack
        /// </summary>
        public UIScreenStackEntry Pop()
        {
            if (_stack.Count == 0)
                return null;

            var entry = _stack.Pop();
            _currentDepth.Value = _stack.Count;
            _screenPopped.OnNext(entry);

            return entry;
        }

        /// <summary>
        /// Peek at the top screen without removing it
        /// </summary>
        public UIScreenStackEntry Peek()
        {
            return _stack.Count > 0 ? _stack.Peek() : null;
        }

        /// <summary>
        /// Get the current active screen
        /// </summary>
        public UIScreen Current => _stack.Count > 0 ? _stack.Peek().Screen : null;

        /// <summary>
        /// Get the current active screen asset
        /// </summary>
        public UIScreenAsset CurrentAsset => _stack.Count > 0 ? _stack.Peek().Asset : null;

        #endregion

        #region Advanced Navigation

        /// <summary>
        /// Pop screens until reaching the specified screen type
        /// </summary>
        public List<UIScreenStackEntry> PopTo(UIScreenType screenType)
        {
            if (screenType == UIScreenType.None)
                return new List<UIScreenStackEntry>();

            var poppedScreens = new List<UIScreenStackEntry>();

            while (_stack.Count > 0)
            {
                var current = _stack.Peek();
                if (current.Asset.ScreenType == screenType)
                    break;

                poppedScreens.Add(Pop());
            }

            return poppedScreens;
        }

        /// <summary>
        /// Pop all screens except the root (first screen)
        /// </summary>
        public List<UIScreenStackEntry> PopToRoot()
        {
            var poppedScreens = new List<UIScreenStackEntry>();

            while (_stack.Count > 1)
            {
                poppedScreens.Add(Pop());
            }

            return poppedScreens;
        }

        /// <summary>
        /// Replace the top screen with a new one
        /// </summary>
        public UIScreenStackEntry Replace(UIScreen newScreen, UIScreenAsset newAsset)
        {
            if (newScreen == null || newAsset == null)
                return null;

            UIScreenStackEntry oldEntry = null;
            if (_stack.Count > 0)
            {
                oldEntry = Pop();
            }

            Push(newScreen, newAsset);
            return oldEntry;
        }

        /// <summary>
        /// Clear all screens from the stack
        /// </summary>
        public List<UIScreenStackEntry> Clear()
        {
            var allScreens = new List<UIScreenStackEntry>();

            while (_stack.Count > 0)
            {
                allScreens.Add(Pop());
            }

            return allScreens;
        }

        #endregion

        #region Stack Inspection

        /// <summary>
        /// Check if a specific screen is in the stack
        /// </summary>
        public bool Contains(UIScreenType screenType)
        {
            if (screenType == UIScreenType.None)
                return false;

            return _stack.Any(entry => entry.Asset.ScreenType == screenType);
        }

        /// <summary>
        /// Find a screen entry by screen type (returns the topmost instance)
        /// </summary>
        public UIScreenStackEntry Find(UIScreenType screenType)
        {
            if (screenType == UIScreenType.None)
                return null;

            return _stack.FirstOrDefault(entry => entry.Asset.ScreenType == screenType);
        }

        /// <summary>
        /// Find all screen entries by screen type (for multiple instances)
        /// </summary>
        public List<UIScreenStackEntry> FindAll(UIScreenType screenType)
        {
            if (screenType == UIScreenType.None)
                return new List<UIScreenStackEntry>();

            return _stack.Where(entry => entry.Asset.ScreenType == screenType).ToList();
        }

        /// <summary>
        /// Remove a specific screen instance from the stack
        /// </summary>
        public bool Remove(UIScreen screenInstance)
        {
            if (screenInstance == null)
                return false;

            var stackArray = _stack.ToArray();
            var newStack = new Stack<UIScreenStackEntry>();

            bool removed = false;
            
            // Rebuild stack without the target screen instance
            for (int i = stackArray.Length - 1; i >= 0; i--)
            {
                if (stackArray[i].Screen == screenInstance && !removed)
                {
                    // Skip this entry (remove it)
                    _screenPopped.OnNext(stackArray[i]);
                    removed = true;
                }
                else
                {
                    newStack.Push(stackArray[i]);
                }
            }

            if (removed)
            {
                _stack.Clear();
                while (newStack.Count > 0)
                {
                    _stack.Push(newStack.Pop());
                }
                _currentDepth.Value = _stack.Count;
            }

            return removed;
        }

        /// <summary>
        /// Get the depth of a specific screen in the stack (0 = top)
        /// </summary>
        public int GetDepth(UIScreenType screenType)
        {
            if (screenType == UIScreenType.None)
                return -1;

            var entries = _stack.ToArray();
            for (int i = 0; i < entries.Length; i++)
            {
                if (entries[i].Asset.ScreenType == screenType)
                    return i;
            }

            return -1;
        }

        /// <summary>
        /// Get all screens in the stack (top to bottom)
        /// </summary>
        public IReadOnlyList<UIScreenStackEntry> GetAll()
        {
            return _stack.ToList();
        }

        /// <summary>
        /// Get navigation breadcrumbs (bottom to top)
        /// </summary>
        public IReadOnlyList<string> GetBreadcrumbs()
        {
            return _stack.Reverse().Select(entry => entry.Asset.ScreenType.ToString()).ToList();
        }

        #endregion

        #region Stack Validation

        /// <summary>
        /// Validate stack integrity and report issues
        /// </summary>
        public List<string> ValidateStack()
        {
            var issues = new List<string>();

            if (_stack.Count > _maxDepth)
            {
                issues.Add($"Stack depth {_stack.Count} exceeds maximum {_maxDepth}");
            }

            var seenSingleInstanceScreens = new HashSet<UIScreenType>();
            var invalidDuplicates = new List<UIScreenType>();

            foreach (var entry in _stack)
            {
                if (entry.Screen == null)
                {
                    issues.Add("Found null screen in stack");
                    continue;
                }

                if (entry.Asset == null)
                {
                    issues.Add($"Screen '{entry.Screen.name}' has null asset");
                    continue;
                }

                // Only check for duplicates on single-instance screens
                if (!entry.Asset.AllowMultipleInstances)
                {
                    if (seenSingleInstanceScreens.Contains(entry.Asset.ScreenType))
                    {
                        invalidDuplicates.Add(entry.Asset.ScreenType);
                    }
                    else
                    {
                        seenSingleInstanceScreens.Add(entry.Asset.ScreenType);
                    }
                }
            }

            if (invalidDuplicates.Count > 0)
            {
                issues.Add($"Invalid duplicate single-instance screens in stack: {string.Join(", ", invalidDuplicates)}");
            }

            return issues;
        }

        #endregion

        #region Cleanup

        public void Dispose()
        {
            _currentDepth?.Dispose();
            _screenPushed?.Dispose();
            _screenPopped?.Dispose();
        }

        #endregion
    }

    /// <summary>
    /// Represents a screen entry in the UI stack with metadata
    /// </summary>
    public class UIScreenStackEntry
    {
        public UIScreen Screen { get; }
        public UIScreenAsset Asset { get; }
        public DateTime PushedAt { get; }
        public TimeSpan TimeOnStack => DateTime.UtcNow - PushedAt;

        public UIScreenStackEntry(UIScreen screen, UIScreenAsset asset, DateTime pushedAt)
        {
            Screen = screen ?? throw new ArgumentNullException(nameof(screen));
            Asset = asset ?? throw new ArgumentNullException(nameof(asset));
            PushedAt = pushedAt;
        }

        public override string ToString()
        {
            return $"{Asset.ScreenType} (pushed {TimeOnStack:mm\\:ss} ago)";
        }
    }
}