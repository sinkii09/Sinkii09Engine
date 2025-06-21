using Cysharp.Threading.Tasks;

namespace Sinkii09.Engine.Services
{
    public interface IService
    {
        // Define common service methods here if needed
        UniTask<bool> Initialize();
        void Reset();
        void Terminate();
    }
}