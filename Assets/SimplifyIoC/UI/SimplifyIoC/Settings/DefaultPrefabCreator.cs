using System;
using System.Collections.Generic;
using SimplifyIoC.Bootstraps;
using SimplifyIoC.UI;
using UnityEngine;

namespace SimplifyIoC.Settings
{
    [CreateAssetMenu(fileName = "DefaultPrefabCreator", menuName = "Aixuma/DefaultPrefabCreator")]
    public class DefaultPrefabCreator : CustomPrefabCreator
    {
        [SerializeField] private List<SimplifyUI> _views;
        private Dictionary<string, SimplifyUI> _nameCache;
        private Dictionary<Type, SimplifyUI> _typeCache;

        private void OnEnable()
        {
            Debug.Log("DefaultPrefabCreator.OnEnable");
            _nameCache = new Dictionary<string, SimplifyUI>();
            _typeCache = new Dictionary<Type, SimplifyUI>();
            foreach (var p in _views)
            {
                if(p == null) continue;
                _nameCache.Add(p.name, p);
                _typeCache.Add(p.GetType(), p);
            }
        }
        public override bool GetUIPrefab(string name, out SimplifyUI prefab)
        {
            return _nameCache.TryGetValue(name, out prefab);
        }

        public override void GetUIPrefab(string name, Action<SimplifyUI> callback)
        {
            callback?.Invoke(_nameCache.GetValueOrDefault(name));
        }
        public override bool GetUIPrefab(Type type, out SimplifyUI prefab)
        {
            return _typeCache.TryGetValue(type, out prefab);
        }
        public override void GetUIPrefab(Type type, Action<SimplifyUI> callback)
        {
            callback?.Invoke(_typeCache.GetValueOrDefault(type));
        }
        public override bool GetBootstrapPrefab(string name, out Bootstrap prefab)
        {
            prefab = null;
            return false;
        }
        public override void GetBootstrapPrefab(string name, Action<Bootstrap> prefab)
        {
        }
    }
}
