using System;
using SimplifyIoC.Bootstraps;
using UnityEngine;

namespace SimplifyIoC.Mediations
{
    public abstract class CustomPrefabCreator : ScriptableObject, IPrefabCreator
    {
        public abstract bool GetUIPrefab(string name, out SimplifyUI prefab);
        public abstract void GetUIPrefab(string name, Action<SimplifyUI> callback);
        public abstract bool GetUIPrefab(Type type, out SimplifyUI prefab);
        public abstract void GetUIPrefab(Type type, Action<SimplifyUI> callback);
        public abstract bool GetBootstrapPrefab(string name, out Bootstrap prefab);
        public abstract void GetBootstrapPrefab(string name, Action<Bootstrap> callback);
    }
}
