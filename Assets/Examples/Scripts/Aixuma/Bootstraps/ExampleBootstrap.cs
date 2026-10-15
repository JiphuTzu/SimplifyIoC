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
    public class ExampleBootstrap : SimplifyBootstrap<ExampleStartupCommand>
    {
        //序列化字段：Unity 在 Awake 之前完成反序列化，所以 Init（→ MapBindings → BindValues）里读它是安全的。
        public GameObject[] objects;

        protected override void Awake()
        {
            //跨场景共享的两种写法：
            //  ① 显式认父（如下）——语义最清楚，也最可控；
            //  ② 不传 parent——框架级锚点（Bootstrap.root，第一个构造的链根 Bootstrap）会兜底，
            //     本 Bootstrap 自动挂到常驻的 CrossContextBootstrap 之下（2026-10-15 恢复 firstContext 语义）。
            //单独打开本场景、CrossContextBootstrap 不在场时两者都为 null，本 Bootstrap 自成链根并接任锚点。
            //注意：覆写 Awake 后直接 Init 即可，不要先调 base.Awake()——那样这里传的父会被忽略。
            Init(FindObjectOfType<CrossContextBootstrap>());
        }

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
