# Service Lifecycle Management

This diagram shows the complete lifecycle of services from registration through initialization, health monitoring, and shutdown.

## Service State Diagram

```mermaid
stateDiagram-v2
    [*] --> Uninitialized
    
    Uninitialized --> Initializing : InitializeAsync()
    Initializing --> Initialized : Success
    Initializing --> Failed : Error/Exception
    Initializing --> Cancelled : Cancellation
    
    Initialized --> Healthy : HealthCheck Success
    Initialized --> Unhealthy : HealthCheck Failed
    Initialized --> Restarting : RestartServiceAsync()
    Initialized --> Shutting_Down : ShutdownAsync()
    
    Healthy --> Unhealthy : HealthCheck Failed
    Unhealthy --> Healthy : HealthCheck Success
    Healthy --> Restarting : RestartServiceAsync()
    Unhealthy --> Restarting : RestartServiceAsync()
    
    Restarting --> Shutting_Down : Shutdown Phase
    Shutting_Down --> Shut_Down : Success
    Shutting_Down --> Failed : Shutdown Error
    
    Shut_Down --> Initializing : Restart Phase
    Failed --> [*] : Cleanup
    Cancelled --> [*] : Cleanup
    
    state Initializing {
        [*] --> Resolving_Dependencies
        Resolving_Dependencies --> Loading_Configuration
        Loading_Configuration --> Creating_Instance
        Creating_Instance --> Calling_InitializeAsync
        Calling_InitializeAsync --> [*]
    }
    
    state Restarting {
        [*] --> Shutdown_Service
        Shutdown_Service --> Initialize_Service
        Initialize_Service --> [*]
    }
    
    note right of Uninitialized
        Service discovered via reflection
        Registration created
        Dependencies analyzed
    end note
    
    note right of Initializing
        - Resolve dependencies
        - Load configuration
        - Create instance
        - Call InitializeAsync()
        - Handle timeout
    end note
    
    note right of Initialized
        Service ready for use
        Available via DI container
        Health monitoring active
    end note
    
    note right of Restarting
        Used for error recovery
        Maintains service availability
        Preserves dependency order
    end note
```

## Service State Transitions Flow

```mermaid
flowchart TD
    A[Service Registration] --> B[ServiceLifecycleInfo Created]
    B --> C[State: Uninitialized]
    
    C --> D[InitializeAllAsync Called]
    D --> E[Dependency Graph Built]
    E --> F[Topological Sort]
    F --> G[Service Initialization Loop]
    
    G --> H[Get Next Service in Order]
    H --> I[State: Initializing]
    I --> J[Start Stopwatch]
    J --> K[Resolve Service Instance]
    
    K --> L[Container.Resolve]
    L --> M{Service Exists?}
    M -->|No| N[Create Instance]
    M -->|Yes| O[Return Cached Instance]
    
    N --> P[Load Configuration]
    P --> Q{Configuration Required?}
    Q -->|Yes| R[Load from Resources]
    Q -->|No| S[Skip Configuration]
    
    R --> T{Config Found?}
    T -->|No| U[Create Default Config]
    T -->|Yes| V[Validate Configuration]
    
    U --> W[Constructor Injection]
    V --> W
    S --> W
    
    W --> X[Resolve Constructor Parameters]
    X --> Y[Create Service Instance]
    Y --> Z[Cache if Singleton]
    Z --> AA[Call InitializeAsync]
    O --> AA
    
    AA --> BB[Service.InitializeAsync]
    BB --> CC{Timeout?}
    CC -->|Yes| DD[Cancel and Mark Failed]
    CC -->|No| EE{Success?}
    
    EE -->|Yes| FF[State: Initialized]
    EE -->|No| GG[State: Failed]
    DD --> GG
    
    FF --> HH[Record Timing]
    GG --> II[Record Error]
    HH --> JJ[Update LifecycleInfo]
    II --> JJ
    
    JJ --> KK{More Services?}
    KK -->|Yes| H
    KK -->|No| LL[All Services Processed]
    
    LL --> MM[Generate Report]
    MM --> NN{All Successful?}
    NN -->|Yes| OO[Engine Initialized]
    NN -->|No| PP[Initialization Failed]
    
    %% Health Monitoring
    FF --> QQ[Start Health Monitoring]
    QQ --> RR[Periodic HealthCheckAsync]
    RR --> SS{Health Check Result?}
    SS -->|Healthy| TT[State: Healthy]
    SS -->|Unhealthy| UU[State: Unhealthy]
    
    TT --> VV[Continue Monitoring]
    UU --> WW[Log Health Issue]
    VV --> RR
    WW --> RR
    
    %% Restart Flow
    UU --> XX[RestartServiceAsync Called]
    XX --> YY[State: Restarting]
    YY --> ZZ[Shutdown Service]
    ZZ --> AAA[State: Shutting Down]
    AAA --> BBB[Call ShutdownAsync]
    BBB --> CCC[State: Shut Down]
    CCC --> DDD[Re-initialize Service]
    DDD --> I
    
    %% Engine Termination
    OO --> EEE[Engine.TerminateAsync]
    EEE --> FFF[ShutdownAllAsync]
    FFF --> GGG[Reverse Dependency Order]
    GGG --> HHH[Shutdown Each Service]
    HHH --> III[All Services Terminated]
    
    %% Styling
    classDef stateNode fill:#e8f5e8
    classDef processNode fill:#e3f2fd
    classDef decisionNode fill:#fff3e0
    classDef errorNode fill:#ffebee
    classDef successNode fill:#e8f5e8
    
    class C,I,FF,GG,TT,UU,YY,AAA,CCC stateNode
    class B,D,E,F,G,H,J,K,L,N,P,R,U,V,W,X,Y,Z,AA,BB,HH,II,JJ,LL,MM,QQ,RR,WW,VV,XX,ZZ,BBB,DDD,EEE,FFF,GGG,HHH processNode
    class M,Q,T,CC,EE,KK,NN,SS decisionNode
    class DD,GG,II,PP,UU errorNode
    class O,S,FF,OO,III successNode
```

## Key Lifecycle Phases

### 1. Registration Phase
- Services discovered via reflection
- `ServiceLifecycleInfo` objects created
- Initial state set to `Uninitialized`

### 2. Initialization Phase
- Services resolved in dependency order
- Configuration loaded and validated
- Service instances created via DI
- `InitializeAsync()` called with timeout protection

### 3. Runtime Phase
- Services available via `Engine.GetService<T>()`
- Periodic health monitoring
- State transitions between Healthy/Unhealthy
- Error recovery via restart mechanism

### 4. Shutdown Phase
- Services shut down in reverse dependency order
- Each service's `ShutdownAsync()` called
- Resources cleaned up and disposed

### 5. Error Handling
- Individual service failures don't stop engine
- Failed services marked and reported
- Restart capability for error recovery
- Comprehensive logging and diagnostics