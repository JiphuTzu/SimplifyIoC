using SimplifyIoC.Contexts;
using Aixuma.Commands;
using Aixuma.Mediators;
using Aixuma.Signals;
using Aixuma.Views;
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
    public class CrossContextBootstrap : SimplifyBootstrap
    {
        protected override void Awake()
        {
            context = new CrossContext(this);

			DontDestroyOnLoad(gameObject);
        }
        private class CrossContext : SimplifyContext<CrossContextStartupCommand>
        {
            public CrossContext(Bootstrap view) : base(view)
            {
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
}
