using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Sinkii09.Engine.Editor.Core.Advanced
{
    /// <summary>
    /// VS Code-style command palette for quick menu access
    /// </summary>
    public class CommandPaletteWindow : EditorWindow
    {
        private string _searchText = "";
        private Vector2 _scrollPosition;
        private List<PaletteItem> _filteredItems = new List<PaletteItem>();
        private List<PaletteItem> _allItems = new List<PaletteItem>();
        private int _selectedIndex = 0;
        private bool _focusSearchField = true;
        private WorkflowMenuSystem.ProjectContext _context;
        private WorkflowMenuSystem.UserRole _userRole;
        
        private const int MAX_VISIBLE_ITEMS = 10;
        private const float ITEM_HEIGHT = 24f;

        public static void OpenWindow(WorkflowMenuSystem.ProjectContext context, WorkflowMenuSystem.UserRole userRole)
        {
            var window = CreateInstance<CommandPaletteWindow>();
            window._context = context;
            window._userRole = userRole;
            window.titleContent = new GUIContent("Command Palette");
            window.minSize = new Vector2(500, 300);
            window.maxSize = new Vector2(800, 400);
            
            // Center the window
            var main = EditorGUIUtility.GetMainWindowPosition();
            var pos = new Rect(
                main.x + (main.width - 600) * 0.5f,
                main.y + (main.height - 350) * 0.3f,
                600, 350);
            window.position = pos;
            
            window.ShowUtility();
            window.Focus();
        }

        private void OnEnable()
        {
            LoadPaletteItems();
            FilterItems();
        }

        private void LoadPaletteItems()
        {
            _allItems.Clear();

            // Add menu items
            var contextualItems = ContextualMenuSystem.GetContextualMenuItems();
            foreach (var item in contextualItems)
            {
                _allItems.Add(new PaletteItem
                {
                    Type = PaletteItemType.MenuItem,
                    Title = item.Metadata.DisplayName ?? item.Method.Name,
                    Description = item.Metadata.Description ?? "",
                    Path = item.GenerateMenuPath(),
                    Icon = GetCategoryIcon(item.WorkflowAttribute.Category),
                    Category = item.WorkflowAttribute.Category.ToString(),
                    Shortcut = item.Metadata.Shortcut,
                    Priority = item.Performance.RecommendationWeight,
                    MenuItem = item
                });
            }

            // Add recommendations
            var recommendations = ContextualMenuSystem.GetSmartRecommendations();
            foreach (var rec in recommendations)
            {
                _allItems.Add(new PaletteItem
                {
                    Type = PaletteItemType.Recommendation,
                    Title = rec.Title,
                    Description = rec.Description,
                    Path = rec.MenuPath,
                    Icon = rec.Icon,
                    Category = "Recommended",
                    Priority = rec.Priority / 10f,
                    Recommendation = rec
                });
            }

            // Add quick actions
            var quickActions = ContextualMenuSystem.GetQuickActions();
            foreach (var action in quickActions)
            {
                _allItems.Add(new PaletteItem
                {
                    Type = PaletteItemType.QuickAction,
                    Title = action.Name,
                    Description = action.Description,
                    Path = action.MenuPath,
                    Icon = action.Icon,
                    Category = action.Category.ToString(),
                    Shortcut = action.Shortcut,
                    Priority = 1.5f,
                    QuickAction = action
                });
            }

            // Sort by priority and relevance
            _allItems = _allItems.OrderByDescending(i => i.Priority).ToList();
        }

        private void OnGUI()
        {
            DrawHeader();
            DrawSearchField();
            DrawItemList();
            DrawFooter();

            HandleKeyboardInput();
        }

        private void DrawHeader()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label("Command Palette", EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                GUILayout.Label($"Context: {_context}", EditorStyles.miniLabel);
            }
        }

        private void DrawSearchField()
        {
            EditorGUILayout.Space();
            
            GUI.SetNextControlName("SearchField");
            var newSearchText = EditorGUILayout.TextField(_searchText, EditorStyles.toolbarSearchField);
            
            if (newSearchText != _searchText)
            {
                _searchText = newSearchText;
                _selectedIndex = 0;
                FilterItems();
            }

            // Focus search field on first frame
            if (_focusSearchField)
            {
                EditorGUI.FocusTextInControl("SearchField");
                _focusSearchField = false;
            }
        }

        private void DrawItemList()
        {
            var listRect = GUILayoutUtility.GetRect(0, MAX_VISIBLE_ITEMS * ITEM_HEIGHT, GUILayout.ExpandWidth(true));
            var viewRect = new Rect(0, 0, listRect.width - 16, _filteredItems.Count * ITEM_HEIGHT);
            
            _scrollPosition = GUI.BeginScrollView(listRect, _scrollPosition, viewRect);

            for (int i = 0; i < _filteredItems.Count; i++)
            {
                var item = _filteredItems[i];
                var itemRect = new Rect(0, i * ITEM_HEIGHT, viewRect.width, ITEM_HEIGHT);
                
                DrawPaletteItem(itemRect, item, i == _selectedIndex);
            }

            GUI.EndScrollView();
        }

        private void DrawPaletteItem(Rect rect, PaletteItem item, bool selected)
        {
            var style = selected ? EditorStyles.selectionRect : GUIStyle.none;
            
            if (selected)
            {
                EditorGUI.DrawRect(rect, new Color(0.24f, 0.48f, 0.90f, 0.75f));
            }

            var contentRect = new Rect(rect.x + 4, rect.y, rect.width - 8, rect.height);

            // Icon
            var iconRect = new Rect(contentRect.x, contentRect.y + 2, 16, 16);
            if (!string.IsNullOrEmpty(item.Icon))
            {
                GUI.Label(iconRect, item.Icon);
            }

            // Title and category
            var titleRect = new Rect(iconRect.xMax + 4, contentRect.y, contentRect.width * 0.6f, rect.height);
            var titleContent = $"{item.Title}";
            if (!string.IsNullOrEmpty(item.Category) && item.Type != PaletteItemType.Recommendation)
            {
                titleContent += $" ({item.Category})";
            }
            GUI.Label(titleRect, titleContent, EditorStyles.boldLabel);

            // Description
            if (!string.IsNullOrEmpty(item.Description))
            {
                var descRect = new Rect(titleRect.xMax + 8, contentRect.y, contentRect.width * 0.3f, rect.height);
                GUI.Label(descRect, item.Description, EditorStyles.miniLabel);
            }

            // Shortcut
            if (!string.IsNullOrEmpty(item.Shortcut))
            {
                var shortcutRect = new Rect(contentRect.xMax - 80, contentRect.y, 80, rect.height);
                GUI.Label(shortcutRect, item.Shortcut, EditorStyles.centeredGreyMiniLabel);
            }

            // Handle click
            if (Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition))
            {
                _selectedIndex = _filteredItems.IndexOf(item);
                ExecuteSelectedItem();
                Event.current.Use();
            }
        }

        private void DrawFooter()
        {
            EditorGUILayout.Space();
            
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (_filteredItems.Count > 0)
                {
                    GUILayout.Label($"{_selectedIndex + 1}/{_filteredItems.Count}", EditorStyles.miniLabel);
                }
                else
                {
                    GUILayout.Label("No items found", EditorStyles.miniLabel);
                }
                
                GUILayout.FlexibleSpace();
                GUILayout.Label("Enter to execute • Esc to close • ↑↓ to navigate", EditorStyles.miniLabel);
            }
        }

        private void HandleKeyboardInput()
        {
            if (Event.current.type != EventType.KeyDown)
                return;

            switch (Event.current.keyCode)
            {
                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                    ExecuteSelectedItem();
                    Event.current.Use();
                    break;

                case KeyCode.Escape:
                    Close();
                    Event.current.Use();
                    break;

                case KeyCode.UpArrow:
                    _selectedIndex = Mathf.Max(0, _selectedIndex - 1);
                    EnsureSelectedItemVisible();
                    Event.current.Use();
                    break;

                case KeyCode.DownArrow:
                    _selectedIndex = Mathf.Min(_filteredItems.Count - 1, _selectedIndex + 1);
                    EnsureSelectedItemVisible();
                    Event.current.Use();
                    break;

                case KeyCode.PageUp:
                    _selectedIndex = Mathf.Max(0, _selectedIndex - 5);
                    EnsureSelectedItemVisible();
                    Event.current.Use();
                    break;

                case KeyCode.PageDown:
                    _selectedIndex = Mathf.Min(_filteredItems.Count - 1, _selectedIndex + 5);
                    EnsureSelectedItemVisible();
                    Event.current.Use();
                    break;
            }
        }

        private void EnsureSelectedItemVisible()
        {
            if (_filteredItems.Count == 0) return;

            var itemY = _selectedIndex * ITEM_HEIGHT;
            var viewHeight = MAX_VISIBLE_ITEMS * ITEM_HEIGHT;
            
            if (itemY < _scrollPosition.y)
            {
                _scrollPosition.y = itemY;
            }
            else if (itemY + ITEM_HEIGHT > _scrollPosition.y + viewHeight)
            {
                _scrollPosition.y = itemY + ITEM_HEIGHT - viewHeight;
            }
        }

        private void FilterItems()
        {
            if (string.IsNullOrEmpty(_searchText))
            {
                _filteredItems = new List<PaletteItem>(_allItems);
            }
            else
            {
                var searchLower = _searchText.ToLowerInvariant();
                _filteredItems = _allItems.Where(item => MatchesSearch(item, searchLower)).ToList();
                
                // Sort by relevance
                _filteredItems = _filteredItems.OrderByDescending(item => CalculateSearchScore(item, searchLower)).ToList();
            }

            _selectedIndex = Mathf.Clamp(_selectedIndex, 0, _filteredItems.Count - 1);
        }

        private bool MatchesSearch(PaletteItem item, string searchTerm)
        {
            return item.Title.ToLowerInvariant().Contains(searchTerm) ||
                   (item.Description?.ToLowerInvariant().Contains(searchTerm) ?? false) ||
                   (item.Category?.ToLowerInvariant().Contains(searchTerm) ?? false);
        }

        private float CalculateSearchScore(PaletteItem item, string searchTerm)
        {
            float score = 0f;
            
            var title = item.Title.ToLowerInvariant();
            var description = item.Description?.ToLowerInvariant() ?? "";

            // Exact matches get highest score
            if (title == searchTerm) score += 100f;
            else if (title.StartsWith(searchTerm)) score += 50f;
            else if (title.Contains(searchTerm)) score += 25f;

            if (description.Contains(searchTerm)) score += 10f;

            // Priority bonus
            score += item.Priority * 5f;

            // Type-based scoring
            switch (item.Type)
            {
                case PaletteItemType.Recommendation:
                    score += 20f;
                    break;
                case PaletteItemType.QuickAction:
                    score += 15f;
                    break;
            }

            return score;
        }

        private void ExecuteSelectedItem()
        {
            if (_filteredItems.Count == 0 || _selectedIndex >= _filteredItems.Count)
                return;

            var selectedItem = _filteredItems[_selectedIndex];
            
            try
            {
                switch (selectedItem.Type)
                {
                    case PaletteItemType.MenuItem:
                        if (selectedItem.MenuItem?.Method != null)
                        {
                            selectedItem.MenuItem.Method.Invoke(null, null);
                        }
                        break;

                    case PaletteItemType.Recommendation:
                    case PaletteItemType.QuickAction:
                        // Try to execute via menu path
                        EditorApplication.ExecuteMenuItem(selectedItem.Path);
                        break;
                }
                
                Close();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[CommandPalette] Failed to execute {selectedItem.Title}: {ex.Message}");
            }
        }

        private string GetCategoryIcon(WorkflowMenuSystem.WorkflowCategory category)
        {
            return category switch
            {
                WorkflowMenuSystem.WorkflowCategory.ProjectSetup => "🚀",
                WorkflowMenuSystem.WorkflowCategory.DailyDevelopment => "💻", 
                WorkflowMenuSystem.WorkflowCategory.QualityAssurance => "🔍",
                WorkflowMenuSystem.WorkflowCategory.ReleasePipeline => "📦",
                WorkflowMenuSystem.WorkflowCategory.Maintenance => "🔧",
                _ => "⚙️"
            };
        }
    }

    /// <summary>
    /// Palette item for command palette display
    /// </summary>
    [Serializable]
    public class PaletteItem
    {
        public PaletteItemType Type;
        public string Title;
        public string Description;
        public string Path;
        public string Icon;
        public string Category;
        public string Shortcut;
        public float Priority;
        
        // Reference objects
        public RegisteredMenuItem MenuItem;
        public ContextualRecommendation Recommendation;
        public QuickAction QuickAction;
    }

    /// <summary>
    /// Types of items in the command palette
    /// </summary>
    public enum PaletteItemType
    {
        MenuItem,
        Recommendation,
        QuickAction
    }
}