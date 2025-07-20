using System;

namespace Sinkii09.Engine.Services.AutoSave
{
    public class AutoSaveEvent
    {
        public string SlotName { get; set; }
        public AutoSaveReason Reason { get; set; }
        public DateTime Timestamp { get; set; }
        public bool Success { get; set; }
        public string Error { get; set; }
        public long SaveSizeBytes { get; set; }
        public float SaveDurationMs { get; set; }
        public string ProviderName { get; set; }
    }

    public class AutoSaveConditions
    {
        public bool RequirePlayerAlive { get; set; } = true;
        public bool RequireNotInCombat { get; set; } = true;
        public bool RequireNotInCutscene { get; set; } = true;
        public bool RequireNotInMainMenu { get; set; } = true;
        public bool RequireNotLoading { get; set; } = true;
        public bool RequireUnsavedChanges { get; set; } = false;
        public AutoSaveCondition CustomConditions { get; set; } = AutoSaveCondition.None;
    }

    public class TimerSettings
    {
        public bool Enabled { get; set; } = true;
        public float Interval { get; set; } = 300f; // 5 minutes
        public float MinimumInterval { get; set; } = 30f; // Prevent save spam
    }

    public class SceneChangeSettings
    {
        public bool Enabled { get; set; } = true;
        public bool SaveBeforeLoad { get; set; } = true;
        public bool SaveAfterLoad { get; set; } = false;
        public string[] IgnoredScenes { get; set; } = { "MainMenu", "LoadingScreen", "Splash" };
    }

    public class ApplicationLifecycleSettings
    {
        public bool SaveOnPause { get; set; } = true;
        public bool SaveOnFocusLost { get; set; } = true;
        public bool SaveOnQuit { get; set; } = true;
        public float QuitSaveTimeout { get; set; } = 5f; // Maximum time to wait for save on quit
    }
}