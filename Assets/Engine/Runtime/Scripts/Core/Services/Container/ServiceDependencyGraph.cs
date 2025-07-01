using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Sinkii09.Engine.Services
{
    /// <summary>
    /// Represents a dependency graph for services, enabling visualization and analysis
    /// </summary>
    public class ServiceDependencyGraph
    {
        /// <summary>
        /// Node representing a service in the dependency graph
        /// </summary>
        public class ServiceNode
        {
            public Type ServiceType { get; set; }
            public ServiceRegistration Registration { get; set; }
            public List<ServiceNode> Dependencies { get; set; }
            public List<ServiceNode> Dependents { get; set; }
            public int Depth { get; set; }
            public bool Visited { get; set; }
            public bool InStack { get; set; }
            
            public ServiceNode(Type serviceType, ServiceRegistration registration)
            {
                ServiceType = serviceType;
                Registration = registration;
                Dependencies = new List<ServiceNode>();
                Dependents = new List<ServiceNode>();
                Depth = -1;
            }
        }
        
        private readonly Dictionary<Type, ServiceNode> _nodes;
        private readonly List<List<Type>> _circularDependencies;
        
        public IReadOnlyDictionary<Type, ServiceNode> Nodes => _nodes;
        public IReadOnlyList<List<Type>> CircularDependencies => _circularDependencies;
        public bool HasCircularDependencies => _circularDependencies.Count > 0;
        
        public ServiceDependencyGraph()
        {
            _nodes = new Dictionary<Type, ServiceNode>();
            _circularDependencies = new List<List<Type>>();
        }
        
        /// <summary>
        /// Build the dependency graph from service registrations
        /// </summary>
        public void Build(IDictionary<Type, ServiceRegistration> registrations)
        {
            _nodes.Clear();
            _circularDependencies.Clear();
            
            // Infrastructure types to exclude from dependency analysis
            var infrastructureTypes = new HashSet<Type>
            {
                typeof(IServiceProvider),
                typeof(IServiceContainer),
                typeof(ServiceContainer)
            };
            
            // Create nodes (excluding infrastructure types)
            foreach (var kvp in registrations)
            {
                // Skip infrastructure types to avoid self-referential issues
                if (infrastructureTypes.Contains(kvp.Key))
                {
                    Debug.Log($"Skipping infrastructure type {kvp.Key.Name} from dependency graph");
                    continue;
                }
                
                _nodes[kvp.Key] = new ServiceNode(kvp.Key, kvp.Value);
            }
            
            // Build edges
            foreach (var node in _nodes.Values)
            {
                // Add required dependencies
                foreach (var depType in node.Registration.RequiredDependencies)
                {
                    // Skip infrastructure dependencies
                    if (infrastructureTypes.Contains(depType))
                        continue;
                        
                    if (_nodes.TryGetValue(depType, out var depNode))
                    {
                        node.Dependencies.Add(depNode);
                        depNode.Dependents.Add(node);
                    }
                }
                
                // Add optional dependencies if they exist
                foreach (var depType in node.Registration.OptionalDependencies)
                {
                    // Skip infrastructure dependencies
                    if (infrastructureTypes.Contains(depType))
                        continue;
                        
                    if (_nodes.TryGetValue(depType, out var depNode))
                    {
                        node.Dependencies.Add(depNode);
                        depNode.Dependents.Add(node);
                    }
                }
            }
            
            // Detect circular dependencies
            DetectCircularDependencies();
            
            // Only calculate depths if no circular dependencies
            if (!HasCircularDependencies)
            {
                CalculateDepths();
            }
            else
            {
                Debug.LogWarning($"Skipping depth calculation due to {_circularDependencies.Count} circular dependencies");
                // Set all depths to 0 as a fallback
                foreach (var node in _nodes.Values)
                {
                    node.Depth = 0;
                }
            }
        }
        
        /// <summary>
        /// Get services in initialization order (topological sort)
        /// </summary>
        public List<Type> GetInitializationOrder()
        {
            if (HasCircularDependencies)
            {
                throw new InvalidOperationException("Cannot determine initialization order due to circular dependencies");
            }
            
            var sorted = new List<Type>();
            var visited = new HashSet<Type>();
            var stack = new HashSet<Type>();
            
            foreach (var node in _nodes.Values)
            {
                if (!visited.Contains(node.ServiceType))
                {
                    TopologicalSort(node, visited, stack, sorted);
                }
            }
            
            sorted.Reverse();
            return sorted;
        }
        
        /// <summary>
        /// Get all dependencies of a service (including transitive)
        /// </summary>
        public HashSet<Type> GetAllDependencies(Type serviceType)
        {
            var dependencies = new HashSet<Type>();
            
            if (_nodes.TryGetValue(serviceType, out var node))
            {
                CollectDependencies(node, dependencies);
            }
            
            return dependencies;
        }
        
        /// <summary>
        /// Get all dependents of a service (services that depend on this)
        /// </summary>
        public HashSet<Type> GetAllDependents(Type serviceType)
        {
            var dependents = new HashSet<Type>();
            
            if (_nodes.TryGetValue(serviceType, out var node))
            {
                CollectDependents(node, dependents);
            }
            
            return dependents;
        }
        
        /// <summary>
        /// Generate a visual representation of the dependency graph
        /// </summary>
        public string GenerateVisualization()
        {
            var sb = new StringBuilder();
            sb.AppendLine("Service Dependency Graph:");
            sb.AppendLine("========================");
            
            // Group by depth
            var depthGroups = _nodes.Values
                .GroupBy(n => n.Depth)
                .OrderBy(g => g.Key);
            
            foreach (var group in depthGroups)
            {
                sb.AppendLine($"\nDepth {group.Key}:");
                foreach (var node in group.OrderBy(n => n.ServiceType.Name))
                {
                    sb.AppendLine($"  {node.ServiceType.Name}");
                    if (node.Dependencies.Count > 0)
                    {
                        sb.AppendLine($"    Dependencies: {string.Join(", ", node.Dependencies.Select(d => d.ServiceType.Name))}");
                    }
                }
            }
            
            if (HasCircularDependencies)
            {
                sb.AppendLine("\nCircular Dependencies Detected:");
                foreach (var cycle in _circularDependencies)
                {
                    sb.AppendLine($"  {string.Join(" -> ", cycle.Select(t => t.Name))} -> {cycle[0].Name}");
                }
            }
            
            return sb.ToString();
        }
        
        /// <summary>
        /// Generate a report of the dependency analysis
        /// </summary>
        public ServiceDependencyReport GenerateReport()
        {
            return new ServiceDependencyReport
            {
                TotalServices = _nodes.Count,
                MaxDepth = _nodes.Values.Any() ? _nodes.Values.Max(n => n.Depth) : 0,
                CircularDependencies = _circularDependencies.Select(c => c.ToList()).ToList(),
                ServicesWithNoDependencies = _nodes.Values.Count(n => n.Dependencies.Count == 0),
                ServicesWithNoDependents = _nodes.Values.Count(n => n.Dependents.Count == 0),
                AverageDependencies = _nodes.Values.Any() ? _nodes.Values.Average(n => n.Dependencies.Count) : 0
            };
        }
        
        private void DetectCircularDependencies()
        {
            var visited = new HashSet<Type>();
            var stack = new List<Type>();
            
            foreach (var node in _nodes.Values)
            {
                if (!visited.Contains(node.ServiceType))
                {
                    DetectCycles(node, visited, stack);
                }
            }
        }
        
        private bool DetectCycles(ServiceNode node, HashSet<Type> visited, List<Type> stack)
        {
            visited.Add(node.ServiceType);
            stack.Add(node.ServiceType);
            node.InStack = true;
            
            foreach (var dep in node.Dependencies)
            {
                if (!visited.Contains(dep.ServiceType))
                {
                    if (DetectCycles(dep, visited, stack))
                    {
                        return true;
                    }
                }
                else if (dep.InStack)
                {
                    // Found a cycle
                    var cycleStart = stack.IndexOf(dep.ServiceType);
                    var cycle = stack.Skip(cycleStart).ToList();
                    _circularDependencies.Add(cycle);
                }
            }
            
            stack.Remove(node.ServiceType);
            node.InStack = false;
            return false;
        }
        
        private void CalculateDepths()
        {
            foreach (var node in _nodes.Values)
            {
                if (node.Depth == -1)
                {
                    CalculateDepth(node, null);
                }
            }
        }
        
        private int CalculateDepth(ServiceNode node, HashSet<Type> currentPath = null)
        {
            if (node.Depth != -1)
                return node.Depth;
            
            // Initialize path tracking for cycle detection
            if (currentPath == null)
                currentPath = new HashSet<Type>();
            
            // Check for cycles during depth calculation
            if (currentPath.Contains(node.ServiceType))
            {
                Debug.LogError($"Circular dependency detected during depth calculation for {node.ServiceType.Name}");
                // Return a safe depth to break the cycle
                node.Depth = 0;
                return 0;
            }
            
            if (node.Dependencies.Count == 0)
            {
                node.Depth = 0;
                return 0;
            }
            
            currentPath.Add(node.ServiceType);
            try
            {
                var maxDepth = 0;
                foreach (var dep in node.Dependencies)
                {
                    var depthValue = CalculateDepth(dep, currentPath);
                    maxDepth = Math.Max(maxDepth, depthValue);
                }
                
                node.Depth = maxDepth + 1;
                return node.Depth;
            }
            finally
            {
                currentPath.Remove(node.ServiceType);
            }
        }
        
        private void TopologicalSort(ServiceNode node, HashSet<Type> visited, HashSet<Type> stack, List<Type> sorted)
        {
            visited.Add(node.ServiceType);
            stack.Add(node.ServiceType);
            
            foreach (var dep in node.Dependencies)
            {
                if (!visited.Contains(dep.ServiceType))
                {
                    TopologicalSort(dep, visited, stack, sorted);
                }
                else if (stack.Contains(dep.ServiceType))
                {
                    throw new InvalidOperationException($"Circular dependency detected: {node.ServiceType.Name} -> {dep.ServiceType.Name}");
                }
            }
            
            stack.Remove(node.ServiceType);
            sorted.Add(node.ServiceType);
        }
        
        private void CollectDependencies(ServiceNode node, HashSet<Type> dependencies)
        {
            foreach (var dep in node.Dependencies)
            {
                if (dependencies.Add(dep.ServiceType))
                {
                    CollectDependencies(dep, dependencies);
                }
            }
        }
        
        private void CollectDependents(ServiceNode node, HashSet<Type> dependents)
        {
            foreach (var dep in node.Dependents)
            {
                if (dependents.Add(dep.ServiceType))
                {
                    CollectDependents(dep, dependents);
                }
            }
        }
    }
    
    /// <summary>
    /// Report containing dependency analysis results
    /// </summary>
    public class ServiceDependencyReport
    {
        public int TotalServices { get; set; }
        public int MaxDepth { get; set; }
        public List<List<Type>> CircularDependencies { get; set; }
        public int ServicesWithNoDependencies { get; set; }
        public int ServicesWithNoDependents { get; set; }
        public double AverageDependencies { get; set; }
        
        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.AppendLine("Service Dependency Analysis Report");
            sb.AppendLine("=================================");
            sb.AppendLine($"Total Services: {TotalServices}");
            sb.AppendLine($"Maximum Dependency Depth: {MaxDepth}");
            sb.AppendLine($"Services with No Dependencies: {ServicesWithNoDependencies}");
            sb.AppendLine($"Services with No Dependents: {ServicesWithNoDependents}");
            sb.AppendLine($"Average Dependencies per Service: {AverageDependencies:F2}");
            
            if (CircularDependencies.Count > 0)
            {
                sb.AppendLine($"\nCircular Dependencies Found: {CircularDependencies.Count}");
                foreach (var cycle in CircularDependencies)
                {
                    sb.AppendLine($"  {string.Join(" -> ", cycle.Select(t => t.Name))}");
                }
            }
            else
            {
                sb.AppendLine("\nNo circular dependencies detected.");
            }
            
            return sb.ToString();
        }
    }
}