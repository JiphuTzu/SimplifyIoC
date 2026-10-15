using SimplifyIoC.Commands;
using SimplifyIoC.Bootstraps;
using UnityEngine;
//============================================================
//支持中文，文件使用UTF-8编码
//@author	JiphuTzu
//@create	20210310
//@company	Aixuma
//
//@description:
//============================================================
namespace Aixuma.Commands
{
    public class CreateObjectCommand : Command
    {
		//2.0.0：Context 更名为 Bootstrap 后，框架绑定的命名实例就是 Bootstrap 自身
		//（Bootstrap.AddCoreComponents 里 Bind<Bootstrap>().ToValue(this).ToName(BootstrapKeys.Bootstrap)）。
		[Inject(BootstrapKeys.Bootstrap)]
		public Bootstrap root{get;set;}
        [Inject]
        public GameObject[] objects { get; set; }
        [Inject]
        public Vector3 pos { get; set; }
        public override void Execute()
        {
            Debug.Log("create object command execute");
            var index = Random.Range(0, objects.Length);
            var go = GameObject.Instantiate(objects[index],root.transform);
            go.transform.position = pos + Vector3.up * Random.Range(0.8f, 1.6f);
            go.SetActive(true);
        }
    }
}