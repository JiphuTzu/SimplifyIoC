using System;
using SimplifyIoC.Bootstraps;
using SimplifyIoC.UI;

namespace SimplifyIoC.Settings
{
    public interface IPrefabCreator
    {
        bool GetUIPrefab(string name, out SimplifyUI prefab);
        void GetUIPrefab(string name, Action<SimplifyUI> callback);
        bool GetUIPrefab(Type type, out SimplifyUI prefab);
        void GetUIPrefab(Type type, Action<SimplifyUI> callback);
        bool GetBootstrapPrefab(string name, out Bootstrap prefab);
        void GetBootstrapPrefab(string name, Action<Bootstrap> callback);
    }
}
