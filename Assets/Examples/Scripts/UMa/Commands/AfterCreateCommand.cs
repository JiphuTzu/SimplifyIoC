using System.Collections;
using System.Collections.Generic;
using SimplifyIoC.Commands;
using UnityEngine;
//============================================================
//支持中文，文件使用UTF-8编码
//@author	JiphuTzu
//@create	20210312
//@company	UMa
//
//@description:
//============================================================
namespace UMa.Commands
{
    public class AfterCreateCommand : Command
    {
        public override void Execute()
        {
            Debug.Log("after create command execute");
        }
    }
}
