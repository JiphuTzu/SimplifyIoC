using SimplifyIoC.Mediations;
using SimplifyIoC.Utils;
using Aixuma.Signals;
using Aixuma.Views;
using UnityEngine;
//============================================================
//支持中文，文件使用UTF-8编码
//@author	JiphuTzu
//@create	20210310
//@company	Aixuma
//
//@description:	阶段 4 起改用 Mediator<TView> 强类型视图：
//				不再写 [Inject] CameraView view，也不需要在 OnRegister 里手抄解析。
//============================================================
namespace Aixuma.Mediators
{
    public class CameraMediator : Mediator<CameraView>
    {
        [Inject]
        public CreateObjectSignal cos { get; set; }

        [BindEvent("onClick", nameof(view))]
        private void OnFloorClicked(Vector3 pos)
        {
            cos.Dispatch(pos);
        }
    }
}
