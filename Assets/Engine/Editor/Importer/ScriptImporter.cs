using Sinkii09.Engine.Common.Script;
using System.IO;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;
using static Sinkii09.Engine.Common.PackagePath;

namespace Sinkii09.Engine.Editor.Importer
{
    [ScriptedImporter(1, "script")]
    public class ScriptImporter : ScriptedImporter
    {
        private Texture2D _scriptIcon;

        public override void OnImportAsset(AssetImportContext ctx)
        {
            string content = string.Empty;

            try
            {
                byte[] bytes = File.ReadAllBytes(ctx.assetPath);
                content = System.Text.Encoding.UTF8.GetString(bytes);

                if (content.Length > 0 && content[0] == '\uFEFF')
                {
                    content = content.Substring(1); // Remove BOM if present
                    File.WriteAllText(ctx.assetPath, content, System.Text.Encoding.UTF8);
                }
            }
            catch (IOException ex)
            {
                ctx.LogImportError($"IOException: {ex.Message}");
            }
            finally
            {
                string assetName = Path.GetFileNameWithoutExtension(ctx.assetPath);
                Script script = Script.FromScripText(assetName, content);

                script.hideFlags = HideFlags.NotEditable;

                if (_scriptIcon == null)
                {
                    _scriptIcon = AssetDatabase.LoadAssetAtPath<Texture2D>($"{ScriptIconPath}");
                }

                ctx.AddObjectToAsset("nscript", script, _scriptIcon);
                ctx.SetMainObject(script);
            }
        }
    }
}