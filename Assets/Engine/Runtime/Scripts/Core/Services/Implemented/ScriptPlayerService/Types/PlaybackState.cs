namespace Sinkii09.Engine.Services
{
    /// <summary>
    /// Enumeration of possible playback states for the script player
    /// </summary>
    public enum PlaybackState
    {
        /// <summary>
        /// No script is loaded or playing
        /// </summary>
        Idle,

        /// <summary>
        /// Script is being loaded
        /// </summary>
        Loading,

        /// <summary>
        /// Script is playing
        /// </summary>
        Playing,

        /// <summary>
        /// Script playback is paused
        /// </summary>
        Paused,

        /// <summary>
        /// Script playback is stopped
        /// </summary>
        Stopped,

        /// <summary>
        /// Waiting for a command to complete
        /// </summary>
        Waiting,

        /// <summary>
        /// Script execution completed successfully
        /// </summary>
        Completed,

        /// <summary>
        /// Script execution failed with an error
        /// </summary>
        Failed
    }
}