# SimplifyIoC
A simplify version of [StrangeIoC](https://github.com/strangeioc/strangeioc).
## 修改目标
+ 修改为一个UPM的结构，能够通过UnityPackageManager的git方式安装。
+ 删除MiniJSON，放弃json注入。
+ 主力使用Signal，删除掉Event相关代码。
+ 修改为符合Unity的习惯命名空间
## 使用方法
在Unity中打开PackageManager。左上角`+`下拉，找到`add package from git URL`，输入以下链接地址，点击`add`即可完成插件的添加。
```
    https://github.com/JiphuTzu/SimplifyIoC.git#1.0.1
```

## 版本发布方式
+ [详细说明](https://www.jianshu.com/p/153841d65846)
+ 将 "Assets/SimplifyIoC" 目录放到“ upm” 分支
```
    git subtree split --prefix=Assets/SimplifyIoC --branch upm
```
+ 设置tag的版本名并上传
```
    git tag 1.0.0 upm
    git push origin upm --tags
```