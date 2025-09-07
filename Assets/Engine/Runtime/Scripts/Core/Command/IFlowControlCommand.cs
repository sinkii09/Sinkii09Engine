using Cysharp.Threading.Tasks;
using Sinkii09.Engine.Services;
using System.Threading;

namespace Sinkii09.Engine.Commands
{
    /// <summary>
    /// Interface for commands that can return flow control results
    /// </summary>
    public interface IFlowControlCommand
    {
        /// <summary>
        /// Execute the command and return a result indicating flow control actions
        /// </summary>
        UniTask<CommandResult> ExecuteWithResultAsync(CancellationToken cancellationToken = default);
    }
}