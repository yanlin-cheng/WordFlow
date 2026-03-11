# Git 多仓库配置指南

本文档介绍如何配置 Git 多仓库工作流，实现测试仓库与发布仓库分离。

---

## 仓库规划

| 平台 | 测试仓库（私有） | 发布仓库（公开） |
|------|-----------------|-----------------|
| **GitHub** | `yanlin-cheng/WordFlow-dev` | `yanlin-cheng/WordFlow` |
| **Gitee** | `yanlin-cheng/WordFlow-dev` | `yanlin-cheng/WordFlow` |

---

## 第一步：创建远程仓库

### GitHub

1. 访问 https://github.com/new
2. 创建 `WordFlow-dev`（设置为 Private 私有）
3. 创建 `WordFlow`（保持 Public 公开）

### Gitee

1. 访问 https://gitee.com/new
2. 创建 `WordFlow-dev`（设置为私有）
3. 创建 `WordFlow`（保持公开）

---

## 第二步：配置 Git 远程仓库

在本地项目根目录执行以下命令：

```bash
# 查看当前远程仓库
git remote -v

# 移除旧的远程仓库（如果需要）
git remote remove origin
git remote remove old

# 添加测试仓库远程（私有）
git remote add dev-github git@github.com:yanlin-cheng/WordFlow-dev.git
git remote add dev-gitee git@gitee.com:yanlin-cheng/WordFlow-dev.git

# 添加发布仓库远程（公开）
git remote add release-github git@github.com:yanlin-cheng/WordFlow.git
git remote add release-gitee git@gitee.com:yanlin-cheng/WordFlow.git

# 验证配置
git remote -v
```

### 预期输出

```
dev-gitee       git@gitee.com:yanlin-cheng/WordFlow-dev.git (fetch)
dev-gitee       git@gitee.com:yanlin-cheng/WordFlow-dev.git (push)
dev-github      git@github.com:yanlin-cheng/WordFlow-dev.git (fetch)
dev-github      git@github.com:yanlin-cheng/WordFlow-dev.git (push)
release-gitee   git@gitee.com:yanlin-cheng/WordFlow.git (fetch)
release-gitee   git@gitee.com:yanlin-cheng/WordFlow.git (push)
release-github  git@github.com:yanlin-cheng/WordFlow.git (fetch)
release-github  git@github.com:yanlin-cheng/WordFlow.git (push)
```

---

## 第三步：配置 Git 别名（可选）

为了方便推送，可以配置 Git 别名：

```bash
# 推送到测试仓库
git config --global alias.push-dev "push dev-github main"

# 推送到发布仓库
git config --global alias.push-release "push release-github main"
```

**注意**：Git 别名只能配置一个命令，如果需要推送到多个仓库，可以使用以下脚本方式。

---

## 第四步：日常开发工作流

### 开发流程

```
┌─────────────────────────────────────────────────────────┐
│                    日常开发流程                          │
├─────────────────────────────────────────────────────────┤
│                                                         │
│  第 1 步：本地开发                                        │
│  git add .                                              │
│  git commit -m "功能描述"                                │
│                                                         │
│  ↓                                                      │
│                                                         │
│  第 2 步：推送到测试仓库（私有）                            │
│  git push dev-github main                               │
│  git push dev-gitee main                                │
│                                                         │
│  ↓                                                      │
│  在虚拟机或另一台电脑拉取测试                             │
│                                                         │
│  ↓                                                      │
│  第 3 步：测试通过，推送到发布仓库（公开）                  │
│  git push release-github main                           │
│  git push release-gitee main                            │
│                                                         │
└─────────────────────────────────────────────────────────┘
```

### 常用命令

```bash
# 1. 本地提交
git add .
git commit -m "修改内容"

# 2. 推送到测试仓库（GitHub + Gitee）
git push dev-github main
git push dev-gitee main

# 3. 测试通过后，推送到发布仓库
git push release-github main
git push release-gitee main

# 4. 创建版本标签
git tag v1.0.0
git push release-github v1.0.0
git push release-gitee v1.0.0
```

---

## 第五步：创建发布版本

### 1. 打标签

```bash
# 创建版本标签
git tag v1.0.0

# 推送标签到发布仓库
git push release-github v1.0.0
git push release-gitee v1.0.0
```

### 2. 在 GitHub/Gitee 创建 Release

1. 访问 GitHub/Gitee 仓库页面
2. 进入 Releases 页面
3. 点击 "Create a new release" 或 "发布版本"
4. 选择刚才创建的标签
5. 填写版本说明
6. 上传安装包和模型文件
7. 发布

---

## 第六步：模型文件上传

### 上传到 Releases

将模型文件上传到 GitHub/Gitee Releases，供用户下载：

```
模型文件命名规范：
- sensevoice-small-onnx.tar.bz2
- paraformer-zh.tar.bz2
- WordFlow_Setup.exe
```

### 更新 models.json

确保 `Data/models.json` 中的下载链接正确：

```json
{
  "downloadSources": [
    {
      "name": "Gitee",
      "url": "https://gitee.com/yanlin-cheng/WordFlow/releases/download/v1.0.0/sensevoice-small-onnx.tar.bz2",
      "region": "cn",
      "priority": 1
    },
    {
      "name": "GitHub",
      "url": "https://github.com/yanlin-cheng/WordFlow/releases/download/v1.0.0/sensevoice-small-onnx.tar.bz2",
      "region": "global",
      "priority": 2
    }
  ]
}
```

---

## 常见问题

### Q1: 如何查看当前远程仓库配置？

```bash
git remote -v
```

### Q2: 如何修改远程仓库 URL？

```bash
git remote set-url dev-github 新的 URL
```

### Q3: 如何删除远程仓库？

```bash
git remote remove 仓库名称
```

### Q4: 推送失败怎么办？

1. 检查网络连接
2. 检查 SSH 密钥配置
3. 确认仓库权限

### Q5: 如何配置 SSH 密钥？

```bash
# 生成 SSH 密钥
ssh-keygen -t ed25519 -C "your_email@example.com"

# 查看公钥
cat ~/.ssh/id_ed25519.pub

# 将公钥添加到 GitHub/Gitee
```

---

## 总结

| 操作 | 命令 | 说明 |
|------|------|------|
| 本地提交 | `git add . && git commit -m "描述"` | 本地提交 |
| 推送测试 | `git push dev-github main && git push dev-gitee main` | 推送到私有测试库 |
| 推送发布 | `git push release-github main && git push release-gitee main` | 推送到公开库 |
| 创建版本 | `git tag v1.0.0 && git push release-github v1.0.0` | 打标签 |

---

**提示**：推送前请确保已在 GitHub 和 Gitee 创建对应的仓库！
