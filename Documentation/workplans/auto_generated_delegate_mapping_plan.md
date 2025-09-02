# Step-by-Step Auto-Generated Delegate Mapping System Implementation Plan

## Step 1: Create Roslyn-based Input Actions Analyzer
**File**: `Assets/Engine/Editor/InputSystem/InputActionsAnalyzer.cs`
- Add Microsoft.CodeAnalysis NuGet package references to Unity project
- Create `InputActionsInfo`, `ActionMapInfo`, `ActionInfo` data classes
- Implement Roslyn syntax tree parsing to extract:
  - Action map structs (`PlayerActions`, `UIActions`)
  - InputAction properties within each struct
  - Property names and return types (Vector2, float, bool)
- Use Unity AssetDatabase to auto-discover `InputSystem_Actions.cs` MonoScript
- Parse source code using `CSharpSyntaxTree.ParseText(monoScript.text)`

## Step 2: Create Code Generator for Delegate Cache
**File**: `Assets/Engine/Editor/InputSystem/InputActionCodeGenerator.cs`
- Create template-based code generator using StringBuilder
- Generate optimized enum definitions from discovered actions:
  ```csharp
  public enum PlayerAction { Move = 0, Look = 1, Attack = 2, ... }
  public enum UIAction { Navigate = 0, Submit = 1, Cancel = 2, ... }
  ```
- Generate delegate dictionaries:
  ```csharp
  private static readonly Dictionary<PlayerAction, Func<bool>> PlayerPressedDelegates = new();
  private static readonly Dictionary<PlayerAction, Func<Vector2>> PlayerVector2Delegates = new();
  ```
- Generate initialization methods that map enums to Unity's generated action references
- Output to: `Assets/Engine/Runtime/Scripts/Core/Services/Implemented/InputService/Generated/InputActionDelegateCache.cs`

## Step 3: Create Editor Menu Integration
**File**: `Assets/Engine/Editor/InputSystem/InputSystemCodeGeneratorMenu.cs`
- Add Unity MenuItem: `"Engine/Input System/Generate Action Cache"`
- Integrate analyzer + generator workflow
- Add automatic regeneration on InputSystem_Actions.cs changes using AssetPostprocessor
- Add validation to ensure InputSystem_Actions.cs exists before generation
- Display generation results in Unity Console

## Step 4: Update Existing Enum Mappings
**Files**: 
- Update `InputActionEnums.cs` to use generated enums instead of hardcoded ones
- Remove manual `PlayerActionMap` and `UIActionMap` dictionaries
- Keep enum definitions as fallback for compatibility during transition

## Step 5: Integrate Generated Cache with InputService
**File**: `Assets/Engine/Runtime/Scripts/Core/Services/Implemented/InputService/InputService.cs`
- Add dependency injection for `InputSystem_Actions` instance
- Initialize `InputActionDelegateCache` in service `Initialize()` method
- Update all `IsActionPressed()`, `IsActionTriggered()`, `GetVector2Value()` methods to use delegate cache instead of string lookups
- Add performance logging to verify delegate cache performance improvements

## Step 6: Add Automatic Regeneration System
**File**: `Assets/Engine/Editor/InputSystem/InputActionsAssetWatcher.cs`
- Create AssetPostprocessor to watch for changes to `InputSystem_Actions.inputactions`
- Trigger automatic code regeneration when input actions asset is modified
- Add safeguards to prevent infinite regeneration loops
- Display regeneration notifications in Unity Console

## Step 7: Testing and Validation
- Create unit tests for generated delegate cache performance vs original string mapping
- Test enum-to-delegate mapping accuracy for all discovered actions
- Verify IL2CPP compatibility (no runtime reflection)
- Benchmark performance improvements (should be 10-50x faster than Dictionary<enum,string> lookups)
- Test automatic regeneration workflow

## Step 8: Documentation and Integration
- Update InputService implementation documentation
- Add code generation workflow to engine development guide
- Create example usage showing the seamless enum-based API with optimized performance
- Mark existing string-based mappings as deprecated

## Key Technical Decisions

1. **Use MonoScript.text for source access** (no hardcoded paths)
2. **Dictionary<Enum, Func<T>> for delegate mapping** (clean enum-based API)
3. **Template-based code generation** (maintainable and extensible)  
4. **AssetPostprocessor for automatic regeneration** (keeps mappings in sync)
5. **Roslyn for robust parsing** (handles complex C# syntax correctly)

## Expected Outcome

- **10-50x performance improvement** over enum-to-string-to-action lookups
- **Zero maintenance** - automatically stays in sync with InputActions changes
- **Type-safe enum API** - maintains clean developer experience
- **IL2CPP compatible** - no runtime reflection or string operations
- **Future-proof** - works with any InputActions configuration Unity generates