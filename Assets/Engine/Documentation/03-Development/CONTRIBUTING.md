# Contributing to Sinkii09 Engine

Thank you for your interest in contributing to the Sinkii09 Engine! This document provides guidelines and information for contributors.

## 📋 Table of Contents

1. [Code of Conduct](#code-of-conduct)
2. [Getting Started](#getting-started)
3. [Development Workflow](#development-workflow)
4. [Coding Standards](#coding-standards)
5. [Testing Guidelines](#testing-guidelines)
6. [Documentation Requirements](#documentation-requirements)
7. [Pull Request Process](#pull-request-process)
8. [Issue Reporting](#issue-reporting)
9. [Architecture Guidelines](#architecture-guidelines)
10. [Performance Considerations](#performance-considerations)

---

## 📜 Code of Conduct

### Our Pledge
We are committed to providing a welcoming and inclusive environment for all contributors, regardless of background, identity, or experience level.

### Expected Behavior
- Use welcoming and inclusive language
- Respect differing viewpoints and experiences
- Accept constructive criticism gracefully
- Focus on what is best for the community
- Show empathy towards other community members

### Unacceptable Behavior
- Harassment, discrimination, or offensive comments
- Personal attacks or trolling
- Publishing private information without consent
- Any conduct that would be inappropriate in a professional setting

---

## 🚀 Getting Started

### Prerequisites
- **Unity 2021.3 LTS** or newer
- **Git** for version control
- **Visual Studio 2022** or **JetBrains Rider** (recommended)
- **.NET 6.0 SDK** or newer

### Development Environment Setup

1. **Clone the Repository**
   ```bash
   git clone https://github.com/Sinkii09/Engine.git
   cd Engine
   ```

2. **Open in Unity**
   ```bash
   # Open Unity Hub and add the project folder
   # Ensure Unity 2021.3 LTS is installed
   ```

3. **Install Dependencies**
   ```bash
   # Dependencies are managed through Unity Package Manager
   # They should install automatically when opening the project
   ```

4. **Verify Setup**
   ```bash
   # Run the test suite to ensure everything works
   # Window → General → Test Runner → Run All
   ```

### Project Structure Understanding
```
Assets/Engine/
├── Runtime/Scripts/Core/     # Core engine systems
├── Editor/                   # Unity Editor tools
├── Tests/                    # Unit and integration tests
├── Resources/               # Runtime resources
└── Documentation/           # Additional docs
```

---

## 🔄 Development Workflow

### Branching Strategy
We follow a **GitFlow-inspired** workflow:

- **`main`** - Production-ready code
- **`develop`** - Development integration branch
- **`feature/feature-name`** - Feature development
- **`bugfix/issue-description`** - Bug fixes
- **`hotfix/critical-fix`** - Emergency fixes
- **`release/version-number`** - Release preparation

### Workflow Steps

1. **Create Feature Branch**
   ```bash
   git checkout develop
   git pull origin develop
   git checkout -b feature/enhanced-actor-system
   ```

2. **Develop and Test**
   ```bash
   # Make your changes
   # Write tests for new functionality
   # Ensure all tests pass
   ```

3. **Commit Changes**
   ```bash
   git add .
   git commit -m "feat: add actor state persistence system

   - Implement IStatefulActor interface
   - Add actor state serialization
   - Create state restoration methods
   - Add comprehensive unit tests
   
   Closes #123"
   ```

4. **Push and Create Pull Request**
   ```bash
   git push origin feature/enhanced-actor-system
   # Create PR through GitHub interface
   ```

### Commit Message Format
We follow [Conventional Commits](https://www.conventionalcommits.org/):

```
<type>[optional scope]: <description>

[optional body]

[optional footer(s)]
```

**Types:**
- `feat:` - New feature
- `fix:` - Bug fix
- `docs:` - Documentation changes
- `style:` - Code style changes (formatting, etc.)
- `refactor:` - Code refactoring
- `test:` - Adding or updating tests
- `chore:` - Maintenance tasks

**Examples:**
```bash
feat(actor): add character animation system
fix(resource): resolve memory leak in provider
docs: update API documentation for command system
test(service): add integration tests for service locator
```

---

## 💻 Coding Standards

### C# Coding Style

**Naming Conventions:**
```csharp
// Classes: PascalCase
public class ServiceManager { }

// Interfaces: IPascalCase
public interface IEngineService { }

// Methods: PascalCase
public void InitializeService() { }

// Properties: PascalCase
public string ServiceName { get; set; }

// Fields: camelCase with underscore prefix for private
private readonly IService _service;
public readonly IService service;

// Constants: UPPER_CASE
private const string DEFAULT_CONFIG_PATH = "Configs/Default";

// Parameters and locals: camelCase
public void ProcessCommand(string commandName) { }
```

**Code Structure:**
```csharp
using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Sinkii09.Engine.Services
{
    /// <summary>
    /// Manages engine services and their lifecycle.
    /// </summary>
    public class ServiceManager : IServiceManager
    {
        #region Fields
        
        private readonly Dictionary<Type, IService> _services;
        private readonly IConfigurationProvider _configProvider;
        
        #endregion
        
        #region Properties
        
        public bool IsInitialized { get; private set; }
        
        #endregion
        
        #region Constructor
        
        public ServiceManager(IConfigurationProvider configProvider)
        {
            _configProvider = configProvider ?? throw new ArgumentNullException(nameof(configProvider));
            _services = new Dictionary<Type, IService>();
        }
        
        #endregion
        
        #region Public Methods
        
        public async UniTask InitializeAsync()
        {
            // Implementation
        }
        
        #endregion
        
        #region Private Methods
        
        private void ValidateService(IService service)
        {
            // Implementation
        }
        
        #endregion
    }
}
```

### Unity-Specific Guidelines

**SerializeField Usage:**
```csharp
[SerializeField] private string serviceName;
[SerializeField] private ServiceConfiguration configuration;
```

**Component References:**
```csharp
// Cache component references
private Transform _transform;
private Rigidbody _rigidbody;

private void Awake()
{
    _transform = transform;
    _rigidbody = GetComponent<Rigidbody>();
}
```

**Coroutines vs UniTask:**
```csharp
// Prefer UniTask for new code
public async UniTask LoadResourceAsync(string path)
{
    var resource = await Resources.LoadAsync<GameObject>(path);
    return resource;
}

// Only use Coroutines when Unity APIs require them
private IEnumerator LegacyOperation()
{
    yield return new WaitForSeconds(1f);
}
```

---

## 🧪 Testing Guidelines

### Test Structure
```
Tests/
├── Unit/                    # Unit tests
│   ├── Services/
│   ├── Commands/
│   └── Resources/
├── Integration/             # Integration tests
│   ├── ServiceIntegration/
│   └── SystemIntegration/
└── Performance/             # Performance tests
    ├── Benchmarks/
    └── MemoryTests/
```

### Unit Test Example
```csharp
using NUnit.Framework;
using Cysharp.Threading.Tasks;
using Sinkii09.Engine.Services;

[TestFixture]
public class ServiceManagerTests
{
    private ServiceManager serviceManager;
    private MockConfigurationProvider mockConfigProvider;

    [SetUp]
    public void Setup()
    {
        mockConfigProvider = new MockConfigurationProvider();
        serviceManager = new ServiceManager(mockConfigProvider);
    }

    [Test]
    public async Task InitializeAsync_ShouldInitializeAllServices()
    {
        // Arrange
        var mockService = new MockService();
        serviceManager.RegisterService<IMockService>(mockService);

        // Act
        await serviceManager.InitializeAsync();

        // Assert
        Assert.IsTrue(serviceManager.IsInitialized);
        Assert.IsTrue(mockService.IsInitialized);
    }

    [Test]
    public void RegisterService_WithNullService_ShouldThrowException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => 
            serviceManager.RegisterService<IMockService>(null));
    }

    [TearDown]
    public void TearDown()
    {
        serviceManager?.Dispose();
    }
}
```

### Testing Requirements
- **Unit Tests**: 80% code coverage minimum
- **Integration Tests**: All service interactions
- **Performance Tests**: Critical path benchmarks
- **Mock Objects**: Use for external dependencies
- **Async Testing**: Use UniTask.ToCoroutine() for Unity Test Runner

---

## 📚 Documentation Requirements

### Code Documentation
```csharp
/// <summary>
/// Manages the lifecycle and registration of engine services.
/// Provides dependency injection and initialization ordering.
/// </summary>
/// <remarks>
/// Services are initialized in dependency order to ensure
/// proper system startup. Use RegisterService before calling
/// InitializeAsync.
/// </remarks>
public class ServiceManager : IServiceManager
{
    /// <summary>
    /// Registers a service instance with the specified interface type.
    /// </summary>
    /// <typeparam name="T">The service interface type</typeparam>
    /// <param name="service">The service implementation instance</param>
    /// <exception cref="ArgumentNullException">Thrown when service is null</exception>
    /// <exception cref="InvalidOperationException">Thrown when service is already registered</exception>
    /// <example>
    /// <code>
    /// serviceManager.RegisterService&lt;IAudioService&gt;(new AudioService());
    /// </code>
    /// </example>
    public void RegisterService<T>(T service) where T : class, IService
    {
        // Implementation
    }
}
```

### API Documentation
- **XML Documentation**: All public APIs
- **Usage Examples**: For complex features
- **Architecture Decisions**: Document why, not just what
- **Performance Notes**: For performance-critical code

---

## 🔀 Pull Request Process

### Before Creating a PR

1. **Ensure Tests Pass**
   ```bash
   # Run all tests in Unity Test Runner
   # Ensure no compilation errors
   # Verify performance benchmarks
   ```

2. **Update Documentation**
   - Update API documentation
   - Add to CHANGELOG.md
   - Update README if needed

3. **Code Review Checklist**
   - [ ] Code follows style guidelines
   - [ ] Tests cover new functionality
   - [ ] Documentation is updated
   - [ ] No breaking changes (or properly documented)
   - [ ] Performance impact considered

### PR Description Template
```markdown
## Description
Brief description of changes and motivation.

## Type of Change
- [ ] Bug fix (non-breaking change which fixes an issue)
- [ ] New feature (non-breaking change which adds functionality)
- [ ] Breaking change (fix or feature that would cause existing functionality to not work as expected)
- [ ] Documentation update

## Testing
- [ ] Unit tests added/updated
- [ ] Integration tests added/updated
- [ ] Manual testing performed
- [ ] Performance impact assessed

## Related Issues
Closes #123
Related to #456

## Screenshots (if applicable)
<!-- Add screenshots for UI changes -->

## Checklist
- [ ] My code follows the style guidelines
- [ ] I have performed a self-review
- [ ] I have commented my code, particularly in hard-to-understand areas
- [ ] I have made corresponding changes to the documentation
- [ ] My changes generate no new warnings
- [ ] I have added tests that prove my fix is effective or that my feature works
- [ ] New and existing unit tests pass locally with my changes
```

### Review Process
1. **Automated Checks**: CI/CD pipeline validation
2. **Peer Review**: At least one team member review
3. **Architecture Review**: For significant changes
4. **Performance Review**: For performance-critical changes

---

## 🐛 Issue Reporting

### Bug Reports
Use the bug report template:

```markdown
**Describe the bug**
A clear and concise description of what the bug is.

**To Reproduce**
Steps to reproduce the behavior:
1. Go to '...'
2. Click on '....'
3. Scroll down to '....'
4. See error

**Expected behavior**
A clear and concise description of what you expected to happen.

**Screenshots**
If applicable, add screenshots to help explain your problem.

**Environment:**
- Unity Version: [e.g. 2021.3.16f1]
- Engine Version: [e.g. 0.1.0]
- OS: [e.g. Windows 11]
- Platform: [e.g. Standalone, Android]

**Additional context**
Add any other context about the problem here.

**Error Logs**
```
Paste error logs here
```
```

### Feature Requests
```markdown
**Is your feature request related to a problem? Please describe.**
A clear and concise description of what the problem is.

**Describe the solution you'd like**
A clear and concise description of what you want to happen.

**Describe alternatives you've considered**
A clear and concise description of any alternative solutions or features you've considered.

**Additional context**
Add any other context or screenshots about the feature request here.

**Implementation Ideas**
If you have ideas about how this could be implemented, please share them.
```

---

## 🏗️ Architecture Guidelines

### Service Design Principles

1. **Single Responsibility**: Each service has one clear purpose
2. **Dependency Injection**: Use constructor injection for dependencies
3. **Interface Segregation**: Keep interfaces focused and minimal
4. **Async-First**: Use UniTask for all async operations

### Example Service Implementation
```csharp
[InitializeAtRuntime(Priority = 100)]
public class AudioService : IAudioService
{
    private readonly IResourceService _resourceService;
    private readonly IAudioConfiguration _configuration;
    
    public AudioService(IResourceService resourceService, IAudioConfiguration configuration)
    {
        _resourceService = resourceService ?? throw new ArgumentNullException(nameof(resourceService));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }
    
    public async UniTask<bool> InitializeAsync()
    {
        // Initialize audio system
        return true;
    }
    
    public void Reset()
    {
        // Reset service state
    }
    
    public void Terminate()
    {
        // Cleanup resources
    }
}
```

### Command Implementation
```csharp
[CommandAlias("play")]
public class PlayAudioCommand : Command
{
    [RequiredParameter]
    public StringParameter audioPath = new StringParameter();
    
    [ParameterAlias("volume")]
    public DecimalParameter volume = new DecimalParameter(1.0f);
    
    public override async UniTask ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var audioService = Engine.GetService<IAudioService>();
        await audioService.PlayAsync(audioPath.Value, volume.Value, cancellationToken);
    }
}
```

---

## ⚡ Performance Considerations

### Performance Guidelines

1. **Avoid Allocations in Hot Paths**
   ```csharp
   // Bad: Creates garbage
   public string GetPath() => $"Resources/{name}.asset";
   
   // Good: Use StringBuilder or cached strings
   private readonly StringBuilder _pathBuilder = new StringBuilder();
   public string GetPath()
   {
       _pathBuilder.Clear();
       _pathBuilder.Append("Resources/");
       _pathBuilder.Append(name);
       _pathBuilder.Append(".asset");
       return _pathBuilder.ToString();
   }
   ```

2. **Use Object Pooling**
   ```csharp
   public class CommandPool : ObjectPool<Command>
   {
       protected override Command CreateItem() => new Command();
       protected override void OnReturnedToPool(Command command) => command.Reset();
   }
   ```

3. **Cache Component References**
   ```csharp
   private Transform _transform;
   private void Awake() => _transform = transform;
   ```

4. **Use UniTask for Async Operations**
   ```csharp
   public async UniTask<Resource> LoadAsync(string path)
   {
       return await Resources.LoadAsync<Resource>(path);
   }
   ```

### Profiling Requirements
- Profile new features with Unity Profiler
- Benchmark performance-critical code
- Monitor memory allocations
- Test on target platforms

---

## 🎯 Quality Checklist

Before submitting any contribution:

### Code Quality
- [ ] Follows coding standards
- [ ] No compiler warnings
- [ ] No static analysis warnings
- [ ] Performance impact considered

### Testing
- [ ] Unit tests written and passing
- [ ] Integration tests updated if needed
- [ ] Manual testing performed
- [ ] Edge cases considered

### Documentation
- [ ] Public APIs documented
- [ ] README updated if needed
- [ ] CHANGELOG.md updated
- [ ] Architecture docs updated if needed

### Compatibility
- [ ] Works on target Unity versions
- [ ] No breaking changes (or properly documented)
- [ ] Platform compatibility maintained
- [ ] Dependencies properly managed

---

## 🤝 Getting Help

- **Questions**: Use GitHub Discussions
- **Bugs**: Create GitHub Issues
- **Chat**: Join our Discord server (link TBD)
- **Email**: engine-dev@sinkii09.com (TBD)

---

## 📄 License

By contributing to Sinkii09 Engine, you agree that your contributions will be licensed under the MIT License.

---

Thank you for contributing to making Sinkii09 Engine better! 🚀