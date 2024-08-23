using SimplifyIoC.Mediations;
using UnityEngine;
using UnityEngine.Events;
//============================================================
//支持中文，文件使用UTF-8编码
//@author	JiphuTzu
//@create	20210311
//@company	UMa
//
//@description:
//============================================================
namespace UMa.Views
{
    public class LifeTimeView : View {
		public float lifeTime = 2;
		public UnityEvent onDead;
		private float _startTime;
        protected override void Start()
        {
            base.Start();
			_startTime = Time.time;
        }
		private void Update(){
			if(Time.time-_startTime>lifeTime){
				onDead.Invoke();
			}
		}
	}
}
