using SimplifyIoC.Contexts;
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
    public class ExampleBootstrap : SimplifyBootstrap
    {
        public GameObject[] objects;
        protected override void Awake()
        {
            //跨场景共享的两种写法：
            //  ① 显式认父（如下）——语义最清楚，也最可控；
            //  ② 不传 parent——框架级锚点（Context.rootContext，第一个构造的链根 Context）会兜底，
            //     本 Context 自动挂到常驻的 CrossContext 之下（2026-10-15 恢复 firstContext 语义）。
            //单独打开本场景、CrossContext 不在场时两者都为 null，本 Context 自成链根并接任锚点。
            var crossRoot = FindObjectOfType<CrossContextBootstrap>();
            context = crossRoot != null && crossRoot.context != null
                ? new ExampleContext(this, crossRoot.context)
                : new ExampleContext(this);
        }
        //=========Context=============
        private class ExampleContext : SimplifyContext<ExampleStartupCommand>
        {
            public ExampleContext(Bootstrap view) : base(view) { }
            public ExampleContext(Bootstrap view, Context parent) : base(view, parent) { }
            protected override void BindSignals()
            {
				
            }
            protected override void BindValues()
            {
                var eb = bootstrap as ExampleBootstrap;
                BindValue(eb.objects);
            }
            protected override void BindCommands()
            {
                BindCommand<CreateObjectSignal, CreateObjectCommand,AfterCreateCommand>();
            }
            protected override void BindViews()
            {
				BindView<LifeTimeView,LifeTimeMediator>();
            }
        }
    }
}