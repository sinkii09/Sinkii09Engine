# Unified Editor Menu System

A type-safe, extensible menu system for Sinkii09 Engine editor tools using enums for consistency and maintainability.

## 🎯 Overview

The unified menu system provides:
- **Type Safety**: Enum-based categories and priorities
- **Consistency**: Standardized menu paths and ordering  
- **Extensibility**: Easy to add new categories and items
- **Maintainability**: Centralized configuration and validation

## 📁 Menu Structure

```
Engine/
├── 📋 Setup (0-99)                 # Essential setup & configuration
│   ├── Dependencies/ (0-9)         # Package dependency management
│   ├── Services/ (10-19)          # Service setup and configuration  
│   └── Configurations/ (20-29)     # Configuration management
├── 🔧 Tools (100-199)             # Daily development tools
│   ├── Scripts/ (110-119)         # Script manipulation tools
│   ├── UI/ (120-129)              # UI development tools
│   └── Assets/ (130-139)          # Asset processing tools
├── 📊 Analysis (200-299)          # Code analysis & debugging
│   ├── Services/ (210-219)        # Service dependency analysis
│   ├── Dependencies/ (220-229)    # Dependency analysis
│   └── Performance/ (230-239)     # Performance profiling
├── ⚡ Generators (300-399)        # Code & asset generation
│   ├── Code/ (310-319)           # Code generation tools
│   ├── Assets/ (320-329)         # Asset generation
│   └── UI/ (330-339)             # UI generation
├── ⚙️ Configuration (400-499)     # Configuration management
│   ├── Creation/ (410-419)       # Create configurations
│   ├── Validation/ (420-429)     # Validate configurations
│   └── Management/ (430-439)     # Manage existing configs
├── 🔬 Development (500-599)       # Development utilities
│   ├── Testing/ (510-519)        # Testing tools and utilities
│   ├── Debugging/ (520-529)      # Debugging helpers
│   └── Utilities/ (530-539)      # General dev utilities
├── 📦 Export (600-699)           # Export & packaging
│   ├── Package/ (610-619)        # Unity package export
│   └── Project/ (620-629)        # Project export tools
└── ❓ Help (900-999)              # Help & documentation
    ├── Documentation/ (910-919)   # Documentation tools
    └── About/ (990-999)          # About and version info
```

## 🔧 Usage

### Basic Menu Item Creation

```csharp
using Sinkii09.Engine.Editor.Core;

public class MyEditorTool
{
    // Simple menu item
    [MenuItem("Engine/Tools/Scripts/My Tool", false, 110)]
    public static void MyTool()
    {
        // Tool implementation
    }
    
    // Using enum system for type safety
    [MenuItem("Engine/Analysis/Services/My Analyzer", false, 210)]
    public static void MyAnalyzer()
    {
        // Analyzer implementation  
    }
}
```

### Using the Menu System Enums

```csharp
using Sinkii09.Engine.Editor.Core;

public class MyEditorWindow : EditorWindow
{
    // Generate menu path using enums
    private static readonly string MenuPath = EditorMenuSystem.GetMenuPath(
        EditorMenuSystem.MenuCategory.Tools, 
        EditorMenuSystem.MenuSubcategory.Scripts, 
        "My Window"
    );
    
    private static readonly int MenuPriority = EditorMenuSystem.GetPriority(
        EditorMenuSystem.MenuCategory.Tools,
        EditorMenuSystem.MenuSubcategory.Scripts,
        5 // offset
    );
    
    [MenuItem("Engine/Tools/Scripts/My Window", false, 115)] // 110 + 5
    public static void ShowWindow()
    {
        GetWindow<MyEditorWindow>("My Window");
    }
}
```

### Menu Helper Utilities

```csharp
using Sinkii09.Engine.Editor.Core;

public class MyTools
{
    // Quick path generation
    [MenuItem(EditorMenuHelper.QuickPaths.ToolsScripts("Script Processor"), false, 111)]
    public static void ProcessScripts() { }
    
    [MenuItem(EditorMenuHelper.QuickPaths.AnalysisServices("Service Report"), false, 211)]
    public static void GenerateServiceReport() { }
}
```

## 📊 Priority System

### Priority Ranges
- **Setup**: 0-99 (essential first-time setup)
- **Tools**: 100-199 (daily development tools)
- **Analysis**: 200-299 (analysis and debugging)
- **Generators**: 300-399 (code/asset generation)
- **Configuration**: 400-499 (config management)
- **Development**: 500-599 (dev utilities)
- **Export**: 600-699 (export and packaging)
- **Help**: 900-999 (help and documentation)

### Subcategory Spacing
- Use 10-point gaps between subcategories
- Sequential numbering within subcategories
- Leave room for future expansion

### Example Priority Calculation
```csharp
// For "Engine/Analysis/Services/My Tool"
var priority = (int)MenuCategory.Analysis +        // 200
               (int)MenuSubcategory.ServiceAnalysis + // 10  
               5;                                   // offset
// Result: 215
```

## 🎨 Menu Item Guidelines

### Naming Conventions
- **Be Descriptive**: "Export as UnityPackage" not "Export"
- **Be Consistent**: Use similar terms for similar actions
- **Be Concise**: Avoid overly long menu item names
- **Use Action Verbs**: "Generate", "Validate", "Export", "Analyze"

### Menu Depth
- **Maximum 3 levels**: Engine/Category/Subcategory/Item
- **Avoid deeper nesting**: Creates complex navigation
- **Group related items**: Use subcategories effectively

### Validation Menu Items
For items that can be enabled/disabled, use validation:

```csharp
[MenuItem("Engine/Development/Testing/Run Tests", false, 510)]
public static void RunTests() { /* ... */ }

[MenuItem("Engine/Development/Testing/Run Tests", true)]
public static bool ValidateRunTests() 
{
    return EditorApplication.isPlaying; // Only available in play mode
}
```

## 🔍 Built-in Tools

### Menu Validation
```csharp
// Check menu structure
EditorMenuHelper.ValidateMenuStructure();

// Show guidelines
EditorMenuHelper.ShowMenuGuidelines();
```

### Available Tools by Category

#### Setup (0-99)
- **Dependencies** (0-9): Package management, dependency checking
- **Services** (10-19): Service setup and configuration
- **Configurations** (20-29): Configuration management

#### Tools (100-199) 
- **Scripts** (110-119): Script creation, processing
- **UI** (120-129): UI development tools  
- **Assets** (130-139): Asset processing

#### Analysis (200-299)
- **Services** (210-219): Service dependency analysis
- **Dependencies** (220-229): Package dependency analysis
- **Performance** (230-239): Performance profiling

#### Export (600-699)
- **Package** (610-619): Unity package export tools
- **Project** (620-629): Project export and release tools

## 🚀 Adding New Menu Items

### Step 1: Choose Category and Subcategory
```csharp
// Determine the best category for your tool
var category = EditorMenuSystem.MenuCategory.Tools;
var subcategory = EditorMenuSystem.MenuSubcategory.Scripts;
```

### Step 2: Calculate Priority
```csharp
// Get base priority + offset for ordering
var priority = EditorMenuSystem.GetPriority(category, subcategory, 5);
```

### Step 3: Create Menu Item
```csharp
[MenuItem("Engine/Tools/Scripts/My New Tool", false, 115)]
public static void MyNewTool()
{
    // Implementation
}
```

### Step 4: Add Validation (if needed)
```csharp
[MenuItem("Engine/Tools/Scripts/My New Tool", true)]
public static bool ValidateMyNewTool()
{
    return /* validation logic */;
}
```

## 🔧 Migration Guide

### Converting Existing Menu Items

**Old Style:**
```csharp
[MenuItem("Engine/Dependencies/Check Dependencies")]
public static void CheckDependencies() { }
```

**New Style:**
```csharp
[MenuItem("Engine/Setup/Dependencies/Check Dependencies", false, 10)]
public static void CheckDependencies() { }
```

### Migration Steps

1. **Add namespace**: `using Sinkii09.Engine.Editor.Core;`
2. **Update menu path**: Use new category structure
3. **Add priority**: Use enum-based priority system
4. **Test menu ordering**: Verify items appear in correct order

## 🎯 Best Practices

### DO ✅
- Use enum-based categories and subcategories
- Include priority values for consistent ordering
- Group related functionality in subcategories  
- Follow naming conventions
- Add validation for conditional menu items
- Use descriptive, action-oriented names

### DON'T ❌
- Hard-code menu paths without using the system
- Create menus deeper than 3 levels
- Use inconsistent naming patterns
- Forget to add priorities
- Create overlapping priority ranges
- Mix different types of functionality in same category

## 🔍 Validation Tools

The menu system includes built-in validation:

- **Structure Validation**: Check menu hierarchy
- **Priority Conflicts**: Detect overlapping priorities  
- **Naming Validation**: Verify naming conventions
- **Coverage Reports**: See all registered menu items

Access via: `Engine > Help > Documentation > Validate Menu Structure`

---

*For additional help, see: `Engine > Help > Documentation > Menu Guidelines`*