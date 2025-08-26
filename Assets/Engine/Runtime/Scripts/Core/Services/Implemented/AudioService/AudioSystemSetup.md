# Audio System Setup Guide

## 🎵 Quick Start

### 1. Create Audio Service Configuration
1. Right-click in Project window
2. Go to `Create > Engine > Services > AudioServiceConfiguration`
3. Name it "AudioServiceConfig"
4. Configure the settings in the Inspector

### 2. Setup Unity Audio Mixer
1. Create Audio Mixer: `Assets > Create > Audio Mixer`
2. Name it "MasterAudioMixer"
3. Open Audio Mixer window (`Window > Audio > Audio Mixer`)
4. Create the following Groups (buses):
   - **Master** (default)
   - **Music**
   - **SFX**
   - **Voice**
   - **Ambient**
   - **UI**
   - **System**

### 3. Configure Audio Mixer Groups
For each group, expose the volume parameter:
1. Select the group
2. Right-click on "Volume" in Inspector
3. Select "Expose 'Volume' to script"
4. Rename exposed parameters to: `MusicVolume`, `SFXVolume`, etc.

### 4. Link Configuration to Audio Mixer
1. Select your AudioServiceConfiguration asset
2. Assign the MasterAudioMixer to "Master Audio Mixer" field
3. Set bus names to match your mixer groups:
   - Music Bus Name: "Music"
   - SFX Bus Name: "SFX"
   - Voice Bus Name: "Voice"
   - Ambient Bus Name: "Ambient"
   - UI Bus Name: "UI"
   - System Bus Name: "System"

## 📋 Configuration Settings

### AudioServiceConfiguration Inspector

```yaml
# Audio Management
Max Concurrent Sources: 32          # Maximum audio sources playing simultaneously
Enable Audio Streaming: true        # Stream large audio files
Default Fade Duration: 1.0          # Default fade time in seconds

# Resource Management
Audio Clip Cache Size: 50           # Number of clips to keep in memory
Preload Critical Audio: true        # Load important sounds at startup
Streaming Memory Threshold: 10      # MB threshold for streaming
Audio Resource Base Path: "Audio"   # Root folder for audio resources

# Unity AudioMixer Settings
Master Audio Mixer: [Your Mixer]    # Drag your AudioMixer here
Music Bus Name: "Music"
SFX Bus Name: "SFX"
Voice Bus Name: "Voice"
Ambient Bus Name: "Ambient"
UI Bus Name: "UI"
System Bus Name: "System"

# Spatial Audio
Enable Spatial Audio: true           # 3D positioning support
Max Audio Distance: 50              # Maximum 3D audio range
Default Rolloff Mode: Logarithmic  # Distance attenuation curve
Enable 3D Occlusion: true           # Environmental occlusion

# Performance
Enable Audio Pooling: true          # Reuse AudioSource objects
Audio Source Pool Size: 20          # Initial pool size
Memory Pressure Threshold: 0.8     # Cleanup threshold (0-1)

# Music System
Enable Music Playlists: true
Enable Music Crossfade: true
Default Crossfade Duration: 3.0
Music Loop By Default: true

# SFX System
Enable SFX Pooling: true
Max Simultaneous SFX: 10
Enable SFX Priority: true
```

## 🎮 Code Usage Examples

### Basic Setup in Engine

```csharp
// In your Engine initialization or GameManager
public class GameInitializer : MonoBehaviour
{
    private AudioServiceConfiguration audioConfig;
    
    async void Start()
    {
        // Load configuration
        audioConfig = Resources.Load<AudioServiceConfiguration>("Configurations/AudioServiceConfig");
        
        // Initialize Engine with AudioService
        await Engine.InitializeAsync();
        
        // The AudioService is now ready to use
        var audioService = Engine.GetService<IAudioService>();
    }
}
```

### Playing Different Audio Types

```csharp
// Get the audio service
var audioService = Engine.GetService<IAudioService>();

// 1. Play Background Music
await audioService.PlayMusicAsync("MainTheme", fadeIn: 2f, loop: true);

// 2. Play Sound Effect
await audioService.PlaySFXAsync("explosion", volume: 0.8f, position: transform.position);

// 3. Play UI Sound
await audioService.PlayUIAsync(UIInteractionType.ButtonClick);

// 4. Play Ambient Sound
await audioService.PlayAmbientAsync("forest_ambient", loop: true, volume: 0.6f);

// 5. Play Character Voice
await audioService.PlayVoiceAsync("PlayerCharacter", "greeting_01", duck: true);
```

### Creating Music Playlists

```csharp
// Create a playlist
var playlist = new MusicPlaylist("BattleMusic");
playlist.AddTrack("battle_theme_1");
playlist.AddTrack("battle_theme_2");
playlist.AddTrack("battle_theme_3");
playlist.Shuffle = true;
playlist.Repeat = true;
playlist.CrossfadeDuration = 2f;

// Play the playlist
await audioService.PlayMusicPlaylistAsync(playlist);
```

### Creating Ambient Soundscapes

```csharp
// Define a soundscape
var forestSoundscape = new AmbientSoundscape
{
    Name = "Forest",
    DefaultWeather = WeatherType.Clear,
    DefaultTimeOfDay = TimeOfDay.Day
};

// Add layers
forestSoundscape.Layers.Add(new AmbientSoundscape.AmbientLayerConfig
{
    LayerId = "base",
    AudioClipPath = "forest_base",
    Volume = 0.6f,
    AutoStart = true
});

forestSoundscape.Layers.Add(new AmbientSoundscape.AmbientLayerConfig
{
    LayerId = "birds",
    AudioClipPath = "forest_birds",
    Volume = 0.4f,
    FadeInDuration = 3f,
    AutoStart = true
});

// Create the soundscape
var ambientPlayer = await audioService.CreateAmbientSoundscapeAsync(forestSoundscape);

// Modify environment dynamically
ambientPlayer.CurrentWeather = WeatherType.Rain;
ambientPlayer.CurrentTimeOfDay = TimeOfDay.Dusk;
```

### Volume Control

```csharp
// Master volume
audioService.SetMasterVolume(0.8f);

// Category volumes
audioService.SetCategoryVolume(AudioCategory.Music, 0.7f);
audioService.SetCategoryVolume(AudioCategory.SFX, 1.0f);
audioService.SetCategoryVolume(AudioCategory.Voice, 1.0f);

// Mute categories
audioService.SetCategoryMuted(AudioCategory.Music, false);
```

### Audio Mixer Snapshots

```csharp
// Apply audio mixer snapshots for different game states
await audioService.ApplySnapshotAsync("Combat", transitionTime: 1f);
await audioService.ApplySnapshotAsync("Underwater", transitionTime: 0.5f);
await audioService.ApplySnapshotAsync("Menu", transitionTime: 2f);
```

## 📁 Project Folder Structure

Organize your audio files like this:

```
Assets/
├── Audio/
│   ├── Music/
│   │   ├── MainTheme.ogg
│   │   ├── BattleTheme.ogg
│   │   └── VictoryTheme.ogg
│   ├── SFX/
│   │   ├── Weapons/
│   │   │   ├── sword_swing.wav
│   │   │   └── arrow_shot.wav
│   │   └── Environment/
│   │       ├── door_open.wav
│   │       └── chest_open.wav
│   ├── Voice/
│   │   └── Characters/
│   │       ├── Player/
│   │       │   ├── greeting_01.wav
│   │       │   └── battle_cry_01.wav
│   │       └── NPC/
│   │           └── merchant_greeting.wav
│   ├── Ambient/
│   │   ├── forest_base.ogg
│   │   ├── forest_birds.ogg
│   │   ├── cave_drips.ogg
│   │   └── town_crowd.ogg
│   └── UI/
│       ├── button_click.wav
│       ├── menu_open.wav
│       └── notification.wav
```

## 🎛️ Audio Mixer Setup Details

### Creating Mixer Groups Hierarchy

```
Master (AudioMixerGroup)
├── Music (AudioMixerGroup)
├── SFX (AudioMixerGroup)
├── Voice (AudioMixerGroup)
├── Ambient (AudioMixerGroup)
├── UI (AudioMixerGroup)
└── System (AudioMixerGroup)
```

### Adding Effects to Mixer Groups

1. **Music Group**:
   - Add Reverb for depth
   - Add Compressor for consistent volume

2. **SFX Group**:
   - Add Limiter to prevent clipping
   - Optional: Add Echo for specific environments

3. **Voice Group**:
   - Add Compressor for clarity
   - Add EQ to enhance speech frequencies

4. **Ambient Group**:
   - Add Low Pass Filter for distance effect
   - Add Reverb for environmental depth

### Creating Snapshots

1. In Audio Mixer window, click "Snapshots"
2. Create snapshots for different game states:
   - **Default**: Normal gameplay
   - **Combat**: Boost SFX, duck ambient
   - **Menu**: Duck all except UI
   - **Underwater**: Heavy low-pass filter
   - **Victory**: Boost music, duck others

## 🔧 Script Integration

### With Script Commands

```csharp
// In your script files (.nani or custom format)
@play music:MainTheme fadein:3.0 loop:true
@play sfx:explosion volume:0.8
@voice char:Player clip:greeting_01 duck:true
@ambient forest_base volume:0.6 loop:true
@fadeout music duration:2.0
@snapshot Combat duration:0.5
```

### Creating Custom Audio Commands

```csharp
[CommandAlias("playsound")]
public class PlaySoundCommand : ICommand
{
    [RequiredParameter]
    public CommandParameter<string> Sound { get; set; }
    
    public CommandParameter<float> Volume { get; set; } = 1.0f;
    public CommandParameter<bool> Loop { get; set; } = false;
    
    public async UniTask ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var audioService = Engine.GetService<IAudioService>();
        
        var options = AudioPlayOptions.Default;
        options.Volume = Volume.Value;
        options.Loop = Loop.Value;
        
        await audioService.PlayAsync(Sound.Value, AudioCategory.SFX, options, cancellationToken);
    }
}
```

## 🎯 Best Practices

### 1. Audio File Formats
- **Music**: Use .ogg for looping music (seamless loops)
- **SFX**: Use .wav for short sounds (better quality)
- **Voice**: Use .wav or .ogg depending on length
- **Ambient**: Use .ogg for long loops

### 2. Audio Import Settings
```yaml
Music:
  Load Type: Streaming
  Compression: Vorbis
  Quality: 70-80%
  
SFX:
  Load Type: Decompress on Load
  Compression: PCM or ADPCM
  
Voice:
  Load Type: Compressed in Memory
  Compression: Vorbis
  Quality: 80-90%
  
Ambient:
  Load Type: Streaming
  Compression: Vorbis
  Quality: 60-70%
```

### 3. Performance Tips
- Limit concurrent sounds per category
- Use audio pooling for frequently played SFX
- Stream large music files
- Preload critical UI sounds
- Clean up unused audio resources

### 4. Memory Management
```csharp
// Preload critical audio
await audioService.PreloadAudioAsync(new[] {
    "ui_button_click",
    "ui_error",
    "player_hurt",
    "player_death"
});

// Unload unused audio
await audioService.UnloadAudioAsync(new[] {
    "boss_music",
    "boss_roar"
});

// Clear cache when changing scenes
await audioService.ClearAudioCacheAsync();
```

## 🐛 Troubleshooting

### Common Issues and Solutions

1. **No Audio Playing**
   - Check Master Volume isn't 0
   - Verify AudioMixer is assigned in configuration
   - Ensure audio files are in Resources folder or Addressables

2. **Audio Delay/Latency**
   - Reduce Audio Source Pool Size
   - Use "Decompress on Load" for critical SFX
   - Preload frequently used sounds

3. **Audio Popping/Clicking**
   - Enable fade in/out for music
   - Check audio file for clipping
   - Reduce concurrent SFX limit

4. **Memory Issues**
   - Reduce Audio Clip Cache Size
   - Enable streaming for large files
   - Clear cache between scenes

5. **3D Audio Not Working**
   - Enable Spatial Audio in configuration
   - Set Audio Source spatial blend
   - Check Audio Listener placement

## 📊 Monitoring and Debug

```csharp
// Get audio statistics
var stats = audioService.GetStatistics();
Debug.Log($"Active Players: {stats.ActivePlayers}");
Debug.Log($"Cached Clips: {stats.CachedClips}");
Debug.Log($"Memory Usage: {stats.MemoryUsageBytes / 1024 / 1024}MB");

// Get debug info
string debugInfo = audioService.GetDebugInfo();
Debug.Log(debugInfo);

// Validate audio resources
var validation = audioService.ValidateAudioResources();
foreach (var issue in validation)
{
    Debug.LogWarning($"{issue.Key}: {string.Join(", ", issue.Value)}");
}
```

## ✅ Setup Checklist

- [ ] Create AudioServiceConfiguration asset
- [ ] Create and configure Unity Audio Mixer
- [ ] Set up mixer groups for each category
- [ ] Expose volume parameters to script
- [ ] Link configuration to mixer
- [ ] Organize audio files in folders
- [ ] Configure audio import settings
- [ ] Create mixer snapshots
- [ ] Test basic playback
- [ ] Set up volume controls
- [ ] Configure spatial audio settings
- [ ] Test performance with multiple sounds
- [ ] Set up script commands (if using)
- [ ] Create preload lists for critical audio
- [ ] Test memory management

## 🎉 You're Ready!

Your Audio System is now fully configured and ready to use. Start with simple playback tests and gradually add more complex features like playlists, soundscapes, and dynamic mixing.