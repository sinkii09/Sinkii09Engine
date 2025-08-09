using System.IO;
using UnityEngine;

namespace Sinkii09.Engine.Common
{
    public static class PackagePath
    {
        public static string EditorIconPath => "Assets/Engine/Runtime/Resources/EditorIcons";
        public static string ScriptIconPath => Path.Combine(EditorIconPath, "ScriptAssetIcon.png");
    }
}