using System.Collections.Generic;
using SimplifyIoC.Mediations;
using SimplifyIoC.Utils;
using UnityEngine;
using UnityEngine.UI;

namespace UMa.Views
{
    public class MenuView : View
    {
        public Button[] buttons;
        public List<Button> buttonsList;
        protected override void Start()
        {
            base.Start();
            this.AddAttributeParser(this.GetEventMethodParser()).ParseAttributes();
        }

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
