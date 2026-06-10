# Desktop Pet — Unity 迁移项目

## 项目说明

本项目是将原有 GDI+ 桌面宠物（`D:\C\Desktop pet\text1.c`）迁移至 Unity 引擎的尝试。
利用 Unity 的 3D 渲染能力加载米哈游（崩坏：星穹铁道）角色模型，结合透明窗口叠加技术实现桌面宠物效果。

详细设计文档见 [`project_brief/report.pdf`](./project_brief/report.pdf)。

## 项目状态

- [ ] 第一阶段：Unity 透明窗口环境搭建
- [ ] 第二阶段：符玄 3D 模型导入与渲染
- [ ] 第三阶段：拖拽/点击/物理交互实现
- [ ] 第四阶段：与旧程序聊天功能集成

## 目录结构

```
Desktop_per_pro/
  ├── project_brief/          # 项目设计文档 (LaTeX + PDF)
  │     ├── report.pdf        #   完整方案设计
  │     └── report.tex        #   LaTeX 源码
  ├── Assets/
  │     ├── Scenes/
  │     │     └── Main.unity  #   主场景
  │     ├── Scripts/          #   C# 脚本
  │     ├── Models/FuXuan/    #   符玄 3D 模型（待导入）
  │     └── Plugins/          #   第三方插件
  ├── README.md
  └── .gitignore
```

## 开发环境

- Unity 2022.3.62t7
- C# / HLSL
- Windows 10/11

## 参考

- [Unity_TransparentWindowManager](https://github.com/XJINE/Unity_TransparentWindowManager)
- [SR-Model-Importer](https://github.com/lethern/SR-Model-Importer)
- 原 GDI+ 桌宠项目：`D:\C\Desktop pet\`
