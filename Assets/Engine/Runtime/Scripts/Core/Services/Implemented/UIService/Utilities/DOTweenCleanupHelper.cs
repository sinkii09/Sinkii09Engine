using UnityEngine;
using UnityEngine.SceneManagement;

namespace Sinkii09.Engine.Services
{
    /// <summary>
    /// MonoBehaviour helper to ensure DOTween cleanup happens even if UIService is not properly disposed
    /// This acts as a failsafe to prevent scene cleanup warnings
    /// </summary>
    [DefaultExecutionOrder(-1000)] // Execute early in cleanup order
    public class DOTweenCleanupHelper : MonoBehaviour
    {
        private static DOTweenCleanupHelper _instance;
        private bool _hasCleanedUp = false;

        /// <summary>
        /// Create or get the singleton cleanup helper
        /// </summary>
        public static void EnsureCleanupHelper()
        {
            if (_instance == null)
            {
                var helperObject = new GameObject("DOTweenCleanupHelper");
                _instance = helperObject.AddComponent<DOTweenCleanupHelper>();
                DontDestroyOnLoad(helperObject);
                
                Debug.Log("[DOTweenCleanupHelper] Created DOTween cleanup helper");
            }
        }

        private void Awake()
        {
            // Ensure only one instance exists
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);

            // Subscribe to application events  
            Application.quitting += OnApplicationQuitting;
        }

        private void OnApplicationQuitting()
        {
            PerformCleanup("Application Quit");
        }


        private void OnDestroy()
        {
            PerformCleanup("DOTweenCleanupHelper Destroyed");
            
            // Unsubscribe from events
            Application.quitting -= OnApplicationQuitting;
        }

        private void PerformCleanup(string reason)
        {
            if (_hasCleanedUp) return;

            try
            {
                Debug.Log($"[DOTweenCleanupHelper] Performing DOTween cleanup: {reason}");

                // Kill all active tweens safely
                if (DG.Tweening.DOTween.instance != null)
                {
                    DG.Tweening.DOTween.KillAll(false);
                    DG.Tweening.DOTween.Clear(true);
                }
                
                _hasCleanedUp = true;
                Debug.Log("[DOTweenCleanupHelper] DOTween cleanup completed successfully");
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[DOTweenCleanupHelper] DOTween cleanup encountered an issue: {ex.Message}");
                _hasCleanedUp = true; // Mark as cleaned even if failed to prevent retry loops
            }
        }

        /// <summary>
        /// Manual cleanup trigger for external use
        /// </summary>
        public static void TriggerCleanup(string reason = "Manual Trigger")
        {
            if (_instance != null)
            {
                _instance.PerformCleanup(reason);
            }
        }

        /// <summary>
        /// Reset the cleanup flag (useful for testing)
        /// </summary>
        public static void ResetCleanupFlag()
        {
            if (_instance != null)
            {
                _instance._hasCleanedUp = false;
            }
        }
    }
}