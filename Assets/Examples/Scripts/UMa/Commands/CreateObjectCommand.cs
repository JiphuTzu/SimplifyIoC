using SimplifyIoC.Commands;
using SimplifyIoC.Contexts;
using UnityEngine;
//============================================================
//支持中文，文件使用UTF-8编码
//@author	JiphuTzu
//@create	20210310
//@company	UMa
//
//@description:
//============================================================
namespace UMa.Commands
{
    public class CreateObjectCommand : Command
    {
		[Inject(ContextKeys.CONTEXT_VIEW)]
		public ContextView root{get;set;}
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