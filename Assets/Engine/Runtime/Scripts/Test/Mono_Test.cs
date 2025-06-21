using Sinkii09.Engine;
using Sirenix.OdinInspector;
using System.Threading.Tasks;
using UnityEngine;
using Sinkii09.Engine.Services;
using Sinkii09.Engine.Common.Script;

public class Mono_Test : MonoBehaviour
{
    [Button("Test Script Service")]
    public async Task TestScriptServiceAsync()
    {
        var scriptService = Engine.GetService<IScriptService>();

        Script data = await scriptService.LoadScriptAsync("CustomScript");

    }
}
