using SimplifyIoC.Mediations;
using SimplifyIoC.Utils;
using Aixuma.Signals;
using Aixuma.Views;
using UnityEngine;

namespace Aixuma.Mediators
{
    public class LifeTimeMediator : Mediator<LifeTimeView>
    {
        [Inject]
        public RecordChangedSignal rcs { get; set; }

        public override void OnRegister()
        {
            base.OnRegister();
            rcs.Dispatch(true);
        }

        [BindEvent("onDead", nameof(view))]
        public void OnDead()
        {
            rcs.Dispatch(false);
            Destroy(gameObject);
        }
    }
}
