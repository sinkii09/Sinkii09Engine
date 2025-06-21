using Sinkii09.Engine.Extensions;
using System;
using UnityEngine;

namespace Sinkii09.Engine.Common
{
    [Serializable]
    public class Folder
    {
        public string Path => path;
        public string Name => Path.Contains("/") ? Path.GetAfter("/") : Path;

        [SerializeField] string path = null;

        public Folder(string path)
        {
            this.path = path;
        }
    }
}