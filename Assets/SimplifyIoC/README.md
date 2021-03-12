# SimplifyIoC
A simplify version of [StrangeIoC](https://github.com/strangeioc/strangeioc).

使用参考见[Example](https://github.com/JiphuTzu/SimplifyIoC/tree/main/Assets/Examples)
## 修改目标
+ 修改为一个UPM的结构，能够通过UnityPackageManager的git方式安装。
+ 删除MiniJSON，放弃json注入。
+ 主力使用Signal，删除掉Event相关代码。
+ 修改为符合Unity的习惯命名空间
## 支持平台
+ PC
+ WebGL
+ iOS
+ Android
+ UWP