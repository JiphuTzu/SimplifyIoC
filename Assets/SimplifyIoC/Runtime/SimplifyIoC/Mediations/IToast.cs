namespace SimplifyIoC.Mediations
{
    public interface IToast
    {
        /// <summary>
        /// </summary>
        /// <param name="message">消息内容</param>
        /// <param name="duration">显示时间</param>
        /// <returns></returns>
        IToast SetMessage(string message, float duration = 2);

        IToast MoveUp();
    }
}
