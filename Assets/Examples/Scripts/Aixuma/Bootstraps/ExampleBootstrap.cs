using SimplifyIoC.Bootstraps;
using Aixuma.Commands;
using Aixuma.Mediators;
using Aixuma.Signals;
using Aixuma.Views;
using UnityEngine;
//============================================================
//支持中文，文件使用UTF-8编码
//@author	JiphuTzu
//@create	20210310
//@company	Aixuma
//
//@description:
//============================================================
namespace Aixuma.Bootstraps
{
    //2.0.0：旧 Context 更名为 Bootstrap——本类既是层级锚点（挂在显示层级顶部的那块组件），
    //也是绑定容器本身——旧的「Bootstrap 外壳 + 内嵌绑定容器」两层结构就此消失。
    //
    //跨场景共享：本类不需要认识常驻的 CrossContextBootstrap——Init() 无父参数，归属自动解析，
    //Transform 上没有父时就落到框架级锚点（Bootstrap.root，第一个构造成链根的 Bootstrap，
    //本场景里即 CrossContextBootstrap）之下，跨域绑定随之可用（2026-10-15 恢复 firstContext 语义）。
    //正因为不需要指定父，本类连 Awake 都不必覆写——继承来的 Awake 会在装载时自动 Init。
    //单独打开本场景、CrossContextBootstrap 不在场时锚点为 null，本 Bootstrap 自成链根并接任锚点。
    public class ExampleBootstrap : SimplifyBootstrap<ExampleStartupCommand>
    {
        //序列化字段：Unity 在 Awake 之前完成反序列化，所以 Init（→ MapBindings → BindValues）里读它是安全的。
        public GameObject[] objects;

        protected override void BindSignals()
        {
        }

        protected override void BindValues()
        {
            BindValue(objects);
        }

        protected override void BindCommands()
        {
            BindCommand<CreateObjectSignal, CreateObjectCommand, AfterCreateCommand>();
        }

        protected override void BindViews()
        {
            BindView<LifeTimeView, LifeTimeMediator>();
        }
    }
}
