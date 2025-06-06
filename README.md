# ArisenEngine

ArisenEngine 是一个开源的、面向学习与研究的自研渲染引擎，采用模块化架构，支持多平台构建与现代图形 API（如 Vulkan）。目标是打造一个兼具可扩展性与可读性的图形引擎，供开发者探索图形编程与引擎架构。

---

![License](https://img.shields.io/github/license/ArisenEngine/Engine)
![Contributors](https://img.shields.io/github/contributors/ArisenEngine/Engine)

---

---

## 🙌 贡献者（Contributors）

感谢所有贡献者，特别是以下几位：

<table>
  <tr>
    <td align="center">
      <a href="https://github.com/eatdreamcat">
        <img src="https://avatars.githubusercontent.com/u/35916798?v=4" width="80" alt="eatdreamcat"/>
        <br /><sub><b>eatdreamcat</b></sub>
      </a>
    </td>
    <td align="center">
      <a href="https://github.com/AWhipser">
        <img src="https://avatars.githubusercontent.com/u/15261257?v=4" width="80" alt="AWhipser"/>
        <br /><sub><b>AWhipser</b></sub>
      </a>
    </td>
    <td align="center">
      <a href="https://chat.openai.com/">
        <img src="https://upload.wikimedia.org/wikipedia/commons/e/ef/ChatGPT-Logo.svg" width="80" alt="ChatGPT" style="background:#fff; border-radius: 8px; padding: 8px;" />
        <br /><sub><b>ChatGPT</b></sub>
      </a>
    </td>
  </tr>
</table>

---

## 🧰 环境要求

| 组件          | 要求版本             |
| ------------- | -------------------- |
| CMake         | ≥ 3.29               |
| Vulkan SDK    | 已安装（建议最新版） |
| Visual Studio | 2022（支持 C++23）   |
| Windows SDK   | 10+                  |
| .NET SDK      | 9.0+                 |
| Python        | 3.0+                 |

---

## ⚙️ 编译方式（Windows 平台）

### ✅ 快速构建 Editor（推荐）

执行以下脚本生成编辑器解决方案：

```bash
Scripts/Windows/generate_editor_all.bat
