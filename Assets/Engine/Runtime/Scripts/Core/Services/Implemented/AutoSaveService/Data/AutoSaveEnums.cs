using System;

namespace Sinkii09.Engine.Services.AutoSave
{
    [Flags]
    public enum AutoSaveReason
    {
        None = 0,
        Timer = 1 << 0,
        SceneChange = 1 << 1,
        ApplicationPause = 1 << 2,
        ApplicationFocusLost = 1 << 3,
        ApplicationQuit = 1 << 4,
        Manual = 1 << 5,
        Checkpoint = 1 << 6,
        Combat = 1 << 7,
        Custom = 1 << 8
    }
    public enum AutoSaveSlotStrategy
    {
        Rotating,
        Timestamped,
        Numbered
    }

    public enum AutoSaveCondition
    {
        None = 0,
        PlayerAlive = 1 << 0,
        NotInCombat = 1 << 1,
        NotInCutscene = 1 << 2,
        NotInMainMenu = 1 << 3,
        NotLoading = 1 << 4,
        HasUnsavedChanges = 1 << 5
    }

    public enum AutoSavePriority
    {
        Lowest = 0,
        Low = 25,
        Normal = 50,
        High = 75,
        Highest = 100,
        Critical = 200
    }
}