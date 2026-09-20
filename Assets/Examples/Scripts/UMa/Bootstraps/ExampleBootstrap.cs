using SimplifyIoC.Contexts;
using UMa.Commands;
using UMa.Mediators;
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
namespace UMa.Bootstraps
{
    public class ExampleBootstrap : SimplifyBootstrap
    {
        public GameObject[] objects;
        protected override void Awake()
        {
            //5.6：静态 Context.firstContext 已移除——两个没有 Transform 层级关系的 Context
            //不再自动结为父子。跨域共享必须显式表达：这里找到常驻的 CrossContext
            //（它所在的 GameObject 带 DontDestroyOnLoad），把本 Context 挂成它的子。
            //单独打开本场景、CrossContext 不在场时为 null，本 Context 自成链根。
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