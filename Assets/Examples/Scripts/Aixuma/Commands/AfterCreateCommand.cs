using SimplifyIoC.Commands;
using UnityEngine;

namespace Aixuma.Commands
{
    public class AfterCreateCommand : Command
    {
        public override void Execute()
        {
            Debug.Log("after create command execute");
        }
    }
}
