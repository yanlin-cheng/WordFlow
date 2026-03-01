# Docker 基础与 NanoBot 部署指南

**版本：** v1.0  
**日期：** 2026 年 2 月 28 日  
**目标读者：** Docker 初学者

---

## 第一部分：Docker 常见问题解答

### 问题 1：Docker 相当于一个简单的类似虚拟机的东西吗？

**回答：既是也不是**。Docker 和虚拟机有相似之处，但本质不同。

#### 类比理解

| 类比 | 虚拟机 (VMware/VirtualBox) | Docker |
|------|---------------------------|--------|
| **房子比喻** | 独栋别墅（完整独立系统） | 公寓房间（共享基础设施） |
| **启动速度** | 开机（1-3 分钟） | 打开 app（几秒） |
| **空间占用** | 一整个房子（几十 GB） | 一个房间（几 MB 到几 GB） |
| **系统完整性** | 完整的操作系统 | 只包含必要的组件 |

#### 核心区别

```
┌─────────────────────────────────────────────────────────┐
│                    虚拟机 (VM)                           │
│  ┌─────────────────────────────────────────────────┐   │
│  │  Guest OS (完整的操作系统)                       │   │
│  │  ┌─────────────────────────────────────────┐   │   │
│  │  │  App + Dependencies                     │   │   │
│  │  └─────────────────────────────────────────┘   │   │
│  └─────────────────────────────────────────────────┘   │
│  ↑ 需要模拟完整的硬件环境                                │
└─────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────┐
│                      Docker                              │
│  ┌─────────────────┐  ┌─────────────────┐              │
│  │  Container 1    │  │  Container 2    │              │
│  │  App + Libs     │  │  App + Libs     │              │
│  └─────────────────┘  └─────────────────┘              │
│  ↑ 直接共享宿主机的内核                                   │
│              宿主机 OS (Windows/Linux)                  │
└─────────────────────────────────────────────────────────┘
```

**简单说：**
- **虚拟机** = 完整的另一台电脑（包括操作系统）
- **Docker** = 隔离的应用运行环境（共享电脑的核心）

---

### 问题 2：Docker 把环境装好了就能直接用？

**回答：是的，这正是 Docker 的核心优势！**

#### Docker 的工作方式

```
1. 下载镜像 (Image) → 预配置好的环境模板
2. 运行容器 (Container) → 从镜像启动一个实例
3. 直接使用 → 环境已经配置好
```

#### 实际例子：部署 NanoBot

**传统方式（不用 Docker）：**
```bash
# 1. 安装 Python
# 2. 安装各种依赖
# 3. 配置环境变量
# 4. 处理各种兼容性问题
# ... 可能花几个小时
```

**使用 Docker：**
```bash
# 一行命令搞定
docker run nanobot
# 环境已经配置好，直接能用
```

#### 镜像 (Image) vs 容器 (Container)

| 概念 | 类比 | 说明 |
|------|------|------|
| **镜像** | 光盘/安装包 | 静态的环境模板 |
| **容器** | 运行的程序 | 镜像启动后的实例 |

```
镜像 (Image)  →  docker run  →  容器 (Container)
   (模板)                          (运行中)
```

---

### 问题 3：用 Docker 的话，Visual Studio 是不是就用不着了？

**回答：不是的！Visual Studio 和 Docker 用途不同，互不替代。**

#### 各自用途

| 工具 | 用途 | 你需要的场景 |
|------|------|-------------|
| **Visual Studio** | 代码编辑、调试、编译 | 编写 WordFlow 的 .NET WPF 代码 |
| **Docker** | 运行特定环境的应用 | 运行 NanoBot、Python ASR 服务 |

#### 你的工作流

```
┌─────────────────────────────────────────────────────┐
│                  你的开发环境                        │
│                                                     │
│  ┌─────────────────────────────────────────────┐   │
│  │  Visual Studio / VS Code                    │   │
│  │  - 编写 C# 代码 (WordFlow WPF)               │   │
│  │  - 调试 .NET 应用                            │   │
│  │  - 编译发布                                 │   │
│  └─────────────────────────────────────────────┘   │
│                       ↓                             │
│  ┌─────────────────────────────────────────────┐   │
│  │  Docker Desktop                             │   │
│  │  - 运行 NanoBot (AI 编程助手)                │   │
│  │  - 运行 Python ASR 服务                      │   │
│  │  - 运行数据库/其他依赖                       │   │
│  └─────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────┘
```

**结论：**
- ✅ **Visual Studio 继续用** - 开发 WordFlow 的 .NET 代码
- ✅ **Docker 也要用** - 运行 NanoBot 和其他服务
- ✅ **两者配合** - 不是替代关系

---

## 第二部分：Docker Desktop 安装与配置

### 2.1 Windows 11 安装 Docker Desktop

#### 步骤 1：检查系统要求

```
✅ Windows 11 (家庭版或专业版)
✅ 至少 4GB RAM (推荐 8GB+)
✅ 启用 WSL 2
```

#### 步骤 2：启用 WSL 2

以**管理员身份**打开 PowerShell，执行：

```powershell
# 启用 WSL 功能
wsl --install

# 设置为 WSL 2
wsl --set-default-version 2

# 查看状态
wsl --list --verbose
```

重启电脑后继续下一步。

#### 步骤 3：安装 Docker Desktop

1. 访问：https://www.docker.com/products/docker-desktop/
2. 点击 "Download for Windows"
3. 运行安装程序
4. 安装完成后重启

#### 步骤 4：验证安装

```bash
# 打开命令提示符或 PowerShell
docker --version
docker run hello-world
```

看到欢迎信息说明安装成功。

---

### 2.2 Docker 核心概念速查

| 术语 | 英文 | 说明 | 例子 |
|------|------|------|------|
| **镜像** | Image | 环境模板 | `ubuntu:22.04` |
| **容器** | Container | 运行中的镜像 | 从 ubuntu 镜像启动的容器 |
| **Dockerfile** | Dockerfile | 构建镜像的脚本 | 定义如何制作镜像 |
| **仓库** | Registry | 存放镜像的地方 | Docker Hub |
| **卷** | Volume | 数据持久化 | 把容器内文件映射到主机 |

---

### 2.3 常用 Docker 命令

```bash
# 查看下载的镜像
docker images

# 查看运行中的容器
docker ps

# 查看所有容器（包括停止的）
docker ps -a

# 停止容器
docker stop <容器 ID 或名称>

# 删除容器
docker rm <容器 ID 或名称>

# 删除镜像
docker rmi <镜像 ID 或名称>

# 进入容器内部
docker exec -it <容器 ID> bash

# 查看容器日志
docker logs <容器 ID>
```

---

## 第三部分：NanoBot 部署实战

### 3.1 方式一：使用 Docker Compose（推荐）

#### 步骤 1：克隆项目

```bash
git clone https://github.com/HKUDS/nanobot.git
cd nanobot
```

#### 步骤 2：配置文件

创建或编辑 `docker-compose.yml`：

```yaml
version: '3.8'

services:
  nanobot:
    image: nanobot-ai/nanobot:latest
    container_name: nanobot
    environment:
      - OPENROUTER_API_KEY=你的 API 密钥
    volumes:
      - ./config:/root/.nanobot
    restart: unless-stopped
```

#### 步骤 3：启动服务

```bash
docker-compose up -d
```

#### 步骤 4：查看日志

```bash
docker-compose logs -f
```

---

### 3.2 方式二：直接 Docker 运行

```bash
# 拉取镜像
docker pull nanobot-ai/nanobot:latest

# 运行容器
docker run -d \
  --name nanobot \
  -e OPENROUTER_API_KEY=你的 API 密钥 \
  -v ${PWD}/config:/root/.nanobot \
  nanobot-ai/nanobot:latest
```

---

### 3.3 配置阿里百炼 API

编辑配置文件（位置：`~/.nanobot/config.json` 或 Docker 卷映射的位置）：

```json
{
  "providers": {
    "dashscope": {
      "apiKey": "sk-xxx",
      "baseUrl": "https://dashscope.aliyuncs.com/api/v1"
    }
  },
  "default_provider": "dashscope"
}
```

---

## 第四部分：Docker vs 虚拟机 详细对比

### 4.1 使用场景对比

| 场景 | 推荐方案 | 理由 |
|------|----------|------|
| **运行 NanoBot** | Docker | 轻量、快速、简单 |
| **开发 .NET WPF** | 主机 (Windows) | 需要 Visual Studio |
| **测试 Linux 应用** | 虚拟机 | 需要完整 Linux 环境 |
| **运行数据库** | Docker | 一键启动，方便管理 |
| **隔离危险操作** | 虚拟机 | 完全隔离，更安全 |

### 4.2 资源占用对比

| 指标 | 虚拟机 (Ubuntu) | Docker (Ubuntu 镜像) |
|------|-----------------|---------------------|
| **磁盘占用** | 10-50 GB | 50-500 MB |
| **内存占用** | 2-8 GB (固定) | 按需使用 |
| **启动时间** | 1-3 分钟 | 5-30 秒 |
| **CPU 开销** | 较高 | 几乎无 |

### 4.3 为什么推荐 Docker 运行 NanoBot

1. **轻量** - 镜像只有几百 MB，虚拟机要几十 GB
2. **快速** - 秒级启动，虚拟机要等几分钟
3. **简单** - 一行命令启动，虚拟机要配置网络、共享等
4. **隔离** - 足够隔离 AI 工具的运行环境
5. **可移植** - 镜像可以在任何 Docker 环境运行

---

## 第五部分：你的问题总结

### 问题 1：Docker 相当于简单的虚拟机吗？

**回答：** 类似但不完全一样。
- Docker 更轻量，共享宿主机内核
- 虚拟机是完整的独立系统

### 问题 2：Docker 把环境装好了就能直接用？

**回答：** 是的！
- 镜像就是预配置的环境
- 一行命令启动，开箱即用

### 问题 3：用 Docker 就不需要 Visual Studio 了？

**回答：** 不是的！
- Visual Studio 用于编写代码
- Docker 用于运行服务
- 两者配合使用，互不替代

### 问题 4：还需要虚拟机吗？

**回答：** 对于你的需求（运行 NanoBot），**不需要虚拟机**。
- Docker Desktop 足够
- 更轻量、更简单、更快速

---

## 第六部分：下一步行动

### 立即行动
- [ ] 安装 Docker Desktop
- [ ] 验证安装成功

### 本周完成
- [ ] 部署 NanoBot
- [ ] 配置阿里百炼 API
- [ ] 测试 AI 编程功能

### 日常开发
- [ ] 继续使用 Visual Studio 开发 WordFlow
- [ ] 使用 Docker 运行 NanoBot 辅助开发
- [ ] 定期审查 AI 生成的代码

---

## 附录：常见问题

### Q: Docker 安全吗？
A: Docker 容器是隔离的，但不如虚拟机安全。对于运行 NanoBot 这种应用足够安全。

### Q: Docker 数据会丢失吗？
A: 使用卷 (Volume) 可以持久化数据。删除容器不会影响卷中的数据。

### Q: 可以同时运行多个容器吗？
A: 可以！Docker 的优势之一就是可以同时运行多个隔离的容器。

### Q: Docker 需要联网吗？
A: 下载镜像需要联网。运行容器可以离线（如果镜像已下载）。

---

*文档结束*
