using SimplifyIoC.Extensions.Mediations;
using UMa.Signals;
using UMa.Views;
using UnityEngine;
//============================================================
//支持中文，文件使用UTF-8编码
//@author	JiphuTzu
//@create	20210310
//@company	UMa
//
//@description:
//============================================================
namespace UMa.Mediators
{
    public class CameraMediator : Mediator
    {
        [Inject]
        public CameraView view { get; set; }
        [Inject]
        public CreateObjectSignal cos { get; set; }
        public override void OnRegister()
        {
            base.OnRegister();
            view.OnClicked.AddListener(OnFloorClicked);
        }
        private void OnFloorClicked(Vector3 pos)
        {
            cos.Dispatch(pos);
        }
    }
}
