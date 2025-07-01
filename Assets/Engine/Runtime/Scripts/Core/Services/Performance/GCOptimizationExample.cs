using UnityEngine;
using Cysharp.Threading.Tasks;

namespace Sinkii09.Engine.Services.Performance
{
    /// <summary>
    /// Example component showing how to use the GC optimization system
    /// </summary>
    public class GCOptimizationExample : MonoBehaviour
    {
        [Header("GC Test Settings")]
        [SerializeField] private bool enableGCOptimization = true;
        [SerializeField] private float testInterval = 5f;
        [SerializeField] private int allocationsPerTest = 1000;
        
        private float _lastTestTime;
        private GCOptimizationManager _gcManager;
        
        private void Start()
        {
            // Get or create the GC optimization manager
            _gcManager = GCOptimizationManager.Instance;
            
            Debug.Log($"GC Optimization Example started. Mode: {UnityEngine.Scripting.GarbageCollector.GCMode}");
            
            // Start the allocation test
            _ = AllocationTestLoop();
        }
        
        /// <summary>
        /// Test loop that allocates memory and triggers GC
        /// </summary>
        private async UniTaskVoid AllocationTestLoop()
        {
            while (this != null)
            {
                await UniTask.Delay((int)(testInterval * 1000));
                
                if (enableGCOptimization)
                {
                    await PerformOptimizedAllocationTest();
                }
                else
                {
                    PerformLegacyAllocationTest();
                }
            }
        }
        
        /// <summary>
        /// Allocation test using optimized GC
        /// </summary>
        private async UniTask PerformOptimizedAllocationTest()
        {
            Debug.Log("Starting optimized allocation test...");
            var startTime = Time.realtimeSinceStartup;
            
            // Allocate memory
            AllocateTestMemory();
            
            // Request incremental GC instead of blocking GC
            await _gcManager.RequestIncrementalGCAsync(1);
            
            var duration = (Time.realtimeSinceStartup - startTime) * 1000f;
            var stats = _gcManager.GetStatistics();
            
            Debug.Log($"Optimized test completed in {duration:F1}ms. GC Stats: {stats}");
        }
        
        /// <summary>
        /// Allocation test using legacy forced GC (for comparison)
        /// </summary>
        private void PerformLegacyAllocationTest()
        {
            Debug.Log("Starting legacy allocation test...");
            var startTime = Time.realtimeSinceStartup;
            
            // Allocate memory
            AllocateTestMemory();
            
            // Force immediate GC (causes FPS drops)
            System.GC.Collect(1, System.GCCollectionMode.Forced);
            System.GC.WaitForPendingFinalizers();
            
            var duration = (Time.realtimeSinceStartup - startTime) * 1000f;
            Debug.Log($"Legacy test completed in {duration:F1}ms (may cause FPS drop)");
        }
        
        /// <summary>
        /// Allocate test memory to trigger GC
        /// </summary>
        private void AllocateTestMemory()
        {
            var objects = new object[allocationsPerTest];
            for (int i = 0; i < allocationsPerTest; i++)
            {
                objects[i] = new float[100]; // Allocate ~400 bytes per object
            }
        }
        
        private void OnGUI()
        {
            if (_gcManager == null) return;
            
            var stats = _gcManager.GetStatistics();
            var rect = new Rect(10, 10, 400, 150);
            
            GUI.Box(rect, "");
            GUILayout.BeginArea(rect);
            
            GUILayout.Label($"GC Mode: {stats.Mode}");
            GUILayout.Label($"Time Slice: {stats.IncrementalTimeSliceNanoseconds / 1_000_000}ms");
            GUILayout.Label($"Avg GC Time: {stats.AverageGCTimeMs:F1}ms");
            GUILayout.Label($"Max GC Time: {stats.MaxGCTimeMs:F1}ms");
            GUILayout.Label($"Total GC Frames: {stats.TotalGCFrames}");
            GUILayout.Label($"Time Since Last GC: {stats.TimeSinceLastGC:F1}s");
            
            enableGCOptimization = GUILayout.Toggle(enableGCOptimization, "Enable GC Optimization");
            
            GUILayout.EndArea();
        }
    }
}