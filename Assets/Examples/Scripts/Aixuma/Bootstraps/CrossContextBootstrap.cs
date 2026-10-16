using SimplifyIoC.Bootstraps;
using Aixuma.Commands;
using Aixuma.Mediators;
using Aixuma.Signals;
using Aixuma.Views;
using UnityEngine;
//============================================================
//支持中文，文件使用UTF-8编码
//@author	JiphuTzu
//@create	20210311
//@company	Aixuma
//
//@description:
//============================================================
namespace Aixuma.Bootstraps
{
    //2.0.0：旧 Context 更名为 Bootstrap——本类就是常驻的跨场景根 Bootstrap 本身，
    //各场景的 Bootstrap 以它为显式父（或退而落到框架级锚点 Bootstrap.root）来共享跨域绑定。
    public class CrossContextBootstrap : SimplifyBootstrap<CrossContextStartupCommand>
    {
        protected override void Awake()
        {
            base.Awake();
            DontDestroyOnLoad(gameObject);
        }

        protected override void BindCommands()
        {
        }

        protected override void BindSignals()
        {
            BindSignal<CreateObjectSignal>(true);
            BindSignal<RecordChangedSignal>(true);
        }

        protected override void BindValues()
        {
        }

        protected override void BindViews()
        {
            BindView<CameraView, CameraMediator>();
            //BindView<RecordView, RecordMediator>();
        }
    }
}
