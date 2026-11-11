#!/usr/bin/env python3
"""SimplifyIoC 2.1.0 业务代码静态自检。

用法:
    python lint_mvcs.py <文件或目录> [文件或目录 ...]

扫描 .cs 文件，检查启动命令声明、注入点、默认架构约定（View 承接逻辑 /
UI 用 SimplifyUI / 链式优先 / 启动命令内嵌）、行尾、AOT 禁用法、订阅与异步命令漏项。
ERROR 数大于 0 时退出码为 1。
只做文本层检查，不替代 Unity 编辑器里的编译与运行验证。
"""

from __future__ import annotations

import re
import sys
from pathlib import Path

try:
    sys.stdout.reconfigure(encoding="utf-8")
except Exception:  # pragma: no cover - 老版本 Python / 非标准 stdout
    pass

ERROR = "ERROR"
WARN = "WARN"

FORBIDDEN = [
    (
        "E002",
        r"\bExpression\s*\.\s*Compile\b|\bReflection\s*\.\s*Emit\b|\bSystem\s*\.\s*Reflection\s*\.\s*Emit\b",
        "AOT 禁用 Expression.Compile / Reflection.Emit",
    ),
]


def strip_comments(text: str) -> str:
    """剥离注释但保留换行，以便报告的行号仍然准确。"""

    def blank(match: re.Match) -> str:
        return "\n" * match.group(0).count("\n")

    text = re.sub(r"/\*.*?\*/", blank, text, flags=re.S)
    return re.sub(r"//[^\n]*", "", text)


def collect(paths: list[str]) -> list[Path]:
    files: list[Path] = []
    for raw in paths:
        path = Path(raw)
        if path.is_dir():
            files.extend(sorted(path.rglob("*.cs")))
        elif path.is_file() and path.suffix == ".cs":
            files.append(path)
        else:
            print(f"[WARN] 忽略非 .cs 路径: {path}")
    return files


def check(path: Path) -> list[tuple[str, str, int, str]]:
    data = path.read_bytes()
    issues: list[tuple[str, str, int, str]] = []

    def add(level: str, code: str, line: int, msg: str) -> None:
        issues.append((level, code, line, msg))

    # 行尾：仓库根 .gitattributes 强制纯 LF
    if b"\r\r\n" in data:
        add(ERROR, "E101", 1, "出现 \\r\\r\\n（改行尾时重复转换的典型事故）")
    elif b"\r\n" in data:
        add(ERROR, "E102", 1, "文件含 CRLF 行尾；本仓库要求纯 LF")
    elif b"\r" in data:
        add(ERROR, "E103", 1, "文件含孤立 CR；本仓库要求纯 LF")

    text = data.decode("utf-8-sig", errors="replace")
    code = strip_comments(text)

    def lineno(pos: int) -> int:
        return code.count("\n", 0, pos) + 1

    for cid, pattern, msg in FORBIDDEN:
        for match in re.finditer(pattern, code):
            add(ERROR, cid, lineno(match.start()), msg)

    # [Inject] 只允许标属性（AttributeTargets.Property）
    for match in re.finditer(r"\[Inject[^\]]*\]", code):
        tail = code[match.end() : match.end() + 400]
        semi = tail.find(";")
        brace = tail.find("{")
        if semi == -1 or (brace != -1 and brace < semi):
            continue
        head = tail[:semi]
        if "{ get" not in head and "get;" not in head:
            add(ERROR, "E003", lineno(match.end()), "[Inject] 只能标属性 { get; set; }，不能标字段")

    # 派生 SimplifyBootstrap 必须指定启动命令
    if re.search(r":\s*SimplifyBootstrap\b", code) and "SetStartupCommand" not in code:
        add(ERROR, "E004", 1, "派生 SimplifyBootstrap 必须实现 protected override void SetStartupCommand()")

    # 默认不使用 Mediator：逻辑应留在 View / SimplifyUI
    for match in re.finditer(r"\bMediator\b|\bBindView\s*<|namespace\s+[\w\.]+\.Mediators\b", code):
        add(WARN, "W201", lineno(match.start()), "默认不使用 Mediator；视图逻辑请写在 View / SimplifyUI 内")
        break

    # UI 类应以 SimplifyUI 为基类
    for match in re.finditer(
        r"class\s+\w*(?:UI|Panel|Popup|Hud|Dialog|Toast|Window|Screen)\s*:\s*View\b", code
    ):
        add(WARN, "W202", lineno(match.start()), "UI 界面应以 SimplifyUI 为基类，不要直接继承 View")
        break

    # BindCommand 的 key 必须是 Signal 类型
    for match in re.finditer(r"BindCommand\s*<\s*([A-Za-z_][\w\.]*)", code):
        key = match.group(1)
        if not key.endswith(("Signal", "BaseSignal")):
            add(WARN, "W203", lineno(match.start()), f"BindCommand 的 key `{key}` 不是 Signal 类型；非信号 key 没有触发路径")

    # 链式优先：不要把绑定拆成中间变量分步赋值
    for match in re.finditer(
        r"var\s+\w+\s*=\s*(?:injectionBinder|commandBinder)\s*\.\s*Bind", code
    ):
        add(WARN, "W204", lineno(match.start()), "链式优先：绑定请写成一条 fluent 链，不要引入中间变量分步调用")
        break

    # GetSignal<T>() 已从 View 移除，任何使用都会编译失败
    get_signal = re.search(r"\bGetSignal\s*<", code)
    if get_signal:
        add(WARN, "W205", lineno(get_signal.start()), "GetSignal<T>() 已从 View 移除：UI 用 Signal<T>()，普通 View 用 injectionBinder.GetInstance<T>()")

    # 普通 View / Mediator 不会自动解析声明式特性（只有 SimplifyUI 会）：
    # 写了 [Child] / [BindEvent] / [BindMethod] / [MainThread] 却没有显式解析链会静默失效。
    declarative = re.search(r"\[(?:Child|BindEvent|BindMethod|MainThread)\b", code)
    if declarative and not re.search(r":\s*SimplifyUI\b", code):
        if not re.search(r"\bParseAttributes\s*\(|\bParseFields\s*\(", code):
            add(WARN, "W207", lineno(declarative.start()),
                "普通 View / Mediator 上的声明式特性不会自动解析：改继承 SimplifyUI，或显式调用 AddAttributeParser(...).ParseAttributes()")

    # 启动命令应为 Bootstrap 内的私有嵌套类，不单独建文件
    if path.stem.endswith("StartupCommand") and re.search(
        r"\bpublic\s+class\s+\w*StartupCommand\b", code
    ):
        add(WARN, "W206", 1, "启动命令应为 Bootstrap 内的私有嵌套类，不要单独建 StartupCommand 文件")

    # 异步命令的 Retain / Release 与 Restore 契约
    if re.search(r"\bRetain\s*\(\s*\)", code) and not re.search(r"\bRelease\s*\(\s*\)", code):
        add(WARN, "W301", 1, "出现 Retain() 但未见 Release()，异步命令会永久挂在池外")
    if re.search(r"override\s+void\s+Restore\s*\(", code) and "base.Restore()" not in code:
        add(WARN, "W302", 1, "覆写 Restore() 必须调 base.Restore()，否则订阅组与注入不清理")

    # 裸 AddListener 的泄漏风险
    if (
        re.search(r"\.AddListener\s*\(", code)
        and "subscriptions.Listen" not in code
        and "subscriptions.Track" not in code
        and ".RemoveListener(" not in code
    ):
        add(WARN, "W303", 1, "直接 AddListener 且未走订阅组/未 RemoveListener，确认不会泄漏")

    # 覆写 Start 必须保留 base.Start()（内含 SetStartupCommand + Launch / Init + OnShow）
    if re.search(r"override\s+void\s+Start\s*\(", code) and "base.Start()" not in code:
        add(WARN, "W304", 1, "覆写 Start() 时保留 base.Start()，否则启动命令或 UI 初始化不会触发")

    return issues


def main(argv: list[str]) -> int:
    if len(argv) < 2 or argv[1] in ("-h", "--help"):
        print("用法: python lint_mvcs.py <文件或目录> [文件或目录 ...]")
        return 0 if len(argv) > 1 else 2

    files = collect(argv[1:])
    if not files:
        print("未找到 .cs 文件")
        return 2

    total_e = total_w = 0
    for path in files:
        issues = check(path)
        for level, cid, line, msg in issues:
            print(f"[{level}] {cid} {path}:{line}: {msg}")
            if level == ERROR:
                total_e += 1
            else:
                total_w += 1

    print(f"\n扫描 {len(files)} 个文件：{total_e} ERROR / {total_w} WARN")
    if total_e:
        print("存在 ERROR，修正后再交付。")
    else:
        print("无 ERROR；WARN 请逐条人工确认。")
    return 1 if total_e else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
