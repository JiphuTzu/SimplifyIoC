using UMa.Commands;
using UMa.Views;
using UMa.Mediators;
using UMa.Signals;
using SimplifyIoC.Extensions.Contexts;
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
        private class ExampleContext : SimplifyContext<ExampleBootstrap, ExampleStartupCommand>
        {
            public ExampleContext(ExampleBootstrap view) : base(view) { }
            protected override void BindSignals()
            {
				
            }
            protected override void BindValues()
            {
                BindValue(bootstrap.objects);
            }
            protected override void BindCommands()
            {
                BindCommand<CreateObjectSignal, CreateObjectCommand>();
            }
            protected override void BindViews()
            {
                
				BindView<LifeTimeView,LifeTimeMediator>();
            }
        }
    }
}