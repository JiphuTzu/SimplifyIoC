using SimplifyIoC.Mediations;
using UnityEngine;
using UnityEngine.Events;

namespace Aixuma.Views
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
