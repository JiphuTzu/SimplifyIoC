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
            //常驻：跨场景共享的绑定与单例都挂在它这条链的共享跨域根 binder 上，
            //因此它必须先 Init 才能被别的场景认作父（Init 里会跳过尚未就绪的祖先）。
            Init();
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
