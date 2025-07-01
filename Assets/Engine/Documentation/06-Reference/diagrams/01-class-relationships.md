# Class Relationships Diagram

This diagram shows the main classes and their relationships in the Sinkii09 Engine service architecture.

```mermaid
classDiagram
    class Engine {
        <<static>>
        +bool Initialized
        +bool Initializing
        +IEgineBehaviour Behaviour
        +IConfigProvider ConfigProvider
        -ServiceContainer _serviceContainer
        -ServiceLifecycleManager _lifecycleManager
        +InitializeAsync(configProvider, behaviour)
        +GetService~T~()
        +TryGetService~T~()
        +TerminateAsync()
        -DiscoverAndRegisterServices()
    }

    class ServiceContainer {
        -ConcurrentDictionary~Type, ServiceRegistration~ _registrations
        -ConcurrentDictionary~Type, object~ _resolvedSingletons
        -ServiceDependencyGraph _cachedDependencyGraph
        +RegisterService~TService, TImplementation~()
        +RegisterSingleton~TService~()
        +Resolve~TService~()
        +BuildDependencyGraph()
        +ValidateDependencies()
        -DiscoverDependencies()
        -CreateInstance()
    }

    class ServiceLifecycleManager {
        -IServiceContainer _container
        -Dictionary~Type, ServiceLifecycleInfo~ _lifecycleInfo
        +bool IsInitialized
        +InitializeAllAsync()
        +ShutdownAllAsync()
        +RestartServiceAsync()
        +PerformHealthChecksAsync()
        +GetServiceLifecycleInfo()
    }

    class ServiceDependencyGraph {
        -Dictionary~Type, ServiceNode~ _nodes
        -List~List~Type~~ _circularDependencies
        +bool HasCircularDependencies
        +Build(registrations)
        +GetInitializationOrder()
        +DetectCircularDependencies()
        +CalculateDepths()
        +GenerateVisualization()
    }

    class ServiceNode {
        +Type ServiceType
        +ServiceRegistration Registration
        +List~ServiceNode~ Dependencies
        +List~ServiceNode~ Dependents
        +int Depth
        +bool Visited
        +bool InStack
    }

    class ServiceRegistration {
        +Type ServiceType
        +Type ImplementationType
        +ServiceLifetime Lifetime
        +Type[] RequiredDependencies
        +Type[] OptionalDependencies
        +ServicePriority Priority
        +bool IsEngineService
        +object SingletonInstance
        +Func~IServiceProvider, object~ Factory
    }

    class IEngineService {
        <<interface>>
        +InitializeAsync(provider, cancellationToken)
        +ShutdownAsync(cancellationToken)
        +HealthCheckAsync()
    }

    class IServiceContainer {
        <<interface>>
        +RegisterService~TService, TImplementation~()
        +Resolve~TService~()
        +IsRegistered~TService~()
        +BuildDependencyGraph()
        +CreateScope()
    }

    class EngineServiceAttribute {
        +bool InitializeAtRuntime
        +ServicePriority Priority
        +Type[] RequiredServices
        +Type[] OptionalServices
        +ServiceLifetime Lifetime
        +string Description
        +int InitializationTimeout
    }

    class ActorService {
        -ActorServiceConfiguration _config
        +InitializeAsync()
        +ShutdownAsync()
        +HealthCheckAsync()
    }

    class IActorService {
        <<interface>>
    }

    %% Relationships
    Engine --> ServiceContainer : creates and uses
    Engine --> ServiceLifecycleManager : creates and uses
    ServiceContainer --> ServiceDependencyGraph : builds
    ServiceContainer --> ServiceRegistration : contains
    ServiceDependencyGraph --> ServiceNode : contains
    ServiceLifecycleManager --> ServiceContainer : uses
    ServiceContainer ..|> IServiceContainer : implements
    ServiceContainer ..|> IServiceProvider : implements
    ActorService ..|> IActorService : implements
    IActorService --|> IEngineService : extends
    ActorService --> EngineServiceAttribute : decorated with
    ServiceRegistration --> EngineServiceAttribute : metadata from

    %% Styling
    classDef engineCore fill:#e1f5fe
    classDef serviceSystem fill:#f3e5f5
    classDef interfaces fill:#e8f5e8
    classDef implementations fill:#fff3e0
    
    class Engine engineCore
    class ServiceContainer,ServiceLifecycleManager,ServiceDependencyGraph serviceSystem
    class IEngineService,IServiceContainer,IActorService interfaces
    class ActorService implementations
```

## Key Components

### Core Engine
- **Engine**: Static facade providing service access and initialization
- **ServiceContainer**: Main DI container with registration and resolution
- **ServiceLifecycleManager**: Orchestrates service initialization and lifecycle

### Service Infrastructure  
- **ServiceDependencyGraph**: Analyzes and visualizes service dependencies
- **ServiceRegistration**: Metadata about registered services
- **ServiceNode**: Represents services in the dependency graph

### Service Implementation
- **IEngineService**: Base interface for all engine services
- **ActorService**: Example service implementation
- **EngineServiceAttribute**: Metadata for service configuration