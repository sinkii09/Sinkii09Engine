using Cysharp.Threading.Tasks;

namespace Sinkii09.Engine.Services
{
    public interface IActorService : IService
    {
        // Define methods and properties for the actor service
    }
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