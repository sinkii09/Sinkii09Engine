# Service Discovery & Registration Flow

This diagram shows how services are discovered via reflection and registered in the ServiceContainer during engine initialization.

```mermaid
flowchart TD
    A[Engine.InitializeAsync] --> B[Create ServiceContainer]
    B --> C[Create ServiceLifecycleManager]
    C --> D[DiscoverAndRegisterServices]
    
    D --> E[ReflectionUtils.ExportedDomainTypes]
    E --> F[Get Domain Assemblies]
    F --> G[Filter Assemblies by Name]
    G --> H[Extract Types from Assemblies]
    H --> I[Cache Results]
    
    D --> J[Iterate Through Types]
    J --> K{Type Processing}
    
    K --> L[GetEngineServiceAttribute]
    L --> M{Has Recursion Protection?}
    M -->|No| N[Check Thread-Local Cache]
    M -->|Yes| O[Return Safe Default]
    N --> P{Attribute Found?}
    P -->|Yes| Q[Return Attribute]
    P -->|No| R[Create Default Attribute]
    
    Q --> S{InitializeAtRuntime?}
    R --> S
    S -->|No| T[Skip Service]
    S -->|Yes| U[Validate IEngineService]
    
    U --> V{Is IEngineService?}
    V -->|No| W[Log Warning and Skip]
    V -->|Yes| X{Is Concrete Class?}
    X -->|No| Y[Log Warning and Skip]
    X -->|Yes| Z[GetServiceInterface]
    
    Z --> AA[Find Service Interface]
    AA --> BB{Multiple Interfaces?}
    BB -->|Yes| CC[Use First Interface and Log Warning]
    BB -->|No| DD[Use Found Interface]
    CC --> EE[ValidateServiceForRegistration]
    DD --> EE
    
    EE --> FF{Validation Passed?}
    FF -->|No| GG[Log Error and Skip]
    FF -->|Yes| HH[RegisterService]
    
    HH --> II[Create ServiceRegistration]
    II --> JJ[DiscoverDependencies]
    JJ --> KK[GetEngineServiceAttribute Again]
    KK --> LL[Extract Dependencies]
    LL --> MM[Validate No Self-Dependencies]
    MM --> NN[Store in Container]
    
    NN --> OO[Invalidate Dependency Graph Cache]
    OO --> PP[Log Registration Success]
    
    T --> QQ[Continue to Next Type]
    W --> QQ
    Y --> QQ
    GG --> QQ
    PP --> QQ
    
    QQ --> RR{More Types?}
    RR -->|Yes| K
    RR -->|No| SS[Service Discovery Complete]
    
    SS --> TT[ServiceLifecycleManager.InitializeAllAsync]
    
    %% Error Handling
    K -.->|Exception| UU[Log Type Error and Continue]
    UU --> QQ
    
    JJ -.->|Exception| VV[Set Safe Defaults]
    VV --> NN
    
    %% Styling
    classDef processNode fill:#e3f2fd
    classDef decisionNode fill:#fff3e0
    classDef errorNode fill:#ffebee
    classDef successNode fill:#e8f5e8
    
    class A,B,C,D,E,F,G,H,I,J,L,N,Q,R,U,Z,AA,EE,HH,II,JJ,KK,LL,MM,NN,OO,PP,SS,TT processNode
    class M,P,S,V,X,BB,FF,RR decisionNode
    class O,T,W,Y,GG,UU,VV errorNode
    class QQ,SS successNode
```

## Key Phases

### 1. Reflection-Based Discovery
- Uses `ReflectionUtils.ExportedDomainTypes` to scan assemblies
- Filters assemblies to only include project-specific code
- Handles assembly loading errors gracefully

### 2. Attribute Processing
- Gets `EngineServiceAttribute` from each type
- Has recursion protection to prevent infinite loops
- Creates default attributes for types without explicit attributes

### 3. Service Validation
- Ensures types implement `IEngineService`
- Validates concrete classes only
- Determines primary service interface

### 4. Registration Process
- Creates `ServiceRegistration` with metadata
- Discovers service dependencies
- Validates against self-dependencies
- Stores in ServiceContainer

### 5. Error Handling
- Continues processing even if individual types fail
- Logs warnings and errors appropriately
- Sets safe defaults when needed