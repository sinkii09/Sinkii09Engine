# Sinkii09 Engine Editor Tools

This directory contains organized editor tools for the Sinkii09 Engine, structured by functionality for better maintainability and discoverability.

## 📁 Directory Structure

```
Assets/Engine/Editor/
├── Analysis/           # Code analysis and debugging tools
├── Configuration/      # Service configuration management
├── Dependencies/       # Dependency management system
├── Development/        # Development and testing utilities
├── Export/            # Package and project export tools
├── Generators/        # Code and asset generators
└── Importer/          # Asset import and processing
```

## 🔧 Tool Categories

### **Analysis Tools**
Located in: `Analysis/`

- **ServiceDependencyAnalyzer** (`ServiceDependencyAnalyzer.cs`)
  - **Menu**: `Engine > Analysis > Service Dependency Analyzer`
  - **Purpose**: Analyze service dependencies, detect circular dependencies, generate dependency graphs
  - **Namespace**: `Sinkii09.Engine.Editor.Analysis`

### **Configuration Management**
Located in: `Configuration/`

- **ConfigInstaller** (`ConfigInstaller.cs`)
  - **Auto-run**: Initializes on Unity load
  - **Purpose**: Automatically creates missing service configurations using reflection
  - **Path**: `Assets/Engine/Runtime/Resources/Configs/Services/`
  - **Namespace**: `Sinkii09.Engine.Editor.Configuration`

- **ConfigManagerMenu** (`ConfigManagerMenu.cs`)
  - **Menu**: `Engine > Configuration > *`
  - **Purpose**: Menu items for creating and managing service configurations
  - **Features**: Auto-discovery, validation, bulk operations
  - **Namespace**: `Sinkii09.Engine.Editor.Configuration`

### **Dependency Management**
Located in: `Dependencies/`

- **DependencyCheckerWindow** (`DependencyCheckerWindow.cs`)
  - **Menu**: `Engine > Dependencies > Check Dependencies`
  - **Purpose**: Modern dependency checker with organized UI
  - **Features**: Bulk installation, status tracking, startup checks
  - **Namespace**: `Sinkii09.Engine.Editor.Dependencies`

- **EditorDependencyChecker** (`EditorDependencyChecker.cs`)
  - **Type**: Static utility class
  - **Purpose**: Core dependency checking logic for editor use
  - **Features**: Multiple provider support, detection algorithms
  - **Namespace**: `Sinkii09.Engine.Editor.Dependencies`

- **EditorDependencyDefinition** (`EditorDependencyDefinition.cs`)
  - **Type**: Data class
  - **Purpose**: Editor-serializable dependency definitions
  - **Namespace**: `Sinkii09.Engine.Editor.Dependencies`

### **Development Utilities**
Located in: `Development/`

- **TestToggle** (`TestToggle.cs`)
  - **Menu**: `Engine > Development > Toggle Test Services`
  - **Purpose**: Toggle test-related features via scripting define symbols
  - **Symbol**: `ENABLE_ENGINE_TESTS`
  - **Namespace**: `Sinkii09.Engine.Editor.Development`

- **TestServicePlayModeToggle** (`TestServicePlayModeToggle.cs`)
  - **Purpose**: Runtime controls for test services during play mode
  - **Features**: Session persistence, runtime toggling
  - **Namespace**: `Sinkii09.Engine.Editor.Development`

- **TestConfigDiscovery** (`TestConfigDiscovery.cs`)
  - **Menu**: `Engine > Test > Test Config Discovery`
  - **Purpose**: Verify configuration auto-discovery functionality
  - **Namespace**: `Sinkii09.Engine.Editor.Development`

### **Export Tools**
Located in: `Export/`

- **PackageExporter** (`PackageExporter.cs`)
  - **Menu**: `Engine > Export > Export as UnityPackage`
  - **Purpose**: Export Sinkii09 Engine as Unity package with versioning
  - **Features**: Auto-versioning, selective export, metadata inclusion
  - **Namespace**: `Sinkii09.Engine.Editor.Export`

### **Code Generators**
Located in: `Generators/`

- **UIEnumGenerator** (`UIEnumGenerator.cs`)
  - **Auto-run**: Initializes on Unity load
  - **Purpose**: Auto-generates UIScreenType enum and UIScreenRegistry
  - **Features**: Asset-driven generation, automatic updates
  - **Namespace**: `Sinkii09.Engine.Editor.Generators`

- **ScriptCreator** (`ScriptCreator.cs`)
  - **Menu**: `Assets > Create > Engine > Script`
  - **Purpose**: Create engine-specific script templates
  - **Features**: Custom templates, automatic naming
  - **Namespace**: `Sinkii09.Engine.Editor.Generators`

### **Import Tools**
Located in: `Importer/`

- **ScriptImporter** (`ScriptImporter.cs`)
  - **Type**: Asset post-processor
  - **Purpose**: Custom import handling for `.script` files
  - **Features**: Validation, auto-processing
  - **Namespace**: `Sinkii09.Engine.Editor.Importer`

- **ScriptImporterEditor** (`ScriptImporterEditor.cs`)
  - **Type**: Custom inspector
  - **Purpose**: Editor UI for script import settings
  - **Namespace**: `Sinkii09.Engine.Editor.Importer`

## 🚀 Quick Access Menu Map

### Engine Menu Structure
```
Engine/
├── Analysis/
│   └── Service Dependency Analyzer
├── Configuration/
│   ├── Create All Missing Configs
│   ├── Validate All Configs
│   └── Open Config Directory
├── Dependencies/
│   ├── Check Dependencies
│   ├── Quick Install Missing
│   └── Reset Startup Check
├── Development/
│   └── Toggle Test Services
├── Export/
│   └── Export as UnityPackage
└── Test/
    └── Test Config Discovery
```

### Assets Context Menu
```
Assets/Create/Engine/
└── Script
```

## 📋 Common Workflows

### **Setting Up New Project**
1. Dependencies are automatically checked on startup
2. Missing configurations are auto-created by `ConfigInstaller`
3. Use `Engine > Dependencies > Check Dependencies` to verify setup

### **Development Workflow**
1. Enable test services: `Engine > Development > Toggle Test Services`
2. Use analysis tools: `Engine > Analysis > Service Dependency Analyzer`
3. Validate configurations: `Engine > Configuration > Validate All Configs`

### **Release Workflow**
1. Disable test services: `Engine > Development > Toggle Test Services`
2. Export package: `Engine > Export > Export as UnityPackage`
3. Verify dependencies: `Engine > Dependencies > Check Dependencies`

## 🔧 Extension Guidelines

### **Adding New Editor Tools**

1. **Choose Appropriate Category**: Place in correct subfolder based on functionality
2. **Use Proper Namespace**: Follow pattern `Sinkii09.Engine.Editor.[Category]`
3. **Add Menu Items**: Use consistent menu structure under `Engine/[Category]/`
4. **Document Usage**: Add to this README with description and menu path
5. **Follow Patterns**: Use existing tools as templates for consistency

### **Namespace Convention**
```csharp
namespace Sinkii09.Engine.Editor.[Category]
{
    // [Category] = Analysis, Configuration, Dependencies, etc.
}
```

### **Menu Convention**
```csharp
[MenuItem("Engine/[Category]/[Tool Name]", false, priority)]
```

## 📖 Related Documentation

- **Service Architecture**: `Assets/Engine/Runtime/Scripts/Core/Services/`
- **Configuration System**: `Assets/Engine/Runtime/Resources/Configs/`
- **Engine Documentation**: `Documentation/`
- **Project Memory**: `CLAUDE.md`

## ⚡ Performance Notes

- **Auto-run Tools**: `ConfigInstaller`, `UIEnumGenerator` run on Unity startup
- **Dependency Checks**: Only run once per session unless explicitly refreshed
- **Analysis Tools**: On-demand execution to avoid performance impact
- **Development Tools**: Minimal overhead, safe for continuous use

---

*Last Updated: 2025-01-09*  
*Engine Version: 0.1.0*