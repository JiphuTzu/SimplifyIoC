using System.Collections;
//============================================================
//@author	JiphuTzu
//@create	2019/11/13
//@company	AMLab
//
//@description:在Start中调用context.Launch
//============================================================
namespace SimplifyIoC.Contexts
{
    public abstract class SimplifyBootstrap : ContextView
    {
        //在Awake中进行context的实例化
        protected abstract void Awake();

        protected virtual IEnumerator Start()
        {
            context.Launch();
            yield return null;
        }
    }
}