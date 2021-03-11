using SimplifyIoC.Extensions.Mediations;
using UMa.Signals;
using UMa.Views;
//============================================================
//支持中文，文件使用UTF-8编码
//@author	JiphuTzu
//@create	20210311
//@company	UMa
//
//@description:
//============================================================
namespace UMa.Mediators
{
    public class LifeTimeMediator : Mediator {
		[Inject]
		public LifeTimeView view{get;set;}
		[Inject]
		public RecordChangedSignal rcs{get;set;}
        public override void OnRegister()
        {
            base.OnRegister();
			rcs.Dispatch(true);
			view.OnDead.AddListener(OnDead);
        }
		protected void OnDead(){
			rcs.Dispatch(false);
			Destroy(gameObject);
		}
    
	}
}
