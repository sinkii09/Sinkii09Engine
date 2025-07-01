using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Sinkii09.Engine.Services.Performance
{
    /// <summary>
    /// Implements parallel initialization for independent services
    /// Target: 3x speed improvement through intelligent parallelization and dependency analysis
    /// </summary>
    public class ParallelServiceInitializer
    {
        private readonly IServiceContainer _container;
        private readonly ServiceMetadataCache _metadataCache;
        private readonly ResolutionPathOptimizer _pathOptimizer;
        private readonly ConcurrentDictionary<Type, ServiceInitializationState> _initializationStates;
        
        // Performance metrics
        private long _totalInitializations;
        private long _parallelizedInitializations;
        private long _sequentialInitializations;
        private double _averageSpeedupRatio;
        private TimeSpan _totalTimeSpent;
        
        /// <summary>
        /// Total number of service initializations performed
        /// </summary>
        public long TotalInitializations => _totalInitializations;
        
        /// <summary>
        /// Number of services initialized in parallel
        /// </summary>
        public long ParallelizedInitializations => _parallelizedInitializations;
        
        /// <summary>
        /// Average speedup ratio achieved through parallelization
        /// </summary>
        public double AverageSpeedupRatio => _averageSpeedupRatio;
        
        /// <summary>
        /// Total time spent on all initializations
        /// </summary>
        public TimeSpan TotalTimeSpent => _totalTimeSpent;
        
        public ParallelServiceInitializer(IServiceContainer container)
        {
            _container = container ?? throw new ArgumentNullException(nameof(container));
            _metadataCache = new ServiceMetadataCache();
            _pathOptimizer = new ResolutionPathOptimizer(_metadataCache);
            _initializationStates = new ConcurrentDictionary<Type, ServiceInitializationState>();
        }
        
        /// <summary>
        /// Initialize services with maximum parallelization
        /// </summary>
        public async UniTask<ParallelInitializationResult> InitializeServicesAsync(
            Type[] serviceTypes, 
            CancellationToken cancellationToken = default,
            ParallelInitializationOptions options = null)
        {
            options ??= ParallelInitializationOptions.Default();
            var stopwatch = Stopwatch.StartNew();
            
            var result = new ParallelInitializationResult
            {
                StartTime = DateTime.UtcNow,
                TotalServices = serviceTypes.Length,
                Options = options
            };
            
            try
            {
                // Reset states
                _initializationStates.Clear();
                foreach (var serviceType in serviceTypes)
                {
                    _initializationStates[serviceType] = new ServiceInitializationState(serviceType);
                }
                
                // Build dependency graph and analyze parallelization opportunities
                var dependencyAnalysis = AnalyzeDependencyParallelization(serviceTypes);
                result.DependencyAnalysis = dependencyAnalysis;
                
                // Execute parallel initialization in waves
                var initializationReport = await ExecuteParallelInitializationAsync(
                    dependencyAnalysis.ParallelGroups, 
                    cancellationToken, 
                    options);
                
                result.InitializationReport = initializationReport;
                result.Success = initializationReport.Success;
                result.InitializedServices = initializationReport.InitializedServices;
                result.FailedServices = initializationReport.FailedServices;
                
                // Calculate performance metrics
                var sequentialEstimate = EstimateSequentialTime(serviceTypes);
                var actualTime = stopwatch.Elapsed;
                result.SequentialTimeEstimate = sequentialEstimate;
                result.ActualTime = actualTime;
                result.SpeedupRatio = sequentialEstimate.TotalMilliseconds / actualTime.TotalMilliseconds;
                
                // Update global metrics
                UpdateGlobalMetrics(result);
                
                return result;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = ex.Message;
                result.ActualTime = stopwatch.Elapsed;
                Debug.LogError($"Error in parallel service initialization: {ex.Message}");
                return result;
            }
            finally
            {
                result.EndTime = DateTime.UtcNow;
                stopwatch.Stop();
            }
        }
        
        /// <summary>
        /// Analyze dependency structure for parallelization opportunities
        /// </summary>
        public DependencyParallelizationAnalysis AnalyzeDependencyParallelization(Type[] serviceTypes)
        {
            var analysis = new DependencyParallelizationAnalysis();
            var dependencyGraph = BuildServiceDependencyGraph(serviceTypes);
            
            // Group services into parallel execution waves
            analysis.ParallelGroups = GroupServicesForParallelExecution(serviceTypes, dependencyGraph);
            analysis.MaxParallelism = analysis.ParallelGroups.Max(g => g.Length);
            analysis.TotalWaves = analysis.ParallelGroups.Length;
            
            // Calculate parallelization efficiency
            var totalServices = serviceTypes.Length;
            var independentServices = analysis.ParallelGroups.Sum(g => g.Length == 1 ? 0 : g.Length);
            analysis.ParallelizationEfficiency = totalServices > 0 ? (double)independentServices / totalServices : 0;
            
            // Identify bottlenecks
            analysis.Bottlenecks = IdentifyInitializationBottlenecks(serviceTypes, dependencyGraph);
            
            // Calculate estimated speedup
            analysis.EstimatedSpeedupRatio = CalculateEstimatedSpeedup(analysis.ParallelGroups);
            
            return analysis;
        }
        
        /// <summary>
        /// Execute parallel initialization in dependency-ordered waves
        /// </summary>
        private async UniTask<ParallelInitializationReport> ExecuteParallelInitializationAsync(
            Type[][] parallelGroups,
            CancellationToken cancellationToken,
            ParallelInitializationOptions options)
        {
            var report = new ParallelInitializationReport
            {
                InitializedServices = new List<Type>(),
                FailedServices = new List<Type>(),
                WaveResults = new List<ParallelWaveResult>()
            };
            
            for (int waveIndex = 0; waveIndex < parallelGroups.Length; waveIndex++)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    report.Success = false;
                    report.ErrorMessage = "Initialization cancelled";
                    break;
                }
                
                var wave = parallelGroups[waveIndex];
                var waveResult = await ExecuteParallelWaveAsync(wave, waveIndex, cancellationToken, options);
                
                report.WaveResults.Add(waveResult);
                report.InitializedServices.AddRange(waveResult.InitializedServices);
                report.FailedServices.AddRange(waveResult.FailedServices);
                
                // Stop on failure if configured
                if (!waveResult.Success && !options.ContinueOnFailure)
                {
                    report.Success = false;
                    report.ErrorMessage = $"Wave {waveIndex} failed, stopping initialization";
                    break;
                }
            }
            
            report.Success = report.FailedServices.Count == 0;
            return report;
        }
        
        /// <summary>
        /// Execute a single wave of parallel service initialization
        /// </summary>
        private async UniTask<ParallelWaveResult> ExecuteParallelWaveAsync(
            Type[] waveServices,
            int waveIndex,
            CancellationToken cancellationToken,
            ParallelInitializationOptions options)
        {
            var waveResult = new ParallelWaveResult
            {
                WaveIndex = waveIndex,
                ServiceCount = waveServices.Length,
                StartTime = DateTime.UtcNow,
                InitializedServices = new List<Type>(),
                FailedServices = new List<Type>(),
                ServiceResults = new List<ParallelServiceInitializationResult>()
            };
            
            var stopwatch = Stopwatch.StartNew();
            
            try
            {
                // Create initialization tasks for all services in this wave
                var initializationTasks = waveServices.Select(serviceType =>
                    InitializeSingleServiceAsync(serviceType, cancellationToken, options)
                ).ToArray();
                
                // Wait for all services in this wave to complete
                var serviceResults = await UniTask.WhenAll(initializationTasks);
                
                // Process results
                foreach (var serviceResult in serviceResults)
                {
                    waveResult.ServiceResults.Add(serviceResult);
                    
                    if (serviceResult.Success)
                    {
                        waveResult.InitializedServices.Add(serviceResult.ServiceType);
                        _initializationStates[serviceResult.ServiceType].State = InitializationState.Completed;
                    }
                    else
                    {
                        waveResult.FailedServices.Add(serviceResult.ServiceType);
                        _initializationStates[serviceResult.ServiceType].State = InitializationState.Failed;
                    }
                }
                
                waveResult.Success = waveResult.FailedServices.Count == 0;
                
                // Update parallelization metrics
                if (waveServices.Length > 1)
                {
                    Interlocked.Add(ref _parallelizedInitializations, waveServices.Length);
                }
                else
                {
                    Interlocked.Increment(ref _sequentialInitializations);
                }
            }
            catch (Exception ex)
            {
                waveResult.Success = false;
                waveResult.ErrorMessage = ex.Message;
                Debug.LogError($"Error in wave {waveIndex}: {ex.Message}");
            }
            finally
            {
                stopwatch.Stop();
                waveResult.Duration = stopwatch.Elapsed;
                waveResult.EndTime = DateTime.UtcNow;
            }
            
            return waveResult;
        }
        
        /// <summary>
        /// Initialize a single service asynchronously
        /// </summary>
        private async UniTask<ParallelServiceInitializationResult> InitializeSingleServiceAsync(
            Type serviceType,
            CancellationToken cancellationToken,
            ParallelInitializationOptions options)
        {
            var result = new ParallelServiceInitializationResult
            {
                ServiceType = serviceType,
                StartTime = DateTime.UtcNow
            };
            
            var stopwatch = Stopwatch.StartNew();
            
            try
            {
                // Mark as initializing
                _initializationStates[serviceType].State = InitializationState.Initializing;
                _initializationStates[serviceType].StartTime = DateTime.UtcNow;
                
                // Resolve service from container
                if (!_container.TryResolve(serviceType, out var service))
                {
                    throw new InvalidOperationException($"Service {serviceType.Name} not found in container");
                }
                
                // Initialize if it's an IEngineService
                if (service is IEngineService engineService)
                {
                    using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    if (options.InitializationTimeout > TimeSpan.Zero)
                    {
                        timeoutCts.CancelAfter(options.InitializationTimeout);
                    }
                    
                    var initResult = await engineService.InitializeAsync(_container as IServiceProvider, timeoutCts.Token);
                    result.Success = initResult.IsSuccess;
                    
                    if (!result.Success)
                    {
                        result.ErrorMessage = initResult.ErrorMessage ?? "Initialization failed";
                    }
                }
                else
                {
                    // Non-engine services are considered successfully initialized
                    result.Success = true;
                }
                
                Interlocked.Increment(ref _totalInitializations);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                result.Success = false;
                result.ErrorMessage = "Initialization cancelled";
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = ex.Message;
                Debug.LogError($"Error initializing {serviceType.Name}: {ex.Message}");
            }
            finally
            {
                stopwatch.Stop();
                result.Duration = stopwatch.Elapsed;
                result.EndTime = DateTime.UtcNow;
                
                _initializationStates[serviceType].EndTime = DateTime.UtcNow;
                _initializationStates[serviceType].Duration = stopwatch.Elapsed;
            }
            
            return result;
        }
        
        /// <summary>
        /// Build dependency graph for services
        /// </summary>
        private Dictionary<Type, Type[]> BuildServiceDependencyGraph(Type[] serviceTypes)
        {
            var dependencyGraph = new Dictionary<Type, Type[]>();
            
            foreach (var serviceType in serviceTypes)
            {
                var metadata = _metadataCache.GetOrCreateMetadata(serviceType, _container);
                var dependencies = metadata?.Dependencies ?? Array.Empty<Type>();
                
                // Filter dependencies to only include services in our initialization set
                var relevantDependencies = dependencies.Where(d => serviceTypes.Contains(d)).ToArray();
                dependencyGraph[serviceType] = relevantDependencies;
            }
            
            return dependencyGraph;
        }
        
        /// <summary>
        /// Group services for parallel execution while respecting dependencies
        /// </summary>
        private Type[][] GroupServicesForParallelExecution(Type[] serviceTypes, Dictionary<Type, Type[]> dependencyGraph)
        {
            var groups = new List<List<Type>>();
            var remaining = new HashSet<Type>(serviceTypes);
            var completed = new HashSet<Type>();
            
            while (remaining.Count > 0)
            {
                var currentGroup = new List<Type>();
                var canExecute = new List<Type>();
                
                // Find services that can execute in parallel (no dependencies on remaining services)
                foreach (var serviceType in remaining)
                {
                    var dependencies = dependencyGraph.GetValueOrDefault(serviceType, Array.Empty<Type>());
                    var blockedDependencies = dependencies.Where(d => remaining.Contains(d) && !completed.Contains(d));
                    
                    if (!blockedDependencies.Any())
                    {
                        canExecute.Add(serviceType);
                    }
                }
                
                if (canExecute.Count > 0)
                {
                    currentGroup.AddRange(canExecute);
                    foreach (var service in canExecute)
                    {
                        remaining.Remove(service);
                        completed.Add(service);
                    }
                }
                else
                {
                    // Break potential circular dependency by taking the first remaining service
                    var first = remaining.First();
                    currentGroup.Add(first);
                    remaining.Remove(first);
                    completed.Add(first);
                    Debug.LogWarning($"Potential circular dependency detected, processing {first.Name} independently");
                }
                
                groups.Add(currentGroup);
            }
            
            return groups.Select(g => g.ToArray()).ToArray();
        }
        
        /// <summary>
        /// Identify initialization bottlenecks
        /// </summary>
        private List<Type> IdentifyInitializationBottlenecks(Type[] serviceTypes, Dictionary<Type, Type[]> dependencyGraph)
        {
            var bottlenecks = new List<Type>();
            
            // Services with many dependents are potential bottlenecks
            var dependentCount = new Dictionary<Type, int>();
            
            foreach (var kvp in dependencyGraph)
            {
                foreach (var dependency in kvp.Value)
                {
                    dependentCount[dependency] = dependentCount.GetValueOrDefault(dependency, 0) + 1;
                }
            }
            
            // Services with 3+ dependents are considered bottlenecks
            bottlenecks.AddRange(dependentCount.Where(kvp => kvp.Value >= 3).Select(kvp => kvp.Key));
            
            return bottlenecks;
        }
        
        /// <summary>
        /// Calculate estimated speedup from parallelization
        /// </summary>
        private double CalculateEstimatedSpeedup(Type[][] parallelGroups)
        {
            if (parallelGroups.Length == 0)
                return 1.0;
                
            var totalServices = parallelGroups.Sum(g => g.Length);
            var parallelWaves = parallelGroups.Length;
            
            // Amdahl's law approximation
            var parallelPortion = (double)(totalServices - parallelWaves) / totalServices;
            var sequentialPortion = 1.0 - parallelPortion;
            
            // Assume average parallelism of 3 (conservative estimate)
            var averageParallelism = Math.Min(3.0, parallelGroups.Where(g => g.Length > 1).DefaultIfEmpty(new Type[1]).Average(g => g.Length));
            
            return 1.0 / (sequentialPortion + parallelPortion / averageParallelism);
        }
        
        /// <summary>
        /// Estimate sequential initialization time
        /// </summary>
        private TimeSpan EstimateSequentialTime(Type[] serviceTypes)
        {
            // Rough estimation: 50ms per service on average
            var estimatedMs = serviceTypes.Length * 50;
            return TimeSpan.FromMilliseconds(estimatedMs);
        }
        
        /// <summary>
        /// Update global performance metrics
        /// </summary>
        private void UpdateGlobalMetrics(ParallelInitializationResult result)
        {
            // Update average speedup ratio
            var currentAverage = _averageSpeedupRatio;
            var newRatio = result.SpeedupRatio;
            
            if (_totalInitializations == 0)
            {
                _averageSpeedupRatio = newRatio;
            }
            else
            {
                _averageSpeedupRatio = (currentAverage + newRatio) / 2.0;
            }
            
            // Update total time spent
            _totalTimeSpent = _totalTimeSpent.Add(result.ActualTime);
        }
        
        /// <summary>
        /// Get parallelization statistics
        /// </summary>
        public ParallelInitializationStatistics GetStatistics()
        {
            return new ParallelInitializationStatistics
            {
                TotalInitializations = _totalInitializations,
                ParallelizedInitializations = _parallelizedInitializations,
                SequentialInitializations = _sequentialInitializations,
                AverageSpeedupRatio = _averageSpeedupRatio,
                TotalTimeSpent = _totalTimeSpent,
                ParallelizationRatio = _totalInitializations > 0 ? 
                    (double)_parallelizedInitializations / _totalInitializations : 0
            };
        }
    }
    
    /// <summary>
    /// Options for parallel initialization
    /// </summary>
    public class ParallelInitializationOptions
    {
        public TimeSpan InitializationTimeout { get; set; } = TimeSpan.FromSeconds(30);
        public bool ContinueOnFailure { get; set; } = false;
        public int MaxConcurrency { get; set; } = Environment.ProcessorCount;
        public bool EnableProgressReporting { get; set; } = true;
        
        public static ParallelInitializationOptions Default()
        {
            return new ParallelInitializationOptions();
        }
    }
    
    /// <summary>
    /// State of a service during initialization
    /// </summary>
    public class ServiceInitializationState
    {
        public Type ServiceType { get; }
        public InitializationState State { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public TimeSpan Duration { get; set; }
        
        public ServiceInitializationState(Type serviceType)
        {
            ServiceType = serviceType;
            State = InitializationState.Pending;
        }
    }
    
    /// <summary>
    /// Initialization state enumeration
    /// </summary>
    public enum InitializationState
    {
        Pending,
        Initializing,
        Completed,
        Failed
    }
    
    /// <summary>
    /// Result of dependency parallelization analysis
    /// </summary>
    public class DependencyParallelizationAnalysis
    {
        public Type[][] ParallelGroups { get; set; }
        public int MaxParallelism { get; set; }
        public int TotalWaves { get; set; }
        public double ParallelizationEfficiency { get; set; }
        public List<Type> Bottlenecks { get; set; } = new List<Type>();
        public double EstimatedSpeedupRatio { get; set; }
    }
    
    /// <summary>
    /// Result of parallel initialization
    /// </summary>
    public class ParallelInitializationResult
    {
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public TimeSpan ActualTime { get; set; }
        public TimeSpan SequentialTimeEstimate { get; set; }
        public double SpeedupRatio { get; set; }
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
        public int TotalServices { get; set; }
        public List<Type> InitializedServices { get; set; } = new List<Type>();
        public List<Type> FailedServices { get; set; } = new List<Type>();
        public DependencyParallelizationAnalysis DependencyAnalysis { get; set; }
        public ParallelInitializationReport InitializationReport { get; set; }
        public ParallelInitializationOptions Options { get; set; }
    }
    
    /// <summary>
    /// Report of parallel initialization execution
    /// </summary>
    public class ParallelInitializationReport
    {
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
        public List<Type> InitializedServices { get; set; } = new List<Type>();
        public List<Type> FailedServices { get; set; } = new List<Type>();
        public List<ParallelWaveResult> WaveResults { get; set; } = new List<ParallelWaveResult>();
    }
    
    /// <summary>
    /// Result of a single parallel wave
    /// </summary>
    public class ParallelWaveResult
    {
        public int WaveIndex { get; set; }
        public int ServiceCount { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public TimeSpan Duration { get; set; }
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
        public List<Type> InitializedServices { get; set; } = new List<Type>();
        public List<Type> FailedServices { get; set; } = new List<Type>();
        public List<ParallelServiceInitializationResult> ServiceResults { get; set; } = new List<ParallelServiceInitializationResult>();
    }
    
    /// <summary>
    /// Result of single service initialization for parallel processing
    /// </summary>
    public class ParallelServiceInitializationResult
    {
        public Type ServiceType { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public TimeSpan Duration { get; set; }
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
    }
    
    /// <summary>
    /// Statistics for parallel initialization
    /// </summary>
    public struct ParallelInitializationStatistics
    {
        public long TotalInitializations { get; set; }
        public long ParallelizedInitializations { get; set; }
        public long SequentialInitializations { get; set; }
        public double AverageSpeedupRatio { get; set; }
        public TimeSpan TotalTimeSpent { get; set; }
        public double ParallelizationRatio { get; set; }
        
        public override string ToString()
        {
            return $"ParallelInit: {AverageSpeedupRatio:F1}x speedup, {ParallelizationRatio:P1} parallelized, " +
                   $"{TotalTimeSpent.TotalMilliseconds:F0}ms total time";
        }
    }
}