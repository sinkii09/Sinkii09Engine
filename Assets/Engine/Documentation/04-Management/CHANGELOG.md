# Changelog

All notable changes to the Sinkii09 Engine will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
- Initial project structure and documentation
- Basic service locator architecture
- Command system with reflection-based discovery
- Resource management foundation
- Configuration system using ScriptableObjects
- Script parsing and loading capabilities

### Changed
- N/A (Initial release)

### Deprecated
- N/A (Initial release)

### Removed
- N/A (Initial release)

### Fixed
- N/A (Initial release)

### Security
- N/A (Initial release)

---

## [0.1.0] - 2025-01-XX - Foundation Release

### Added
- **Engine Core**
  - Static Engine facade for service access
  - Async initialization with UniTask
  - Service lifecycle management
  - Configuration provider interface

- **Service Architecture**
  - IService interface for all engine services
  - ServiceLocator with dependency injection
  - Automatic service discovery via reflection
  - Service initialization ordering

- **Command System**
  - Command pattern implementation
  - CommandParser with parameter support
  - Reflection-based command discovery
  - Parameter validation and type conversion
  - Support for command aliases and required parameters

- **Resource Management**
  - IResourceProvider interface
  - Basic ProjectResourceProvider implementation
  - Resource loading and unloading
  - Support for multiple provider types

- **Script System**
  - Custom .script file format
  - Script parsing to ScriptLine objects
  - Command and text line differentiation
  - Script loading through ScriptService

- **Configuration**
  - ScriptableObject-based configuration
  - ConfigProvider for runtime config access
  - Service-specific configuration support

- **Extensions**
  - String utility extensions
  - LINQ utility extensions
  - Reflection utility extensions
  - Object utility extensions
  - Parse utility extensions
  - Resource utility extensions

- **Editor Tools**
  - ScriptImporter for .script files
  - ScriptCreator utility
  - Custom script asset icons

- **Testing**
  - Basic CommandParser unit tests
  - Test infrastructure setup

### Dependencies
- Unity 2021.3+
- UniTask (Cysharp.Threading.Tasks)
- Sirenix Odin Inspector
- ZLinq performance library

### Technical Details
- **Assembly Definitions**: Sinkii09.Engine, Sinkii09.Engine.Editor, Sinkii09.Engine.Test
- **Namespace**: Sinkii09.Engine
- **Target Framework**: .NET Standard 2.1
- **Unity Compatibility**: 2021.3 LTS and above

---

## Version History Template

```
## [X.Y.Z] - YYYY-MM-DD - Release Name

### Added
- New features

### Changed
- Changes in existing functionality

### Deprecated
- Soon-to-be removed features

### Removed
- Now removed features

### Fixed
- Bug fixes

### Security
- Security improvements
```

---

## Release Planning

### Version 0.2.0 - Core Services (Target: Week 8)
**Focus**: Enhanced service architecture and actor system

**Planned Features**:
- Enhanced IEngineService with dependency injection
- Actor management system with generic manager pattern
- Multi-provider resource system with reference counting
- Input management with Unity Input System integration

### Version 0.3.0 - Essential Systems (Target: Week 19)
**Focus**: Game development essentials

**Planned Features**:
- State management with save/load system
- Audio management with multi-track support
- UI management framework
- Scene management with async loading

### Version 0.4.0 - Advanced Features (Target: Week 26)
**Focus**: Polish and advanced capabilities

**Planned Features**:
- Animation system with DOTween integration
- Effect system with particle management
- Localization system
- Performance optimization tools

### Version 0.5.0 - Production Ready (Target: Week 36)
**Focus**: Production-ready features

**Planned Features**:
- Analytics and telemetry
- Cloud integration
- Platform-specific features
- Advanced debugging tools

### Version 1.0.0 - First Major Release (Target: Week 42)
**Focus**: Complete, stable, production-ready engine

**Features**:
- All core systems implemented and tested
- Comprehensive documentation
- Sample projects and tutorials
- Performance optimizations
- Editor tools and developer experience

---

## Migration Guides

### Upgrading from 0.1.x to 0.2.x
*Details will be added when 0.2.0 is released*

### Breaking Changes Policy
- Major versions (X.0.0): May include breaking changes with migration guide
- Minor versions (0.X.0): New features, no breaking changes
- Patch versions (0.0.X): Bug fixes only, no breaking changes

---

## Contribution Guidelines

See [CONTRIBUTING.md](CONTRIBUTING.md) for details on:
- How to report bugs
- How to suggest features
- Development workflow
- Code style guidelines
- Testing requirements

---

## Support

- **Documentation**: [README.md](README.md)
- **Architecture**: [ARCHITECTURE.md](ARCHITECTURE.md)
- **Roadmap**: [ROADMAP.md](ROADMAP.md)
- **Issues**: Create GitHub issues for bugs and feature requests
- **Discussions**: Use GitHub Discussions for questions and ideas