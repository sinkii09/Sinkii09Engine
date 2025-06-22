using Cysharp.Threading.Tasks;
using Sinkii09.Engine.Initializer;

namespace Sinkii09.Engine.Services
{
    public interface IActorService : IService
    {
        // Define methods and properties for the actor service
    }

    [InitializeAtRuntime]
    public class ActorService : IActorService
    {
        public UniTask<bool> Initialize()
        {
            return UniTask.FromResult(true);
        }

        public void Reset()
        {
        }

        public void Terminate()
        {
        }
    }
}