# Contributing to Distributed Audio System

感谢您考虑为 Distributed Audio System 做出贡献！

本文档将指导您如何参与项目开发。

## 行为准则

### 我们的承诺

为了营造开放和友好的环境，我们承诺让每个人都能参与项目，不受歧视。

### 我们的标准

积极的行为包括：
- 使用欢迎和包容的语言
- 尊重不同的观点和经验
- 优雅地接受建设性批评
- 关注对社区最有利的事情

不可接受的行为包括：
- 使用性化语言或图像
- 人身攻击或政治攻击
- 公开或私下骚扰
- 未经许可发布他人私人信息

## 如何贡献

### 报告 Bug

1. 在 [Issues](https://github.com/Kezry/distributedAudio/issues) 中搜索现有问题
2. 创建新 Issue，包含：
   - 清晰的标题
   - 详细的问题描述
   - 复现步骤
   - 预期行为 vs 实际行为
   - 系统环境信息
   - 截图或日志（如适用）

### 提交功能建议

1. 在 [Issues](https://github.com/Kezry/distributedAudio/issues) 中讨论您的想法
2. 说明功能的使用场景和好处
3. 考虑实现复杂度和对现有功能的影响

### 拉取请求

我们欢迎各种形式的贡献！

#### 准备工作

1. Fork 项目仓库
2. 从 `main` 分支创建您的功能分支
   ```bash
   git checkout -b feature/AmazingFeature
   ```

#### 代码规范

**C# 代码风格:**
```csharp
// 使用 PascalCase 命名类和方法
public class AudioPlayer
{
    public void PlayAudio() { }
}

// 使用 camelCase 命名变量和参数
private int sampleRate;

// 使用 _camelCase 命名私有字段
private readonly ILogger _logger;
```

**Java 代码风格:**
```java
// 使用 PascalCase 命名类
public class AudioPlayer {
    // 使用 camelCase 命名方法
    public void playAudio() { }
    
    // 使用 mCamelCase 命名成员变量
    private int mSampleRate;
}
```

**XML/XAML 风格:**
```xml
<!-- 使用 4 空格缩进 -->
<Grid>
    <Button Content="Click Me" />
</Grid>
```

#### 提交前检查

- [ ] 代码通过编译
- [ ] 遵循代码风格规范
- [ ] 添加必要的注释
- [ ] 更新相关文档
- [ ] 添加单元测试（如适用）
- [ ] 所有测试通过

#### 提交规范

提交消息格式：
```
<type>(<scope>): <subject>

<body>

<footer>
```

类型（type）：
- `feat`: 新功能
- `fix`: Bug 修复
- `docs`: 文档更新
- `style`: 代码格式（不影响功能）
- `refactor`: 重构
- `test`: 添加测试
- `chore`: 构建/工具变更

示例：
```
feat(player): add low-latency mode for Android 8+

Implement AAudio backend for devices running Android 8.0 or later,
reducing audio latency from 150ms to 45ms.

Closes #123
```

#### Pull Request 流程

1. 推送更改到您的 Fork
   ```bash
   git push origin feature/AmazingFeature
   ```

2. 创建 Pull Request
   - 提供清晰的描述
   - 引用相关的 Issue
   - 勾选适用的检查清单

3. 代码审查
   - 维护者会审查您的代码
   - 可能会请求修改
   - 请及时响应评论

4. 合并
   - 通过审查后，代码将被合并到 `main` 分支

## 开发环境

### Windows 端开发

**必需工具:**
- Visual Studio 2022
- .NET 8.0 SDK
- Git

**可选工具:**
- Windows Driver Kit (WDK) - 驱动开发
- ReSharper - 代码分析

### Android 端开发

**必需工具:**
- Android Studio Hedgehog | 2023.1.1+
- JDK 8+
- Android SDK 21+

**配置:**
```gradle
// 在 build.gradle 中配置
minSdkVersion 19
targetSdkVersion 34
compileSdkVersion 34
```

### 构建项目

```bash
# 克隆仓库
git clone https://github.com/Kezry/distributedAudio.git
cd distributedAudio

# Windows 端
cd WindowsSound
dotnet restore
dotnet build

# Android 端
cd AndroidSoundPlayer
gradlew assembleDebug

# 驱动（需要 WDK）
cd VirtualAudioDriver
msbuild DistributedAudioDriver.sln /p:Configuration=Release
```

## 测试指南

### 单元测试

```bash
# Windows 端
cd WindowsSound
dotnet test

# Android 端
cd AndroidSoundPlayer
gradlew test
```

### 集成测试

运行集成测试套件：
```bash
cd Tools/CompatibilityTest
dotnet run
```

### 性能测试

运行性能测试：
```bash
cd Tools/PerfTest
dotnet run
```

## 文档贡献

### 改进文档

文档与代码同样重要！您可以通过以下方式帮助：
- 修正拼写和语法错误
- 添加代码注释
- 改进现有文档的清晰度
- 添加使用示例

### 文档结构

```
docs/
├── API/           # API 文档
├── guides/        # 使用指南
├── architecture/  # 架构文档
└── troubleshooting/  # 故障排除
```

## 发布流程

### 版本号规范

我们遵循语义化版本 (Semantic Versioning)：

```
MAJOR.MINOR.PATCH

例: 1.2.3
  MAJOR: 不兼容的 API 变更
  MINOR: 向后兼容的功能新增
  PATCH: 向后兼容的 Bug 修复
```

### 发布检查清单

- [ ] 所有测试通过
- [ ] 文档已更新
- [ ] CHANGELOG 已更新
- [ ] 版本号已更新
- [ ] 构建成功
- [ ] 发布说明已准备

## 社区

### 讨论渠道

- [GitHub Discussions](https://github.com/Kezry/distributedAudio/discussions) - 一般讨论
- [GitHub Issues](https://github.com/Kezry/distributedAudio/issues) - Bug 报告和功能请求
- [Email](mailto:c.jac@foxmail.com) - 私人联系

### 认可贡献者

我们会定期在项目 README 中感谢活跃的贡献者。

## 许可证

通过贡献代码，您同意您的贡献将在与项目相同的 [CC BY-NC 4.0](LICENSE) 许可证下发布。

## 联系方式

有任何问题或建议，请通过以下方式联系：

- **Email:** c.jac@foxmail.com
- **GitHub:** https://github.com/Kezry

---

**再次感谢您的贡献！** 🎉
