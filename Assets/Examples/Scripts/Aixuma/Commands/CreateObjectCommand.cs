using SimplifyIoC.Commands;
using SimplifyIoC.Bootstraps;
using UnityEngine;

namespace Aixuma.Commands
{
    public class CreateObjectCommand : Command
    {
		//框架绑定的命名实例就是 Bootstrap 自身
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
            var go = Object.Instantiate(objects[index], root.transform);
            go.transform.position = pos + Vector3.up * Random.Range(0.8f, 1.6f);
            go.SetActive(true);
        }
    }
}