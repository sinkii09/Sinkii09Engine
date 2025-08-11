using UnityEngine;
using Cysharp.Threading.Tasks;
using Sinkii09.Engine;
using Sinkii09.Engine.Services;

/// <summary>
/// Simple UI test script with keyboard shortcuts
/// Press keys to test different navigation patterns
/// </summary>
public class SimpleUITest : MonoBehaviour
{
    private IUIService _uiService;
    
    private void Start()
    {
        _uiService = Engine.GetService<IUIService>();
        
        if (_uiService == null)
        {
            Debug.LogError("UIService not found!");
            return;
        }
        
        Debug.Log("Simple UI Test Ready!");
        Debug.Log("Keyboard Controls:");
        Debug.Log("1 - Fade to Menu");
        Debug.Log("2 - Slide Left to Settings");
        Debug.Log("3 - Scale to Shop");
        Debug.Log("4 - Modal Dialog");
        Debug.Log("5 - Multiple Tooltip Test");
        Debug.Log("6 - Hide All Tooltips");
        Debug.Log("7 - Hide Top Tooltip Only");
        Debug.Log("8 - Show Memory Stats");
        Debug.Log("9 - Tooltip Pool Details");
        Debug.Log("B - Go Back");
        Debug.Log("C - Clear Stack");
        Debug.Log("ESC - Hide Current Screen");
    }
    
    private void Update()
    {
        if (_uiService == null) return;
        
        // Keyboard shortcuts for testing
        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            TestFade().Forget();
        }
        else if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            TestSlideLeft().Forget();
        }
        else if (Input.GetKeyDown(KeyCode.Alpha3))
        {
            TestScale().Forget();
        }
        else if (Input.GetKeyDown(KeyCode.Alpha4))
        {
            TestModal().Forget();
        }
        else if (Input.GetKeyDown(KeyCode.Alpha5))
        {
            TestOverlay().Forget();
        }
        else if (Input.GetKeyDown(KeyCode.Alpha6))
        {
            TestHideTooltips().Forget();
        }
        else if (Input.GetKeyDown(KeyCode.Alpha7))
        {
            TestHideTopTooltip().Forget();
        }
        else if (Input.GetKeyDown(KeyCode.Alpha8))
        {
            ShowMemoryStats();
        }
        else if (Input.GetKeyDown(KeyCode.Alpha9))
        {
            ShowTooltipPoolDetails();
        }
        else if (Input.GetKeyDown(KeyCode.B))
        {
            GoBack().Forget();
        }
        else if (Input.GetKeyDown(KeyCode.C))
        {
            ClearStack().Forget();
        }
        else if (Input.GetKeyDown(KeyCode.Escape))
        {
            HideCurrent().Forget();
        }
    }
    
    private async UniTaskVoid TestFade()
    {
        Debug.Log("Fade Transition to Menu");
        await _uiService.Navigate()
            .To(UIScreenType.Menu)
            .WithTransition(TransitionType.Fade)
            .Replace()
            .ExecuteAsync();
    }

    private async UniTaskVoid TestSlideLeft()
    {
        Debug.Log("Slide Left to Settings");
        await _uiService.Navigate()
            .To(UIScreenType.Settings)
            .WithTransition(TransitionType.SlideLeft)
            .ExecuteAsync();
    }

    private async UniTaskVoid TestScale()
    {
        Debug.Log("Scale Transition to Shop");
        await _uiService.Navigate()
            .To(UIScreenType.Shop)
            .WithTransition(TransitionType.ScaleUp)
            .ExecuteAsync();
    }

    private async UniTaskVoid TestModal()
    {
        Debug.Log("Show Modal Dialog");
        var context = new UIScreenContext();
        //context.Set("title", "Test Modal!");

        await _uiService.Navigate()
            .To(UIScreenType.Dialog)
            .WithContext(context)
            .AsModal()
            .ExecuteAsync();
    }

    private async UniTaskVoid TestOverlay()
    {
        Debug.Log("Show Multiple Tooltip Instances Test");
        
        // Show first tooltip
        Debug.Log("Creating first tooltip...");
        await _uiService.Navigate()
            .To(UIScreenType.Tooltip)
            .AsOverlay()
            .ExecuteAsync();
        
        // Wait a bit to see the first one
        await UniTask.Delay(500);
        
        // Show second tooltip (should create a new instance)
        Debug.Log("Creating second tooltip...");
        await _uiService.Navigate()
            .To(UIScreenType.Tooltip)
            .AsOverlay()
            .ExecuteAsync();
        
        // Wait a bit to see both
        await UniTask.Delay(500);
        
        // Show third tooltip (should create another new instance)
        Debug.Log("Creating third tooltip...");
        await _uiService.Navigate()
            .To(UIScreenType.Tooltip)
            .AsOverlay()
            .ExecuteAsync();
            
        Debug.Log("Multiple tooltip test completed - check logs for pool activity");
    }

    private async UniTaskVoid TestHideTooltips()
    {
        Debug.Log("Hide All Tooltips Test");
        
        // Hide all tooltip instances at once (should return them to pool)
        await _uiService.HideAllInstancesAsync(UIScreenType.Tooltip);
        
        Debug.Log("Hide all tooltips test completed - check logs for pool returns");
    }

    private async UniTaskVoid TestHideTopTooltip()
    {
        Debug.Log("Hide Top Tooltip Only Test");
        
        // Hide only the topmost tooltip instance
        await _uiService.HideTopInstanceAsync(UIScreenType.Tooltip);
        
        Debug.Log("Hide top tooltip test completed - other instances should remain");
    }
    
    private void ShowMemoryStats()
    {
        if (_uiService == null) return;
        
        var stats = _uiService.GetMemoryStats();
        Debug.Log("=== UI System Memory Statistics ===");
        Debug.Log($"Cached Screens: {stats.cachedScreens}");
        Debug.Log($"Pooled Instances Available: {stats.totalPooledInstances}");
        Debug.Log($"Active Instances: {stats.totalActiveInstances}");
        Debug.Log($"Cache Efficiency: {stats.cacheEfficiency:P1}");
        Debug.Log($"Pool Efficiency: {stats.poolEfficiency:P1}");
        
        // Analyze pool efficiency
        if (stats.poolEfficiency >= 0.8)
            Debug.Log("🎯 Pool Efficiency: EXCELLENT - Great reuse rate!");
        else if (stats.poolEfficiency >= 0.6)
            Debug.Log("✅ Pool Efficiency: GOOD - Decent reuse rate");
        else if (stats.poolEfficiency >= 0.4)
            Debug.Log("⚠️ Pool Efficiency: FAIR - Room for improvement");
        else
            Debug.Log("❌ Pool Efficiency: POOR - Consider optimizing");
            
        Debug.Log("=====================================");
        
        // Tips for improvement
        if (stats.poolEfficiency < 0.6)
        {
            Debug.Log("💡 Tips to improve pool efficiency:");
            Debug.Log("   • Hide tooltips before creating new ones");
            Debug.Log("   • Increase pool size in UIServiceConfiguration");
            Debug.Log("   • Check for memory pressure cleanups");
        }
    }
    
    private void ShowTooltipPoolDetails()
    {
        if (_uiService == null) return;
        
        var stats = _uiService.GetPoolStats(UIScreenType.Tooltip);
        Debug.Log("=== Tooltip Pool Detailed Stats ===");
        Debug.Log($"Available in Pool: {stats.available}");
        Debug.Log($"Currently Active: {stats.active}");
        Debug.Log($"Total Created: {stats.totalCreated}");
        Debug.Log($"Pool Efficiency: {stats.efficiency:P1}");
        
        // Analysis
        var reuseRate = stats.totalCreated > 0 ? (double)(stats.totalCreated - stats.available - stats.active) / stats.totalCreated : 0.0;
        Debug.Log($"Reuse Rate: {reuseRate:P1}");
        
        if (stats.efficiency < 0.5)
        {
            Debug.Log("🔍 Low efficiency analysis:");
            if (stats.available == 0 && stats.active > 0)
                Debug.Log("   • All instances are active - create/hide cycle will improve efficiency");
            else if (stats.totalCreated < 6)
                Debug.Log("   • Not enough usage yet - efficiency improves over time");
            else
                Debug.Log("   • Consider testing create → hide → create pattern");
        }
        Debug.Log("====================================");
    }

    //private async UniTaskVoid TestPushReplace()
    //{
    //    Debug.Log("Push Replace Current Screen");
    //    await _uiService.Navigate()
    //        .To(UIScreenType.Game)
    //        .WithTransition(TransitionType.Push)
    //        .Replace()
    //        .ExecuteAsync();
    //}

    private async UniTaskVoid GoBack()
    {
        Debug.Log("Navigate Back (Pop)");
        await _uiService.PopAsync();
    }
    
    private async UniTaskVoid ClearStack()
    {
        Debug.Log("Clear Navigation Stack");
        await _uiService.ClearAsync();
    }
    
    private async UniTaskVoid HideCurrent()
    {
        var currentScreen = _uiService.GetActiveScreenType();
        if (currentScreen != UIScreenType.None)
        {
            Debug.Log($"Hide Current Screen: {currentScreen}");
            await _uiService.HideAsync(currentScreen);
        }
    }
}