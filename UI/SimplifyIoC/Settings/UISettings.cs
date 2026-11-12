using System;
using System.Collections.Generic;
using SimplifyIoC.Bootstraps;
using SimplifyIoC.UI;
using UnityEngine;
using UnityEngine.UI;

// ReSharper disable InconsistentNaming

namespace SimplifyIoC.Settings
{
    public enum UIType
    {
        Screen,
        Overlay,
        Popup,
        Toast
    }

    [CreateAssetMenu(fileName = "UISettings", menuName = "Aixuma/UISettings")]
    public class UISettings : ScriptableObject
    {
        public CustomPrefabCreator[] prefabCreators;
        private List<IPrefabCreator> _creators;
        private Transform _popupMask;
        private readonly Dictionary<Type, Queue<SimplifyUI>> _viewCache = new();
        private readonly List<IToast> _toastsInShow = new();
        private readonly Dictionary<UIType, Transform> _viewRoot = new();
        private readonly List<string> _interactMasks = new();
        private GraphicRaycaster _raycaster;
        public int safeAreaTop { get; private set; }
        public int safeAreaBottom { get; private set; }
        public float canvasHeight { get; private set; }
        public float canvasScale { get;private set; }
        public RectTransform canvasRectTrans { get; private set; }
        public Camera camera { get; private set; }

        public UISettings InitRoots(Transform screen, Transform overlay, Transform popup)
        {
            _popupMask = popup.GetChild(0);
            _popupMask.GetComponent<Button>().onClick.AddListener(HideCurrentPopup);
            _popupMask.gameObject.SetActive(false);
            //
            _raycaster = _popupMask.GetComponentInParent<GraphicRaycaster>();
            _viewRoot.Add(UIType.Popup, popup);
            _viewRoot.Add(UIType.Screen, screen);
            _viewRoot.Add(UIType.Overlay, overlay);
            _viewRoot.Add(UIType.Toast, screen.parent);
            //
            _creators = new List<IPrefabCreator>();
            foreach (var creator in prefabCreators)
                _creators.Add(creator);
            //
            var safeArea = Screen.safeArea;
            var canvas = screen.GetComponentInParent<Canvas>();
            camera = canvas.worldCamera;
            canvasRectTrans = canvas.GetComponent<RectTransform>();
            var height = Screen.height;
            safeAreaBottom = (int)safeArea.y;
            safeAreaTop = (int)(height - safeArea.height - safeArea.y);
            if(safeAreaTop < 50) safeAreaTop = 50;
            if(safeAreaBottom < 50) safeAreaBottom = 50;
            var ort = overlay.GetComponent<RectTransform>();
            ort.offsetMax = new Vector2(0, -safeAreaTop);
            ort.offsetMin = new Vector2(0, safeAreaBottom);
            canvasHeight = canvasRectTrans.sizeDelta.y;
            canvasScale = height / 1920f;
            return this;
        }

        public void AddCreator(IPrefabCreator creator)
        {
            _creators.Add(creator);
        }

        public void AddCreatorAt(CustomPrefabCreator creator, int index)
        {
            if (index < _creators.Count)
                _creators.Insert(index, creator);
            else
                _creators.Add(creator);
        }
        //以中心对齐方式与Canvas进行对齐，进行坐标转换
        public Vector2 ScreenToAnchored(Vector2 screen)
        {
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRectTrans, screen, camera, out var local))
            {
                return local;
            }
            return screen;
        }

        public Vector2 WorldToAnchored(Vector2 world)
        {
            return ScreenToAnchored(WorldToScreen(world));
        }
        public Vector2 WorldToScreen(Vector3 worldPosition)
        {
            return camera.WorldToScreenPoint(worldPosition);
        }

        public Vector3 ScreenToWorld(Vector3 screenPosition,float z = 100)
        {
            screenPosition.z = z;
            return camera.ScreenToWorldPoint(screenPosition);
        }

        public void Interactable(string target, bool value)
        {
            if (_raycaster == null) return;
            if (value)
            {
                if (_interactMasks.Count == 0) return;
                _interactMasks.Remove(target);
                if (_interactMasks.Count == 0) _raycaster.enabled = true;
            }
            else
            {
                _interactMasks.Add(target);
                if (_interactMasks.Count == 1) _raycaster.enabled = false;
            }
        }

        public void Clear()
        {
            _popupMask = null;
            _viewRoot.Clear();
            _toastsInShow.Clear();
        }
        public SimplifyUI CreateView(string name, Dictionary<UIType, Transform> roots = null)
        {
            return GetCustomPrefab(name, out var prefab) 
                ? CreateView(prefab.GetType(), prefab, roots)
                : null;
        }
        public void CreateView(string name, Action<SimplifyUI> onCreate, Dictionary<UIType, Transform> roots = null)
        {
            GetCustomPrefab(0, name, prefab =>
            {
                if (prefab == null) return;
                var view = CreateView(prefab.GetType(), prefab, roots);
                onCreate?.Invoke(view);
            });
        }
        public T CreateView<T>(Dictionary<UIType, Transform> roots = null) where T : SimplifyUI
        {
            var type = typeof(T);
            Debug.Log($"Create View By Type {type}");
            if (GetCustomPrefab(type, out var prefab))
                return CreateView(type, prefab, roots) as T;
            return null;
        }
        public void CreateView<T>(Action<T> callback, Dictionary<UIType, Transform> roots = null) where T : SimplifyUI
        {
            var type = typeof(T);
            Debug.Log($"Create View By Type {type}");
            GetCustomPrefab(0, type, prefab =>
            {
                if(prefab == null || prefab is not T) return;
                var view = CreateView(type, prefab, roots) as T;
                callback?.Invoke(view);
            });
        }
        public bool GetActiveView<T>(out T view) where T : SimplifyUI
        {
            view = null;
            var type = typeof(T);
            if (!_viewCache.TryGetValue(type, out var views)) return false;
            foreach (var v in views)
            {
                if (v.gameObject.activeSelf)
                {
                    view = v as T;
                    return true;
                }
            }

            return false;
        }
        public bool IsViewShowing<T>() where T : SimplifyUI
        {
            var type = typeof(T);
            if (!_viewCache.TryGetValue(type, out var views)) return false;
            foreach (var view in views)
            {
                if (view.gameObject.activeSelf) return true;
            }

            return false;
        }
        public UISettings ReleaseView<T>() where T : SimplifyUI
        {
            var type = typeof(T);
            if (!_viewCache.TryGetValue(type, out var views)) return this;
            foreach (var view in views)
            {
                Destroy(view.gameObject);
            }
            views.Clear();
            _viewCache.Remove(type);
            return this;
        }

        public void CreateCustomBootstrap(string name, Transform parent = null)
        {
            foreach (var creator in _creators)
            {
                if (creator == null 
                    || !creator.GetBootstrapPrefab(name, out var prefab)) 
                    continue;
                parent ??= _viewRoot[UIType.Toast];
                var bs = Instantiate(prefab, parent);
                bs.name = prefab.name;
                break;
            }
        }
        public void CreateCustomBootstrap(string name, Action<Bootstrap> onCreate, Transform parent = null)
        {
            GetCustomBootstrap(0, name, prefab =>
            {
                if(prefab == null) return;
                parent ??= _viewRoot[UIType.Toast];
                var bs = Instantiate(prefab, parent);
                bs.name = prefab.name;
                onCreate?.Invoke(bs);
            });
        }

        
        private bool GetCustomPrefab(string name, out SimplifyUI prefab)
        {
            foreach (var creator in _creators)
            {
                if (creator != null 
                    && creator.GetUIPrefab(name, out prefab)) 
                    return true;
            }
            prefab = null;
            return false;
        }

        private void GetCustomPrefab(int index, string name, Action<SimplifyUI> callback)
        {
            if(index < 0 || index >= _creators.Count) return;
            if (_creators[index] == null)
            {
                GetCustomPrefab(index + 1,name,callback);
                return;
            }
            _creators[index].GetUIPrefab(name, prefab =>
            {
                if(prefab == null) GetCustomPrefab(index + 1,name,callback);
                else callback?.Invoke(prefab);
            });
        }
        private bool GetCustomPrefab(Type type, out SimplifyUI prefab)
        {
            foreach (var creator in _creators)
            {
                if (creator != null 
                    && creator.GetUIPrefab(type, out prefab))
                    return true;
            }
            prefab = null;
            return false;
        }
        private void GetCustomPrefab(int index,Type type, Action<SimplifyUI> callback)
        {
            if(index < 0 || index >= _creators.Count) return;
            if (_creators[index] == null)
            {
                GetCustomPrefab(index + 1,type,callback);
                return;
            }
            _creators[index].GetUIPrefab(type, prefab =>
            {
                if(prefab == null) GetCustomPrefab(index + 1,type,callback);
                else callback?.Invoke(prefab);
            });
        }

        private void GetCustomBootstrap(int index, string name, Action<Bootstrap> callback)
        {
            if(index < 0 || index >= _creators.Count) return;
            if (_creators[index] == null)
            {
                GetCustomBootstrap(index + 1, name,callback);
                return;
            }
            _creators[index].GetBootstrapPrefab(name, prefab =>
            {
                if(prefab == null) GetCustomBootstrap(index + 1,name,callback);
                else callback?.Invoke(prefab);
            });
        }
        private SimplifyUI CreateView(Type type, SimplifyUI prefab, Dictionary<UIType, Transform> roots)
        {
            if (prefab == null) return null;
            SimplifyUI view;
            if (!_viewCache.TryGetValue(type, out var views) || views == null || views.Count == 0)
            {
                var parent = _viewRoot[prefab.type];
                if(roots != null && roots.TryGetValue(prefab.type, out var customParent) && customParent != null)
                    parent = customParent;
                view = Instantiate(prefab, parent);
                view.gameObject.SetActive(false);
                view.name = prefab.name;
                view.onRecycle.AddListener(OnViewRecycle);
            }
            else
            {
                view = views.Dequeue();
                if (view == null) return null;
            }

            if (view.type == UIType.Screen)
                ShowScreen(view);
            else if (view.type == UIType.Overlay)
                ShowOverlay(view);
            else if (view.type == UIType.Popup)
                ShowPopup(view);
            else if (view.type == UIType.Toast)
                ShowToast(view);
            return view;
        }

        private void ShowScreen(SimplifyUI view)
        {
            var trans = view.transform;
            //切换Screen时，会把Popup都隐藏掉
            var root = _viewRoot[UIType.Popup];
            var n = root.childCount;
            for (var i = 0; i < n; i++)
            {
                var child = root.GetChild(i);
                if (child == _popupMask || !child.gameObject.activeSelf) continue;
                var popup = child.GetComponent<SimplifyUI>();
                popup.Hide(false); //不调用OnViewHide重置Mask
                RecycleView(popup);
            }

            _popupMask.gameObject.SetActive(false);

            //切换Screen时，会把Overlay都隐藏掉
            root = _viewRoot[UIType.Overlay];
            n = root.childCount;
            for (var i = 0; i < n; i++)
            {
                var child = root.GetChild(i);
                if (child.gameObject.activeSelf)
                {
                    child.GetComponent<SimplifyUI>().Hide();
                }
            }

            //只显示一个Screen
            root = _viewRoot[UIType.Screen];
            n = root.childCount;
            for (var i = 0; i < n; i++)
            {
                var child = root.GetChild(i);
                if (child != trans && child.gameObject.activeSelf) //已经隐藏状态，就不再重复调用
                    child.GetComponent<SimplifyUI>().Hide();
            }

            //
            view.Show();
        }

        private void ShowOverlay(SimplifyUI view)
        {
            view.Show();
            //需要把所有Popup隐藏掉
            var root = _viewRoot[UIType.Popup];
            var n = root.childCount;
            for (var i = 0; i < n; i++)
            {
                var child = root.GetChild(i);
                if (child != _popupMask && child.gameObject.activeSelf)
                {
                    var popup = child.GetComponent<SimplifyUI>();
                    popup.Hide(false); //不调用OnViewHide重置Mask
                    RecycleView(popup);
                }
            }

            _popupMask.gameObject.SetActive(false);
        }

        private void ShowPopup(SimplifyUI view)
        {
            _popupMask.gameObject.SetActive(true);
            _popupMask.SetAsLastSibling();
            view.transform.SetAsLastSibling();
            view.Show();
        }

        private void ShowToast(SimplifyUI view)
        {
            if (view is not IToast toast) return;
            foreach (var t in _toastsInShow)
            {
                t.MoveUp();
            }

            _toastsInShow.Add(toast);
            view.Show();
        }

        private void HideCurrentPopup()
        {
            var root = _viewRoot[UIType.Popup];
            var popup = root.GetChild(root.childCount - 1).GetComponent<SimplifyUI>();
            if (popup == null || !popup.canHideByMask) return;
            popup.Hide();
        }

        private void OnViewRecycle(SimplifyUI view)
        {
            if (view.type == UIType.Popup)
            {
                var root = _viewRoot[UIType.Popup];
                var count = root.childCount;
                if (root.GetChild(count - 1) == view.transform)
                {
                    if (count <= 2) //只有当前view和mask，把mask隐藏掉
                        _popupMask.gameObject.SetActive(false);
                    else if (root.GetChild(count - 3).gameObject.activeSelf) //mask 下面一个popup在显示状态
                        _popupMask.SetSiblingIndex(count - 3);
                    else
                        _popupMask.gameObject.SetActive(false); //没有popup显示，隐藏msdk
                }
            }
            else if (view.type == UIType.Toast)
            {
                _toastsInShow.Remove(view as IToast);
            }

            RecycleView(view);
        }

        private void RecycleView(SimplifyUI view)
        {
            if (view.disposeOnHide)
            {
                Destroy(view.gameObject);
            }
            else
            {
                var type = view.GetType();
                view.gameObject.SetActive(false);
                if (!_viewCache.TryGetValue(type, out var views))
                {
                    views = new Queue<SimplifyUI>();
                    _viewCache[type] = views;
                }

                views.Enqueue(view);
            }
        }
    }
}