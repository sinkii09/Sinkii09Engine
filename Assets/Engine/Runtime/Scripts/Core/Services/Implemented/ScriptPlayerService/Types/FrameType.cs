namespace Sinkii09.Engine.Services
{
    /// <summary>
    /// Type of execution frame
    /// </summary>
    public enum FrameType
    {
        /// <summary>
        /// Frame created by calling another script
        /// </summary>
        ScriptCall,

        /// <summary>
        /// Frame created by calling a subroutine/label
        /// </summary>
        SubroutineCall,

        /// <summary>
        /// Frame created by a loop construct
        /// </summary>
        Loop,

        /// <summary>
        /// Frame created by a conditional branch
        /// </summary>
        Conditional,

        /// <summary>
        /// Frame created by a try-catch block
        /// </summary>
        TryCatch
    }
}