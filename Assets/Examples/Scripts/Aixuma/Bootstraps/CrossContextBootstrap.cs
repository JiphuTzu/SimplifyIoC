using SimplifyIoC.Bootstraps;
using Aixuma.Commands;
using Aixuma.Mediators;
using Aixuma.Signals;
using Aixuma.Views;

namespace Aixuma.Bootstraps
{
    //本类就是常驻的跨场景根 Bootstrap 本身，
    //各场景的 Bootstrap 以它为显式父（或退而落到框架级锚点 Bootstrap.root）来共享跨域绑定。
    public class CrossContextBootstrap : SimplifyBootstrap
    {
        protected override void Awake()
        {
            base.Awake();
            DontDestroyOnLoad(gameObject);
        }

        protected override void SetStartupCommand()
        {
            SetStartupCommand<CrossContextStartupCommand>();
        }

        protected override void BindSignals()
        {
            BindSignal<CreateObjectSignal>(true);
            BindSignal<RecordChangedSignal>(true);
        }

        protected override void BindViews()
        {
            BindView<CameraView, CameraMediator>();
        }
    }
}
