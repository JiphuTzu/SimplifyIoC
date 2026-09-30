# SimplifyIoC

A simplify version of [StrangeIoC](https://github.com/strangeioc/strangeioc).

当前版本 **1.3.0**（Unity 2022.3+）。包内说明见 [Assets/SimplifyIoC/README.md](Assets/SimplifyIoC/README.md)，版本变更见 [CHANGELOG](Assets/SimplifyIoC/CHANGELOG.md)。

## 修改内容
+ 修改为一个UPM的结构，能够通过UnityPackageManager的git方式安装。
+ 删除MiniJSON，放弃json注入。
+ 主力使用Signal，删除掉Event相关代码（`EventDispatcher` 自 1.1.0 起已移除）。
+ 修改为符合Unity的习惯命名空间。
+ `InjectAttribute` 继承 `PreserveAttribute`，避免 CodeStrip 对构造函数和 setter 的过度优化；另随包提供 `link.xml` 作为裁剪兜底。
+ 1.3.0（含破坏性变更，详见 CHANGELOG 的「升级注意」）：声明式绑定改为框架在注入完成后自动解析，不再手写 `AddAttributeParser(...).ParseAttributes()`；`AddListener` / `AddOnce` 返回订阅句柄 `SignalSubscription`；所有命令统一池化；Context / Binder 的作用域与 View 归属规则重构。

## 使用方法
在Unity中打开PackageManager。左上角`+`下拉，找到`add package from git URL`，输入以下链接地址，点击`add`即可完成插件的添加。
```
    https://github.com/JiphuTzu/SimplifyIoC.git#upm
```
示例工程见 `Assets/Examples`，内含 `BasicSignalExample` 与 `CrossContextExample` 两个场景。

## 版本发布方式
+ [详细说明](https://www.jianshu.com/p/153841d65846)（[英文原文](https://www.patreon.com/posts/25070968)）
+ 将 "Assets/SimplifyIoC" 目录放到“ upm” 分支
```
    git subtree split --prefix=Assets/SimplifyIoC --branch upm
```
+ 设置tag的版本名并上传
```
    git tag 1.3.0 upm
    git push origin upm --tags
```
