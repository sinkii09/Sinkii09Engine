using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using Sinkii09.Engine.Common.Resources;
using Sinkii09.Engine.Common;

namespace Sinkii09.Engine.Services
{
    public abstract class ResourceRunner
    {
        public readonly IResourceProvider Provider;
        public readonly string Path;
        public readonly Type ResourceType;

        public ResourceRunner(IResourceProvider provider, string path, Type resourceType)
        {
            Provider = provider;
            Path = path;
            ResourceType = resourceType;
        }

        public UniTask.Awaiter GetAwaiter() => GetAwaiterImpl();

        public abstract UniTask RunAsync();
        public abstract void Cancel();
        protected abstract UniTask.Awaiter GetAwaiterImpl();
    }
    public abstract class ResourceRunner<T> : ResourceRunner
    {
        public T Result { get; private set; }
        public UniTaskCompletionSource<T> CompletionSource = new UniTaskCompletionSource<T>();
        public ResourceRunner(IResourceProvider provider, string path, Type resourceType) : base(provider, path, resourceType)
        {
        }

        protected void SetResult(T result)
        {
            Result = result;
            CompletionSource.TrySetResult(Result);
        }
        public override void Cancel()
        {
            CompletionSource.TrySetCanceled();
        }

        public new UniTask<T>.Awaiter GetAwaiter() => CompletionSource.Task.GetAwaiter();
        protected override UniTask.Awaiter GetAwaiterImpl()
        {
            return ((UniTask)CompletionSource.Task).GetAwaiter();
        }
    }
    public abstract class LocateResourceRunner<T> : ResourceRunner<IEnumerable<string>> where T : UnityEngine.Object
    {
        public LocateResourceRunner(IResourceProvider provider, string path) : base(provider, path, typeof(T))
        {
        }
    }

    public abstract class LoadResourceRunner<T> : ResourceRunner<Resource<T>> where T : UnityEngine.Object
    {
        public LoadResourceRunner(IResourceProvider provider, string path) : base(provider, path, typeof(T))
        {
        }
    }
    public abstract class LocateFolderRunner : ResourceRunner<IEnumerable<Folder>>
    {
        public LocateFolderRunner(IResourceProvider provider, string path) : base(provider, path, typeof(Folder))
        {
        }
    }
}