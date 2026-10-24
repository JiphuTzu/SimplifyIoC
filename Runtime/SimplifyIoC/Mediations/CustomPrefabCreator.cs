using System;
using SimplifyIoC.Bootstraps;
using UnityEngine;

namespace SimplifyIoC.Mediations
{
    public abstract class CustomPrefabCreator : ScriptableObject
    {
        public abstract bool GetUIPrefab(string name, out SimplifyUI prefab);
        public abstract bool GetUIPrefab(Type type, out SimplifyUI prefab);

        public abstract bool GetBootstrapPrefab(string name, out Bootstrap prefab);
    }
}
