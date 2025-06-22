using Cysharp.Threading.Tasks;
using Sinkii09.Engine.Commands;
using Sinkii09.Engine.Initializer;

namespace Sinkii09.Engine.Services
{
    public interface IScript
    {

    }
    public interface IScriptPlayerService : IService
    {
        bool IsPlaying { get; }
        IScript PlayedScript { get; }
        ICommand PlayedCommand { get; }

        void Play(IScript script);
        void Stop();
    }

    [InitializeAtRuntime]
    public class ScriptPlayerService : IScriptPlayerService
    {
        public bool IsPlaying { get; private set; }

        public IScript PlayedScript { get; private set; }

        public ICommand PlayedCommand { get; private set; }

        public UniTask<bool> Initialize()
        {


            return UniTask.FromResult(true);
        }

        public void Play(IScript script)
        {

        }

        public void Reset()
        {
        }

        public void Stop()
        {

        }

        public void Terminate()
        {
        }
    }
}