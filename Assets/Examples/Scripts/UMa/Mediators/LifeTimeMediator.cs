using SimplifyIoC.Mediations;
using SimplifyIoC.Utils;
using UMa.Signals;
using UMa.Views;
using UnityEngine;

//============================================================
//支持中文，文件使用UTF-8编码
//@author	JiphuTzu
//@create	20210311
//@company	UMa
//
//@description:	阶段 4 起改用 Mediator<TView> 强类型视图：
//				不再写 [Inject] LifeTimeView view，也不需要在 OnRegister 里手抄解析。
//============================================================
namespace UMa.Mediators
{
    public class LifeTimeMediator : Mediator<LifeTimeView>
    {
        [Inject]
        public RecordChangedSignal rcs { get; set; }

        public override void OnRegister()
        {
            base.OnRegister();
            rcs.Dispatch(true);
        }

        [BindEvent("onDead", nameof(view))]
        public void OnDead()
        {
            rcs.Dispatch(false);
            Destroy(gameObject);
        }
    }
}
