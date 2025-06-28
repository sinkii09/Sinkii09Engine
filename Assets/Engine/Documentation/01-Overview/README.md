    # Sinkii09 Engine

[![Unity Version](https://img.shields.io/badge/Unity-2021.3%2B-blue.svg)](https://unity3d.com/get-unity/download)
[![Version](https://img.shields.io/badge/Version-0.1.0-green.svg)](https://github.com/Sinkii09/Engine)
[![License](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

A modular, service-oriented game engine framework for Unity that provides reusable core systems and utilities for rapid game development.

## 📚 Documentation

All comprehensive documentation is organized in the [Documentation](../) folder with the following structure:

```
../Documentation/
├── 📋 DOCUMENTATION_INDEX.md          # Complete documentation catalog
├── 01-Overview/                       # Project introduction and planning
│   ├── README.md                      # Project overview and getting started
│   └── ROADMAP.md                     # Development roadmap and milestones
├── 02-Architecture/                   # Technical design and patterns
│   ├── ARCHITECTURE.md                # Technical architecture guide
│   └── ENGINE_VISUALIZATION.md        # Visual system diagrams
├── 03-Development/                    # Development guidelines and standards
│   └── CONTRIBUTING.md                # Contribution guidelines and workflow
├── 04-Management/                     # Project management and tracking
│   ├── PROGRESS_TRACKER.md            # Weekly progress and milestones
│   ├── DEVELOPMENT_TRACKER.md         # Daily development logging
│   └── CHANGELOG.md                   # Version history and releases
├── 05-Quality/                        # Quality assurance and testing
│   └── TESTING.md                     # Testing strategy and QA processes
└── 06-Reference/                      # Technical reference (planned)
    └── README.md                      # Reference documentation index (planned)
```

### 🚀 **Quick Access**
- **[📖 Get Started](../01-Overview/README.md)** - New to the project? Start here
- **[🏗️ Architecture Guide](../02-Architecture/ARCHITECTURE.md)** - Technical design and patterns
- **[📊 Engine Visualization](../02-Architecture/ENGINE_VISUALIZATION.md)** - Visual system diagrams
- **[💻 Contributing Guide](../03-Development/CONTRIBUTING.md)** - Development guidelines and workflow
- **[📈 Progress Tracker](../04-Management/PROGRESS_TRACKER.md)** - Weekly progress and milestones
- **[🧪 Testing Strategy](../05-Quality/TESTING.md)** - Testing and QA processes

### 📋 **Complete Documentation Index**
For a complete catalog of all documentation with detailed descriptions and cross-references, visit the **[📋 Documentation Index](../DOCUMENTATION_INDEX.md)**

## 🚀 Quick Installation

### Unity Package Manager
```
1. Open Unity Package Manager (Window → Package Manager)
2. Click + and select "Add package from git URL"
3. Enter: https://github.com/Sinkii09/Engine.git
```

### Manual Installation
```
1. Clone or download this repository
2. Copy the Assets/Engine folder to your Unity project's Assets directory
3. Unity will automatically import and compile the package
```

## 🎯 Core Features

- **Service-Oriented Architecture** - Modular service system with dependency injection
- **Command System** - Flexible command pattern with script-based execution  
- **Resource Management** - Multi-provider resource loading system
- **Custom Scripting** - Domain-specific scripting language with `.script` files
- **Async-First Design** - Built on UniTask for high-performance async operations
- **Editor Tools** - Custom Unity Editor integrations and asset importers

## 🔧 Basic Usage

```csharp
using Sinkii09.Engine;
using Sinkii09.Engine.Services;

// Initialize the engine with services
var services = new List<IService>
{
    new ResourceService(),
    new ScriptService(),
    new ActorService()
};

await Engine.InitializeAsync(configProvider, behaviour, services);

// Get services
var resourceService = Engine.GetService<IResourceService>();
var config = Engine.GetConfig<ScriptsConfig>();
```

## 📋 Project Status

**Current Version**: 0.1.0  
**Development Phase**: Foundation (Phase 1)  
**Overall Progress**: 12%

### Implementation Status
- ✅ **Engine Core** - Static facade and initialization
- ✅ **Service Locator** - Dependency injection container  
- ✅ **Command System** - Reflection-based command discovery
- ⚠️ **Resource Service** - Basic loading (multi-provider pending)
- ❌ **Actor Service** - Not implemented
- ❌ **Audio System** - Not implemented
- ❌ **UI Management** - Not implemented

## 🛣️ Development Roadmap

### **Phase 1: Core Foundation** (Weeks 1-8)
- Enhanced service architecture with dependency injection
- Actor system with generic manager pattern  
- Multi-provider resource system
- Input management system

### **Phase 2: Essential Systems** (Weeks 9-19)
- State management with save/load system
- Audio management system
- UI management framework
- Scene management system

### **Phase 3+: Advanced Features** (Weeks 20+)
- Animation system, effects, localization
- Performance optimization
- Developer tools and documentation

## 🤝 Contributing

We welcome contributions! Please see our [Contributing Guidelines](../03-Development/CONTRIBUTING.md) for:

- Development setup and workflow
- Coding standards and best practices  
- Pull request process
- Testing requirements

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](../../../../LICENSE) file for details.

## 🙏 Acknowledgments

- Unity Technologies for the Unity Engine
- Cysharp for UniTask
- Sirenix for Odin Inspector
- The Unity community for inspiration and feedback

---

**Author**: Sinkii09  
**Version**: 0.1.0  
**Unity Version**: 2021.3+

For detailed documentation, visit the [Documentation](../) folder.