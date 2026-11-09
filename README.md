# 学习好 · StudyGoodAndGood

给初中生用的学习管控工具：**先答题，再换取看视频 / 用电脑的时间**。
包含 Windows 电脑版、安卓电视版、手机远程管理页、本地 AI 讲题老师，以及一套约 3 万道初中选择题的题库。

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
├─ Omni-Edu/                  # 本地 AI 老师（启动器、说明书、运行时下载脚本）
│  ├─ OmniEdu.exe             # 启动器（模型缺失时自动从魔搭下载）
│  ├─ 获取运行时.ps1          # 首次使用前运行，下载 llama.cpp 推理运行时
│  ├─ models/                 # 模型文件（不入库，首次运行自动下载约 12.4GB）
│  └─ runtime/                # 推理运行时（不入库，用脚本下载约 1.2GB）
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

## 六、本地 AI 老师（Omni-Edu，可选）

电脑版答错题时，可以调用本机的 Omni-Edu 小老师把题目讲明白。**完全离线运行，题目和对话内容不出本机。**

### 使用限制

| 项目 | 要求 |
|---|---|
| 操作系统 | **仅 Windows 10 / 11**；安卓手机、安卓电视、苹果设备暂不支持 |
| 显卡 | **NVIDIA 显卡，推荐 RTX 3060 12GB 及以上**；显存低于 8GB 无法运行，8GB 只能跑 4B 小模型 |
| 磁盘 | 模型约 12.4GB、运行时约 1.2GB，另需临时下载空间 |
| 运行占用 | 9B 模型约占用 7–8GB 显存，关闭 AI 页面后自动释放 |

### 首次准备（只需做一次）

1. 不用单独安装：保持仓库里 `Omni-Edu` 和 `C2510` 的相对位置不变，双击 `C2510\StudyGoodAndGood.exe` 首次安装时会自动记录 `Omni-Edu` 的路径，**任意盘符都可以**（也兼容旧的 `H:\Omni-Edu`）。如果之后移动了文件夹，请把新路径填到 `rules.json` 的 `AiLauncherPath`。
2. 在 `Omni-Edu` 文件夹里运行 `获取运行时.ps1`，从 llama.cpp 官方发布页下载 Windows CUDA 12.4 运行时（两个 zip，约 650MB）。
   - 仓库里为什么不带 runtime：其中 `ggml-cuda.dll` 和 `cublasLt64_12.dll` 分别有 585MB / 451MB，超过 GitHub 单文件 100MB 限制，无法直接提交。
3. 先双击 `OmniEdu.exe`，等它自动从魔搭下载模型（约 12.4GB，需联网，只需一次）。下载完成后，电脑版锁屏里的 AI 老师就能离线使用。

### 使用方式

- 锁屏界面点「有问题？问 AI 老师」
- 答题没过关时，结果页点「AI 老师讲解错题」
- AI 页面关闭后，推理进程自动退出并释放显存

> 官方运行时下载地址（脚本已内置；国内访问 GitHub 慢时可挂代理）：
> `https://github.com/ggml-org/llama.cpp/releases/tag/b12211`

## 七、说明

- 电视版签名密钥（`studygoodtv.keystore`）**未包含**在仓库中，请自行生成后再发布，避免更新包被他人冒签。
- 本项目为家庭自用工具，安装前请确认设备为本人所有；电脑版会注册开机自启与本地管理端口（8099），电视版为 8787。
- 使用前请先阅读 [`C2510/家长使用说明书.pdf`](./C2510/家长使用说明书.pdf)。
