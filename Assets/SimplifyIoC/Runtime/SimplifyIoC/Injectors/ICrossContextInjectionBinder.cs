/*
 * @interface SimplifyIoC.Injectors.ICrossContextInjectionBinder
 *
 * InjectionBinder 的跨域版本，允许在多个 Bootstrap 之间共享注入绑定。
 *
 * @see SimplifyIoC.Injectors.IInjectionBinder
 */

namespace SimplifyIoC.Injectors
{
    public interface ICrossContextInjectionBinder : IInjectionBinder
	{
		//Cross-context Injection Binder is shared across all child bootstraps
		IInjectionBinder crossContextBinder { get; set; }
	}
}
