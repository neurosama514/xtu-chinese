# vendor/

打包完整安装包（内含官方安装程序）时，把 Intel 官方下载的安装包放到这个目录：

```
vendor/XTUSetup_7.14.2.135.exe
```

- 本项目**不会**把该文件提交到仓库（见 `.gitignore`），因为它属于 Intel 的版权文件
- 没有该文件时，`build.ps1 -Stage installer` 仍可生成"仅汉化补丁"的安装器
  （适用于对方已装 XTU 的情况）

官方下载页面：<https://www.intel.com/content/www/us/en/download/17881>
