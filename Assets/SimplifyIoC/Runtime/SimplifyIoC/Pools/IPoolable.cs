/*
 * @interface SimplifyIoC.Pools.IPoolable
 *
 * 属于 Pool 的对象所实现的接口。
 *
 * 池化实例在复用之前通常需要清理——这既是实例管理的需要，也是内存管理的需要：
 * 你不会希望挂在实例上的引用一直占着内存。实现 IPoolable 后，
 * 把所有清理代码放进 Restore()，实例归还 Pool 时会自动执行。
 *
 * 不实现 IPoolable 也完全可以，但那样就必须在把实例归还 Pool 之前自行完成清理。
 */

namespace SimplifyIoC.Pools
{
    public interface IPoolable
	{
		/// <summary>
		/// Clean up this instance for reuse.
		/// </summary>
		/// Restore methods should clean up the instance sufficiently to remove prior state.
		void Restore ();

		/// <summary>
		/// Keep this instance from being returned to the pool 
		/// </summary>
		void Retain ();

		/// <summary>
		/// Release this instance back to the pool.
		/// </summary>
		/// Release methods should clean up the instance sufficiently to remove prior state.
		void Release();

		/// <summary>
		/// Is this instance retained?
		/// </summary>
		/// <value><c>true</c> if retained; otherwise, <c>false</c>.</value>
		bool retain { get; }
	}
}