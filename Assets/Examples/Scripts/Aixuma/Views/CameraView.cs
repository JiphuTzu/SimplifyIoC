using System;
using SimplifyIoC.Mediations;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;

namespace Aixuma.Views
{
    public class CameraView : View {
        public UnityEvent<Vector3> onClick;
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
                onClick.Invoke(hit.point);
            }
        }
	}
}
