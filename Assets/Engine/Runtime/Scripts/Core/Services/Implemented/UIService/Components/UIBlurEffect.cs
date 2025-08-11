using UnityEngine;
using UnityEngine.UI;
using System;

namespace Sinkii09.Engine.Services
{
    /// <summary>
    /// Simple placeholder blur effect for modal backdrops
    /// TODO: Implement proper blur effect when needed
    /// </summary>
    [RequireComponent(typeof(Image))]
    public class UIBlurEffect : MonoBehaviour
    {
        private Image _image;
        
        /// <summary>
        /// Whether the blur effect is currently active
        /// </summary>
        public bool IsBlurring { get; private set; }
        
        /// <summary>
        /// Event triggered when blur state changes
        /// </summary>
        public event Action<bool> BlurStateChanged;

        private void Awake()
        {
            _image = GetComponent<Image>();
        }

        /// <summary>
        /// Enable blur effect (placeholder)
        /// </summary>
        public void EnableBlur(float duration = 0.3f)
        {
            if (IsBlurring) return;
            
            // Placeholder: just show a semi-transparent overlay
            _image.color = new Color(0, 0, 0, 0.3f);
            
            IsBlurring = true;
            BlurStateChanged?.Invoke(true);
            
            Debug.Log("[UIBlurEffect] Blur enabled (placeholder implementation)");
        }
        
        /// <summary>
        /// Disable blur effect (placeholder)
        /// </summary>
        public void DisableBlur(float duration = 0.3f)
        {
            if (!IsBlurring) return;
            
            // Placeholder: hide the overlay
            _image.color = Color.clear;
            
            IsBlurring = false;
            BlurStateChanged?.Invoke(false);
            
            Debug.Log("[UIBlurEffect] Blur disabled (placeholder implementation)");
        }

        /// <summary>
        /// Set blur intensity (placeholder)
        /// </summary>
        public void SetBlurIntensity(float intensity)
        {
            intensity = Mathf.Clamp01(intensity);
            _image.color = new Color(0, 0, 0, intensity * 0.3f);
        }
    }
}