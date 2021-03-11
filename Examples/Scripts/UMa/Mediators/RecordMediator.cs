using SimplifyIoC.Extensions.Mediations;
using UMa.Signals;
using UMa.Views;
//============================================================
//支持中文，文件使用UTF-8编码
//@author	JiphuTzu
//@create	20210311
//@company	UMa
//
//@description:
//============================================================
namespace UMa.Mediators
{
    public class RecordMediator : Mediator
    {
        [Inject]
        public RecordView view { get; set; }
        [ListensTo(typeof(RecordChangedSignal))]
        private void OnChanged(bool add)
        {
            if (add) view.Add();
            else view.Minus();
        }
    }
}
