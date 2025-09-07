# Turn-Based System Implementation Workplan

## Document Summary
**Purpose**: Create a comprehensive, flexible turn-based system for the Sinkii09 Engine that can handle multiple turn-based game types through a single, configurable service architecture.

**Status**: Planning Phase  
**Priority**: Medium  
**Estimated Duration**: 4-6 weeks  
**Dependencies**: ActorService, ResourceService, Command System

## Architecture Overview
- **Single System Approach**: One TurnBasedService with pluggable strategies instead of multiple separate systems
- **Strategy Pattern**: Different turn modes (Classic, Initiative, ATB, Simultaneous, Phase-based) as interchangeable strategies
- **Event Sourcing**: Complete turn history for replay, undo/redo, and save/load functionality
- **CQRS Pattern**: Separate command and query responsibilities for better performance and maintainability

## Turn-Based System Types Supported

### 1. Classic Turn-Based
- Sequential turns, one participant at a time
- Examples: Chess, Final Fantasy (classic), XCOM

### 2. Initiative-Based
- Turn order determined by speed/initiative stats
- Examples: D&D, Divinity Original Sin, most JRPGs

### 3. Time-Based Turns (ATB - Active Time Battle)
- Participants act when their time gauge fills
- Examples: Final Fantasy 4-9, Chrono Trigger

### 4. Simultaneous Turns
- All participants plan actions, then execute simultaneously
- Examples: Frozen Synapse, Combat Mission

### 5. Phase-Based
- Multiple phases per turn (movement, action, resolution)
- Examples: Warhammer 40K, many board game adaptations

### 6. Real-Time with Pause
- Real-time with ability to pause for tactical decisions
- Examples: Dragon Age, Pillars of Eternity

## Phase 1: Core Architecture Foundation (Week 1)

### 1.1 Service Setup
- [ ] Create ITurnBasedService interface following engine patterns
- [ ] Implement TurnBasedService with [EngineService] attribute
- [ ] Create TurnBasedServiceConfiguration ScriptableObject
- [ ] Set up dependency injection with existing services (ActorService, ResourceService)

### 1.2 Time Abstraction Layer
- [ ] Design IGameTimeManager interface for temporal control
- [ ] Implement GameTime value type for turn-based time representation
- [ ] Create time manipulation system (pause, resume, time dilation)
- [ ] Add time-based event system

### 1.3 Core Interfaces
- [ ] Design ITurnParticipant interface for turn-taking entities
- [ ] Create ITurnAction interface for executable turn actions
- [ ] Implement ITurnModeStrategy interface for pluggable turn systems
- [ ] Design ITurnContext for action execution environment

## Phase 2: Turn Management Systems (Week 2)

### 2.1 Strategy Pattern Implementation
- [ ] Create ClassicTurnStrategy (sequential turns)
- [ ] Implement InitiativeBasedStrategy (speed-based ordering)
- [ ] Build ATBStrategy (Active Time Battle system)
- [ ] Develop SimultaneousTurnStrategy (all plan, then execute)
- [ ] Create PhaseBasedStrategy (movement, action, resolution phases)

### 2.2 Participant Management
- [ ] Implement ParticipantRegistry for active combatants
- [ ] Create TurnQueue with priority/initiative ordering
- [ ] Build ParticipantStateManager for tracking turn states
- [ ] Add initiative calculation and modification system

### 2.3 Action Scheduling
- [ ] Design IActionScheduler for time-based action queuing
- [ ] Implement action validation and conflict resolution
- [ ] Create batch processing for simultaneous actions
- [ ] Add interrupt system for reaction-based gameplay

## Phase 3: Advanced Features (Week 3-4)

### 3.1 Event Sourcing System
- [ ] Design ITurnEventStore interface for event persistence
- [ ] Implement turn event serialization/deserialization
- [ ] Create replay system for turn history playback
- [ ] Build state reconstruction from event history

### 3.2 CQRS Implementation
- [ ] Separate command handlers (ITurnCommand) for write operations
- [ ] Create query handlers (ITurnQuery) for read operations
- [ ] Implement command validation and authorization
- [ ] Add query caching for performance optimization

### 3.3 State Machine Architecture
- [ ] Design hierarchical state machine for turn phases
- [ ] Implement state transitions and validation
- [ ] Create state-specific behavior handlers
- [ ] Add state persistence and recovery

## Phase 4: Performance & Optimization (Week 4-5)

### 4.1 Caching Systems
- [ ] Implement ITurnCache for frequently accessed data
- [ ] Create turn order prediction and caching
- [ ] Build participant state snapshot caching
- [ ] Add query result caching with invalidation

### 4.2 Memory Management
- [ ] Object pooling for turn actions and states
- [ ] Memory-efficient event storage
- [ ] Garbage collection optimization for turn cycles
- [ ] Integration with existing MemoryPressureMonitor

### 4.3 Multi-Threading Support
- [ ] Actor model implementation for participant processing
- [ ] Async/await optimization with UniTask
- [ ] Thread-safe data structures for concurrent access
- [ ] Cancellation token support throughout system

## Phase 5: Integration & Configuration (Week 5-6)

### 5.1 Engine Service Integration
- [ ] ActorService integration for participant management
- [ ] ResourceService integration for battle configuration assets
- [ ] Command system integration for action execution
- [ ] SaveLoadService integration for turn state persistence

### 5.2 Configuration System
- [ ] BattleModeConfiguration ScriptableObjects for different game modes
- [ ] Runtime configuration switching
- [ ] Validation system for configuration conflicts
- [ ] Default configuration presets for common turn-based styles

### 5.3 Advanced Features
- [ ] Time effect system (haste, slow, stun effects)
- [ ] Interrupt and reaction system
- [ ] Turn prediction and lookahead
- [ ] Conditional turn modifications

## Phase 6: Testing & Documentation (Week 6)

### 6.1 Comprehensive Testing
- [ ] Unit tests for all core components
- [ ] Integration tests with existing engine services
- [ ] Performance benchmarks for different turn modes
- [ ] Memory leak detection and prevention

### 6.2 Example Implementations
- [ ] Classic JRPG battle system example
- [ ] Real-time with pause tactical system
- [ ] Board game style turn system
- [ ] Action point based system

### 6.3 Documentation
- [ ] API documentation for all public interfaces
- [ ] Configuration guide for different turn-based styles
- [ ] Integration examples with existing engine features
- [ ] Performance optimization guidelines

## Technical Specifications

### Key Design Patterns
- **Strategy Pattern**: Pluggable turn mode implementations
- **Event Sourcing**: Complete audit trail and replay capability
- **CQRS**: Optimized read/write operations
- **State Machine**: Robust turn flow management
- **Observer Pattern**: Event-driven architecture

### Architecture Components

#### Core Service Interface
```csharp
public interface ITurnBasedService : IEngineService
{
    // Core turn management
    UniTask StartBattleAsync(IBattleConfiguration battleConfig);
    UniTask EndBattleAsync();
    UniTask NextTurnAsync();
    UniTask ProcessActionAsync(ITurnAction action);
    
    // State queries
    bool IsInBattle { get; }
    ITurnParticipant CurrentTurnHolder { get; }
    int CurrentTurnNumber { get; }
    TurnPhase CurrentPhase { get; }
    
    // Events
    event Action<ITurnParticipant> OnTurnStarted;
    event Action<ITurnParticipant, ITurnAction> OnActionExecuted;
    event Action<BattleResult> OnBattleEnded;
}
```

#### Turn Mode Strategy
```csharp
public interface ITurnModeStrategy
{
    UniTask<ITurnParticipant> DetermineNextParticipant(IReadOnlyList<ITurnParticipant> participants);
    bool CanParticipantAct(ITurnParticipant participant, GameTime currentTime);
    UniTask OnTurnStart(ITurnParticipant participant);
    UniTask OnTurnEnd(ITurnParticipant participant);
}
```

#### Time Management
```csharp
public interface IGameTimeManager
{
    GameTime CurrentTime { get; }
    UniTask AdvanceTime(TimeSpan duration);
    void PauseTime();
    void ResumeTime();
    event Action<GameTime> OnTimeAdvanced;
}
```

### Performance Targets
- Support 100+ participants simultaneously
- <1ms turn calculation time for most operations
- <50MB memory footprint for average battles
- Zero allocation during steady-state turn processing

### File Structure
```
Assets/Engine/Runtime/Scripts/Core/Services/Implemented/TurnBasedService/
├── ITurnBasedService.cs
├── TurnBasedService.cs
├── Core/
│   ├── ITurnParticipant.cs
│   ├── ITurnAction.cs
│   ├── IGameTimeManager.cs
│   ├── TurnQueue.cs
│   └── ActionValidator.cs
├── Strategies/
│   ├── ITurnModeStrategy.cs
│   ├── ClassicTurnStrategy.cs
│   ├── InitiativeBasedStrategy.cs
│   ├── ATBStrategy.cs
│   ├── SimultaneousTurnStrategy.cs
│   └── PhaseBasedStrategy.cs
├── EventSourcing/
│   ├── ITurnEventStore.cs
│   ├── TurnEvent.cs
│   └── EventSourcedTurnManager.cs
├── CQRS/
│   ├── Commands/
│   └── Queries/
├── Configuration/
│   ├── TurnBasedServiceConfiguration.cs
│   ├── IBattleConfiguration.cs
│   └── BattleModeConfiguration.cs
└── Components/
    ├── TurnParticipant.cs
    ├── BattleManager.cs
    └── TurnTimer.cs
```

### Configuration Requirements
- All behavior configurable through ScriptableObjects
- Runtime mode switching capability
- Validation for configuration conflicts
- Backward compatibility for save files

## Advanced Techniques Included

### 1. Time Dilation/Manipulation
Support for game effects that modify time flow for specific participants (haste, slow effects).

### 2. Predictive Turn Calculation
System can predict future turn order for strategic planning and UI display.

### 3. Interrupt System
Allows participants to interrupt others' actions based on specific conditions.

### 4. Multi-Threading with Actor Model
Each participant can process actions concurrently while maintaining turn order integrity.

## Integration Points

### With ActorService
- Participants are registered actors that implement ITurnParticipant
- Turn actions can control actor behaviors and animations
- Actor states synchronize with turn-based game state

### With Command System
- Turn actions implement both ITurnAction and ICommand interfaces
- Leverage command pattern for undo/redo functionality
- Command queuing integrates with turn scheduling

### With ResourceService
- Battle configurations loaded as ScriptableObject assets
- Action definitions loaded through ResourceService
- UI elements loaded dynamically based on turn mode

### With SaveLoadService
- Complete turn state serialization through event sourcing
- Save/load at any point in battle through event replay
- Backward compatibility through versioned event schema

## Risk Mitigation

### Performance Risks
- **Risk**: Complex turn calculations causing frame drops
- **Mitigation**: Async processing, caching, and performance budgets

### Memory Risks  
- **Risk**: Event sourcing causing memory growth
- **Mitigation**: Event compaction, rolling snapshots, and cleanup policies

### Complexity Risks
- **Risk**: Over-engineering making system hard to use
- **Mitigation**: Simple default configurations and clear documentation

---

**Document Version**: 1.0  
**Created**: January 2025  
**Last Updated**: January 2025  
**Next Review**: After Phase 1 completion