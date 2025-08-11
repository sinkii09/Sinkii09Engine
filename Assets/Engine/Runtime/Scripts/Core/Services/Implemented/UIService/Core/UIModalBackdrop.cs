using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System;
using Cysharp.Threading.Tasks;

namespace Sinkii09.Engine.Services
{
    /// <summary>
    /// Modal backdrop that blocks interaction and provides visual feedback
    /// </summary>
    [RequireComponent(typeof(Canvas))]
    [RequireComponent(typeof(GraphicRaycaster))]
    public class UIModalBackdrop : MonoBehaviour, IPointerClickHandler
    {
        private Canvas _canvas;
        private Image _backgroundImage;
        private UIBlurEffect _blurEffect;
        private UIDisplayConfig _config;
        private Action _onBackdropClicked;
        
        /// <summary>
        /// The screen this backdrop is associated with
        /// </summary>
        public UIScreen AssociatedScreen { get; private set; }
        
        /// <summary>
        /// Initialize the backdrop with configuration
        /// </summary>
        /// <param name="config">Display configuration for the backdrop</param>
        /// <param name="associatedScreen">The screen this backdrop belongs to</param>
        /// <param name="onBackdropClicked">Callback when backdrop is clicked</param>
        public void Initialize(UIDisplayConfig config, UIScreen associatedScreen, Action onBackdropClicked)
        {
            _config = config;
            AssociatedScreen = associatedScreen;
            _onBackdropClicked = onBackdropClicked;
            
            SetupCanvas();
            SetupBackground();
        }
        
        private void SetupCanvas()
        {
            _canvas = GetComponent<Canvas>();
            if (_canvas == null)
            {
                _canvas = gameObject.AddComponent<Canvas>();
            }
            
            // Ensure the associated screen's canvas is properly configured first
            if (AssociatedScreen?.Canvas != null && AssociatedScreen.DisplayConfig != null)
            {
                var modalCanvas = AssociatedScreen.Canvas;
                
                // Make sure modal canvas has override sorting enabled
                modalCanvas.overrideSorting = true;
                modalCanvas.sortingOrder = modalCanvas.sortingOrder + AssociatedScreen.DisplayConfig.SortingOrderOffset;
                
                Debug.Log($"[UIModalBackdrop] Modal canvas sorting order: {modalCanvas.sortingOrder}");
            }
            
            // Set canvas properties for modal backdrop
            _canvas.overrideSorting = true;
            _canvas.sortingOrder = AssociatedScreen.Canvas.sortingOrder - 1; // Just behind the modal
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            
            Debug.Log($"[UIModalBackdrop] Backdrop canvas sorting order: {_canvas.sortingOrder} (behind modal)");
            
            // Ensure we have a GraphicRaycaster for interaction
            if (GetComponent<GraphicRaycaster>() == null)
            {
                gameObject.AddComponent<GraphicRaycaster>();
            }
        }
        
        private void SetupBackground()
        {
            // Create background image if it doesn't exist
            _backgroundImage = GetComponent<Image>();
            if (_backgroundImage == null)
            {
                _backgroundImage = gameObject.AddComponent<Image>();
            }
            
            // Configure background based on backdrop behavior
            switch (_config.BackdropBehavior)
            {
                case ModalBackdropBehavior.None:
                    _backgroundImage.color = Color.clear;
                    break;
                    
                case ModalBackdropBehavior.Dimmed:
                    _backgroundImage.color = new Color(0, 0, 0, _config.BackdropOpacity);
                    break;
                    
                case ModalBackdropBehavior.Blurred:
                    SetupBlurEffect();
                    _backgroundImage.color = new Color(0.1f, 0.1f, 0.1f, _config.BackdropOpacity * 0.3f);
                    break;
                    
                case ModalBackdropBehavior.Custom:
                    // Allow custom configuration
                    break;
            }
            
            // Make the image fill the entire screen
            var rectTransform = _backgroundImage.rectTransform;
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.sizeDelta = Vector2.zero;
            rectTransform.anchoredPosition = Vector2.zero;
        }
        
        /// <summary>
        /// Handle backdrop click events
        /// </summary>
        public void OnPointerClick(PointerEventData eventData)
        {
            if (_config?.CloseOnBackdropClick == true)
            {
                _onBackdropClicked?.Invoke();
            }
        }
        
        /// <summary>
        /// Show the backdrop
        /// </summary>
        public void Show()
        {
            gameObject.SetActive(true);
            _canvas.enabled = true;
            
            // Enable blur effect if configured
            if (_config?.BackdropBehavior == ModalBackdropBehavior.Blurred)
            {
                EnableBlur(_config.BlurAnimationDuration);
            }
        }
        
        /// <summary>
        /// Hide the backdrop
        /// </summary>
        public void Hide()
        {
            // Disable blur effect before hiding
            if (_config?.BackdropBehavior == ModalBackdropBehavior.Blurred)
            {
                var hideDuration = _config.BlurAnimationDuration * 0.7f; // Slightly faster hide
                DisableBlur(hideDuration);
                
                // Wait for blur to start disabling, then hide
                StartCoroutine(DelayedHide(hideDuration * 0.5f));
            }
            else
            {
                _canvas.enabled = false;
                gameObject.SetActive(false);
            }
        }
        
        private System.Collections.IEnumerator DelayedHide(float delay)
        {
            yield return new WaitForSeconds(delay);
            _canvas.enabled = false;
            gameObject.SetActive(false);
        }
        
        /// <summary>
        /// Update the backdrop opacity
        /// </summary>
        /// <param name="opacity">New opacity value (0.0 to 1.0)</param>
        public void SetOpacity(float opacity)
        {
            if (_backgroundImage != null)
            {
                var color = _backgroundImage.color;
                color.a = Mathf.Clamp01(opacity);
                _backgroundImage.color = color;
            }
        }
        
        /// <summary>
        /// Update the backdrop color
        /// </summary>
        /// <param name="color">New backdrop color</param>
        public void SetBackdropColor(Color color)
        {
            if (_backgroundImage != null)
            {
                _backgroundImage.color = color;
            }
        }
        
        /// <summary>
        /// Setup blur effect for the backdrop
        /// </summary>
        private void SetupBlurEffect()
        {
            // Add blur effect component if it doesn't exist
            _blurEffect = GetComponent<UIBlurEffect>();
            if (_blurEffect == null)
            {
                _blurEffect = gameObject.AddComponent<UIBlurEffect>();
            }
            
            // Configure blur settings based on display config
            ConfigureBlurSettings();
        }
        
        /// <summary>
        /// Configure blur effect settings (placeholder)
        /// </summary>
        private void ConfigureBlurSettings()
        {
            if (_blurEffect == null) return;
            
            // Subscribe to blur state changes
            _blurEffect.BlurStateChanged += OnBlurStateChanged;
            
            Debug.Log("[UIModalBackdrop] Blur effect configured (placeholder implementation)");
        }
        
        /// <summary>
        /// Handle blur state changes
        /// </summary>
        private void OnBlurStateChanged(bool isBlurring)
        {
            // Optional: Adjust other visual elements based on blur state
            if (isBlurring)
            {
                // Blur is active - maybe reduce backdrop opacity slightly
                if (_backgroundImage != null)
                {
                    var color = _backgroundImage.color;
                    color.a *= 0.8f;
                    _backgroundImage.color = color;
                }
            }
        }
        
        /// <summary>
        /// Enable blur effect with animation
        /// </summary>
        public void EnableBlur(float duration = 0.3f)
        {
            if (_blurEffect != null)
            {
                // Temporarily hide the associated modal screen during blur capture
                var modalWasActive = AssociatedScreen.gameObject.activeInHierarchy;
                if (modalWasActive)
                {
                    AssociatedScreen.gameObject.SetActive(false);
                }
                
                _blurEffect.EnableBlur(duration);
                
                // Re-enable the modal after blur capture completes
                if (modalWasActive)
                {
                    ReEnableModalAfterCapture().Forget();
                }
            }
        }
        
        private async UniTask ReEnableModalAfterCapture()
        {
            // Wait for blur capture to complete (blur effect waits for end of frame)
            await UniTask.DelayFrame(2);
            
            if (AssociatedScreen != null)
            {
                AssociatedScreen.gameObject.SetActive(true);
                Debug.Log($"[UIModalBackdrop] Re-enabled modal {AssociatedScreen.name} after blur capture");
            }
        }
        
        /// <summary>
        /// Disable blur effect with animation
        /// </summary>
        public void DisableBlur(float duration = 0.3f)
        {
            if (_blurEffect != null)
            {
                _blurEffect.DisableBlur(duration);
            }
        }
        
        /// <summary>
        /// Set blur intensity directly (0-1)
        /// </summary>
        public void SetBlurIntensity(float intensity)
        {
            if (_blurEffect != null)
            {
                _blurEffect.SetBlurIntensity(intensity);
            }
        }
        
        /// <summary>
        /// Check if blur is currently active
        /// </summary>
        public bool IsBlurActive => _blurEffect?.IsBlurring ?? false;
        
        private void OnDestroy()
        {
            // Cleanup blur event subscription
            if (_blurEffect != null)
            {
                _blurEffect.BlurStateChanged -= OnBlurStateChanged;
            }
        }
    }
}