using System.Collections.Generic;
using SimplifyIoC.Mediations;
using SimplifyIoC.Utils;
using UnityEngine;
using UnityEngine.UI;

namespace UMa.Views
{
    //阶段 4：删掉 Start() 里手抄的 this.AddAttributeParser(this.GetEventMethodParser()).ParseAttributes()，
    //[BindEvent] 由框架在注入完成后自动解析（见 View.InitAttributes / MediationBinder.InjectViewAndChildren）。
    public class MenuView : View
    {
        public Button[] buttons;
        public List<Button> buttonsList;

        [BindEvent("onClick", nameof(buttons))]
        private void OnClick()
        {
            Debug.Log("OnClick");
        }

        [BindEvent("onClick", nameof(buttonsList))]
        private void OnListClick()
        {
            Debug.Log("OnListClick");
        }
    }
}
