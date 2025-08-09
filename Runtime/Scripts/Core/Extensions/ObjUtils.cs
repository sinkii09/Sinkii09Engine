using System.Collections;

namespace Sinkii09.Engine.Extensions
{
    public static class ObjUtils
    {
        public static bool IsValid(object obj)
        {
            if (obj is UnityEngine.Object unityObject)
                return unityObject != null && unityObject;
            else return false;
        }
    }
}