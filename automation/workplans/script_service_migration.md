---
milestone: "Enhanced Service Architecture"
default_labels: ["enhancement", "core", "service", "migration"]
priority: "high"
---

# Epic: ScriptService Enhanced Migration Implementation

**Description**: Complete migration of ScriptService from legacy commented code to modern enhanced service architecture with dependency injection, async operations, configuration system, hot-reload support, and comprehensive testing infrastructure.

**Priority**: High
**Estimated Effort**: 5 days
**Current Progress**: 0% Complete (Planning phase)
**Milestone**: Enhanced Service Architecture  
**Team Size**: 1-2 developers
**Timeline**: 5 days sequential implementation

## Current State Analysis
- ✅ ScriptService entirely commented out (legacy code in lines 9-78)
- ✅ Script domain model preserved (Script.cs, ScriptLine.cs)
- ✅ Enhanced ResourceService available as dependency
- ✅ Service Container with DI ready
- ✅ Configuration system infrastructure complete
- ⚠️ No active ScriptService implementation
- ⚠️ ScriptsConfig needs migration to new system

## Enhancement Goals
1. **Modern Service Architecture**: Full EngineService attributes, async lifecycle, dependency injection
2. **Configuration System**: Comprehensive ScriptServiceConfiguration with validation
3. **Hot-Reload Support**: Script change detection, validation, and automatic reloading
4. **Performance Optimization**: Caching, preloading, batch operations, memory management
5. **Error Handling**: Retry policies, validation, comprehensive error reporting
6. **Testing Infrastructure**: 95%+ coverage with unit, integration, and performance tests

## Technical Specifications Summary
- **Performance Targets**: 25% faster script loading vs legacy, <100ms hot-reload time
- **Reliability Targets**: 99.9% uptime, automatic error recovery, memory leak prevention
- **Features**: Async operations, caching, hot-reload, batch loading, memory management
- **Integration**: Full ServiceContainer integration, ResourceService dependency

## Issues

### Issue 1: Configuration System Implementation

**Description**: Design and implement comprehensive ScriptServiceConfiguration system with validation, hot-reload support, and integration with existing configuration infrastructure.
**Labels**: task, configuration, design, phase-1
**Estimated Effort**: 0.5 days
**Priority**: Critical

#### Implementation Tasks

**Task 1.1: ScriptServiceConfiguration Creation (2 hours)**
- [ ] Create ScriptServiceConfiguration.cs extending ServiceConfigurationBase
- [ ] Define configuration sections:
  - Loading settings (DefaultScriptsPath, MaxConcurrentLoads, EnableScriptCaching, MaxCacheSize)
  - Performance settings (EnablePreloading, PreloadPaths, ScriptValidationTimeout)
  - Hot-Reload settings (EnableHotReload, HotReloadCheckInterval)
  - Error Handling settings (MaxRetryAttempts, RetryDelaySeconds, EnableScriptValidation)
- [ ] Implement comprehensive Validate() method with business rule validation
- [ ] Add [CreateAssetMenu] attribute for Unity Inspector creation
- [ ] Create default configuration asset in Resources/Configs/Services/

**Task 1.2: Configuration Integration (1 hour)**
- [ ] Update existing ScriptsConfig to work with new configuration system
- [ ] Ensure backward compatibility with existing ScriptsConfig assets
- [ ] Add configuration migration utilities if needed
- [ ] Test configuration loading and validation pipeline

#### Acceptance Criteria
- [ ] ScriptServiceConfiguration properly validates all settings
- [ ] Configuration integrates with ServiceConfigurationBase infrastructure
- [ ] Default configuration asset created and functional
- [ ] Backward compatibility maintained with existing assets
- [ ] Configuration validation provides clear error messages

#### Deliverables
- [ ] **ScriptServiceConfiguration.cs** - Complete configuration class with validation
- [ ] **Default configuration asset** - Functional default settings
- [ ] **Configuration validation tests** - Unit tests for all validation scenarios
- [ ] **Migration utilities** - Tools for updating existing configurations

### Issue 2: Enhanced Interface Design

**Description**: Design modern IScriptService interface with async operations, caching, hot-reload support, and comprehensive event system following enhanced service architecture patterns.
**Labels**: task, interface, design, phase-2
**Estimated Effort**: 0.5 days
**Priority**: Critical

#### Interface Design Tasks

**Task 2.1: IScriptService Interface Design (2 hours)**
- [ ] Define core async operations:
  - UniTask<Script> LoadScriptAsync(string name, CancellationToken cancellationToken = default)
  - UniTask<IEnumerable<Script>> LoadScriptsAsync(string[] names, CancellationToken cancellationToken = default)
  - UniTask<bool> ScriptExistsAsync(string name, CancellationToken cancellationToken = default)
- [ ] Add cache management methods:
  - bool IsScriptLoaded(string name), bool IsScriptLoading(string name)
  - Script GetLoadedScriptOrNull(string name), void UnloadScript(string name)
  - UniTask UnloadAllScriptsAsync()
- [ ] Add hot-reload support:
  - UniTask<bool> ValidateScriptAsync(string name, CancellationToken cancellationToken = default)
  - UniTask ReloadScriptAsync(string name, CancellationToken cancellationToken = default)
- [ ] Define comprehensive event system:
  - event Action<string> ScriptLoadStarted, ScriptLoadCompleted, ScriptLoadFailed, ScriptReloaded

**Task 2.2: Supporting Types Creation (1 hour)**
- [ ] Create ScriptLoadResult for detailed load operation results
- [ ] Define ScriptValidationResult for script validation outcomes
- [ ] Add ScriptServiceStatistics for monitoring and reporting
- [ ] Create ScriptCacheInfo for cache management and statistics
- [ ] Define ScriptServiceEvents for event handling infrastructure

#### Acceptance Criteria
- [ ] Interface extends IEngineService with proper async lifecycle methods
- [ ] All operations support CancellationToken for proper async cancellation
- [ ] Interface provides comprehensive cache management capabilities
- [ ] Hot-reload functionality properly defined with validation support
- [ ] Event system covers all major script service operations
- [ ] Supporting types provide rich information for monitoring and debugging

#### Deliverables
- [ ] **IScriptService.cs** - Complete modern interface definition
- [ ] **ScriptLoadResult.cs** - Rich result type for load operations
- [ ] **ScriptValidationResult.cs** - Detailed validation result information
- [ ] **ScriptServiceStatistics.cs** - Comprehensive monitoring data structure
- [ ] **Interface documentation** - Complete API documentation with examples

### Issue 3: Core Service Implementation

**Description**: Implement complete ScriptService with enhanced architecture, dependency injection, async lifecycle management, script loading, caching, and memory management integration.
**Labels**: task, implementation, core, phase-3
**Estimated Effort**: 2 days
**Priority**: Critical

#### Core Implementation Tasks

**Task 3.1: Service Structure Setup (2 hours)**
- [ ] Create ScriptService class with proper attributes:
  - [EngineService(ServiceCategory.Core, ServicePriority.High, Description = "...")]
  - [ServiceConfiguration(typeof(ScriptServiceConfiguration))]
- [ ] Implement constructor with dependency injection:
  - ScriptServiceConfiguration config, IResourceService resourceService, IServiceProvider serviceProvider
- [ ] Set up internal data structures:
  - ConcurrentDictionary<string, Script> _scriptCache
  - ConcurrentDictionary<string, UniTask<Script>> _loadingTasks
  - SemaphoreSlim _concurrentLoadSemaphore
  - FileSystemWatcher _hotReloadWatcher (if hot-reload enabled)

**Task 3.2: Async Lifecycle Implementation (2 hours)**
- [ ] Implement InitializeAsync(IServiceProvider provider, CancellationToken cancellationToken):
  - Validate ResourceService dependency availability
  - Initialize script cache and loading task management
  - Set up hot-reload file watcher if enabled
  - Perform script preloading based on configuration
  - Return ServiceInitializationResult with success/failure details
- [ ] Implement HealthCheckAsync():
  - Validate script cache integrity and memory usage
  - Check ResourceService dependency health
  - Validate hot-reload watcher status if enabled
  - Return ServiceHealthResult with comprehensive status
- [ ] Implement ShutdownAsync(CancellationToken cancellationToken):
  - Cancel all pending loading operations gracefully
  - Dispose file system watcher if active
  - Clear script cache and release memory
  - Return ServiceShutdownResult with cleanup status

**Task 3.3: Script Loading Core Implementation (3 hours)**
- [ ] Implement LoadScriptAsync<T> with comprehensive features:
  - Check cache first for already loaded scripts
  - Implement loading task deduplication (prevent concurrent loads of same script)
  - Use semaphore to limit concurrent loading operations
  - Integrate with ResourceService for actual file loading
  - Add retry policies for failed loads with exponential backoff
  - Update cache and fire appropriate events
  - Handle cancellation token properly throughout operation
- [ ] Implement LoadScriptsAsync for batch operations:
  - Optimize batch loading with parallel operations
  - Respect concurrent load limits from configuration
  - Provide progress reporting for large batch operations
  - Handle partial failures gracefully
- [ ] Add comprehensive error handling and logging:
  - Classify errors (transient, recoverable, fatal)
  - Log detailed error information with correlation IDs
  - Fire ScriptLoadFailed events with exception details
  - Implement fallback mechanisms for critical scripts

**Task 3.4: Cache Management Implementation (1 hour)**
- [ ] Implement intelligent caching with LRU eviction policy
- [ ] Add memory pressure response integration with ServiceContainer
- [ ] Implement cache statistics collection and monitoring
- [ ] Add cache warming for frequently used scripts during initialization
- [ ] Implement UnloadScript and UnloadAllScriptsAsync with proper cleanup

#### Performance Requirements
- Script loading: 25% faster than legacy implementation
- Memory usage: Efficient caching with automatic cleanup under pressure
- Concurrent operations: Support configured MaxConcurrentLoads without deadlocks
- Cache hit ratio: >90% for frequently accessed scripts

#### Acceptance Criteria
- [ ] Service properly registered with EngineService attributes
- [ ] Constructor injection works with all required dependencies
- [ ] Async lifecycle methods implemented with proper error handling
- [ ] Script loading supports caching, deduplication, and retry policies
- [ ] Cache management includes LRU eviction and memory pressure response
- [ ] All operations properly support cancellation tokens
- [ ] Comprehensive logging and event firing throughout operations
- [ ] Thread-safe operations under concurrent access

#### Deliverables
- [ ] **ScriptService.cs** - Complete service implementation with all features
- [ ] **Script loading pipeline** - Async loading with caching and retry logic
- [ ] **Cache management system** - LRU caching with memory pressure integration
- [ ] **Error handling infrastructure** - Classification, logging, and recovery
- [ ] **Performance optimization** - Concurrent loading and cache warming

### Issue 4: Advanced Features Implementation

**Description**: Implement advanced ScriptService features including hot-reload system, performance optimizations, memory management integration, and comprehensive error handling.
**Labels**: task, implementation, advanced, phase-4
**Estimated Effort**: 1 day
**Priority**: High

#### Advanced Features Tasks

**Task 4.1: Hot-Reload System Implementation (3 hours)**
- [ ] Implement script change detection using FileSystemWatcher:
  - Monitor configured script directories for file changes
  - Filter changes to relevant script file extensions
  - Debounce rapid file system events to prevent thrashing
  - Handle file system watcher disposal and error recovery
- [ ] Add script validation pipeline:
  - Implement ValidateScriptAsync with syntax checking
  - Parse script content and validate against domain rules
  - Check for syntax errors and structural issues
  - Provide detailed validation results with error locations
- [ ] Implement ReloadScriptAsync with dependency management:
  - Validate script before reloading to prevent invalid scripts
  - Update cache with new script content atomically
  - Handle script dependencies and cascading updates
  - Fire ScriptReloaded events with change notifications
  - Support rollback if reload fails validation

**Task 4.2: Performance Optimization Implementation (2 hours)**
- [ ] Implement script preloading based on configuration:
  - Load scripts from PreloadPaths during initialization
  - Support pattern-based preloading (e.g., "Core/*", "Boot/*")
  - Implement background preloading to avoid blocking initialization
  - Provide preloading progress reporting and metrics
- [ ] Add batch loading optimization for multiple scripts:
  - Group related scripts for efficient loading
  - Implement parallel loading with proper concurrency limits
  - Optimize resource requests to ResourceService
  - Cache loading results to avoid duplicate work
- [ ] Optimize script parsing and validation:
  - Cache parsing results for unchanged scripts
  - Implement lazy parsing for non-critical scripts
  - Optimize Script.FromScriptText performance
  - Add script parsing metrics and monitoring

**Task 4.3: Memory Management Integration (2 hours)**
- [ ] Implement IMemoryPressureResponder interface:
  - Register with ServiceContainer memory monitoring
  - Implement OnMemoryPressure callback with cleanup logic
  - Define memory pressure thresholds and response strategies
  - Provide memory usage reporting and statistics
- [ ] Add automatic cleanup policies:
  - Implement LRU-based cache eviction under memory pressure
  - Add configurable cache size limits and cleanup thresholds
  - Implement script reference counting for safe unloading
  - Support manual memory cleanup via ForceMemoryCleanupAsync
- [ ] Implement resource usage monitoring:
  - Track memory usage of cached scripts
  - Monitor loading operation resource consumption
  - Provide detailed memory statistics and reporting
  - Add memory leak detection and prevention

**Task 4.4: Error Handling Integration (1 hour)**
- [ ] Add comprehensive error classification system:
  - Classify script loading errors (file not found, parsing errors, validation failures)
  - Distinguish between transient and permanent failures
  - Provide detailed error context and recovery suggestions
- [ ] Implement retry policies for transient failures:
  - Exponential backoff with jitter for network-related failures
  - Configurable retry attempts and delay settings
  - Skip retries for permanent failures (syntax errors, missing files)
- [ ] Add detailed error reporting and metrics:
  - Track error rates and patterns for monitoring
  - Provide error correlation IDs for debugging
  - Log comprehensive error information for troubleshooting

#### Performance Targets
- Hot-reload time: <100ms for script change detection and reload
- Memory cleanup: <50ms response time to memory pressure events
- Preloading: Complete within initialization timeout without blocking
- Error recovery: <1s average time for retry policy execution

#### Acceptance Criteria
- [ ] Hot-reload system detects file changes and validates scripts before reloading
- [ ] Performance optimizations achieve 25% improvement over legacy implementation
- [ ] Memory management integrates with ServiceContainer and responds to pressure
- [ ] Error handling provides comprehensive classification and retry logic
- [ ] All advanced features configurable through ScriptServiceConfiguration
- [ ] Features work reliably under concurrent access and stress conditions

#### Deliverables
- [ ] **Hot-reload system** - File watching, validation, and atomic reloading
- [ ] **Performance optimizations** - Preloading, batch operations, parsing optimization
- [ ] **Memory management integration** - Pressure response and automatic cleanup
- [ ] **Enhanced error handling** - Classification, retry policies, detailed reporting

### Issue 5: Testing Infrastructure

**Description**: Create comprehensive testing infrastructure for ScriptService including unit tests, integration tests, performance tests, and mock infrastructure with 95%+ code coverage.
**Labels**: task, testing, validation, phase-5
**Estimated Effort**: 1 day
**Priority**: High

#### Testing Infrastructure Tasks

**Task 5.1: Unit Tests Implementation (3 hours)**
- [ ] **Script Loading Tests** (15 test cases):
  - Test successful script loading with caching
  - Test concurrent script loading with deduplication
  - Test script loading with cancellation token support
  - Test script loading retry logic for transient failures
  - Test script loading error handling for permanent failures
- [ ] **Configuration System Tests** (10 test cases):
  - Test ScriptServiceConfiguration validation rules
  - Test configuration loading and default value handling
  - Test configuration validation error reporting
  - Test configuration hot-reload and change detection
- [ ] **Cache Management Tests** (15 test cases):
  - Test script caching and retrieval logic
  - Test LRU eviction under memory pressure
  - Test cache statistics and monitoring
  - Test cache warming during initialization
  - Test script unloading and cache cleanup
- [ ] **Hot-Reload Tests** (10 test cases):
  - Test script change detection and validation
  - Test script reloading with dependency updates
  - Test hot-reload error handling and rollback
  - Test hot-reload event firing and notifications

**Task 5.2: Integration Tests Implementation (2 hours)**
- [ ] **ResourceService Integration Tests** (8 test cases):
  - Test ScriptService dependency injection with ResourceService
  - Test script loading through ResourceService integration
  - Test error propagation from ResourceService failures
  - Test resource cleanup coordination between services
- [ ] **ServiceContainer Integration Tests** (6 test cases):
  - Test ScriptService registration and lifecycle management
  - Test dependency resolution through ServiceContainer
  - Test service initialization order and dependencies
  - Test service health monitoring and reporting
- [ ] **Configuration Integration Tests** (6 test cases):
  - Test configuration loading through ServiceContainer
  - Test configuration validation during service initialization
  - Test configuration hot-reload with running service
  - Test configuration error handling and service recovery

**Task 5.3: Performance Tests Implementation (2 hours)**
- [ ] **Loading Performance Tests** (5 benchmarks):
  - Benchmark script loading time vs legacy implementation (target: 25% improvement)
  - Benchmark concurrent loading scalability (target: handle MaxConcurrentLoads)
  - Benchmark cache hit ratio under realistic usage patterns (target: >90%)
  - Benchmark memory usage during intensive loading operations
  - Benchmark hot-reload performance (target: <100ms reload time)
- [ ] **Memory Management Tests** (5 test cases):
  - Test memory pressure response and cleanup effectiveness
  - Test memory leak prevention during repeated load/unload cycles
  - Test cache size management and LRU eviction efficiency
  - Test memory usage monitoring and reporting accuracy
  - Test resource disposal and cleanup completeness

**Task 5.4: Mock Infrastructure Implementation (1 hour)**
- [ ] **MockScriptService Creation**:
  - Create configurable mock service for testing consumers
  - Support script loading simulation with configurable delays
  - Support error injection for testing error handling scenarios
  - Provide event simulation for testing event handling logic
- [ ] **Test Utilities and Helpers**:
  - Create script test asset generation utilities
  - Add script validation test helpers
  - Create performance testing framework integration
  - Add test configuration asset creation utilities

#### Test Coverage Requirements
- **Overall Coverage**: 95%+ code coverage across all ScriptService components
- **Unit Test Coverage**: 100% of public API methods and critical internal logic
- **Integration Coverage**: All major integration points and dependency scenarios
- **Performance Coverage**: All performance-critical paths and optimization features

#### Acceptance Criteria
- [ ] All unit tests pass with 95%+ code coverage
- [ ] Integration tests validate all major service interactions
- [ ] Performance tests confirm 25% improvement over legacy implementation
- [ ] Mock infrastructure supports comprehensive testing scenarios
- [ ] Test suite runs reliably in automated CI/CD pipeline
- [ ] Test documentation provides clear guidance for adding new tests

#### Deliverables
- [ ] **ScriptServiceTests.cs** - Comprehensive unit test suite (40+ tests)
- [ ] **ScriptServiceIntegrationTests.cs** - Service integration validation (20+ tests)
- [ ] **ScriptServicePerformanceTests.cs** - Performance benchmark suite (10+ benchmarks)
- [ ] **MockScriptService.cs** - Configurable mock service for testing
- [ ] **Script test utilities** - Test helpers and asset generation tools

### Issue 6: Integration and Cleanup

**Description**: Complete ScriptService integration with enhanced service architecture, remove legacy code, create documentation, and perform final validation with comprehensive testing.
**Labels**: task, integration, cleanup, phase-6
**Estimated Effort**: 1 day
**Priority**: High

#### Integration and Cleanup Tasks

**Task 6.1: Service Registration Integration (1 hour)**
- [ ] Register ScriptService in ServiceContainer auto-registration:
  - Verify EngineService attribute triggers automatic registration
  - Test service discovery through reflection-based registration
  - Validate dependency injection chain with ResourceService
  - Confirm service initialization order in ServiceLifecycleManager
- [ ] Test service lifecycle integration:
  - Verify service initializes properly during engine startup
  - Test service health monitoring and reporting
  - Validate service shutdown and cleanup procedures
  - Test service restart and recovery scenarios

**Task 6.2: Legacy Code Removal and Cleanup (2 hours)**
- [ ] Remove all commented legacy code from ScriptService.cs (lines 9-78):
  - Delete commented IScriptService interface
  - Delete commented ScriptService implementation
  - Clean up unused using statements and imports
  - Verify no external dependencies on commented code
- [ ] Update file structure and organization:
  - Organize new ScriptService files in proper directory structure
  - Update assembly definitions if needed
  - Clean up any temporary or development files
  - Ensure consistent naming conventions and file organization
- [ ] Verify no breaking changes to Script domain model:
  - Confirm Script.cs, ScriptLine.cs remain unchanged
  - Verify script parsing and creation logic preserved
  - Test script serialization and deserialization compatibility
  - Validate existing script assets continue to work

**Task 6.3: Documentation and Examples Creation (2 hours)**
- [ ] Create comprehensive API documentation:
  - Document all IScriptService interface methods with examples
  - Document ScriptServiceConfiguration options and validation rules
  - Provide hot-reload setup and usage guidelines
  - Document performance optimization best practices
- [ ] Add usage examples and integration guides:
  - Create script loading examples for common scenarios
  - Document dependency injection setup and configuration
  - Provide hot-reload configuration examples
  - Add troubleshooting guide for common issues
- [ ] Create migration guide from legacy usage:
  - Document differences between legacy and enhanced ScriptService
  - Provide migration steps for existing script consumers
  - Document configuration migration from ScriptsConfig
  - Add compatibility notes and breaking changes

**Task 6.4: Final Validation and Testing (3 hours)**
- [ ] Run complete test suite and verify results:
  - Execute all unit tests and confirm 95%+ coverage
  - Run integration tests and validate service interactions
  - Perform performance tests and confirm improvement targets
  - Execute stress tests and validate stability under load
- [ ] Perform end-to-end integration testing:
  - Test complete engine initialization with ScriptService
  - Validate script loading through Engine.GetService<IScriptService>()
  - Test hot-reload functionality in realistic scenarios
  - Verify memory management and cleanup under various conditions
- [ ] Validate performance targets and requirements:
  - Confirm 25% performance improvement over legacy implementation
  - Validate hot-reload time meets <100ms target
  - Test memory usage and cleanup effectiveness
  - Confirm concurrent loading scalability meets requirements
- [ ] Conduct final code review and cleanup:
  - Review all new code for consistency and quality
  - Ensure proper error handling and logging throughout
  - Verify thread safety and concurrent access handling
  - Clean up any remaining TODO comments or development artifacts

#### Final Acceptance Criteria
- [ ] ScriptService fully integrated with enhanced service architecture
- [ ] All legacy code removed without breaking existing functionality
- [ ] Complete documentation and examples available
- [ ] All tests pass with required coverage and performance targets
- [ ] Service works reliably under production-like conditions
- [ ] Code review completed with all issues addressed

#### Deliverables
- [ ] **Fully integrated ScriptService** - Complete enhanced service implementation
- [ ] **Clean codebase** - All legacy code removed and organized
- [ ] **Comprehensive documentation** - API docs, examples, and migration guides
- [ ] **Validated implementation** - All tests passing with performance targets met
- [ ] **Production-ready service** - Reliable operation under realistic conditions

## Epic Success Criteria
- [ ] All 6 issues completed successfully with acceptance criteria met
- [ ] ScriptService achieves 25% performance improvement over legacy implementation
- [ ] Hot-reload functionality operational with <100ms update time
- [ ] Service fully integrated with enhanced architecture (EngineService attributes, DI, async lifecycle)
- [ ] Test suite achieves 95%+ coverage with all tests passing
- [ ] Memory management effective with automatic cleanup under pressure
- [ ] Zero breaking changes to Script domain model or existing script assets
- [ ] Complete legacy code removal without functionality loss

## Epic Deliverables Summary
- [ ] **Enhanced ScriptService Implementation** - Complete modern service with all features
- [ ] **ScriptServiceConfiguration** - Comprehensive configuration system with validation
- [ ] **Hot-Reload System** - File watching, validation, and automatic reloading
- [ ] **Performance Optimizations** - Caching, preloading, batch operations, memory management
- [ ] **Comprehensive Test Suite** - 95%+ coverage with unit, integration, and performance tests
- [ ] **Documentation Package** - API docs, examples, migration guides, best practices
- [ ] **Clean Integration** - Legacy code removal and enhanced architecture integration

## Timeline and Resource Allocation
- **Phase 1 (Configuration)**: 0.5 days - Configuration system design and implementation
- **Phase 2 (Interface)**: 0.5 days - Enhanced interface design and supporting types
- **Phase 3 (Core Implementation)**: 2 days - Service implementation, lifecycle, and loading logic
- **Phase 4 (Advanced Features)**: 1 day - Hot-reload, performance optimization, memory management
- **Phase 5 (Testing)**: 1 day - Comprehensive test infrastructure and validation
- **Phase 6 (Integration)**: 1 day - Service integration, cleanup, documentation, final validation
- **Total**: 5 days sequential implementation

## Risk Mitigation Summary
- **Legacy Dependencies**: Analysis completed, no external dependencies on legacy code
- **Performance Regression**: Comprehensive benchmarking and performance test infrastructure
- **Memory Leaks**: Memory pressure integration and automated cleanup testing
- **Hot-Reload Complexity**: Incremental implementation with comprehensive validation and rollback
- **Integration Issues**: Thorough testing with existing enhanced service infrastructure