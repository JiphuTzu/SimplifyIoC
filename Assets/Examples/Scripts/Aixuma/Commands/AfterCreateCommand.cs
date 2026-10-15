using SimplifyIoC.Commands;
using UnityEngine;
//============================================================
//支持中文，文件使用UTF-8编码
//@author	JiphuTzu
//@create	20210312
//@company	Aixuma
//
//@description:
//============================================================
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
