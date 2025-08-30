# LocalizationService Implementation Plan

*Generated from SERVICES_ROADMAP.md analysis and existing engine architecture*

## 📋 Context Analysis

**From SERVICES_ROADMAP.md**: LocalizationService is listed as **Priority 1** with estimated effort of **2-3 weeks**.

**Key Requirements**:
- Text localization with placeholder support
- Audio localization (voice, music)
- Asset path localization for different regions
- Runtime language switching
- Localized resource management
- Font switching for different languages
- RTL text support

**Existing Infrastructure**:
- ✅ ResourceService with sophisticated asset loading
- ✅ ResourcePathResolver with **Localization category** already defined (ResourceTypes.cs:139)  
- ✅ ServiceConfiguration pattern established
- ✅ IEngineService architecture with dependency injection
- ✅ AudioService integration ready for voice localization

## 🏗️ Implementation Architecture

### **Core Service Structure**

**LocalizationService.cs**:
```csharp
[EngineService(ServiceCategory.Core, ServicePriority.High,
    Description = "Manages multi-language text, audio, and asset localization with runtime switching",
    RequiredServices = new[] { typeof(IResourceService) })]
[ServiceConfiguration(typeof(LocalizationServiceConfiguration))]
public class LocalizationService : ILocalizationService
{
    #region Dependencies
    private readonly LocalizationServiceConfiguration _config;
    private IResourceService _resourceService;
    private IUIService _uiService;
    private IAudioService _audioService;
    #endregion
    
    #region State Management
    private SystemLanguage _currentLanguage;
    private readonly Dictionary<string, LocalizedString> _cachedStrings;
    private readonly Dictionary<SystemLanguage, LocalizationData> _languageData;
    #endregion
    
    #region Events
    public event Action<SystemLanguage, SystemLanguage> OnLanguageChanged;
    public event Action<string> OnLocalizationDataLoaded;
    #endregion
}
```

**Key Features**:
1. **Text Localization** - String keys with placeholder support (`"Hello {playerName}!"`)
2. **Audio Localization** - Voice lines, music variants per language  
3. **Asset Path Localization** - Different sprites/textures per region
4. **Runtime Language Switching** - Hot-swapping without restart
5. **Font Management** - Language-specific font switching (Latin, CJK, Arabic)
6. **RTL Support** - Right-to-left text rendering

### **Configuration Design**

**LocalizationServiceConfiguration.cs**:
```csharp
[CreateAssetMenu(fileName = "LocalizationServiceConfiguration", 
    menuName = "Engine/Services/LocalizationServiceConfiguration", order = 8)]
public class LocalizationServiceConfiguration : ServiceConfigurationBase
{
    #region Language Settings
    [Header("Language Settings")]
    [SerializeField] private SystemLanguage _defaultLanguage = SystemLanguage.English;
    
    [SerializeField] private SystemLanguage[] _supportedLanguages = new[]
    {
        SystemLanguage.English,
        SystemLanguage.Japanese,
        SystemLanguage.Korean,
        SystemLanguage.ChineseSimplified,
        SystemLanguage.Spanish,
        SystemLanguage.French,
        SystemLanguage.German
    };
    
    [SerializeField] private LocalizationAsset[] _localizationAssets;
    #endregion
    
    #region Resource Paths
    [Header("Resource Paths")]  
    [SerializeField] private string _localizationDataPath = "Localization/{language}";
    [SerializeField] private string _localizedAudioPath = "Audio/{language}";
    [SerializeField] private string _localizedTexturePath = "Textures/{language}";
    [SerializeField] private string _localizedFontPath = "Fonts/{language}";
    #endregion
    
    #region Font Management
    [Header("Font Management")]
    [SerializeField] private FontMapping[] _fontMappings;
    [SerializeField] private bool _enableFontFallback = true;
    [SerializeField] private Font _fallbackFont;
    #endregion
    
    #region Performance Settings
    [Header("Performance")]
    [SerializeField, Range(100, 10000)] private int _stringCacheSize = 1000;
    [SerializeField] private bool _preloadAllLanguages = false;
    [SerializeField] private bool _enableAsyncLoading = true;
    #endregion
    
    #region RTL Support
    [Header("RTL Support")]
    [SerializeField] private bool _enableRTLSupport = true;
    [SerializeField] private SystemLanguage[] _rtlLanguages = new[]
    {
        SystemLanguage.Arabic,
        SystemLanguage.Hebrew
    };
    #endregion
}
```

### **Data Structures**

**LocalizationKey.cs**: Type-safe localization keys
```csharp
[Serializable]
public struct LocalizationKey
{
    public string Category;
    public string Key;
    public string FullKey => $"{Category}.{Key}";
    
    public static implicit operator string(LocalizationKey key) => key.FullKey;
    public static implicit operator LocalizationKey(string key) => FromString(key);
}
```

**LocalizationData.cs**: ScriptableObject for language data  
```csharp
[CreateAssetMenu(fileName = "LocalizationData", menuName = "Engine/Localization/LocalizationData")]
public class LocalizationData : ScriptableObject
{
    [SerializeField] private SystemLanguage _language;
    [SerializeField] private LocalizationEntry[] _entries;
    [SerializeField] private AudioLocalizationEntry[] _audioEntries;
    [SerializeField] private AssetLocalizationEntry[] _assetEntries;
}
```

**FontMapping.cs**: Language-to-font mapping
```csharp
[Serializable]
public class FontMapping
{
    [SerializeField] private SystemLanguage _language;
    [SerializeField] private Font _font;
    [SerializeField] private TMP_FontAsset _tmpFont;
    [SerializeField] private bool _isRTL;
}
```

**LocalizedString.cs**: Reactive localized text with auto-updates
```csharp
[Serializable]
public class LocalizedString
{
    [SerializeField] private LocalizationKey _key;
    private string _cachedValue;
    private SystemLanguage _cachedLanguage;
    
    public string Value => GetLocalizedValue();
    public event Action<string> OnValueChanged;
}
```

### **Integration Points**

**ResourceService Integration**:
- Use existing `ResourceCategory.Localization` (ResourceTypes.cs:139)
- Leverage ResourcePathResolver for language-specific paths
- Integrate with existing caching and loading systems

**AudioService Integration**:
- Extend audio loading to support localized voice files
- Add language-aware audio path resolution
- Support fallback audio (English if localized missing)

**UIService Integration**: 
- Automatic text refresh when language changes
- Font switching for UI elements
- RTL layout support

## 📁 File Structure

```
LocalizationService/
├── LocalizationService.cs              # Main service implementation
├── ILocalizationService.cs              # Service interface
├── Data/
│   ├── LocalizationKey.cs               # Type-safe key system
│   ├── LocalizationData.cs              # Language data container
│   ├── LocalizedString.cs               # Reactive localized text
│   ├── LocalizationEntry.cs             # Individual text entries
│   ├── AudioLocalizationEntry.cs        # Audio localization entries
│   ├── AssetLocalizationEntry.cs        # Asset path entries
│   └── FontMapping.cs                   # Font-language mappings
├── Providers/
│   ├── ILocalizationProvider.cs         # Data provider interface
│   ├── AssetLocalizationProvider.cs     # ScriptableObject provider
│   └── JsonLocalizationProvider.cs      # JSON file provider (optional)
├── Utils/
│   ├── LocalizationUtils.cs             # Helper utilities
│   ├── RTLTextProcessor.cs              # Right-to-left text handling
│   └── PlaceholderProcessor.cs          # Parameter substitution
└── Components/
    ├── LocalizedText.cs                 # UI component for auto-updating text
    └── LocalizedImage.cs                # UI component for localized sprites
```

**Configuration Location**:
```
Assets/Engine/Runtime/Scripts/Core/Services/Configuration/Concretes/LocalizationServiceConfiguration.cs
```

## 🔄 Implementation Phases

### **Phase 1: Core Infrastructure (Week 1)**
1. **LocalizationService class** with IEngineService implementation
   - Constructor with dependency injection
   - InitializeAsync with language detection
   - Basic string lookup functionality

2. **LocalizationServiceConfiguration** ScriptableObject
   - Language settings and supported languages
   - Resource path configuration
   - Performance settings

3. **Basic text localization** with key-value pairs
   - LocalizationKey struct for type safety
   - LocalizationData ScriptableObject
   - Simple string retrieval: `GetText(key)`

4. **ResourceService integration** for loading language data
   - Use ResourceCategory.Localization
   - Async loading with caching
   - Error handling and fallbacks

### **Phase 2: Advanced Features (Week 2)**  
1. **Runtime language switching** with event system
   - `SetLanguage(SystemLanguage)` method
   - OnLanguageChanged events
   - Hot-swapping all cached strings

2. **Audio localization integration** with AudioService
   - Localized audio path resolution
   - Voice line language switching
   - Background music variants

3. **Asset path localization** for sprites/textures  
   - Language-specific asset paths
   - Automatic sprite switching
   - Fallback to default language assets

4. **Font management and switching system**
   - FontMapping configuration
   - Automatic font switching for different languages
   - TMPro integration

### **Phase 3: Polish & Integration (Week 3)**
1. **RTL text support and processing**
   - RTLTextProcessor utility
   - Arabic/Hebrew text handling
   - Layout direction detection

2. **Placeholder/parameter support** for dynamic text
   - PlaceholderProcessor utility
   - `"Hello {playerName}!"` → `"Hello John!"`
   - Type-safe parameter passing

3. **Fallback system** for missing translations
   - Graceful degradation to default language
   - Missing key reporting and logging
   - Development-time warnings

4. **Performance optimization and caching**
   - String interning for memory efficiency
   - Lazy loading of language data
   - Cache warming strategies

5. **UI Components**
   - LocalizedText component for automatic updates
   - LocalizedImage component for sprites
   - Integration with existing UI system

6. **Comprehensive testing and documentation**
   - Unit tests for core functionality
   - Integration tests with other services
   - Performance benchmarking

## 🎯 Success Criteria

### **Functional Requirements**
✅ **Text Localization**: `LocalizationService.GetText("ui.welcome")` returns localized string  
✅ **Language Switching**: `LocalizationService.SetLanguage(SystemLanguage.Japanese)` hot-swaps all text  
✅ **Audio Localization**: Voice lines load from language-specific paths automatically  
✅ **Asset Localization**: Sprites/textures switch based on current language  
✅ **Font Management**: UI automatically uses appropriate fonts for each language  
✅ **Parameter Support**: Dynamic text with placeholders works correctly  
✅ **RTL Support**: Arabic/Hebrew text renders properly with correct layout  

### **Performance Requirements**
✅ **Language Switching**: <16ms for complete language swap  
✅ **Text Lookups**: <1ms average for cached strings  
✅ **Memory Usage**: <10MB for typical game with 5 languages  
✅ **Loading Time**: <500ms to load complete language pack  

### **Integration Requirements**
✅ **ResourceService**: Seamless integration with existing asset loading  
✅ **AudioService**: Voice localization works without code changes  
✅ **UIService**: Automatic UI text updates on language change  
✅ **SaveLoadService**: Language preference persists correctly  

## 🔧 Technical Notes

### **Dependencies**
- **Required**: IResourceService (for asset loading)
- **Optional**: IUIService (for automatic UI updates)
- **Optional**: IAudioService (for voice localization)
- **Optional**: ISaveLoadService (for language persistence)

### **CLAUDE.md Compliance**
✅ **ScriptableObject Configuration**: All settings via LocalizationServiceConfiguration  
✅ **Enum-based Type Safety**: SystemLanguage enum, no hardcoded strings  
✅ **Dependency Injection**: Constructor-based service dependencies  
✅ **Resource Integration**: Uses existing ResourceService and ResourceCategory.Localization  
✅ **Region Organization**: Proper #region usage throughout  

### **Architecture Integration**
- Follows established `[EngineService]` and `[ServiceConfiguration]` patterns
- Integrates with ServiceContainer and dependency resolution
- Uses existing ResourcePathResolver for language-specific paths
- Leverages established performance monitoring and error handling

---

**Last Updated**: January 30, 2025  
**Engine Version**: 0.1.0  
**Estimated Effort**: 2-3 weeks  
**Priority**: 1 (Core Narrative Services)  
**Dependencies**: ResourceService ✅, AudioService ✅, UIService ✅