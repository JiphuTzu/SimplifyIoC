using System;
using SimplifyIoC.Mediations;
using UnityEngine;
using UnityEngine.Events;
//============================================================
//支持中文，文件使用UTF-8编码
//@author	JiphuTzu
//@create	20210310
//@company	UMa
//
//@description:
//============================================================
namespace UMa.Views
{
    public class CameraView : View {
        [Serializable]
        public class ClickEvent:UnityEvent<Vector3>{}
        public ClickEvent OnClicked;
        private Camera _camera;
        protected override void Start()
        {
            base.Start();
            _camera =GetComponent<Camera>();
        }
        private void Update(){
            if(!Input.GetMouseButtonUp(0)) return;
            var ray = _camera.ScreenPointToRay(Input.mousePosition);
            if(Physics.Raycast(ray,out var hit)){
                OnClicked.Invoke(hit.point);
            }
        }
	}
}
