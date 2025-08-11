using UnityEngine;
using Sinkii09.Engine.Services;

namespace Sinkii09.Engine.Tests
{
    /// <summary>
    /// Test script demonstrating blur backdrop functionality
    /// </summary>
    public class BlurBackdropTest : MonoBehaviour
    {
        [Header("UI Service Reference")]
        [SerializeField] private bool _useUIService = true;
        
        [Header("Test Configuration")]
        [SerializeField] private UIScreenType _testScreenType = UIScreenType.Settings;
        [SerializeField] private bool _useBlurredModal = true;
        
        [Header("Manual Blur Testing")]
        [SerializeField] private UIBlurEffect _testBlurEffect;
        [SerializeField] private float _blurIntensity = 1f;
        [SerializeField] private bool _enableBlur;
        
        private IUIService _uiService;
        
        private void Start()
        {
            // Get UI service if using service-based testing
            if (_useUIService)
            {
                _uiService = Engine.Engine.GetService<IUIService>();
            }
        }
        
        private void Update()
        {
            // Manual blur testing
            if (_testBlurEffect != null)
            {
                _testBlurEffect.SetBlurIntensity(_blurIntensity);
                
                if (_enableBlur && !_testBlurEffect.IsBlurring)
                {
                    _testBlurEffect.EnableBlur();
                }
                else if (!_enableBlur && _testBlurEffect.IsBlurring)
                {
                    _testBlurEffect.DisableBlur();
                }
            }
        }
        
        [ContextMenu("Test Regular Modal")]
        public void TestRegularModal()
        {
            if (_uiService == null)
            {
                Debug.LogError("UIService not available");
                return;
            }
            
            Debug.Log("Showing regular modal screen");
            _ = _uiService.ShowAsync(_testScreenType, null, UIDisplayConfig.Modal);
        }
        
        [ContextMenu("Test Blurred Modal")]
        public void TestBlurredModal()
        {
            if (_uiService == null)
            {
                Debug.LogError("UIService not available");
                return;
            }
            
            Debug.Log("Showing blurred modal screen");
            _ = _uiService.ShowAsync(_testScreenType, null, UIDisplayConfig.BlurredModal);
        }
        
        [ContextMenu("Hide Current Screen")]
        public void HideCurrentScreen()
        {
            if (_uiService == null)
            {
                Debug.LogError("UIService not available");
                return;
            }
            
            Debug.Log("Hiding current screen");
            _ = _uiService.HideAsync(_testScreenType);
        }
        
        [ContextMenu("Test Blur Performance")]
        public void TestBlurPerformance()
        {
            if (_testBlurEffect == null)
            {
                Debug.LogError("No UIBlurEffect assigned");
                return;
            }
            
            Debug.Log("Testing blur performance...");
            
            // Measure blur enable time
            var startTime = Time.realtimeSinceStartup;
            _testBlurEffect.EnableBlur(0.1f);
            
            // Log performance data
            var enableTime = (Time.realtimeSinceStartup - startTime) * 1000f;
            Debug.Log($"Blur enable took {enableTime:F2}ms");
            
            // Test blur intensity changes
            for (float i = 0; i <= 1; i += 0.2f)
            {
                _testBlurEffect.SetBlurIntensity(i);
                Debug.Log($"Set blur intensity to {i:F1}");
            }
        }
        
        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(10, 10, 200, 200));
            GUILayout.Label("Blur Backdrop Test");
            
            if (GUILayout.Button("Regular Modal"))
            {
                TestRegularModal();
            }
            
            if (GUILayout.Button("Blurred Modal"))
            {
                TestBlurredModal();
            }
            
            if (GUILayout.Button("Hide Screen"))
            {
                HideCurrentScreen();
            }
            
            GUILayout.Space(10);
            GUILayout.Label("Manual Blur Control:");
            
            if (_testBlurEffect != null)
            {
                _enableBlur = GUILayout.Toggle(_enableBlur, "Enable Blur");
                _blurIntensity = GUILayout.HorizontalSlider(_blurIntensity, 0f, 1f);
                GUILayout.Label($"Intensity: {_blurIntensity:F2}");
                
                if (_testBlurEffect.IsBlurring)
                {
                    GUILayout.Label("Status: Blurring");
                }
                else
                {
                    GUILayout.Label("Status: Not blurring");
                }
            }
            
            GUILayout.EndArea();
        }
    }
}