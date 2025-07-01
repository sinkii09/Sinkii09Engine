using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Sinkii09.Engine.Services.Performance
{
    /// <summary>
    /// Analyze and optimize dependency resolution paths for shortest-path traversal
    /// Implements Dijkstra-like algorithm for optimal service resolution ordering
    /// </summary>
    public class ResolutionPathOptimizer
    {
        public readonly struct ResolutionNode
        {
            public readonly Type ServiceType;
            public readonly int Depth;
            public readonly double Cost;
            public readonly Type[] Path;
            
            public ResolutionNode(Type serviceType, int depth, double cost, Type[] path)
            {
                ServiceType = serviceType;
                Depth = depth;
                Cost = cost;
                Path = path;
            }
        }
        
        private readonly ConcurrentDictionary<Type, ResolutionPlan> _optimizedPaths;
        private readonly ConcurrentDictionary<Type, double> _resolutionCosts;
        private readonly ServiceMetadataCache _metadataCache;
        
        // Performance metrics
        private long _pathOptimizations;
        private long _pathCacheHits;
        private double _averagePathLength;
        
        /// <summary>
        /// Number of path optimizations performed
        /// </summary>
        public long PathOptimizationCount => _pathOptimizations;
        
        /// <summary>
        /// Number of cached path hits
        /// </summary>
        public long PathCacheHits => _pathCacheHits;
        
        /// <summary>
        /// Average path length for optimized resolutions
        /// </summary>
        public double AveragePathLength => _averagePathLength;
        
        public ResolutionPathOptimizer(ServiceMetadataCache metadataCache = null)
        {
            _optimizedPaths = new ConcurrentDictionary<Type, ResolutionPlan>();
            _resolutionCosts = new ConcurrentDictionary<Type, double>();
            _metadataCache = metadataCache ?? new ServiceMetadataCache();
        }
        
        /// <summary>
        /// Get optimized resolution plan for a service
        /// </summary>
        public ResolutionPlan GetOptimizedPlan(Type serviceType, IServiceContainer container)
        {
            if (_optimizedPaths.TryGetValue(serviceType, out var cachedPlan))
            {
                System.Threading.Interlocked.Increment(ref _pathCacheHits);
                return cachedPlan;
            }
            
            return CreateOptimizedPlan(serviceType, container);
        }
        
        /// <summary>
        /// Batch optimize resolution plans for multiple services
        /// </summary>
        public Dictionary<Type, ResolutionPlan> BatchOptimize(Type[] serviceTypes, IServiceContainer container)
        {
            var plans = new Dictionary<Type, ResolutionPlan>();
            var dependencyGraph = BuildDependencyGraph(serviceTypes, container);
            
            foreach (var serviceType in serviceTypes)
            {
                var plan = OptimizeSinglePath(serviceType, dependencyGraph, container);
                plans[serviceType] = plan;
                _optimizedPaths.TryAdd(serviceType, plan);
            }
            
            return plans;
        }
        
        /// <summary>
        /// Find the shortest resolution path using modified Dijkstra algorithm
        /// </summary>
        public Type[] FindShortestPath(Type sourceType, Type targetType, IServiceContainer container)
        {
            var visited = new HashSet<Type>();
            var distances = new Dictionary<Type, double>();
            var previous = new Dictionary<Type, Type>();
            var queue = new SortedSet<ResolutionNode>(new ResolutionNodeComparer());
            
            // Initialize
            distances[sourceType] = 0;
            queue.Add(new ResolutionNode(sourceType, 0, 0, new[] { sourceType }));
            
            while (queue.Count > 0)
            {
                var current = queue.Min;
                queue.Remove(current);
                
                if (visited.Contains(current.ServiceType))
                    continue;
                    
                visited.Add(current.ServiceType);
                
                if (current.ServiceType == targetType)
                {
                    return ReconstructPath(previous, sourceType, targetType);
                }
                
                // Explore neighbors (dependencies)
                var metadata = _metadataCache.GetOrCreateMetadata(current.ServiceType, container);
                if (metadata?.Dependencies != null)
                {
                    foreach (var dependency in metadata.Dependencies)
                    {
                        if (visited.Contains(dependency))
                            continue;
                            
                        var cost = CalculateResolutionCost(dependency, container);
                        var newDistance = distances[current.ServiceType] + cost;
                        
                        if (!distances.ContainsKey(dependency) || newDistance < distances[dependency])
                        {
                            distances[dependency] = newDistance;
                            previous[dependency] = current.ServiceType;
                            
                            var newPath = current.Path.Concat(new[] { dependency }).ToArray();
                            queue.Add(new ResolutionNode(dependency, current.Depth + 1, newDistance, newPath));
                        }
                    }
                }
            }
            
            return null; // No path found
        }
        
        /// <summary>
        /// Calculate dependency resolution order for optimal performance
        /// </summary>
        public Type[] CalculateOptimalOrder(Type[] serviceTypes, IServiceContainer container)
        {
            var graph = BuildDependencyGraph(serviceTypes, container);
            var ordered = new List<Type>();
            var visited = new HashSet<Type>();
            var visiting = new HashSet<Type>();
            
            foreach (var serviceType in serviceTypes)
            {
                if (!visited.Contains(serviceType))
                {
                    OptimalTopologicalSort(serviceType, graph, visited, visiting, ordered);
                }
            }
            
            ordered.Reverse(); // Reverse for correct dependency order
            return ordered.ToArray();
        }
        
        /// <summary>
        /// Validate resolution path for cycles and efficiency
        /// </summary>
        public PathValidationResult ValidatePath(Type[] resolutionPath, IServiceContainer container)
        {
            var result = new PathValidationResult
            {
                IsValid = true,
                PathLength = resolutionPath.Length,
                EstimatedCost = 0,
                Issues = new List<string>()
            };
            
            var seen = new HashSet<Type>();
            
            for (int i = 0; i < resolutionPath.Length; i++)
            {
                var serviceType = resolutionPath[i];
                
                // Check for cycles
                if (seen.Contains(serviceType))
                {
                    result.IsValid = false;
                    result.Issues.Add($"Circular dependency detected: {serviceType.Name} appears multiple times");
                    continue;
                }
                
                seen.Add(serviceType);
                
                // Calculate cost
                var cost = CalculateResolutionCost(serviceType, container);
                result.EstimatedCost += cost;
                
                // Check for inefficiencies
                if (cost > 10.0) // High cost threshold
                {
                    result.Issues.Add($"High resolution cost for {serviceType.Name}: {cost:F2}");
                }
            }
            
            // Check path efficiency
            if (result.PathLength > 10) // Deep dependency chain
            {
                result.Issues.Add($"Deep dependency chain detected: {result.PathLength} levels");
            }
            
            return result;
        }
        
        /// <summary>
        /// Create optimized resolution plan
        /// </summary>
        private ResolutionPlan CreateOptimizedPlan(Type serviceType, IServiceContainer container)
        {
            System.Threading.Interlocked.Increment(ref _pathOptimizations);
            
            var metadata = _metadataCache.GetOrCreateMetadata(serviceType, container);
            var dependencies = metadata?.Dependencies ?? Array.Empty<Type>();
            
            // Calculate optimal resolution order
            var resolutionOrder = CalculateOptimalOrder(dependencies.Concat(new[] { serviceType }).ToArray(), container);
            
            // Calculate costs
            var totalCost = resolutionOrder.Sum(t => CalculateResolutionCost(t, container));
            var criticalPath = IdentifyCriticalPath(resolutionOrder, container);
            
            var plan = new ResolutionPlan
            {
                ServiceType = serviceType,
                ResolutionOrder = resolutionOrder,
                TotalCost = totalCost,
                CriticalPath = criticalPath,
                CanUseParallelResolution = CanResolveInParallel(dependencies, container),
                EstimatedTime = TimeSpan.FromMilliseconds(totalCost),
                ParallelGroups = GroupForParallelExecution(dependencies, container)
            };
            
            _optimizedPaths.TryAdd(serviceType, plan);
            
            // Update metrics
            UpdateAveragePathLength(resolutionOrder.Length);
            
            return plan;
        }
        
        /// <summary>
        /// Build dependency graph for path optimization
        /// </summary>
        private Dictionary<Type, Type[]> BuildDependencyGraph(Type[] serviceTypes, IServiceContainer container)
        {
            var graph = new Dictionary<Type, Type[]>();
            
            foreach (var serviceType in serviceTypes)
            {
                var metadata = _metadataCache.GetOrCreateMetadata(serviceType, container);
                graph[serviceType] = metadata?.Dependencies ?? Array.Empty<Type>();
            }
            
            return graph;
        }
        
        /// <summary>
        /// Optimize single service resolution path
        /// </summary>
        private ResolutionPlan OptimizeSinglePath(Type serviceType, Dictionary<Type, Type[]> dependencyGraph, IServiceContainer container)
        {
            var visited = new HashSet<Type>();
            var path = new List<Type>();
            var cost = 0.0;
            
            OptimalDepthFirstSearch(serviceType, dependencyGraph, visited, path, ref cost, container);
            
            return new ResolutionPlan
            {
                ServiceType = serviceType,
                ResolutionOrder = path.ToArray(),
                TotalCost = cost,
                EstimatedTime = TimeSpan.FromMilliseconds(cost),
                CanUseParallelResolution = false, // Single path
                CriticalPath = path.ToArray(),
                ParallelGroups = new[] { path.ToArray() }
            };
        }
        
        /// <summary>
        /// Optimal depth-first search for dependency resolution
        /// </summary>
        private void OptimalDepthFirstSearch(Type current, Dictionary<Type, Type[]> graph, HashSet<Type> visited, List<Type> path, ref double cost, IServiceContainer container)
        {
            if (visited.Contains(current))
                return;
                
            visited.Add(current);
            
            if (graph.TryGetValue(current, out var dependencies))
            {
                // Sort dependencies by cost (resolve cheaper ones first)
                var sortedDeps = dependencies
                    .Where(d => !visited.Contains(d))
                    .OrderBy(d => CalculateResolutionCost(d, container))
                    .ToArray();
                    
                foreach (var dependency in sortedDeps)
                {
                    OptimalDepthFirstSearch(dependency, graph, visited, path, ref cost, container);
                }
            }
            
            path.Add(current);
            cost += CalculateResolutionCost(current, container);
        }
        
        /// <summary>
        /// Optimal topological sort considering resolution costs
        /// </summary>
        private void OptimalTopologicalSort(Type current, Dictionary<Type, Type[]> graph, HashSet<Type> visited, HashSet<Type> visiting, List<Type> ordered)
        {
            if (visiting.Contains(current))
                return; // Cycle detected
                
            if (visited.Contains(current))
                return;
                
            visiting.Add(current);
            
            if (graph.TryGetValue(current, out var dependencies))
            {
                foreach (var dependency in dependencies)
                {
                    OptimalTopologicalSort(dependency, graph, visited, visiting, ordered);
                }
            }
            
            visiting.Remove(current);
            visited.Add(current);
            ordered.Add(current);
        }
        
        /// <summary>
        /// Calculate resolution cost for a service type
        /// </summary>
        private double CalculateResolutionCost(Type serviceType, IServiceContainer container)
        {
            if (_resolutionCosts.TryGetValue(serviceType, out var cachedCost))
                return cachedCost;
                
            var metadata = _metadataCache.GetOrCreateMetadata(serviceType, container);
            double cost = 1.0; // Base cost
            
            // Factors affecting cost
            if (metadata != null)
            {
                // Singleton is cheaper (already instantiated)
                if (metadata.SingletonInstance != null)
                    cost *= 0.1;
                    
                // Factory method has medium cost
                else if (metadata.Factory != null)
                    cost *= 0.5;
                    
                // Complex constructor increases cost
                else if (metadata.BestConstructor != null)
                {
                    var paramCount = metadata.BestConstructor.GetParameters().Length;
                    cost *= 1.0 + (paramCount * 0.2);
                }
                
                // Dependencies increase cost
                if (metadata.Dependencies != null)
                    cost *= 1.0 + (metadata.Dependencies.Length * 0.1);
            }
            
            _resolutionCosts.TryAdd(serviceType, cost);
            return cost;
        }
        
        /// <summary>
        /// Check if services can be resolved in parallel
        /// </summary>
        private bool CanResolveInParallel(Type[] serviceTypes, IServiceContainer container)
        {
            // Quick check: if any service depends on another in the list, can't parallelize
            for (int i = 0; i < serviceTypes.Length; i++)
            {
                var metadata = _metadataCache.GetOrCreateMetadata(serviceTypes[i], container);
                if (metadata?.Dependencies != null)
                {
                    foreach (var dependency in metadata.Dependencies)
                    {
                        if (serviceTypes.Contains(dependency))
                            return false;
                    }
                }
            }
            
            return true;
        }
        
        /// <summary>
        /// Group services for parallel execution
        /// </summary>
        private Type[][] GroupForParallelExecution(Type[] serviceTypes, IServiceContainer container)
        {
            var groups = new List<List<Type>>();
            var remaining = new HashSet<Type>(serviceTypes);
            
            while (remaining.Count > 0)
            {
                var currentGroup = new List<Type>();
                var toProcess = remaining.ToArray();
                
                foreach (var serviceType in toProcess)
                {
                    var metadata = _metadataCache.GetOrCreateMetadata(serviceType, container);
                    var dependencies = metadata?.Dependencies ?? Array.Empty<Type>();
                    
                    // Can add to current group if no dependencies on remaining services
                    if (!dependencies.Any(d => remaining.Contains(d)))
                    {
                        currentGroup.Add(serviceType);
                        remaining.Remove(serviceType);
                    }
                }
                
                if (currentGroup.Count > 0)
                {
                    groups.Add(currentGroup);
                }
                else
                {
                    // Break potential circular dependency
                    var first = remaining.First();
                    groups.Add(new List<Type> { first });
                    remaining.Remove(first);
                }
            }
            
            return groups.Select(g => g.ToArray()).ToArray();
        }
        
        /// <summary>
        /// Identify critical path in resolution order
        /// </summary>
        private Type[] IdentifyCriticalPath(Type[] resolutionOrder, IServiceContainer container)
        {
            return resolutionOrder
                .OrderByDescending(t => CalculateResolutionCost(t, container))
                .Take(Math.Max(1, resolutionOrder.Length / 3))
                .ToArray();
        }
        
        /// <summary>
        /// Reconstruct path from Dijkstra algorithm
        /// </summary>
        private Type[] ReconstructPath(Dictionary<Type, Type> previous, Type source, Type target)
        {
            var path = new List<Type>();
            var current = target;
            
            while (current != null)
            {
                path.Add(current);
                previous.TryGetValue(current, out current);
            }
            
            path.Reverse();
            return path.ToArray();
        }
        
        /// <summary>
        /// Update average path length metric
        /// </summary>
        private void UpdateAveragePathLength(int pathLength)
        {
            var currentAverage = _averagePathLength;
            var count = _pathOptimizations;
            _averagePathLength = (currentAverage * (count - 1) + pathLength) / count;
        }
        
        /// <summary>
        /// Get optimization statistics
        /// </summary>
        public PathOptimizerStatistics GetStatistics()
        {
            return new PathOptimizerStatistics
            {
                OptimizedPathCount = _optimizedPaths.Count,
                PathOptimizationCount = _pathOptimizations,
                PathCacheHits = _pathCacheHits,
                AveragePathLength = _averagePathLength,
                CachedCostCount = _resolutionCosts.Count
            };
        }
        
        /// <summary>
        /// Clear all cached paths and costs
        /// </summary>
        public void Clear()
        {
            _optimizedPaths.Clear();
            _resolutionCosts.Clear();
            
            System.Threading.Interlocked.Exchange(ref _pathOptimizations, 0);
            System.Threading.Interlocked.Exchange(ref _pathCacheHits, 0);
            _averagePathLength = 0;
        }
    }
    
    /// <summary>
    /// Optimized resolution plan for a service
    /// </summary>
    public class ResolutionPlan
    {
        public Type ServiceType { get; set; }
        public Type[] ResolutionOrder { get; set; }
        public double TotalCost { get; set; }
        public TimeSpan EstimatedTime { get; set; }
        public bool CanUseParallelResolution { get; set; }
        public Type[] CriticalPath { get; set; }
        public Type[][] ParallelGroups { get; set; }
    }
    
    /// <summary>
    /// Path validation result
    /// </summary>
    public class PathValidationResult
    {
        public bool IsValid { get; set; }
        public int PathLength { get; set; }
        public double EstimatedCost { get; set; }
        public List<string> Issues { get; set; }
    }
    
    /// <summary>
    /// Path optimizer statistics
    /// </summary>
    public struct PathOptimizerStatistics
    {
        public int OptimizedPathCount { get; set; }
        public long PathOptimizationCount { get; set; }
        public long PathCacheHits { get; set; }
        public double AveragePathLength { get; set; }
        public int CachedCostCount { get; set; }
        
        public override string ToString()
        {
            return $"PathOptimizer: {OptimizedPathCount} paths, {AveragePathLength:F1} avg length, " +
                   $"{PathCacheHits} cache hits";
        }
    }
    
    /// <summary>
    /// Comparer for resolution nodes in priority queue
    /// </summary>
    public class ResolutionNodeComparer : IComparer<ResolutionPathOptimizer.ResolutionNode>
    {
        public int Compare(ResolutionPathOptimizer.ResolutionNode x, ResolutionPathOptimizer.ResolutionNode y)
        {
            var costComparison = x.Cost.CompareTo(y.Cost);
            if (costComparison != 0)
                return costComparison;
                
            var depthComparison = x.Depth.CompareTo(y.Depth);
            if (depthComparison != 0)
                return depthComparison;
                
            return x.ServiceType.GetHashCode().CompareTo(y.ServiceType.GetHashCode());
        }
    }
}