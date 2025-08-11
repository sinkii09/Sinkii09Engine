using UnityEngine;
using Cysharp.Threading.Tasks;
using Sinkii09.Engine;
using Sinkii09.Engine.Services;
using System.Threading;
/// <summary>
/// Test script for UI Navigation functionality
/// Demonstrates various navigation patterns with transitions
/// </summary>
public class UINavigationTest : MonoBehaviour
{
    [Header("Test Configuration")]
    [SerializeField] private float delayBetweenTests = 2f;
    [SerializeField] private bool autoRunTests = false;
    
    private IUIService _uiService;
    private CancellationTokenSource _cancellationTokenSource;
    
    private void Start()
    {
        // Initialize UI Service
        _uiService = Engine.GetService<IUIService>();
        
        if (_uiService == null)
        {
            Debug.LogError("UIService not found! Make sure Engine is initialized.");
            return;
        }
        
        if (autoRunTests)
        {
            RunAllTestsAsync().Forget();
        }
    }
    
    private void OnDestroy()
    {
        _cancellationTokenSource?.Cancel();
        _cancellationTokenSource?.Dispose();
    }

    #region Button Callbacks - Can be connected to UI Buttons

    public void TestFadeTransition()
    {
        NavigateWithFadeAsync().Forget();
    }
    
    //public void TestSlideLeftTransition()
    //{
    //    NavigateWithSlideLeftAsync().Forget();
    //}
    
    //public void TestSlideRightTransition()
    //{
    //    NavigateWithSlideRightAsync().Forget();
    //}
    
    //public void TestScaleTransition()
    //{
    //    NavigateWithScaleAsync().Forget();
    //}
    
    //public void TestModalDialog()
    //{
    //    ShowModalDialogAsync().Forget();
    //}
    
    //public void TestOverlay()
    //{
    //    ShowOverlayAsync().Forget();
    //}
    
    //public void TestPushTransition()
    //{
    //    NavigateWithPushAsync().Forget();
    //}
    
    public void TestComplexNavigation()
    {
        ComplexNavigationFlowAsync().Forget();
    }
    
    public void TestNavigateBack()
    {
        NavigateBackAsync().Forget();
    }
    
    public void TestClearStack()
    {
        ClearNavigationStackAsync().Forget();
    }
    
    #endregion
    
    #region Navigation Tests
    
    private async UniTaskVoid NavigateWithFadeAsync()
    {
        Debug.Log("Testing: Fade Transition to Menu");
        
        await _uiService.Navigate()
            .To(UIScreenType.Menu)
            .WithTransition(TransitionType.Fade)
            .ExecuteAsync();
    }
    
    //private async UniTaskVoid NavigateWithSlideLeftAsync()
    //{
    //    Debug.Log("Testing: Slide Left to Settings");
        
    //    await _uiService.Navigate()
    //        .To(UIScreenType.Settings)
    //        .WithTransition(TransitionType.SlideLeft)
    //        .ExecuteAsync();
    //}
    
    //private async UniTaskVoid NavigateWithSlideRightAsync()
    //{
    //    Debug.Log("Testing: Slide Right to Inventory");
        
    //    await _uiService.Navigate()
    //        .To(UIScreenType.Inventory)
    //        .WithTransition(TransitionType.SlideRight)
    //        .ExecuteAsync();
    //}
    
    //private async UniTaskVoid NavigateWithScaleAsync()
    //{
    //    Debug.Log("Testing: Scale Transition to Shop");
        
    //    await _uiService.Navigate()
    //        .To(UIScreenType.Shop)
    //        .WithInTransition(TransitionType.ScaleUp)
    //        .WithOutTransition(TransitionType.ScaleDown)
    //        .ExecuteAsync();
    //}
    
    //private async UniTaskVoid ShowModalDialogAsync()
    //{
    //    Debug.Log("Testing: Modal Dialog");
        
    //    // Create context data for the modal
    //    var context = new UIScreenContext();
    //    context.Set("title", "Test Modal");
    //    context.Set("message", "This is a modal dialog with backdrop!");
        
    //    await _uiService.Navigate()
    //        .To(UIScreenType.Dialog)
    //        .WithContext(context)
    //        .AsModal()
    //        .ExecuteAsync();
    //}
    
    //private async UniTaskVoid ShowOverlayAsync()
    //{
    //    Debug.Log("Testing: Overlay (Tooltip)");
        
    //    var context = new UIScreenContext();
    //    context.Set("text", "This is an overlay tooltip!");
        
    //    await _uiService.Navigate()
    //        .To(UIScreenType.Tooltip)
    //        .WithContext(context)
    //        .AsOverlay()
    //        .ExecuteAsync();
    //}
    
    //private async UniTaskVoid NavigateWithPushAsync()
    //{
    //    Debug.Log("Testing: Push Transition (Replace)");
        
    //    await _uiService.Navigate()
    //        .To(UIScreenType.Game)
    //        .WithTransition(TransitionType.Push)
    //        .Replace()
    //        .ExecuteAsync();
    //}
    
    private async UniTaskVoid ComplexNavigationFlowAsync()
    {
        Debug.Log("Testing: Complex Navigation Flow");
        
        // Clear stack and go to menu
        await _uiService.Navigate()
            .To(UIScreenType.Menu)
            .ClearStack()
            .WithTransition(TransitionType.Fade)
            .ExecuteAsync();
        
        await UniTask.Delay(1000);
        
        //// Navigate to settings with slide
        //await _uiService.Navigate()
        //    .To(UIScreenType.Settings)
        //    .WithTransition(TransitionType.SlideLeft)
        //    .ExecuteAsync();
        
        //await UniTask.Delay(1000);
        
        //// Show modal on top
        //await _uiService.Navigate()
        //    .To(UIScreenType.Dialog)
        //    .AsModal()
        //    .ExecuteAsync();
        
        //await UniTask.Delay(2000);
        
        //// Hide modal
        //await _uiService.HideAsync(UIScreenType.Dialog);
        
        //await UniTask.Delay(1000);
        
        //// Go back to menu
        //await _uiService.PopAsync();
    }
    
    private async UniTaskVoid NavigateBackAsync()
    {
        Debug.Log("Testing: Navigate Back (Pop)");
        
        var currentScreen = _uiService.GetActiveScreenType();
        await _uiService.PopAsync();
        
        Debug.Log($"Navigated back from {currentScreen} to {_uiService.GetActiveScreenType()}");
    }
    
    private async UniTaskVoid ClearNavigationStackAsync()
    {
        Debug.Log("Testing: Clear Navigation Stack");
        
        await _uiService.ClearAsync();
        Debug.Log("Navigation stack cleared!");
    }
    
    #endregion
    
    #region Automated Test Runner
    
    private async UniTaskVoid RunAllTestsAsync()
    {
        _cancellationTokenSource = new CancellationTokenSource();
        var token = _cancellationTokenSource.Token;
        
        Debug.Log("=== Starting UI Navigation Tests ===");
        
        try
        {
            // Test 1: Basic Fade
            //await NavigateWithFadeAsync();
            await UniTask.Delay((int)(delayBetweenTests * 1000), cancellationToken: token);
            
            //// Test 2: Slide Transitions
            //await NavigateWithSlideLeftAsync();
            //await UniTask.Delay((int)(delayBetweenTests * 1000), cancellationToken: token);
            
            //await NavigateWithSlideRightAsync();
            //await UniTask.Delay((int)(delayBetweenTests * 1000), cancellationToken: token);
            
            //// Test 3: Scale Transition
            //await NavigateWithScaleAsync();
            //await UniTask.Delay((int)(delayBetweenTests * 1000), cancellationToken: token);
            
            //// Test 4: Modal
            //await ShowModalDialogAsync();
            //await UniTask.Delay((int)(delayBetweenTests * 1000), cancellationToken: token);
            //await _uiService.HideAsync(UIScreenType.Dialog);
            
            //// Test 5: Overlay
            //await ShowOverlayAsync();
            //await UniTask.Delay((int)(delayBetweenTests * 1000), cancellationToken: token);
            //await _uiService.HideAsync(UIScreenType.Tooltip);
            
            //// Test 6: Push Replace
            //await NavigateWithPushAsync();
            //await UniTask.Delay((int)(delayBetweenTests * 1000), cancellationToken: token);
            
            //// Test 7: Complex Flow
            //await ComplexNavigationFlowAsync();
            
            Debug.Log("=== All UI Navigation Tests Completed ===");
        }
        catch (System.OperationCanceledException)
        {
            Debug.Log("Tests cancelled");
        }
    }
    
    #endregion
    
    #region Debug Info
    
    private void OnGUI()
    {
        if (_uiService == null) return;
        
        // Display current navigation info
        GUILayout.BeginArea(new Rect(10, 10, 300, 200));
        GUILayout.Box("UI Navigation Debug Info");
        
        GUILayout.Label($"Active Screen: {_uiService.GetActiveScreenType()}");
        GUILayout.Label($"Stack Depth: {_uiService.GetStackDepth()}/{_uiService.GetMaxStackDepth()}");
        
        var breadcrumbs = _uiService.GetNavigationBreadcrumbs();
        if (breadcrumbs != null && breadcrumbs.Count > 0)
        {
            GUILayout.Label("Navigation Stack:");
            foreach (var crumb in breadcrumbs)
            {
                GUILayout.Label($"  - {crumb}");
            }
        }
        
        GUILayout.EndArea();
    }
    
    #endregion
}