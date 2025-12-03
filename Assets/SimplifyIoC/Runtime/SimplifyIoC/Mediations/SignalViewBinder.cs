using System;
using System.Reflection;
using SimplifyIoC.Signals;
using UnityEngine;

namespace SimplifyIoC.Mediations
{
    public class SignalViewBinder : MediationBinder
    {
        public override void Trigger(MediationEvent evt, IView view)
        {
            switch (evt)
            {
                case MediationEvent.AWAKE:
                    InjectViewAndChildren(view);
                    break;
                case MediationEvent.DESTROYED:
                    UnmapView(view, null);
                    break;
                default:
                    break;
            }
        }
    }
}
