using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Sinkii09.Engine.Services.Performance
{
    /// <summary>
    /// Advanced topological sorting optimizer with parallel processing, caching, and incremental updates
    /// Target: 3x faster sorting through parallel processing, 90% faster repeated sorts through caching
    /// </summary>
    public class TopologicalSortOptimizer
    {
        #region Configuration
        
        /// <summary>
        /// Configuration for topological sort optimization
        /// </summary>
        public struct TopologicalSortConfig
        {
            public int ParallelProcessingThreshold { get; set; }
            public int MaxCacheSize { get; set; }
            public bool EnableIncrementalSorting { get; set; }
            public bool EnableParallelProcessing { get; set; }
            public bool EnablePerformanceMonitoring { get; set; }
            
            public static TopologicalSortConfig Default => new TopologicalSortConfig
            {
                ParallelProcessingThreshold = 50,
                MaxCacheSize = 100,
                EnableIncrementalSorting = true,
                EnableParallelProcessing = true,
                EnablePerformanceMonitoring = true
            };
        }
        
        #endregion
        
        #region Private Fields
        
        private readonly TopologicalSortConfig _config;
        private readonly ConcurrentDictionary<string, TopologicalSortResult> _resultCache;
        private readonly object _cacheLock = new object();
        
        // Performance metrics
        private long _totalSorts;
        private long _cacheHits;
        private long _parallelSorts;
        private long _incrementalSorts;
        private long _totalSortTimeMs;
        
        #endregion
        
        #region Constructor
        
        public TopologicalSortOptimizer(TopologicalSortConfig config = default)
        {
            _config = config.Equals(default(TopologicalSortConfig)) ? TopologicalSortConfig.Default : config;
            _resultCache = new ConcurrentDictionary<string, TopologicalSortResult>();
        }
        
        #endregion
        
        #region Public Methods
        
        /// <summary>
        /// Optimized topological sort with parallel processing and caching
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public async UniTask<int[]> TopologicalSortAsync(
            int nodeCount, 
            IReadOnlyDictionary<int, int[]> dependencyIndices,
            IReadOnlyDictionary<int, int[]> dependentIndices,
            string graphHash = null)
        {
            var startTime = DateTime.UtcNow;
            System.Threading.Interlocked.Increment(ref _totalSorts);
            
            try
            {
                // Try cache first if hash provided
                if (!string.IsNullOrEmpty(graphHash) && _resultCache.TryGetValue(graphHash, out var cachedResult))
                {
                    System.Threading.Interlocked.Increment(ref _cacheHits);
                    return cachedResult.SortedIndices;
                }
                
                int[] result;
                
                // Choose algorithm based on graph size and configuration
                if (_config.EnableParallelProcessing && nodeCount >= _config.ParallelProcessingThreshold)
                {
                    result = await ParallelTopologicalSortAsync(nodeCount, dependencyIndices, dependentIndices);
                    System.Threading.Interlocked.Increment(ref _parallelSorts);
                }
                else
                {
                    result = OptimizedKahnsAlgorithm(nodeCount, dependencyIndices, dependentIndices);
                }
                
                // Cache result if hash provided
                if (!string.IsNullOrEmpty(graphHash))
                {
                    CacheResult(graphHash, result);
                }
                
                return result;
            }
            finally
            {
                var duration = (DateTime.UtcNow - startTime).Milliseconds;
                System.Threading.Interlocked.Add(ref _totalSortTimeMs, duration);
            }
        }
        
        /// <summary>
        /// Synchronous optimized topological sort
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int[] TopologicalSort(
            int nodeCount,
            IReadOnlyDictionary<int, int[]> dependencyIndices,
            IReadOnlyDictionary<int, int[]> dependentIndices,
            string graphHash = null)
        {
            var startTime = DateTime.UtcNow;
            System.Threading.Interlocked.Increment(ref _totalSorts);
            
            try
            {
                // Try cache first if hash provided
                if (!string.IsNullOrEmpty(graphHash) && _resultCache.TryGetValue(graphHash, out var cachedResult))
                {
                    System.Threading.Interlocked.Increment(ref _cacheHits);
                    return cachedResult.SortedIndices;
                }
                
                // Use optimized Kahn's algorithm for synchronous calls
                var result = OptimizedKahnsAlgorithm(nodeCount, dependencyIndices, dependentIndices);
                
                // Cache result if hash provided
                if (!string.IsNullOrEmpty(graphHash))
                {
                    CacheResult(graphHash, result);
                }
                
                return result;
            }
            finally
            {
                var duration = (DateTime.UtcNow - startTime).Milliseconds;
                System.Threading.Interlocked.Add(ref _totalSortTimeMs, duration);
            }
        }
        
        /// <summary>
        /// Incremental topological sort for dynamic service addition
        /// </summary>
        public int[] IncrementalTopologicalSort(
            int[] previousOrder,
            int newNodeIndex,
            int[] newNodeDependencies,
            IReadOnlyDictionary<int, int[]> dependencyIndices,
            IReadOnlyDictionary<int, int[]> dependentIndices)
        {
            if (!_config.EnableIncrementalSorting || previousOrder == null)
            {
                // Fallback to full sort
                return OptimizedKahnsAlgorithm(dependencyIndices.Count, dependencyIndices, dependentIndices);
            }
            
            System.Threading.Interlocked.Increment(ref _incrementalSorts);
            
            // Fast path: if new node has no dependencies, add it at the beginning
            if (newNodeDependencies.Length == 0)
            {
                var result = new int[previousOrder.Length + 1];
                result[0] = newNodeIndex;
                Array.Copy(previousOrder, 0, result, 1, previousOrder.Length);
                return result;
            }
            
            // Find insertion point based on dependencies
            var insertionIndex = FindInsertionPoint(previousOrder, newNodeDependencies);
            
            // Create new order with inserted node
            return InsertNodeInOrder(previousOrder, newNodeIndex, insertionIndex);
        }
        
        /// <summary>
        /// Generate hash for dependency graph to enable caching
        /// </summary>
        public string GenerateGraphHash(IReadOnlyDictionary<int, int[]> dependencyIndices)
        {
            using (var sha256 = SHA256.Create())
            {
                var sb = new StringBuilder();
                
                // Sort by key for consistent hashing
                foreach (var kvp in dependencyIndices.OrderBy(x => x.Key))
                {
                    sb.Append(kvp.Key);
                    sb.Append(":");
                    sb.Append(string.Join(",", kvp.Value.OrderBy(x => x)));
                    sb.Append(";");
                }
                
                var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(sb.ToString()));
                return Convert.ToBase64String(hash);
            }
        }
        
        /// <summary>
        /// Get performance statistics
        /// </summary>
        public TopologicalSortStatistics GetStatistics()
        {
            return new TopologicalSortStatistics
            {
                TotalSorts = _totalSorts,
                CacheHits = _cacheHits,
                ParallelSorts = _parallelSorts,
                IncrementalSorts = _incrementalSorts,
                TotalSortTimeMs = _totalSortTimeMs,
                CacheSize = _resultCache.Count,
                CacheHitRatio = _totalSorts > 0 ? (double)_cacheHits / _totalSorts : 0,
                AverageSortTimeMs = _totalSorts > 0 ? (double)_totalSortTimeMs / _totalSorts : 0
            };
        }
        
        /// <summary>
        /// Clear cache and reset statistics
        /// </summary>
        public void Reset()
        {
            lock (_cacheLock)
            {
                _resultCache.Clear();
            }
            
            System.Threading.Interlocked.Exchange(ref _totalSorts, 0);
            System.Threading.Interlocked.Exchange(ref _cacheHits, 0);
            System.Threading.Interlocked.Exchange(ref _parallelSorts, 0);
            System.Threading.Interlocked.Exchange(ref _incrementalSorts, 0);
            System.Threading.Interlocked.Exchange(ref _totalSortTimeMs, 0);
        }
        
        #endregion
        
        #region Private Methods
        
        /// <summary>
        /// Parallel topological sort for large graphs
        /// </summary>
        private async UniTask<int[]> ParallelTopologicalSortAsync(
            int nodeCount,
            IReadOnlyDictionary<int, int[]> dependencyIndices,
            IReadOnlyDictionary<int, int[]> dependentIndices)
        {
            // Identify strongly connected components and independent subgraphs
            var subgraphs = IdentifyIndependentSubgraphs(nodeCount, dependencyIndices, dependentIndices);
            
            if (subgraphs.Count <= 1)
            {
                // No parallelization benefit, use optimized single-threaded algorithm
                return OptimizedKahnsAlgorithm(nodeCount, dependencyIndices, dependentIndices);
            }
            
            // Sort subgraphs in parallel
            var subgraphTasks = subgraphs.Select(async subgraph =>
            {
                var subgraphDependencies = CreateSubgraphDependencies(subgraph, dependencyIndices);
                var subgraphDependents = CreateSubgraphDependents(subgraph, dependentIndices);
                
                return await UniTask.RunOnThreadPool(() =>
                    OptimizedKahnsAlgorithm(subgraph.Count, subgraphDependencies, subgraphDependents));
            }).ToArray();
            
            var subgraphResults = await UniTask.WhenAll(subgraphTasks);
            
            // Merge results maintaining global topological order
            return MergeSubgraphResults(subgraphResults, subgraphs, dependencyIndices);
        }
        
        /// <summary>
        /// Optimized Kahn's algorithm implementation
        /// </summary>
        private int[] OptimizedKahnsAlgorithm(
            int nodeCount,
            IReadOnlyDictionary<int, int[]> dependencyIndices,
            IReadOnlyDictionary<int, int[]> dependentIndices)
        {
            // Pre-allocate arrays for better performance
            var inDegree = new int[nodeCount];
            var queue = new Queue<int>(nodeCount);
            var result = new List<int>(nodeCount);
            
            // Calculate in-degrees efficiently
            foreach (var kvp in dependencyIndices)
            {
                foreach (var dep in kvp.Value)
                {
                    if (dep < nodeCount) // Bounds check
                    {
                        inDegree[dep]++;
                    }
                }
            }
            
            // Find nodes with no incoming edges
            for (int i = 0; i < nodeCount; i++)
            {
                if (dependencyIndices.ContainsKey(i) && inDegree[i] == 0)
                {
                    queue.Enqueue(i);
                }
            }
            
            // Process nodes
            while (queue.Count > 0)
            {
                var nodeIndex = queue.Dequeue();
                result.Add(nodeIndex);
                
                if (dependentIndices.TryGetValue(nodeIndex, out var dependents))
                {
                    foreach (var dependent in dependents)
                    {
                        if (dependent < nodeCount) // Bounds check
                        {
                            inDegree[dependent]--;
                            if (inDegree[dependent] == 0)
                            {
                                queue.Enqueue(dependent);
                            }
                        }
                    }
                }
            }
            
            // Validate no cycles
            if (result.Count != dependencyIndices.Count)
            {
                throw new InvalidOperationException($"Graph contains cycles. Expected {dependencyIndices.Count} nodes, got {result.Count}");
            }
            
            return result.ToArray();
        }
        
        /// <summary>
        /// Identify independent subgraphs for parallel processing
        /// </summary>
        private List<HashSet<int>> IdentifyIndependentSubgraphs(
            int nodeCount,
            IReadOnlyDictionary<int, int[]> dependencyIndices,
            IReadOnlyDictionary<int, int[]> dependentIndices)
        {
            var visited = new bool[nodeCount];
            var subgraphs = new List<HashSet<int>>();
            
            foreach (var node in dependencyIndices.Keys)
            {
                if (!visited[node])
                {
                    var subgraph = new HashSet<int>();
                    ExploreConnectedComponent(node, visited, subgraph, dependencyIndices, dependentIndices);
                    
                    if (subgraph.Count > 1) // Only consider non-trivial subgraphs
                    {
                        subgraphs.Add(subgraph);
                    }
                }
            }
            
            return subgraphs;
        }
        
        /// <summary>
        /// Explore connected component using DFS
        /// </summary>
        private void ExploreConnectedComponent(
            int node,
            bool[] visited,
            HashSet<int> component,
            IReadOnlyDictionary<int, int[]> dependencyIndices,
            IReadOnlyDictionary<int, int[]> dependentIndices)
        {
            if (visited[node] || component.Contains(node))
                return;
            
            visited[node] = true;
            component.Add(node);
            
            // Explore dependencies
            if (dependencyIndices.TryGetValue(node, out var dependencies))
            {
                foreach (var dep in dependencies)
                {
                    ExploreConnectedComponent(dep, visited, component, dependencyIndices, dependentIndices);
                }
            }
            
            // Explore dependents
            if (dependentIndices.TryGetValue(node, out var dependents))
            {
                foreach (var dependent in dependents)
                {
                    ExploreConnectedComponent(dependent, visited, component, dependencyIndices, dependentIndices);
                }
            }
        }
        
        /// <summary>
        /// Create subgraph dependencies for parallel processing
        /// </summary>
        private Dictionary<int, int[]> CreateSubgraphDependencies(
            HashSet<int> subgraph,
            IReadOnlyDictionary<int, int[]> globalDependencies)
        {
            var subgraphDeps = new Dictionary<int, int[]>();
            
            foreach (var node in subgraph)
            {
                if (globalDependencies.TryGetValue(node, out var deps))
                {
                    var subgraphLocalDeps = deps.Where(subgraph.Contains).ToArray();
                    subgraphDeps[node] = subgraphLocalDeps;
                }
                else
                {
                    subgraphDeps[node] = Array.Empty<int>();
                }
            }
            
            return subgraphDeps;
        }
        
        /// <summary>
        /// Create subgraph dependents for parallel processing
        /// </summary>
        private Dictionary<int, int[]> CreateSubgraphDependents(
            HashSet<int> subgraph,
            IReadOnlyDictionary<int, int[]> globalDependents)
        {
            var subgraphDeps = new Dictionary<int, int[]>();
            
            foreach (var node in subgraph)
            {
                if (globalDependents.TryGetValue(node, out var deps))
                {
                    var subgraphLocalDeps = deps.Where(subgraph.Contains).ToArray();
                    subgraphDeps[node] = subgraphLocalDeps;
                }
                else
                {
                    subgraphDeps[node] = Array.Empty<int>();
                }
            }
            
            return subgraphDeps;
        }
        
        /// <summary>
        /// Merge parallel subgraph results maintaining global topological order
        /// </summary>
        private int[] MergeSubgraphResults(
            int[][] subgraphResults,
            List<HashSet<int>> subgraphs,
            IReadOnlyDictionary<int, int[]> globalDependencies)
        {
            // This is a simplified merge - in practice, would need more sophisticated
            // inter-subgraph dependency analysis for correct global ordering
            var result = new List<int>();
            
            // For now, append subgraph results (works for truly independent subgraphs)
            for (int i = 0; i < subgraphResults.Length; i++)
            {
                result.AddRange(subgraphResults[i]);
            }
            
            return result.ToArray();
        }
        
        /// <summary>
        /// Find insertion point for incremental sorting
        /// </summary>
        private int FindInsertionPoint(int[] previousOrder, int[] newNodeDependencies)
        {
            if (newNodeDependencies.Length == 0)
                return 0;
            
            // Find the latest position of any dependency
            int latestDependencyIndex = -1;
            
            for (int i = 0; i < previousOrder.Length; i++)
            {
                if (newNodeDependencies.Contains(previousOrder[i]))
                {
                    latestDependencyIndex = i;
                }
            }
            
            return latestDependencyIndex + 1;
        }
        
        /// <summary>
        /// Insert node in order at specified index
        /// </summary>
        private int[] InsertNodeInOrder(int[] previousOrder, int newNodeIndex, int insertionIndex)
        {
            var result = new int[previousOrder.Length + 1];
            
            // Copy elements before insertion point
            Array.Copy(previousOrder, 0, result, 0, insertionIndex);
            
            // Insert new node
            result[insertionIndex] = newNodeIndex;
            
            // Copy elements after insertion point
            Array.Copy(previousOrder, insertionIndex, result, insertionIndex + 1, previousOrder.Length - insertionIndex);
            
            return result;
        }
        
        /// <summary>
        /// Cache topological sort result
        /// </summary>
        private void CacheResult(string graphHash, int[] result)
        {
            var cacheResult = new TopologicalSortResult
            {
                SortedIndices = result,
                CachedAt = DateTime.UtcNow
            };
            
            lock (_cacheLock)
            {
                // Simple LRU eviction if cache is full
                if (_resultCache.Count >= _config.MaxCacheSize)
                {
                    var oldestKey = _resultCache
                        .OrderBy(kvp => kvp.Value.CachedAt)
                        .First().Key;
                    
                    _resultCache.TryRemove(oldestKey, out _);
                }
                
                _resultCache.TryAdd(graphHash, cacheResult);
            }
        }
        
        #endregion
        
        #region Nested Types
        
        /// <summary>
        /// Cached topological sort result
        /// </summary>
        private struct TopologicalSortResult
        {
            public int[] SortedIndices { get; set; }
            public DateTime CachedAt { get; set; }
        }
        
        #endregion
    }
    
    /// <summary>
    /// Performance statistics for topological sorting
    /// </summary>
    public struct TopologicalSortStatistics
    {
        public long TotalSorts { get; set; }
        public long CacheHits { get; set; }
        public long ParallelSorts { get; set; }
        public long IncrementalSorts { get; set; }
        public long TotalSortTimeMs { get; set; }
        public int CacheSize { get; set; }
        public double CacheHitRatio { get; set; }
        public double AverageSortTimeMs { get; set; }
        
        public override string ToString()
        {
            return $"TopologicalSort: {TotalSorts} sorts, {CacheHitRatio:P1} cache hit ratio, " +
                   $"{ParallelSorts} parallel, {IncrementalSorts} incremental, " +
                   $"Avg: {AverageSortTimeMs:F2}ms";
        }
    }
}