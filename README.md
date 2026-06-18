# Desktop Pet — 符玄桌面宠物

![Unity](https://img.shields.io/badge/Unity-2022.3.62_LTS-000000?logo=unity)
![Live2D](https://img.shields.io/badge/Live2D-Cubism_5--r.4-FF6B9D)
![C#](https://img.shields.io/badge/C%23-512BD4?logo=csharp)

将 **崩坏：星穹铁道 — 符玄** 作为 Live2D 桌面宠物，在 Windows 桌面上陪伴你。

利用 Unity 透明窗口 + Live2D Cubism SDK 渲染，结合物理模拟、交互反馈、昼夜/天气响应，营造生动的桌面伙伴体验。

---

## ✨ 功能一览

### 🎮 交互
- **拖拽移动** — 按住任意位置拖拽，角色会挣扎划水 + 衣服/头发物理摆动
- **分区点击反馈** — 点击头部/身体/腿部有不同反应（歪头戳脸/害羞捂胸/踢腿）
- **右键菜单** — 设置、动作、聊天、便签四标签面板
- **AI 对话** — 底部输入框 + DeepSeek Function Calling，可调用 20+ 工具
- **点击穿透** — 鼠标在宠物上可交互，在宠物外直接穿透到桌面，无需拖拽"激活"

### 🎭 动画
- **自然待机** — Perlin 噪声驱动的呼吸、身体微晃、眼球微动
- **11 种空闲动作** — 歪头卖萌、微笑眯眼、挑眉、星辉环绕、伸懒腰、爱心眨眼、数钱、委屈、法阵展开、害羞黑脸、困惑歪头
- **走路动画** — 横版走路 + 身体颠簸 + 无缝空闲过渡
- **眨眼** — 自动随机眨眼
- **鼠标跟随** — 眼球平滑追踪鼠标位置

### 🌤 时间/天气响应
- **昼夜感知** — 读取系统时间，夜晚眼皮微垂、犯困动作增多
- **天气响应** — 通过 wttr.in API 获取当地天气：
  - ☀️ 晴/多云 → 自然微笑
  - 🌧 阴雨/雷雨 → 委屈表情 + 皱眉
  - ❄️ 下雪 → 好奇张嘴睁大眼
- **待机气泡** — 30 秒无交互后头顶冒泡，内容根据时间/天气变化（"早安~""打雷了好可怕！""好晚了该睡了~"）

### 🤖 AI 聊天
- **DeepSeek API** — 集成 DeepSeek Chat + Function Calling，最多 5 轮工具调用循环
- **20+ 工具** — 打开网页、搜索、截图、调音量、记便签、查天气等
- **自动闲聊** — 无操作一段时间后角色主动搭话
- **句子队列** — 长回复逐句显示，打字机效果

### 📋 便签提醒
- **增删改查** — 本地 JSON 持久化，支持每日/工作日/每周重复
- **到期提醒** — 头顶气泡 + Windows Toast 通知
- **手机推送** — 通过 Server酱³ 推送到手机 App
- **AI 驱动** — 聊天时直接说"提醒我下午3点买菜"，AI 自动调用工具

### 🏃 物理
- **CubismPhysics** — 衣服/头发/裙子/配饰自然物理摆动
- **直接驱动** — 拖拽时帧间速度实时输入物理系统，裙子/法盘/头发惯性跟随
- **头发驱动** — 20 个输出参数全部物理绕过，实现飘逸效果

### 🖥 技术特性
- **透明窗口** — Win32 API（DWM + WS_EX_LAYERED）实现 Unity 窗口穿透 + 镂空
- **点击穿透** — 每帧动态管理 WS_EX_TRANSPARENT，宠物内交互、宠物外穿透
- **系统托盘** — Shell_NotifyIcon 最小化到通知区域，支持开机自启
- **底部输入栏** — 内置 AI 聊天输入框 + 角色预设提示词
- **调试窗口** — 实时调参面板
- **编码优化** — 默认 GBK 编码兼容中文

---

## 📂 目录结构

```
Desktop_per_pro/
├── code/desktop unity/
│   ├── Assets/
│   │   ├── Scripts/              ← C# 脚本（完整）
│   │   │   ├── DesktopPet.cs         # 主控制器
│   │   │   ├── Live2DRenderer.cs     # Live2D 渲染 + 动画 + 物理驱动
│   │   │   ├── DragHandler.cs        # 拖拽/点击交互
│   │   │   ├── TimeWeatherController.cs  # 昼夜/天气
│   │   │   ├── ChatBubble.cs         # 头顶气泡
│   │   │   ├── ChatManager.cs        # AI 对话 + Function Calling
│   │   │   ├── AutoChat.cs           # 自动闲聊
│   │   │   ├── BottomInputBar.cs     # 底部输入栏
│   │   │   ├── ContextMenu.cs        # 右键菜单（设置/动作/聊天/便签）
│   │   │   ├── ReminderManager.cs    # 便签提醒 + Server酱³ 推送
│   │   │   ├── SystemTrayManager.cs  # 系统托盘图标
│   │   │   ├── DebugWindow.cs        # 调试调参面板
│   │   │   ├── WindowOverlay.cs      # 透明窗口（DWM + WS_EX_LAYERED）
│   │   │   ├── IPetRenderer.cs       # 渲染接口
│   │   │   ├── HybridRenderer.cs     # 混合渲染器
│   │   │   ├── Model3DRenderer.cs    # 3D 渲染器
│   │   │   └── ToolCallInvoker.cs    # AI 工具调用分发
│   │   ├── Live2D/Models/Fuxuan/     ← 符玄 Live2D 模型
│   │   ├── Scenes/scene.scene        # 主场景
│   │   └── Resources/                # 资源文件夹
│   ├── Packages/
│   │   └── manifest.json             # 依赖管理
│   └── ProjectSettings/              # Unity 项目设置
├── file/                             # 原始模型文件（gitignored）
├── project_brief/                    # 设计文档 (LaTeX + PDF)
└── README.md
```

## 📜 脚本架构

```
DesktopPet (主控制器, Update order=0)
├── DragHandler          ← 鼠标交互
├── ChatBubble           ← 头顶气泡
├── ChatManager          ← AI 对话
├── AutoChat             ← 自动闲聊
├── BottomInputBar       ← 底部输入栏
├── ContextMenu          ← 右键菜单
├── TimeWeatherController ← 时间/天气
├── DebugWindow          ← 调试面板
├── WindowOverlay        ← 透明窗口
└── Live2DRenderer (IPetRenderer, Update order=801)
    └── CubismPhysicsController (order=800) ← 物理
```

**执行顺序：**
1. `DesktopPet.Update(0)` → 状态更新（位置、速度、行走相位）
2. `CubismPhysicsController.Update(800)` → 衣服/头发物理模拟
3. `Live2DRenderer.LateUpdate(801)` → 覆盖被物理重置的参数 + 空闲动画 + 交互反馈

## 🔧 开发环境

| 工具 | 版本 |
|---|---|
| Unity | 2022.3.62 LTS |
| Live2D Cubism SDK | 5-r.4 |
| .NET / C# | .NET Framework 4.x / C# 9.0 |
| Windows | 10/11 |
| IDE | Visual Studio 2022 / VS Code |

## 🚀 快速开始

1. **克隆仓库**
   ```bash
   git clone https://github.com/xuan-852/Desktop_per_pro.git
   ```

2. **导入 Live2D Cubism SDK**
   - 从 [Live2D 官网](https://www.live2d.com/sdk/about/) 下载 Cubism SDK 5-r.4
   - 导入到 Unity 项目中

3. **放置模型**
   - 将 符玄 Live2D 模型文件放到 `Assets/Live2D/Models/Fuxuan/` 目录下
   - Cubism SDK 会自动生成 Prefab

4. **在 Unity 中打开场景** `Assets/Scenes/scene.scene`
   - 检查 `DesktopPet` 对象的 Inspector 中 `Live2DRenderer.modelPrefab` 是否已引用

5. **运行** → 点击 Play

## 📦 依赖

- [Live2D Cubism SDK 5-r.4](https://www.live2d.com/sdk/about/)
- Newtonsoft.Json（Unity 包管理器安装）
- Unity UI (UGUI)

## 📄 许可证

本项目仅用于个人学习和技术研究，严禁商业用途。

- Live2D 模型版权归 © 米哈游（崩坏：星穹铁道）所有
- Cubism SDK 版权归 © Live2D Inc. 所有

## 📚 参考

- [Live2D Cubism SDK 文档](https://docs.live2d.com/)
- [Unity 透明窗口实现](https://github.com/XJINE/Unity_TransparentWindowManager)
- 原 GDI+ 桌宠项目：`D:\C\Desktop pet\`
- 流萤 Live2D 模型来源：[B站@是依七哒](https://space.bilibili.com/457683484) / [Scighost/Firefly](https://github.com/Scighost/Firefly)
