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
            context = new ExampleContext(this);
        }
        //=========Context=============
        private class ExampleContext : SimplifyContext<ExampleStartupCommand>
        {
            public ExampleContext(Bootstrap view) : base(view) { }
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