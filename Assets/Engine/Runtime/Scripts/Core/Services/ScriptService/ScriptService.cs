using Cysharp.Threading.Tasks;
using Sinkii09.Engine.Common.Script;
using Sinkii09.Engine.Configs;
using Sinkii09.Engine.Initializer;
using System;

namespace Sinkii09.Engine.Services
{
    public interface IScriptService : IService
    {
        event Action OnScriptLoadStarted;
        event Action OnScriptLoadCompleted;

        UniTask<Script> LoadScriptAsync(string name);
        //UniTask<IEnumerable<Script>> LoadAllScriptsAsync();

        void UnloadScript(string name);
        void UnloadAllScripts();
    }

    [InitializeAtRuntime]
    public class ScriptService : IScriptService
    {
        public event Action OnScriptLoadStarted;
        public event Action OnScriptLoadCompleted;

        private IResourceService _resourceService;
        private ScriptsConfig _scriptsConfig;
        private ResourceLoader<Script> _resourceLoader;

        public ScriptService(IResourceService resourceService, ScriptsConfig scriptsConfig)
        {
            _resourceService = resourceService;
            _scriptsConfig = scriptsConfig;
            _resourceLoader = _scriptsConfig.ResouceLoaderConfig.CreateFor<Script>(_resourceService);
        }

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

        public async UniTask<Script> LoadScriptAsync(string name)
        {
            OnScriptLoadStarted?.Invoke();

            if (_resourceLoader.IsLoaded(name))
            {
                OnScriptLoadCompleted?.Invoke();
                return _resourceLoader.GetLoadedOrNull(name);
            }

            var scriptResource = await _resourceLoader.LoadAsync(name);

            OnScriptLoadCompleted?.Invoke();
            return scriptResource;
        }

        public void UnloadScript(string name)
        {

        }

        public void UnloadAllScripts()
        {

        }
    }
}