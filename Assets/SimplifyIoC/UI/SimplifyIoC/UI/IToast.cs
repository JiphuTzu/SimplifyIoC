namespace SimplifyIoC.UI
{
    public interface IToast
    {
        /// <summary>设置提示内容与显示时长。</summary>
        /// <param name="message">消息内容</param>
        /// <param name="duration">显示时间</param>
        IToast SetMessage(string message, float duration = 2);

        IToast MoveUp();
    }
}
