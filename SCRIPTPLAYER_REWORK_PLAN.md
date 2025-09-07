# 📋 ScriptPlayer Service Rework Plan - Complete Implementation Guide

## 📊 **Current Status Overview**
- **Phase 1**: ✅ **COMPLETE** - Critical Memory Leak Fixes (100%)
- **Phase 2**: ✅ **COMPLETE** - Resource Management Improvements (100%)  
- **Phase 3**: 🔵 **PENDING** - TODO Items Implementation (0%)
- **QA & Documentation**: 🔵 **PENDING** - Testing and Documentation (0%)

**Overall Progress: 6/8 phases complete (75%)**

---

## ✅ **Phase 1: COMPLETE - Critical Memory Leak Fixes**

### **Implemented Components:**
- **ResourcePreloader**: Simple IDisposable with null event assignment
- **ScriptExecutionEngine**: Simple IDisposable with null event assignment  
- **TimeoutManager**: Full IDisposable with CancellationToken cleanup
- **PlaybackStateManager**: Simple IDisposable with null event assignment
- **ScriptPlayerService**: Updated shutdown to dispose all components

### **Key Achievements:**
- ✅ All event subscription memory leaks eliminated
- ✅ Clean disposal patterns using simple null assignment
- ✅ Thread-safe component disposal chains
- ✅ ~100+ lines of complex event handling code removed

---

## ✅ **Phase 2: COMPLETE - Resource Management Improvements**

### **Implemented Components:**
- **CommandResultPool**: Full IDisposable with disposal safety and exception handling
- **ScriptExecutionContext**: Complete IDisposable pattern with finalizer safety

### **Key Achievements:**
- ✅ Finalizer protection for CancellationTokenSource disposal
- ✅ Robust exception handling in all disposal operations
- ✅ Fallback behavior for disposed objects
- ✅ Comprehensive pool cleanup with safety checks

---

## 🔵 **Phase 3: PENDING - TODO Items Implementation**

### **3.1 ProcessTextContentAsync - Text Content Processing Logic**
**Location**: `ScriptPlayerService.cs:1100`

**Current State:**
```csharp
// TODO: Implement text content processing logic
private async UniTask<CommandResult> ProcessTextContentAsync(
    TextScriptLine textLine,
    CancellationToken cancellationToken = default)
{
    await UniTask.Yield(cancellationToken);
    return CommandResult.Success();
}
```

**Implementation Requirements:**
```csharp
private async UniTask<CommandResult> ProcessTextContentAsync(
    TextScriptLine textLine,
    CancellationToken cancellationToken = default)
{
    try
    {
        // 1. Extract text content and metadata
        var textContent = textLine.Text ?? string.Empty;
        var characterName = textLine.CharacterName;
        var displayOptions = textLine.DisplayOptions;
        
        // 2. Apply text processing (localization, variables, etc.)
        var processedText = await ProcessTextVariables(textContent, cancellationToken);
        
        // 3. Integrate with dialog system (if available)
        if (_config.EnableDialogSystem)
        {
            await DisplayTextInDialogSystem(characterName, processedText, displayOptions, cancellationToken);
        }
        
        // 4. Handle text display timing and auto-advance
        if (displayOptions?.AutoAdvanceDelay > 0)
        {
            await UniTask.Delay((int)(displayOptions.AutoAdvanceDelay * 1000), cancellationToken);
        }
        
        // 5. Wait for user input if required
        if (!displayOptions?.AutoAdvance ?? true)
        {
            await WaitForUserInput(cancellationToken);
        }
        
        return CommandResult.Success();
    }
    catch (OperationCanceledException)
    {
        return CommandResult.Stop("Text processing cancelled");
    }
    catch (Exception ex)
    {
        return CommandResult.Failed($"Text processing failed: {ex.Message}", ex);
    }
}

// Helper methods to implement:
private async UniTask<string> ProcessTextVariables(string text, CancellationToken cancellationToken)
private async UniTask DisplayTextInDialogSystem(string character, string text, TextDisplayOptions options, CancellationToken cancellationToken)
private async UniTask WaitForUserInput(CancellationToken cancellationToken)
```

**Dependencies:**
- Dialog system integration
- Text variable processing system
- User input handling system
- Localization support (optional)

**Priority**: **HIGH** - Core functionality for script text display

---

### **3.2 HandleFlowControl - Script Calling Implementation**
**Location**: `ScriptPlayerService.cs:1327`

**Current State:**
```csharp
case FlowControlAction.CallScript:
    // TODO: Implement script calling logic
    Debug.LogWarning("[ScriptPlayer] Script calling not yet implemented");
    return CommandResult.Success();
```

**Implementation Requirements:**
```csharp
case FlowControlAction.CallScript:
    try
    {
        // 1. Extract script call parameters
        var targetScriptName = result.TargetLabel; // Reuse TargetLabel for script name
        var returnLine = _executionContext.CurrentLineIndex + 1;
        
        // 2. Validate script exists and can be loaded
        var targetScript = await LoadScriptAsync(targetScriptName, cancellationToken);
        if (targetScript == null)
        {
            return CommandResult.Failed($"Script '{targetScriptName}' not found or failed to load");
        }
        
        // 3. Save current execution state to call stack
        var currentFrame = new ScriptExecutionFrame
        {
            Script = _executionContext.Script,
            ReturnLineIndex = returnLine,
            LocalVariables = new Dictionary<string, object>(_executionContext.Variables),
            CallTimestamp = DateTime.Now
        };
        _executionContext.PushFrame(currentFrame);
        
        // 4. Switch to new script execution
        _executionContext.Script = targetScript;
        _executionContext.CurrentLineIndex = 0;
        
        // 5. Initialize new script context
        await InitializeScriptLabels(targetScript);
        
        if (_config.LogExecutionFlow)
        {
            Debug.Log($"[ScriptPlayer] Called script '{targetScriptName}' from line {returnLine - 1}");
        }
        
        return CommandResult.Success();
    }
    catch (Exception ex)
    {
        return CommandResult.Failed($"Script call failed: {ex.Message}", ex);
    }
```

**Additional Methods to Implement:**
```csharp
// Script loading and management
private async UniTask<Script> LoadScriptAsync(string scriptName, CancellationToken cancellationToken)
{
    // Use existing ResourceService to load script
    // Handle script caching and validation
}

// Script return handling (when called script ends)
private CommandResult HandleScriptReturn()
{
    var frame = _executionContext.PopFrame();
    if (frame != null)
    {
        // Restore previous script context
        _executionContext.Script = frame.Script;
        _executionContext.CurrentLineIndex = frame.ReturnLineIndex;
        
        // Optionally merge variables
        MergeScriptVariables(frame.LocalVariables);
        
        return CommandResult.Success();
    }
    
    // No more frames - script execution complete
    return CommandResult.Stop("Script execution completed");
}

private void MergeScriptVariables(Dictionary<string, object> frameVariables)
{
    // Merge logic for script variables (keep, overwrite, or selective merge)
}
```

**Dependencies:**
- ResourceService for script loading
- Script parsing and validation
- Call stack management (already exists in ScriptExecutionContext)
- Variable scoping rules definition

**Priority**: **MEDIUM** - Important for complex script workflows

---

### **3.3 GetLineText - Original Text Storage**
**Location**: `ScriptPlayerService.cs:1119-1122`

**Current State:**
```csharp
private string GetLineText(IScriptLine line)
{
    // TODO: Store original text in ScriptLine for better debugging
    return line?.ToString() ?? "Unknown line";
}
```

**Implementation Requirements:**

**Step 1: Enhance ScriptLine Interface**
```csharp
// Add to IScriptLine interface
public interface IScriptLine
{
    // Existing properties...
    
    /// <summary>
    /// Original text content as it appeared in the script file
    /// </summary>
    string OriginalText { get; set; }
}
```

**Step 2: Update Script Parsing**
```csharp
// In script parser/loader, store original text
private void ParseScriptLine(string rawLine, int lineNumber)
{
    var scriptLine = CreateScriptLine(rawLine); // Existing logic
    scriptLine.OriginalText = rawLine; // Store original text
    
    // Continue with existing parsing logic...
}
```

**Step 3: Implement GetLineText Method**
```csharp
private string GetLineText(IScriptLine line)
{
    if (line == null)
        return "null";
        
    // Return original text if available, otherwise fall back to ToString()
    if (!string.IsNullOrEmpty(line.OriginalText))
    {
        return line.OriginalText;
    }
    
    // Fallback for backward compatibility
    return line.ToString() ?? $"{line.GetType().Name} (no text)";
}
```

**Step 4: Enhanced Debugging Support**
```csharp
private string GetDetailedLineInfo(IScriptLine line, int lineIndex)
{
    var lineText = GetLineText(line);
    var lineType = line.GetType().Name;
    
    return $"Line {lineIndex + 1} [{lineType}]: {lineText}";
}
```

**Dependencies:**
- Script parsing system updates
- IScriptLine interface modification
- Backward compatibility considerations

**Priority**: **LOW** - Quality of life improvement for debugging

---

## 🔍 **QA & Documentation Phase**

### **4.1 Stress Testing and Validation**
**Objectives:**
- Memory leak testing with long-running scripts
- Performance testing with frequent start/stop cycles
- Exception handling validation
- Thread safety testing
- Resource disposal verification

**Test Scenarios:**
```csharp
// Memory leak test
for (int i = 0; i < 1000; i++)
{
    await scriptPlayer.PlayScriptAsync(testScript);
    await scriptPlayer.StopAsync();
    // Check memory usage and GC pressure
}

// Exception handling test  
// Test disposal chain robustness
// Test concurrent access patterns
```

### **4.2 Documentation and Best Practices**
**Deliverables:**
- Disposal pattern guidelines for future components
- Memory management best practices
- Performance optimization recommendations
- Error handling patterns
- Testing strategies documentation

---

## 🎯 **Implementation Priority Order**

1. **HIGH**: ProcessTextContentAsync (Core functionality)
2. **MEDIUM**: HandleFlowControl script calling (Advanced workflows)  
3. **LOW**: GetLineText original text storage (Debugging QoL)
4. **ONGOING**: QA testing and documentation

## 📈 **Success Criteria for Phase 3**

- ✅ Text content properly displays and processes
- ✅ Script calling and returns work correctly
- ✅ Call stack management functions properly
- ✅ Original text available for debugging
- ✅ All TODO comments resolved
- ✅ No regression in existing functionality
- ✅ Performance maintained or improved

---

**Total Estimated Work**: 3-5 days for complete Phase 3 implementation
**Dependencies**: Dialog system, ResourceService, Script parsing system
**Risk Level**: Medium (requires integration with multiple systems)