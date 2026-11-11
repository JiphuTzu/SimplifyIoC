using SimplifyIoC.Mediations;
using SimplifyIoC.Utils;
using Aixuma.Signals;
using Aixuma.Views;
using UnityEngine;

namespace Aixuma.Mediators
{
    public class CameraMediator : Mediator<CameraView>
    {
        [Inject]
        public CreateObjectSignal cos { get; set; }

        [BindEvent("onClick", nameof(view))]
        private void OnFloorClicked(Vector3 pos)
        {
            cos.Dispatch(pos);
        }
    }
}
