using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Sinkii09.Engine.Editor.Core.Advanced
{
    /// <summary>
    /// Tracks menu usage for analytics and smart recommendations
    /// </summary>
    public class MenuUsageTracker
    {
        private Dictionary<string, MenuUsageData> _usageData = new Dictionary<string, MenuUsageData>();
        private const string USAGE_DATA_PATH = "Library/MenuUsageData.json";
        private const int MAX_USAGE_HISTORY = 1000;

        public MenuUsageTracker()
        {
            LoadUsageData();
            EditorApplication.quitting += SaveUsageData;
        }

        /// <summary>
        /// Register a menu item for tracking
        /// </summary>
        public void RegisterMenuItem(string menuPath, RegisteredMenuItem menuItem)
        {
            if (!_usageData.ContainsKey(menuPath))
            {
                _usageData[menuPath] = new MenuUsageData
                {
                    MenuPath = menuPath,
                    DisplayName = menuItem.Metadata.DisplayName,
                    FirstSeen = DateTime.Now,
                    TrackUsage = menuItem.Performance.TrackUsage
                };
            }
        }

        /// <summary>
        /// Record menu usage
        /// </summary>
        public void RecordUsage(string menuPath, TimeSpan executionTime = default)
        {
            if (_usageData.TryGetValue(menuPath, out var data))
            {
                data.UsageCount++;
                data.LastUsed = DateTime.Now;
                data.TotalExecutionTime += executionTime;
                
                // Add to recent usage (with limit)
                data.RecentUsage.Add(DateTime.Now);
                if (data.RecentUsage.Count > MAX_USAGE_HISTORY)
                {
                    data.RecentUsage.RemoveAt(0);
                }
                
                // Update frequency calculations
                UpdateFrequencyMetrics(data);
                
                // Auto-save periodically
                if (data.UsageCount % 10 == 0)
                {
                    SaveUsageData();
                }
            }
        }

        /// <summary>
        /// Get usage frequency for a menu item (0.0 to 1.0)
        /// </summary>
        public float GetUsageFrequency(string menuPath)
        {
            if (_usageData.TryGetValue(menuPath, out var data))
            {
                return data.UsageFrequency;
            }
            return 0f;
        }

        /// <summary>
        /// Get frequently used menu items
        /// </summary>
        public RegisteredMenuItem[] GetFrequentlyUsedItems(int maxCount = 10)
        {
            var frequentPaths = _usageData.Values
                .Where(data => data.TrackUsage && data.UsageCount > 2)
                .OrderByDescending(data => data.UsageFrequency)
                .ThenByDescending(data => data.UsageCount)
                .Take(maxCount)
                .Select(data => data.MenuPath)
                .ToArray();

            // Convert to RegisteredMenuItems (this would need integration with MenuRegistrationSystem)
            return new RegisteredMenuItem[0]; // Placeholder
        }

        /// <summary>
        /// Get recently used menu items
        /// </summary>
        public string[] GetRecentlyUsedItems(int maxCount = 5, TimeSpan withinTimespan = default)
        {
            if (withinTimespan == default)
                withinTimespan = TimeSpan.FromDays(7);

            var cutoffTime = DateTime.Now - withinTimespan;

            return _usageData.Values
                .Where(data => data.LastUsed > cutoffTime)
                .OrderByDescending(data => data.LastUsed)
                .Take(maxCount)
                .Select(data => data.MenuPath)
                .ToArray();
        }

        /// <summary>
        /// Get usage statistics for reporting
        /// </summary>
        public MenuUsageStatistics GetUsageStatistics()
        {
            var stats = new MenuUsageStatistics();
            
            var trackedItems = _usageData.Values.Where(d => d.TrackUsage).ToArray();
            
            stats.TotalTrackedMenus = trackedItems.Length;
            stats.TotalUsageCount = trackedItems.Sum(d => d.UsageCount);
            stats.AverageUsagePerMenu = trackedItems.Length > 0 ? (float)stats.TotalUsageCount / trackedItems.Length : 0f;
            stats.MostUsedMenu = trackedItems.OrderByDescending(d => d.UsageCount).FirstOrDefault()?.MenuPath ?? "None";
            stats.TotalExecutionTime = trackedItems.Aggregate(TimeSpan.Zero, (sum, d) => sum + d.TotalExecutionTime);
            
            // Calculate usage patterns
            stats.UsageByHour = new int[24];
            stats.UsageByDayOfWeek = new int[7];
            
            foreach (var data in trackedItems)
            {
                foreach (var usage in data.RecentUsage)
                {
                    stats.UsageByHour[usage.Hour]++;
                    stats.UsageByDayOfWeek[(int)usage.DayOfWeek]++;
                }
            }
            
            return stats;
        }

        /// <summary>
        /// Clear usage data (for privacy or reset)
        /// </summary>
        public void ClearUsageData(bool keepRegistrations = true)
        {
            if (keepRegistrations)
            {
                foreach (var data in _usageData.Values)
                {
                    data.UsageCount = 0;
                    data.LastUsed = DateTime.MinValue;
                    data.TotalExecutionTime = TimeSpan.Zero;
                    data.RecentUsage.Clear();
                    data.UsageFrequency = 0f;
                    data.RecentFrequency = 0f;
                }
            }
            else
            {
                _usageData.Clear();
            }
            
            SaveUsageData();
        }

        private void UpdateFrequencyMetrics(MenuUsageData data)
        {
            var totalMenus = _usageData.Count;
            var totalUsage = _usageData.Values.Sum(d => d.UsageCount);
            
            // Overall frequency (relative to all menus)
            data.UsageFrequency = totalUsage > 0 ? (float)data.UsageCount / totalUsage : 0f;
            
            // Recent frequency (last 30 days)
            var recentCutoff = DateTime.Now - TimeSpan.FromDays(30);
            var recentUsage = data.RecentUsage.Count(u => u > recentCutoff);
            var totalRecentUsage = _usageData.Values.Sum(d => d.RecentUsage.Count(u => u > recentCutoff));
            
            data.RecentFrequency = totalRecentUsage > 0 ? (float)recentUsage / totalRecentUsage : 0f;
        }

        private void LoadUsageData()
        {
            try
            {
                if (File.Exists(USAGE_DATA_PATH))
                {
                    var json = File.ReadAllText(USAGE_DATA_PATH);
                    var wrapper = JsonUtility.FromJson<MenuUsageDataWrapper>(json);
                    
                    _usageData = wrapper.UsageData?.ToDictionary(d => d.MenuPath, d => d) ?? new Dictionary<string, MenuUsageData>();
                    
                    Debug.Log($"[MenuUsageTracker] Loaded usage data for {_usageData.Count} menu items");
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[MenuUsageTracker] Failed to load usage data: {ex.Message}");
                _usageData = new Dictionary<string, MenuUsageData>();
            }
        }

        private void SaveUsageData()
        {
            try
            {
                var wrapper = new MenuUsageDataWrapper
                {
                    UsageData = _usageData.Values.ToArray(),
                    LastSaved = DateTime.Now
                };
                
                var json = JsonUtility.ToJson(wrapper, true);
                File.WriteAllText(USAGE_DATA_PATH, json);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[MenuUsageTracker] Failed to save usage data: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Usage data for individual menu items
    /// </summary>
    [Serializable]
    public class MenuUsageData
    {
        public string MenuPath;
        public string DisplayName;
        public int UsageCount;
        public DateTime FirstSeen;
        public DateTime LastUsed;
        public TimeSpan TotalExecutionTime;
        public float UsageFrequency;
        public float RecentFrequency;
        public bool TrackUsage = true;
        public List<DateTime> RecentUsage = new List<DateTime>();
    }

    /// <summary>
    /// Wrapper for JSON serialization
    /// </summary>
    [Serializable]
    public class MenuUsageDataWrapper
    {
        public MenuUsageData[] UsageData;
        public DateTime LastSaved;
    }

    /// <summary>
    /// Usage statistics for reporting and analytics
    /// </summary>
    public class MenuUsageStatistics
    {
        public int TotalTrackedMenus;
        public int TotalUsageCount;
        public float AverageUsagePerMenu;
        public string MostUsedMenu;
        public TimeSpan TotalExecutionTime;
        public int[] UsageByHour = new int[24];
        public int[] UsageByDayOfWeek = new int[7];
        
        public override string ToString()
        {
            var report = $"Menu Usage Statistics:\n" +
                        $"- Total Tracked Menus: {TotalTrackedMenus}\n" +
                        $"- Total Usage Count: {TotalUsageCount}\n" +
                        $"- Average Usage Per Menu: {AverageUsagePerMenu:F1}\n" +
                        $"- Most Used Menu: {MostUsedMenu}\n" +
                        $"- Total Execution Time: {TotalExecutionTime}\n\n";
                        
            report += "Usage by Hour of Day:\n";
            for (int i = 0; i < 24; i++)
            {
                if (UsageByHour[i] > 0)
                    report += $"  {i:00}:00 - {UsageByHour[i]} uses\n";
            }
            
            report += "\nUsage by Day of Week:\n";
            var days = new[] { "Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday" };
            for (int i = 0; i < 7; i++)
            {
                if (UsageByDayOfWeek[i] > 0)
                    report += $"  {days[i]} - {UsageByDayOfWeek[i]} uses\n";
            }
            
            return report;
        }
    }
}