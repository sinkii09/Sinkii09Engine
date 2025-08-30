# ResourcePathResolver Service Implementation Workplan

## Project Overview
- **Service Name**: ResourcePathResolver
- **Scope**: Engine-wide resource path resolution (Actor System, Script System, Audio, UI, etc.)
- **Duration**: 5 days
- **Priority**: High (Critical infrastructure)
- **Dependencies**: Enhanced Service Architecture, ResourceService

## Problem Statement

### Current Issues Across Engine
1. **Actor System**: 13+ different GetResourcePath implementations with inconsistent formats
2. **Script System**: Hard-coded script paths in multiple locations  
3. **Resource System**: Scattered path generation logic across providers
4. **No Configuration**: Hard-coded constants instead of configurable paths
5. **No Validation**: Broken resource paths fail silently or at runtime

### Specific Actor System Issues
- Character paths: `"Characters/{name}/{expression}_{pose}_{outfit}"` vs `"Characters/Default/{expression}_{pose}_{outfit}"`
- Background paths: `"Backgrounds/Scene/{location}_{variant}"`
- Metadata paths: `"Actors/Characters/{actorId}"` vs `"{basePath}/Characters/{actorId}"`
- Parameter mismatches: Some need characterName, others don't
- Mixed string/enum resource categories

## Implementation Plan

### Day 1: Core ResourcePathResolver Service Design
**Goal**: Create the foundational service architecture

**Tasks**:
1. **Create service interface and implementation**
   ```csharp
   [EngineService(ServiceCategory.Core, ServicePriority.High)]
   public class ResourcePathResolver : IResourcePathResolver, IEngineService
   {
       // Unified path resolution for ALL engine resources
       string ResolveResourcePath(ResourceType resourceType, string resourceId, ResourceCategory category = ResourceCategory.Primary, params object[] parameters);
       string[] GetFallbackPaths(ResourceType resourceType, string resourceId, ResourceCategory category = ResourceCategory.Primary);
       bool ValidateResourcePath(string path, out string correctedPath);
       void RegisterPathTemplate(ResourceType resourceType, ResourceCategory category, string pathTemplate);
   }
   ```

2. **Create configuration system**
   - `ResourcePathResolverConfiguration.cs` (ScriptableObject)
   - Support for path templates with parameter substitution
   - Environment-specific overrides (Development/Production)
   - Fallback path hierarchies

3. **Define resource type enums**
   ```csharp
   public enum ResourceType
   {
       Actor, Script, Audio, UI, Texture, Prefab, Animation, Shader, Material, Config
   }
   
   public enum ResourceCategory  
   {
       Primary, Fallback, Development, Production, Sprites, Animations, Audio, Metadata
   }
   ```

**Deliverables**:
- Core service implementation with dependency injection
- Configuration ScriptableObject with Unity Inspector support
- Path template system with parameter substitution
- Comprehensive validation and error handling

**Acceptance Criteria**:
- [ ] ResourcePathResolver service compiles without errors
- [ ] Configuration system loads and validates correctly
- [ ] Path template substitution works with complex parameters
- [ ] Service integrates with enhanced service architecture

### Day 2: Actor System Integration
**Goal**: Replace all 13 GetResourcePath implementations with resolver calls

**Tasks**:
1. **Update appearance classes**
   - Remove all custom GetResourcePath methods from CharacterAppearance, BackgroundAppearance, GenericAppearance
   - Replace with single resolver call: `_pathResolver.ResolveResourcePath(ResourceType.Actor, actorId, category, appearance)`

2. **Update metadata classes**
   - Remove GetResourcePath from ActorMetadata, CharacterMetadata, BackgroundMetadata
   - Delegate to resolver service with proper parameters

3. **Update managers and services**
   - ActorResourceManager: Use resolver for all resource loading
   - ActorFactory: Replace hardcoded paths with resolver calls
   - ActorService: Update resource path collection logic

4. **Create actor-specific path templates**
   ```
   Actor.Character.Primary = "Characters/{actorId}/{expression}_{pose}_{outfit:D2}"
   Actor.Character.Sprites = "Characters/{actorId}/Sprites/{expression}_{pose}_{outfit:D2}"
   Actor.Background.Primary = "Backgrounds/{location}/{variant}_{timeOfDay}"
   Actor.Background.Effects = "Backgrounds/{location}/Effects/{variant}"
   ```

**Deliverables**:
- Zero GetResourcePath methods in appearance classes
- All actor resource loading goes through resolver
- Consistent path formats across all actor types
- Backwards compatibility maintained

**Acceptance Criteria**:
- [ ] All actor appearance classes use resolver service
- [ ] No hardcoded paths remain in actor system
- [ ] All existing resource loading scenarios work
- [ ] Path formats are consistent across all actor types

### Day 3: Engine-Wide Service Integration  
**Goal**: Extend resolver to Script System, Resource System, and other engine components

**Tasks**:
1. **Script System integration**
   - Replace hardcoded script paths in ScriptService
   - Add script-specific resource types and templates
   - Support for script resource categories (Source, Compiled, Metadata)

2. **Resource System integration**  
   - Update ResourceService providers to use resolver
   - Centralize provider-specific path logic
   - Add validation for resource provider paths

3. **Configuration System integration**
   - Update config loading paths throughout engine
   - Standardize config resource paths
   - Add development vs production path switching

4. **Create comprehensive path templates**
   ```
   Script.Source.Primary = "Scripts/{scriptName}.script"
   Script.Compiled.Primary = "Scripts/Compiled/{scriptName}.bytes"
   Audio.Music.Primary = "Audio/Music/{category}/{trackName}"
   UI.Prefab.Primary = "UI/Prefabs/{uiElementName}"
   ```

**Deliverables**:
- Engine-wide resource path consistency
- All major systems use resolver service
- Comprehensive path template library
- Configuration-driven path management

**Acceptance Criteria**:
- [ ] Script system uses resolver for all path operations
- [ ] Resource system providers use centralized path logic
- [ ] Configuration system has standardized paths
- [ ] Path templates cover all major engine resource types

### Day 4: Performance Optimization and Caching
**Goal**: Optimize resolver performance and add intelligent caching

**Tasks**:
1. **Implement path caching**
   - LRU cache for frequently resolved paths
   - Cache invalidation on configuration changes
   - Memory pressure response integration

2. **Path validation optimization**
   - Compile-time path template validation
   - Runtime path existence checking (optional)
   - Batch validation for multiple resources

3. **Performance monitoring**
   - Resolution time tracking
   - Cache hit rate monitoring  
   - Most frequently resolved path analytics

4. **Memory optimization**
   - String interning for common paths
   - Object pooling for path builder operations
   - Minimize allocations in hot paths

**Deliverables**:
- <0.1ms average path resolution time
- 90%+ cache hit rate for common operations
- Memory usage optimization
- Performance monitoring dashboard

**Acceptance Criteria**:
- [ ] Path resolution meets performance targets
- [ ] Cache system works correctly with high hit rates
- [ ] Memory usage is optimized for production
- [ ] Performance monitoring provides useful metrics

### Day 5: Testing and Documentation
**Goal**: Comprehensive testing and production readiness

**Tasks**:
1. **Unit testing**
   - Path resolution accuracy tests
   - Template parameter substitution tests
   - Fallback path hierarchy tests
   - Configuration loading and validation tests

2. **Integration testing**
   - Actor system resource loading scenarios
   - Script system resource resolution
   - Cross-service resource path consistency
   - Configuration override testing

3. **Performance testing**
   - Path resolution benchmarks
   - Cache performance validation
   - Memory usage stress testing
   - Concurrent access thread safety

4. **Documentation**
   - Service API documentation
   - Path template configuration guide
   - Migration guide from hardcoded paths
   - Best practices for new resource types

**Deliverables**:
- 95%+ test coverage for all path resolution scenarios
- Performance benchmarks meeting targets
- Complete documentation package
- Production deployment readiness

**Acceptance Criteria**:
- [ ] All tests pass with high coverage
- [ ] Performance benchmarks meet or exceed targets
- [ ] Documentation is complete and accurate
- [ ] Service is ready for production deployment

## Success Metrics

### Technical Metrics
- **Zero hardcoded resource paths** in engine codebase
- **100% consistency** in resource path formats
- **90%+ cache hit rate** for path resolution
- **<0.1ms average resolution time**
- **50%+ reduction** in path-related code duplication

### Quality Metrics
- **Zero path-related runtime errors** in production testing
- **100% backwards compatibility** for existing resource loading
- **95%+ test coverage** for path resolution logic
- **Complete configuration flexibility** for all resource types

### Maintainability Metrics
- **Single source of truth** for all resource paths
- **Easy addition** of new resource types and categories
- **Centralized debugging** for resource loading issues
- **Configuration-driven** path management without code changes

## Risk Mitigation

### High Risk Areas
1. **Breaking existing resource loading**: Comprehensive backwards compatibility testing
2. **Performance regression**: Extensive benchmarking and optimization
3. **Configuration complexity**: Simple, intuitive configuration interface

### Migration Strategy
1. **Gradual replacement**: Replace one system at a time
2. **Parallel operation**: Support both old and new path methods during transition
3. **Extensive testing**: Validate every resource loading scenario
4. **Rollback capability**: Maintain ability to revert if issues arise

## Future Enhancements
- **Resource discovery**: Automatic detection of available resources
- **Path prediction**: AI-driven resource path suggestions
- **Hot-reloading**: Runtime path template updates
- **Asset bundling**: Integration with Unity asset bundle system

---

## Dependencies
- Enhanced Service Architecture (completed)
- ResourceService implementation (completed)
- DOTween Pro integration (existing)
- UniTask framework (existing)

## Team Dependencies
- Engine architecture team for design review
- QA team for comprehensive testing
- DevOps team for deployment strategy
- Documentation team for user guides

## Deployment Strategy

### Phase 1: Internal Testing (Day 6)
- Deploy to development environment
- Internal team validation
- Performance benchmarking
- Bug fixes and optimizations

### Phase 2: Beta Testing (Day 7)
- Deploy to staging environment
- Limited beta user testing
- Feedback collection and incorporation
- Final performance tuning

### Phase 3: Production Rollout (Day 8)
- Gradual production deployment
- Feature flags for safe rollout
- Monitoring and health checks
- Full documentation release

## Conclusion

The ResourcePathResolver service will eliminate the current patchwork of hardcoded resource paths throughout the engine, replacing it with a unified, configurable, and performant system. This centralization will improve maintainability, reduce bugs, and provide a solid foundation for future engine development.

Upon completion, the engine will have:
- Single source of truth for all resource paths
- Consistent path formats across all systems
- Configuration-driven path management
- High-performance path resolution with caching
- Comprehensive testing and validation
- Complete backwards compatibility

This service will serve as critical infrastructure for the entire engine, supporting current systems while enabling future enhancements and optimizations.