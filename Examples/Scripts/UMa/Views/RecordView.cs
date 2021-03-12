using SimplifyIoC.Mediations;
using UnityEngine.UI;
//============================================================
//支持中文，文件使用UTF-8编码
//@author	JiphuTzu
//@create	20210311
//@company	UMa
//
//@description:
//============================================================
namespace UMa.Views
{
    public class RecordView : View
    {
        public Text record;
        private int _total;
        private int _current;
        protected override void Start()
        {
            base.Start();
            Show();
        }

        public void Add()
        {
            _total += 1;
            _current += 1;
            Show();
        }
        public void Minus()
        {
            _current -= 1;
            Show();
        }
        private void Show()
        {
            record.text = $"{_current}/{_total}";
        }
    }
}
