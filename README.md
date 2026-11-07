# 学习好 · StudyGoodAndGood

给初中生用的学习管控工具：**先答题，再换取看视频 / 用电脑的时间**。
包含 Windows 电脑版、安卓电视版、手机远程管理页，以及一套约 3 万道初中选择题的题库。

---

## 一、两个程序

| 程序 | 管什么 | 运行平台 |
|---|---|---|
| StudyGoodAndGood | 电脑：答完题才进桌面，锁屏答题、错题提权 | Windows 10 / 11 |
| 学习好电视（StudyTV） | 电视：时间用完弹窗拦截，答题解锁，可设为电视桌面 | 安卓电视 / 盒子（Android 5+） |

两边各自独立保存规则，只装一个也能正常使用。

## 二、下载

所有可执行文件、安装包和说明书都在 [`C2510`](./C2510) 文件夹里：

| 文件 | 用途 |
|---|---|
| `StudyGoodAndGood.exe` | 电脑版，双击自安装到 Program Files 并注册开机启动 |
| `学习好电视-TV版.apk` | 电视版安装包 |
| `电视安装助手.exe` | 在电脑上给电视装 APK（局域网扫描或网页下载） |
| `题库编辑器（含导出给电视）.exe` | 自己出题，可导出 JSON 给电视用 |
| `家长使用说明书.pdf` | 图文版说明书，建议先看这个 |
| `家长使用说明书.html` | 网页版说明书，可点目录跳转 |
| `安装说明.txt` | 纯文字速查版 |

> 仓库里的二进制文件可直接用 `raw.githubusercontent.com` 链接下载，例如：
> `https://raw.githubusercontent.com/slash1224/studygoodandgood-questions/main/C2510/学习好电视-TV版.apk`

## 三、题库

- 题量：**30,399 道**，初中 7/8/9 年级选择题，覆盖数学、语文、英语、物理、化学、生物、历史、地理、道法、编程。
- 题库文件：根目录 [`questions.json`](./questions.json)，同时内嵌在电脑版和电视版里。
- 在线更新地址（程序内置，jsDelivr CDN）：
  `https://cdn.jsdelivr.net/gh/slash1224/studygoodandgood-questions@main/questions.json`
- 抽题规则：按科目均衡轮转；家长可在手机管理页勾选“偏科”，被勾选的科目一轮多抽 2 道（约 3 倍权重）；错题按做错次数加权，错 3 次约 6 倍出现概率。

## 四、仓库结构

```
├─ questions.json              # 官方题库（CDN 更新源）
├─ C2510/                    # 发布包：EXE / APK / 说明书
└─ source/
   ├─ StudyLock/               # 电脑版（C# / WPF, .NET 8）
   ├─ StudyTV/                 # 电视版（Java, 无 Gradle 构建）
   ├─ TVInstaller/             # 电视安装助手（C# / WPF，内嵌 adb 与 APK）
   ├─ BankEditor/              # 题库编辑器
   └─ bankbuild/gen/           # 题库生成脚本（Node.js）
```

## 五、自己构建

**电脑版 / 安装助手 / 编辑器**（需要 .NET 8 SDK）：

```powershell
dotnet publish -c Release -r win-x64 --self-contained `
  -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true
```

**电视版**（需要 JDK 17、Android SDK build-tools 34、zxing-core，放在仓库同级的 `tools/` 目录）：

```powershell
powershell -File source\StudyTV\build.ps1
```

`build.ps1` 以脚本所在目录为项目根，工具目录默认取上一级的 `tools`，也可以自行修改。

**题库生成**（需要 Node.js，并且把原始数据集放到 `bankbuild/external/`）：

```powershell
node source\bankbuild\gen\build.js
```

输出会写到 `bankbuild/dist/questions.json`、`StudyLock/questions.json`、`StudyTV/assets/questions.json` 三处。

## 六、说明

- 电视版签名密钥（`studygoodtv.keystore`）**未包含**在仓库中，请自行生成后再发布，避免更新包被他人冒签。
- 本项目为家庭自用工具，安装前请确认设备为本人所有；电脑版会注册开机自启与本地管理端口（8099），电视版为 8787。
- 使用前请先阅读 [`C2510/家长使用说明书.pdf`](./C2510/家长使用说明书.pdf)。
