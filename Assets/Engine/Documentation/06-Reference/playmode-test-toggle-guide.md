# Play Mode Test Service Toggle Guide

## Overview

You can now toggle test services **during Unity Editor play mode** without stopping and restarting. This provides maximum flexibility for development and testing.

## 🎮 **Play Mode Toggle Methods**

### Method 1: Unity Menu (Quick Toggle)

```
Engine > Development > Runtime > Toggle Test Services
```

- ✅ **Instant toggle** during play mode
- ✅ **Checkmark shows current state**
- ⚠️ **Requires engine restart** to take effect

### Method 2: Test Service Manager Window (GUI)

```
Engine > Development > Test Service Manager
```

Opens a dedicated window with:
- 🔘 **Toggle switch** for runtime test services
- 📊 **Status display** showing current state
- 🔄 **Quick restart buttons** 
- 🛠️ **Advanced options** for power users

### Method 3: Direct Menu Actions

```
Engine > Development > Runtime > Restart Engine with Test Services
Engine > Development > Runtime > Restart Engine without Test Services
Engine > Development > Runtime > Show Test Service Status
```

### Method 4: Code/Script Access

```csharp
// Toggle test services programmatically
#if UNITY_EDITOR
using Sinkii09.Engine.Editor;

// Enable test services
TestServicePlayModeToggle.RuntimeTestServicesEnabled = true;

// Check current state
bool isEnabled = TestServicePlayModeToggle.RuntimeTestServicesEnabled;

// Restart engine with new settings
TestServicePlayModeToggle.RestartEngineWithTestServices();
#endif
```

## 🔄 **Workflow Examples**

### Example 1: Testing with Mock Data

```csharp
// 1. Start play mode (test services disabled by default)
// 2. Toggle test services via menu: Engine > Development > Runtime > Toggle Test Services
// 3. Restart engine: Engine > Development > Runtime > Restart Engine with Test Services
// 4. Now MockDataService and other test services are active
// 5. Test your feature with mock data
// 6. Toggle off when done testing
```

### Example 2: Performance Comparison

```csharp
// 1. Start play mode with test services disabled
// 2. Measure performance baseline
// 3. Enable test services and restart
// 4. Measure performance with test services
// 5. Compare results
```

### Example 3: Integration Testing

```csharp
// 1. Enable test services
// 2. Test interaction between real and mock services
// 3. Verify behavior with different service combinations
// 4. Disable test services to test production-like environment
```

## 📊 **Status Monitoring**

### Visual Indicators

- **🟢 Menu Checkmark**: Test services enabled
- **🔴 No Checkmark**: Test services disabled
- **Console Logs**: Show current state and changes

### Status Window Information

The Test Service Manager window shows:
```
Runtime Test Services: [✓] Enabled / [ ] Disabled
Engine State: Playing / Stopped
Should Include Tests: True / False
Current Status: "Test services included: Runtime toggle enabled in play mode"
```

### Console Output Examples

```
🟢 Runtime Test Services: ENABLED
⚠️ Test service changes require engine restart to take effect
🔄 Restarting Engine with new test service settings...
🧪 Test services included: Runtime toggle enabled in play mode
Skipping test service: MockHighPriorityService (Category: Test)
Service discovery completed. Processed 150 types, registered 5 services, skipped 3 test services.
```

## ⚙️ **Technical Details**

### State Persistence

- **Session-based**: Settings persist during Unity session
- **Play mode safe**: Survives play mode stops/starts
- **Editor restart resets**: Settings reset when Unity restarts

### Implementation Details

```csharp
// Runtime state stored in Unity SessionState
SessionState.SetBool("EngineTestServices_RuntimeEnabled", value);

// Runtime detection uses reflection to avoid assembly dependencies
private static bool GetRuntimeTestServiceState()
{
    // Uses reflection to access UnityEditor.SessionState from runtime code
    // This avoids circular dependencies between runtime and editor assemblies
}
```

### Safety Mechanisms

1. **Production Protection**: Runtime toggles only work in Unity Editor
2. **Assembly Protection**: Test assembly still excluded from builds
3. **Fallback Safety**: If reflection fails, defaults to disabled
4. **Clear Messaging**: Always shows restart requirements

## 🛠️ **Advanced Usage**

### Custom Test Service Behavior

```csharp
[EngineService(ServiceCategory.Test)]
public class DevelopmentOnlyService : IEngineService
{
    public async UniTask<ServiceInitializationResult> InitializeAsync(
        IServiceProvider provider, CancellationToken cancellationToken = default)
    {
        // This service only runs when test services are enabled
        Debug.Log("Development service active - mock data available");
        return ServiceInitializationResult.Success();
    }
}
```

### Conditional Code Execution

```csharp
public class MyGameService : IEngineService
{
    public async UniTask<ServiceInitializationResult> InitializeAsync(
        IServiceProvider provider, CancellationToken cancellationToken = default)
    {
        if (ServiceTestUtils.ShouldIncludeTestServices())
        {
            Debug.Log("Running with test services enabled");
            // Use mock data or test configurations
        }
        else
        {
            Debug.Log("Running in production mode");
            // Use real data and production configurations
        }
        
        return ServiceInitializationResult.Success();
    }
}
```

### Automated Testing Workflows

```csharp
#if UNITY_EDITOR
[UnityTest]
public IEnumerator TestServiceToggling()
{
    // Ensure test services are disabled
    TestServicePlayModeToggle.RuntimeTestServicesEnabled = false;
    
    // Start engine
    yield return StartEngine();
    
    // Verify no test services
    Assert.IsFalse(ServiceTestUtils.ShouldIncludeTestServices());
    
    // Enable test services and restart
    TestServicePlayModeToggle.RuntimeTestServicesEnabled = true;
    yield return RestartEngine();
    
    // Verify test services are now active
    Assert.IsTrue(ServiceTestUtils.ShouldIncludeTestServices());
}
#endif
```

## 🎯 **Best Practices**

### ✅ **Do:**
- Use runtime toggles for quick development iterations
- Check status before making assumptions about test services
- Use the Test Service Manager window for comprehensive control
- Test both enabled/disabled states during development

### ❌ **Don't:**
- Rely on runtime toggles in production builds (they don't exist)
- Forget to restart engine after toggle changes
- Assume test services are always available
- Leave test services enabled in final testing

### 🔧 **Recommended Workflow:**

1. **Start development** with test services disabled (default)
2. **Enable when needed** for specific testing scenarios  
3. **Use quick restart** buttons for efficiency
4. **Monitor status** via manager window or console
5. **Disable for final testing** to ensure production-like behavior

This gives you **maximum flexibility** for development while maintaining **production safety**! 🚀