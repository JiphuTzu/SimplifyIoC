using System.Runtime.CompilerServices;

// 阶段 4：测试需要对框架的注入后解析时机、幂等哨兵、短路处理器做白盒回归，
// 因此把 internal 面开放给测试程序集（不影响 Examples / 用户程序集）。
[assembly: InternalsVisibleTo("UMa.SimplifyIoC.Tests")]
