using SimplifyIoC.Extensions.Commands;
using UnityEngine;
using UnityEngine.SceneManagement;
//============================================================
//支持中文，文件使用UTF-8编码
//@author	JiphuTzu
//@create	20210311
//@company	UMa
//
//@description:
//============================================================
namespace UMa.Commands
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
