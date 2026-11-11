using SimplifyIoC.Commands;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Aixuma.Commands
{
    public class CrossContextStartupCommand : Command
    {
        public override void Execute()
        {
            Debug.Log("cross context startup command execute");
			SceneManager.LoadScene(1);
        }
    }
}
