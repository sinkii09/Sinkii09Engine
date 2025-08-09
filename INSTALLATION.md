# Installation Guide

This guide covers multiple ways to install the Sinkii09 Engine in your Unity project.

## 🚀 Method 1: Unity Package Manager (UPM) - Recommended

### From Git URL
1. Open Unity and your project
2. Go to **Window > Package Manager**
3. Click **+ (Plus)** in the top-left corner
4. Select **Add package from git URL...**
5. Enter: `https://github.com/sinkii09/engine.git`
6. Click **Add**

### From Git URL (Specific Version)
```
https://github.com/sinkii09/engine.git#v0.2.0
```

### From Local Clone
1. Clone the repository: `git clone https://github.com/sinkii09/engine.git`
2. In Package Manager, click **+ > Add package from disk...**
3. Navigate to the cloned folder and select `Assets/Engine/package.json`

## 📦 Method 2: UnityPackage Import

⚠️ **Note**: UnityPackage method requires manual dependency installation.

1. Go to [Releases](https://github.com/sinkii09/engine/releases)
2. Download the latest `Sinkii09Engine_vX.X.X.unitypackage`
3. **FIRST**: Install dependencies (see Dependencies section below)
4. In Unity: **Assets > Import Package > Custom Package...**
5. Select the downloaded `.unitypackage` file
6. Click **Import**
7. Check the included `Sinkii09Engine_vX.X.X_Dependencies.txt` file for detailed instructions

## 🔄 Method 3: Git Submodule

```bash
# Navigate to your Unity project root
cd YourUnityProject

# Add as submodule
git submodule add https://github.com/sinkii09/engine.git Assets/Engine

# Initialize and update
git submodule init
git submodule update
```

### Updating Submodule
```bash
git submodule update --remote Assets/Engine
```

## 📁 Method 4: Manual Download

1. Go to [Releases](https://github.com/sinkii09/engine/releases)
2. Download **Source code (zip)**
3. Extract the archive
4. Copy the `Assets/Engine` folder to your project's `Assets` directory

## ✅ Verify Installation

After installation, verify the engine is working:

1. Check **Window > Package Manager** shows "Sinkii09 Engine"
2. Look for `Assets/Engine` folder in your project
3. Create a test script:

```csharp
using UnityEngine;
using Sinkii09.Engine;

public class EngineTest : MonoBehaviour
{
    private async void Start()
    {
        Debug.Log("Initializing Sinkii09 Engine...");
        
        try
        {
            await Engine.InitializeAsync();
            Debug.Log("Engine initialized successfully!");
            
            // Test service access
            var resourceService = Engine.GetService<IResourceService>();
            Debug.Log($"ResourceService available: {resourceService != null}");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Engine initialization failed: {ex.Message}");
        }
    }
}
```

## 🔧 Dependencies

### Required Dependencies

**For UPM Installation (Method 1)**: These are automatically installed.  
**For UnityPackage/Manual (Methods 2-4)**: Install these manually via Package Manager:

#### Install via Package Manager Git URL:
1. **UniTask** - Modern async/await patterns  
   `https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask`

2. **ZLinq** - LINQ performance optimization  
   `https://github.com/Cysharp/ZLinq.git?path=src/ZLinq.Unity/Assets/ZLinq.Unity`

3. **R3** - Reactive Extensions for UI system  
   `https://github.com/Cysharp/R3.git?path=src/R3.Unity/Assets/R3.Unity`

#### Install via Package Manager Search:
4. **Newtonsoft.Json** - JSON serialization  
   Search: "Newtonsoft Json" or package ID: `com.unity.nuget.newtonsoft-json`

5. **Unity Addressables** - Advanced resource management  
   Search: "Addressables" or package ID: `com.unity.addressables`

6. **DOTween** - Animation and tweening system (**REQUIRED**)  
   **Unity Asset Store**: [DOTween Free](https://assetstore.unity.com/packages/tools/animation/dotween-hotween-v2-27676) or [DOTween Pro](https://assetstore.unity.com/packages/tools/animation/dotween-pro-32416)

### Manual Installation Steps:
1. Open **Window > Package Manager**
2. Click **+ (Plus)** > **Add package from git URL**
3. Paste each Git URL above (one at a time)
4. For Newtonsoft.Json and Addressables, use the search function
5. **For DOTween**: Go to Unity Asset Store and purchase/download DOTween Free or Pro

### Optional Dependencies (Auto-detected)
- **Odin Inspector** - Advanced inspector features (Unity Asset Store)

## ⚙️ Unity Requirements

- **Unity Version**: 2022.3+ (Unity 6 recommended)
- **.NET Framework**: .NET Standard 2.1 or higher
- **Scripting Backend**: Mono or IL2CPP
- **API Compatibility**: .NET Standard 2.1

## 🚨 Troubleshooting

### Common Issues

#### "Package not found" Error
- Ensure you have internet connection
- Check the Git URL is correct
- Try adding with specific version tag

#### Assembly Reference Errors  
- Check Unity version compatibility (2022.3+)
- Verify .NET Standard 2.1 is selected
- Restart Unity after installation

#### Dependencies Not Installing
- Manually add dependencies via Package Manager:
  - `com.cysharp.unitask`
  - `com.unity.nuget.newtonsoft-json` 
  - `com.unity.addressables`

#### Git Authentication Issues
If using private repositories:
```bash
# Use SSH instead of HTTPS
https://github.com/sinkii09/engine.git
# becomes
git@github.com:sinkii09/engine.git
```

### Getting Help

- **Documentation**: Check the [README](README.md) and [Wiki](https://github.com/sinkii09/engine/wiki)
- **Issues**: Report problems on [GitHub Issues](https://github.com/sinkii09/engine/issues)
- **Discussions**: Ask questions in [GitHub Discussions](https://github.com/sinkii09/engine/discussions)

## 🔄 Updating

### UPM Packages
1. Go to **Window > Package Manager**
2. Find "Sinkii09 Engine" in the list
3. Click **Update** if available

### Manual Installations
1. Remove the old `Assets/Engine` folder
2. Follow installation steps again with the new version
3. Check [CHANGELOG.md](CHANGELOG.md) for breaking changes

## 🗑️ Uninstallation

### UPM Installation
1. Go to **Window > Package Manager**  
2. Find "Sinkii09 Engine" in **In Project**
3. Click **Remove**

### Manual Installation  
1. Delete the `Assets/Engine` folder
2. Delete any engine-related configuration files

---

**Need help?** Check our [Troubleshooting Guide](Documentation/TROUBLESHOOTING.md) or open an [issue](https://github.com/sinkii09/engine/issues).