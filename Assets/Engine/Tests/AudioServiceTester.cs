using UnityEngine;
using Sirenix.OdinInspector;
using Sinkii09.Engine.Services;
using Cysharp.Threading.Tasks;
using System.Threading;

namespace Sinkii09.Engine.Tests
{
    /// <summary>
    /// Simple audio service tester with Odin Inspector buttons
    /// </summary>
    public class AudioServiceTester : MonoBehaviour
    {
        #region Configuration
        
        [Title("Audio Test Configuration")]
        [SerializeField, Required, AssetList(Path = "Engine/Audio/Music")]
        [InfoBox("Select a music track from the project")]
        private AudioClip musicClip;
        
        [SerializeField, Required, AssetList(Path = "Engine/Audio/SFX")]
        [InfoBox("Select a sound effect from the project")]
        private AudioClip sfxClip;
        
        [SerializeField, Range(0f, 1f)]
        private float musicVolume = 0.7f;
        
        [SerializeField, Range(0f, 1f)]
        private float sfxVolume = 1f;
        
        [SerializeField, Range(0f, 3f)]
        private float fadeInDuration = 1f;
        
        [SerializeField, Range(0f, 3f)]
        private float fadeOutDuration = 1f;
        
        #endregion
        
        #region State
        
        [ShowInInspector, ReadOnly]
        [Title("Playback State")]
        private bool isMusicPlaying;
        
        [ShowInInspector, ReadOnly]
        private bool isPaused;
        
        private IAudioService audioService;
        private IAudioPlayer currentMusicPlayer;
        private CancellationTokenSource cts;
        
        #endregion
        
        #region Unity Lifecycle
        
        private void Start()
        {
            InitializeAudioService();
        }
        
        private void OnDestroy()
        {
            cts?.Cancel();
            cts?.Dispose();
        }
        
        #endregion
        
        #region Initialization
        
        private void InitializeAudioService()
        {
            audioService = Engine.GetService<IAudioService>();
            
            if (audioService == null)
            {
                Debug.LogError("[AudioServiceTester] AudioService not found in ServiceContainer!");
            }
            else
            {
                Debug.Log("[AudioServiceTester] AudioService initialized successfully");
            }
        }
        
        #endregion
        
        #region Music Controls
        
        [ButtonGroup("Music")]
        [Button("Play Music", ButtonSizes.Large)]
        [EnableIf("@musicClip != null && !isMusicPlaying")]
        [GUIColor(0.4f, 0.8f, 0.4f)]
        private async void PlayMusic()
        {
            if (audioService == null)
            {
                Debug.LogError("AudioService not available!");
                return;
            }
            
            cts?.Cancel();
            cts = new CancellationTokenSource();
            
            try
            {
                Debug.Log($"Playing music: {musicClip.name} with fade-in: {fadeInDuration}s");
                
                var options = new AudioPlayOptions
                {
                    Volume = musicVolume,
                    Loop = true,
                    FadeInDuration = fadeInDuration
                };
                
                currentMusicPlayer = await audioService.PlayMusicAsync(
                    musicClip.name, 
                    options.FadeInDuration,
                    options.Loop,
                    cts.Token
                );
                
                isMusicPlaying = true;
                isPaused = false;
                
                Debug.Log("Music started successfully!");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Failed to play music: {e.Message}");
            }
        }
        
        [ButtonGroup("Music")]
        [Button("Stop Music", ButtonSizes.Large)]
        [EnableIf("isMusicPlaying")]
        [GUIColor(0.8f, 0.4f, 0.4f)]
        private async void StopMusic()
        {
            if (currentMusicPlayer == null) return;
            
            try
            {
                Debug.Log($"Stopping music with fade-out: {fadeOutDuration}s");
                
                await currentMusicPlayer.StopAsync(fadeOutDuration);
                
                isMusicPlaying = false;
                isPaused = false;
                currentMusicPlayer = null;
                
                Debug.Log("Music stopped successfully!");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Failed to stop music: {e.Message}");
            }
        }
        
        [ButtonGroup("Music")]
        [Button("Pause/Resume", ButtonSizes.Medium)]
        [EnableIf("isMusicPlaying")]
        [GUIColor(0.4f, 0.6f, 0.8f)]
        private void TogglePause()
        {
            if (currentMusicPlayer == null) return;
            
            if (isPaused)
            {
                currentMusicPlayer.Resume();
                isPaused = false;
                Debug.Log("Music resumed");
            }
            else
            {
                currentMusicPlayer.Pause();
                isPaused = true;
                Debug.Log("Music paused");
            }
        }
        
        #endregion
        
        #region Sound Effects
        
        [ButtonGroup("SFX")]
        [Button("Play Sound Effect", ButtonSizes.Large)]
        [EnableIf("@sfxClip != null")]
        [GUIColor(0.8f, 0.6f, 0.2f)]
        private async void PlaySoundEffect()
        {
            if (audioService == null)
            {
                Debug.LogError("AudioService not available!");
                return;
            }
            
            try
            {
                Debug.Log($"Playing SFX: {sfxClip.name} at volume: {sfxVolume}");
                
                await audioService.PlaySFXAsync(
                    sfxClip.name,
                    sfxVolume,
                    transform.position
                );
                
                Debug.Log("SFX played successfully!");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Failed to play SFX: {e.Message}");
            }
        }
        
        [ButtonGroup("SFX")]
        [Button("Play Random Pitch SFX", ButtonSizes.Medium)]
        [EnableIf("@sfxClip != null")]
        [GUIColor(0.6f, 0.4f, 0.8f)]
        private async void PlayRandomPitchSFX()
        {
            if (audioService == null)
            {
                Debug.LogError("AudioService not available!");
                return;
            }
            
            try
            {
                float randomPitch = Random.Range(0.8f, 1.2f);
                Debug.Log($"Playing SFX: {sfxClip.name} with pitch: {randomPitch}");
                
                var options = new AudioPlayOptions
                {
                    Volume = sfxVolume,
                };
                
                await audioService.PlayAsync(
                    sfxClip.name,
                    AudioCategory.SFX,
                    options
                );
                
                Debug.Log($"SFX played with pitch {randomPitch}!");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Failed to play SFX: {e.Message}");
            }
        }
        
        #endregion
        
        #region Volume Controls
        
        [Button("Set Music Volume", ButtonSizes.Medium)]
        [HorizontalGroup("Volume")]
        private void SetMusicCategoryVolume(float volume)
        {
            if (audioService == null) return;
            
            audioService.SetCategoryVolume(AudioCategory.Music, volume);
            Debug.Log($"Music category volume set to: {volume}");
        }
        
        [Button("Set SFX Volume", ButtonSizes.Medium)]
        [HorizontalGroup("Volume")]
        private void SetSFXCategoryVolume(float volume)
        {
            if (audioService == null) return;
            
            audioService.SetCategoryVolume(AudioCategory.SFX, volume);
            Debug.Log($"SFX category volume set to: {volume}");
        }
        
        #endregion
        
        #region Service Controls
        
        [Title("Service Management")]
        [Button("Stop All Audio", ButtonSizes.Large)]
        [GUIColor(1f, 0.3f, 0.3f)]
        private async void StopAllAudio()
        {
            if (audioService == null) return;
            
            Debug.Log("Stopping all audio...");
            await audioService.StopAllAsync(fadeOutDuration);
            
            isMusicPlaying = false;
            isPaused = false;
            currentMusicPlayer = null;
            
            Debug.Log("All audio stopped!");
        }
        
        [Button("Pause All Audio", ButtonSizes.Medium)]
        [HorizontalGroup("ServiceControls")]
        private void PauseAllAudio()
        {
            if (audioService == null) return;
            
            audioService.PauseAll();
            Debug.Log("All audio paused");
        }
        
        [Button("Resume All Audio", ButtonSizes.Medium)]
        [HorizontalGroup("ServiceControls")]
        private void ResumeAllAudio()
        {
            if (audioService == null) return;
            
            audioService.ResumeAll();
            Debug.Log("All audio resumed");
        }
        
        #endregion
    }
}