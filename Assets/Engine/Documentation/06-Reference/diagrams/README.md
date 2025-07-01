# Engine Architecture Diagrams

This directory contains comprehensive Mermaid diagrams that visualize the Sinkii09 Engine's initialization and dependency injection flow.

## Quick Start

1. **View Online**: Copy any diagram code and paste into [Mermaid Live Editor](https://mermaid.live)
2. **GitHub**: These diagrams render automatically in GitHub markdown
3. **VS Code**: Install the Mermaid extension to preview locally

## Diagram Overview

### 📋 [01-class-relationships.md](./01-class-relationships.md)
**Class Relationship Diagram**
- Shows the main classes and their relationships
- Includes Engine, ServiceContainer, ServiceLifecycleManager, and key interfaces
- Displays inheritance and composition relationships

### 🔄 [02-service-discovery-flow.md](./02-service-discovery-flow.md) 
**Service Discovery & Registration Flow**
- Visualizes the Engine.InitializeAsync() sequence
- Shows how services are discovered via reflection
- Displays the registration process in ServiceContainer

### 🌐 [03-dependency-graph-flow.md](./03-dependency-graph-flow.md)
**Dependency Graph & Initialization Flow**
- Shows how the dependency graph is built
- Displays the topological sorting for initialization order
- Includes circular dependency detection (where your current issue occurs)

### 🔄 [04-service-lifecycle.md](./04-service-lifecycle.md)
**Service Lifecycle Management**
- Shows the ServiceLifecycleManager orchestration
- Displays service states and transitions
- Includes health checking and shutdown processes

### 💉 [05-dependency-injection-flow.md](./05-dependency-injection-flow.md)
**Dependency Injection Flow**
- Shows how services request and receive dependencies
- Displays configuration injection process
- Includes the service resolution mechanism

## Understanding Your Circular Dependency Issue

Based on these diagrams, your "Circular dependencies detected" error occurs in **Diagram #3** at this specific point:

```
ServiceLifecycleManager.InitializeAllAsync 
→ ServiceDependencyGraph.Build 
→ DetectCircularDependencies 
→ [Circular dependency found!] 
→ Initialization Failed
```

### Key Insights:

1. **Current Behavior**: The engine **fails completely** when circular dependencies are detected
2. **Detection Location**: `ServiceDependencyGraph.DetectCircularDependencies()` method
3. **Protection Layers**: Multiple circular dependency protections exist but the system is designed to be strict

### Next Steps for Circular Dependency Resolution:

1. **Identify the Cycle**: The error report should show which services create the circular dependency
2. **Review Service Dependencies**: Check the `RequiredServices` and `OptionalServices` in your `EngineServiceAttribute` declarations
3. **Consider Architecture Changes**: 
   - Make some dependencies optional
   - Use property injection instead of constructor injection
   - Implement lazy initialization patterns

## Diagram Features

- **Color Coding**: Different node types are color-coded for clarity
- **Error Paths**: Red nodes show error conditions and failure paths
- **Decision Points**: Diamond shapes show key decision points
- **Process Flow**: Blue nodes show normal processing steps
- **State Management**: Green nodes show successful states

## Contributing

When updating these diagrams:
1. Test in [Mermaid Live Editor](https://mermaid.live) first
2. Ensure proper syntax and styling
3. Update this README if new diagrams are added
4. Keep explanations clear and concise

## Related Documentation

- [Engine Core Documentation](../01-Overview/)
- [Service Architecture](../02-Architecture/)
- [Development Guidelines](../03-Development/)