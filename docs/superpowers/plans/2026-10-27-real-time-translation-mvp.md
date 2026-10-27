# Windows 实时翻译字幕软件 MVP Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 构建并发布 v0.1.0 Windows 实时翻译字幕软件，支持系统声音或麦克风单音源输入、中英俄自动识别、用户选择目标语言，以及置顶双语字幕条。

**Architecture:** 使用 .NET 10/WPF 构建 Windows 客户端，将 UI、核心状态机、Windows/Azure 基础设施分为三个生产项目。音频统一转换为 16 kHz、16-bit、mono PCM，通过流式会话接口写入 Azure Speech Translation；所有生命周期由单一 `TranslationCoordinator` 管理。

**Tech Stack:** C# 14、.NET SDK 10.0.301、WPF、Azure Speech SDK 1.53.0、NAudio 3.3.0、xUnit v3 4.0.2、GitHub Actions。

## Global Constraints

- 仅支持 Windows x64；目标框架统一为 `net10.0-windows`。
- 输入语言固定为 `zh-CN`、`en-US`、`ru-RU`；目标语言固定为 `zh-Hans`、`en`、`ru`。
- 系统声音和麦克风都属于 MVP，但一次会话只能选择一个。
- 用户点击“开始翻译”后才能采集和上传音频；暂停、返回设置或关闭后不得继续上传。
- Azure API Key 仅保存到 Windows Credential Manager；仓库、配置和日志不得出现真实密钥。
- 默认不保存音频、识别原文、译文或字幕历史。
- 正常网络下，多数稳定译文的目标体验为说话结束后 2～4 秒。
- 音频帧必须进入容量为 20 个 100 ms 帧的有界队列；单消费者顺序写入 Azure，队列满时丢弃最旧帧以限制内存和延迟。
- 每条字幕更新必须携带 Azure ResultId 对应的 `UtteranceId` 和本地单调递增 `Sequence`；最终原文与译文作为一个原子事件发布。
- WAV 测试文件必须通过 RIFF/WAV 解析器读取数据块，禁止假设音频数据固定从第 44 字节开始。
- 不实现服务器、账号、本地模型、第二云端引擎、混音、配音、历史、托盘、全局快捷键或其他平台。
- 每个任务执行 TDD：先写失败测试，再写最小实现，再运行相关测试和全量测试，最后单独提交。
- 遇到 Azure SDK、NAudio 或 Windows 能力与设计不一致时停止该任务，记录复现步骤和证据，不擅自更换技术方案。
- 不修改或提交当前工作区中与项目无关的 `微信开发者工具.lnk`。

---

## File Map

```text
global.json                                      # 固定 .NET SDK
Directory.Build.props                            # 全局编译规则
Directory.Packages.props                         # 集中依赖版本
RealTimeTranslation.slnx                         # 解决方案
src/RealTimeTranslation.Core/
  Languages.cs                                   # 语言、目标代码映射
  AudioContracts.cs                              # 音频帧和音源接口
  TranslationContracts.cs                        # 流式翻译接口与事件
  SubtitleState.cs                               # 当前字幕状态和规约
  BoundedAudioFrameQueue.cs                      # 20 帧有界音频队列
  TranslationCoordinator.cs                      # 会话状态机与生命周期
  RetryPolicy.cs                                 # 有限退避策略
src/RealTimeTranslation.Infrastructure/
  Audio/Pcm16MonoConverter.cs                    # 统一音频格式
  Audio/SystemLoopbackAudioSource.cs             # 系统回环捕获
  Audio/MicrophoneAudioSource.cs                 # 麦克风捕获
  Azure/AzureTranslationProvider.cs              # Azure 工厂
  Azure/AzureTranslationSession.cs               # Azure 连续会话
  Credentials/WindowsCredentialStore.cs          # Credential Manager
  Settings/JsonAppSettingsStore.cs                # 非敏感设置
  Logging/SafeRollingFileLogger.cs                # 脱敏、限量日志
src/RealTimeTranslation.Desktop/
  App.xaml / App.xaml.cs                          # 启动、单实例、依赖组装
  SettingsWindow.xaml / .xaml.cs                 # 设置界面
  SubtitleWindow.xaml / .xaml.cs                 # 置顶字幕条
  ViewModels/SettingsViewModel.cs                 # 设置状态与命令
  ViewModels/SubtitleViewModel.cs                 # 字幕绑定
  Services/WindowOrchestrator.cs                  # 两个窗口切换
tools/RealTimeTranslation.Feasibility/
  Program.cs                                      # 技术验证工具
tests/RealTimeTranslation.Core.Tests/             # 无网络单元测试
tests/RealTimeTranslation.Infrastructure.Tests/   # 本机基础设施测试
tests/RealTimeTranslation.Desktop.Tests/          # ViewModel 与窗口编排测试
tests/RealTimeTranslation.IntegrationTests/       # 真实 Azure 手动测试
docs/feasibility/azure-audio-report.md             # 技术验证记录
.github/workflows/ci.yml                           # 无密钥 CI
.github/workflows/release.yml                      # win-x64 发布物
```

---

### Task 1: 建立可编译、可测试的解决方案基线

**Files:**
- Create: `global.json`
- Create: `Directory.Build.props`
- Create: `Directory.Packages.props`
- Create: `RealTimeTranslation.slnx`
- Create: `src/RealTimeTranslation.Core/RealTimeTranslation.Core.csproj`
- Create: `src/RealTimeTranslation.Infrastructure/RealTimeTranslation.Infrastructure.csproj`
- Create: `src/RealTimeTranslation.Desktop/RealTimeTranslation.Desktop.csproj`
- Create: `tools/RealTimeTranslation.Feasibility/RealTimeTranslation.Feasibility.csproj`
- Create: `tests/RealTimeTranslation.Core.Tests/RealTimeTranslation.Core.Tests.csproj`
- Create: `tests/RealTimeTranslation.Infrastructure.Tests/RealTimeTranslation.Infrastructure.Tests.csproj`
- Create: `tests/RealTimeTranslation.Desktop.Tests/RealTimeTranslation.Desktop.Tests.csproj`
- Create: `tests/RealTimeTranslation.IntegrationTests/RealTimeTranslation.IntegrationTests.csproj`
- Create: `.gitignore`
- Create: `.github/workflows/ci.yml`

**Interfaces:**
- Consumes: approved design document only.
- Produces: a solution where `dotnet build` and `dotnet test` succeed before product code exists.

- [ ] **Step 1: Create the solution and projects**

Run from repository root:

```powershell
dotnet new sln --format slnx --name RealTimeTranslation
dotnet new classlib --framework net10.0 --name RealTimeTranslation.Core --output src/RealTimeTranslation.Core
dotnet new classlib --framework net10.0 --name RealTimeTranslation.Infrastructure --output src/RealTimeTranslation.Infrastructure
dotnet new wpf --framework net10.0 --name RealTimeTranslation.Desktop --output src/RealTimeTranslation.Desktop
dotnet new console --framework net10.0 --name RealTimeTranslation.Feasibility --output tools/RealTimeTranslation.Feasibility
dotnet new xunit --framework net10.0 --name RealTimeTranslation.Core.Tests --output tests/RealTimeTranslation.Core.Tests
dotnet new xunit --framework net10.0 --name RealTimeTranslation.Infrastructure.Tests --output tests/RealTimeTranslation.Infrastructure.Tests
dotnet new xunit --framework net10.0 --name RealTimeTranslation.Desktop.Tests --output tests/RealTimeTranslation.Desktop.Tests
dotnet new xunit --framework net10.0 --name RealTimeTranslation.IntegrationTests --output tests/RealTimeTranslation.IntegrationTests
dotnet sln RealTimeTranslation.slnx add src/RealTimeTranslation.Core/RealTimeTranslation.Core.csproj src/RealTimeTranslation.Infrastructure/RealTimeTranslation.Infrastructure.csproj src/RealTimeTranslation.Desktop/RealTimeTranslation.Desktop.csproj tools/RealTimeTranslation.Feasibility/RealTimeTranslation.Feasibility.csproj tests/RealTimeTranslation.Core.Tests/RealTimeTranslation.Core.Tests.csproj tests/RealTimeTranslation.Infrastructure.Tests/RealTimeTranslation.Infrastructure.Tests.csproj tests/RealTimeTranslation.Desktop.Tests/RealTimeTranslation.Desktop.Tests.csproj tests/RealTimeTranslation.IntegrationTests/RealTimeTranslation.IntegrationTests.csproj
dotnet add src/RealTimeTranslation.Infrastructure/RealTimeTranslation.Infrastructure.csproj reference src/RealTimeTranslation.Core/RealTimeTranslation.Core.csproj
dotnet add src/RealTimeTranslation.Desktop/RealTimeTranslation.Desktop.csproj reference src/RealTimeTranslation.Core/RealTimeTranslation.Core.csproj src/RealTimeTranslation.Infrastructure/RealTimeTranslation.Infrastructure.csproj
dotnet add tools/RealTimeTranslation.Feasibility/RealTimeTranslation.Feasibility.csproj reference src/RealTimeTranslation.Core/RealTimeTranslation.Core.csproj src/RealTimeTranslation.Infrastructure/RealTimeTranslation.Infrastructure.csproj
dotnet add tests/RealTimeTranslation.Core.Tests/RealTimeTranslation.Core.Tests.csproj reference src/RealTimeTranslation.Core/RealTimeTranslation.Core.csproj
dotnet add tests/RealTimeTranslation.Infrastructure.Tests/RealTimeTranslation.Infrastructure.Tests.csproj reference src/RealTimeTranslation.Core/RealTimeTranslation.Core.csproj src/RealTimeTranslation.Infrastructure/RealTimeTranslation.Infrastructure.csproj
dotnet add tests/RealTimeTranslation.Desktop.Tests/RealTimeTranslation.Desktop.Tests.csproj reference src/RealTimeTranslation.Desktop/RealTimeTranslation.Desktop.csproj
dotnet add tests/RealTimeTranslation.IntegrationTests/RealTimeTranslation.IntegrationTests.csproj reference src/RealTimeTranslation.Core/RealTimeTranslation.Core.csproj src/RealTimeTranslation.Infrastructure/RealTimeTranslation.Infrastructure.csproj
```

Expected: eight projects are listed by `dotnet sln RealTimeTranslation.slnx list`.

- [ ] **Step 2: Pin SDK, packages, and compiler rules**

Create `global.json`:

```json
{
  "sdk": {
    "version": "10.0.301",
    "rollForward": "latestPatch",
    "allowPrerelease": false
  },
  "test": {
    "runner": "Microsoft.Testing.Platform"
  }
}
```

Create `Directory.Build.props`:

```xml
<Project>
  <PropertyGroup>
    <TargetFramework>net10.0-windows</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <AnalysisLevel>latest-recommended</AnalysisLevel>
    <LangVersion>14.0</LangVersion>
  </PropertyGroup>
</Project>
```

Create `Directory.Packages.props`:

```xml
<Project>
  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
  </PropertyGroup>
  <ItemGroup>
    <PackageVersion Include="Microsoft.CognitiveServices.Speech" Version="1.53.0" />
    <PackageVersion Include="NAudio" Version="3.3.0" />
    <PackageVersion Include="xunit.v3" Version="4.0.2" />
  </ItemGroup>
</Project>
```

Replace each test project package list with `<PackageReference Include="xunit.v3" />` and set `<OutputType>Exe</OutputType>` because xUnit v3 test projects are standalone executables. Add `Microsoft.CognitiveServices.Speech` and `NAudio` only to Infrastructure.

Remove every generated per-project `<TargetFramework>` element so `Directory.Build.props` is the single source of truth. Keep `<UseWPF>true</UseWPF>` and `<OutputType>WinExe</OutputType>` in the Desktop project.

- [ ] **Step 3: Add the first failing smoke test**

Replace the generated core test with:

```csharp
namespace RealTimeTranslation.Core.Tests;

public sealed class SolutionSmokeTests
{
    [Fact]
    public void CoreAssemblyIsLoadable()
    {
        Assert.NotNull(typeof(SolutionSmokeTests).Assembly);
    }
}
```

Run: `dotnet test tests/RealTimeTranslation.Core.Tests/RealTimeTranslation.Core.Tests.csproj`

Expected: FAIL until the project package references and target frameworks are consistent; after corrections, PASS with 1 test.

- [ ] **Step 4: Add deterministic CI**

Create `.github/workflows/ci.yml`:

```yaml
name: ci
on:
  push:
    branches: [master, main]
  pull_request:
jobs:
  build-test:
    runs-on: windows-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: 10.0.301
      - run: dotnet restore RealTimeTranslation.slnx --locked-mode
      - run: dotnet build RealTimeTranslation.slnx --no-restore --configuration Release
      - run: dotnet test RealTimeTranslation.slnx --no-build --configuration Release
```

Run `dotnet restore RealTimeTranslation.slnx --use-lock-file` once and commit all generated `packages.lock.json` files.

- [ ] **Step 5: Verify and commit**

Run:

```powershell
dotnet format RealTimeTranslation.slnx --verify-no-changes
dotnet build RealTimeTranslation.slnx --configuration Release
dotnet test RealTimeTranslation.slnx --configuration Release
```

Expected: build succeeds with zero warnings; smoke test passes.

```powershell
git add global.json Directory.Build.props Directory.Packages.props RealTimeTranslation.slnx src tests tools .github .gitignore
git commit -m "build: establish .NET solution baseline"
```

---

### Task 2: 定义语言、音频和流式翻译核心契约

**Files:**
- Create: `src/RealTimeTranslation.Core/Languages.cs`
- Create: `src/RealTimeTranslation.Core/AudioContracts.cs`
- Create: `src/RealTimeTranslation.Core/TranslationContracts.cs`
- Test: `tests/RealTimeTranslation.Core.Tests/LanguageMappingTests.cs`
- Test: `tests/RealTimeTranslation.Core.Tests/AudioFrameTests.cs`

**Interfaces:**
- Consumes: none beyond BCL.
- Produces: `TargetLanguage`, `LanguageCatalog`, `AudioFormat`, `AudioFrame`, `IAudioCaptureSource`, `ITranslationProvider`, `ITranslationSession`, `TranslationUpdateEventArgs`.

- [ ] **Step 1: Write failing mapping and validation tests**

```csharp
public sealed class LanguageMappingTests
{
    [Theory]
    [InlineData(TargetLanguage.SimplifiedChinese, "zh-Hans")]
    [InlineData(TargetLanguage.English, "en")]
    [InlineData(TargetLanguage.Russian, "ru")]
    public void Target_code_is_fixed(TargetLanguage language, string expected) =>
        Assert.Equal(expected, LanguageCatalog.GetTargetCode(language));

    [Fact]
    public void Candidate_locales_are_exactly_the_MVP_languages() =>
        Assert.Equal(["zh-CN", "en-US", "ru-RU"], LanguageCatalog.SourceLocales);
}

public sealed class AudioFrameTests
{
    [Fact]
    public void Empty_audio_frame_is_rejected() =>
        Assert.Throws<ArgumentException>(() => new AudioFrame([]));
}
```

Run: `dotnet test tests/RealTimeTranslation.Core.Tests --filter "LanguageMappingTests|AudioFrameTests"`

Expected: FAIL because the types do not exist.

- [ ] **Step 2: Implement the minimal contracts**

```csharp
namespace RealTimeTranslation.Core;

public enum TargetLanguage { SimplifiedChinese, English, Russian }
public enum AudioSourceKind { SystemAudio, Microphone }

public static class LanguageCatalog
{
    public static IReadOnlyList<string> SourceLocales { get; } = ["zh-CN", "en-US", "ru-RU"];

    public static string GetTargetCode(TargetLanguage language) => language switch
    {
        TargetLanguage.SimplifiedChinese => "zh-Hans",
        TargetLanguage.English => "en",
        TargetLanguage.Russian => "ru",
        _ => throw new ArgumentOutOfRangeException(nameof(language))
    };
}

public sealed record AudioFormat(int SampleRate, short BitsPerSample, short Channels)
{
    public static AudioFormat AzurePcm { get; } = new(16_000, 16, 1);
}

public sealed record AudioFrame
{
    public AudioFrame(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (data.Length == 0) throw new ArgumentException("Audio frame cannot be empty.", nameof(data));
        Data = data;
    }

    public byte[] Data { get; }
}

public sealed class AudioFrameEventArgs(AudioFrame frame) : EventArgs
{
    public AudioFrame Frame { get; } = frame;
}

public sealed class AudioSourceFaultEventArgs(Exception exception) : EventArgs
{
    public Exception Exception { get; } = exception;
}

public interface IAudioCaptureSource : IAsyncDisposable
{
    AudioFormat OutputFormat { get; }
    event EventHandler<AudioFrameEventArgs>? FrameAvailable;
    event EventHandler<AudioSourceFaultEventArgs>? Faulted;
    ValueTask StartAsync(CancellationToken cancellationToken);
    ValueTask StopAsync(CancellationToken cancellationToken);
}

public abstract record TranslationUpdate(string UtteranceId, long Sequence)
{
    protected static string RequireId(string value) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("UtteranceId cannot be empty.", nameof(value))
            : value;
}

public sealed record PartialTranscriptUpdate : TranslationUpdate
{
    public PartialTranscriptUpdate(string utteranceId, long sequence, string text)
        : base(RequireId(utteranceId), sequence) => Text = text ?? string.Empty;

    public string Text { get; }
}

public sealed record FinalUtteranceUpdate : TranslationUpdate
{
    public FinalUtteranceUpdate(
        string utteranceId,
        long sequence,
        string originalText,
        string translatedText,
        string detectedLanguage)
        : base(RequireId(utteranceId), sequence)
    {
        OriginalText = originalText ?? string.Empty;
        TranslatedText = translatedText ?? string.Empty;
        DetectedLanguage = detectedLanguage ?? string.Empty;
    }

    public string OriginalText { get; }
    public string TranslatedText { get; }
    public string DetectedLanguage { get; }
}

public sealed record TranslationCanceledUpdate(
    string UtteranceId,
    long Sequence,
    string ErrorCode) : TranslationUpdate(UtteranceId, Sequence);

public sealed class TranslationUpdateEventArgs(TranslationUpdate update) : EventArgs
{
    public TranslationUpdate Update { get; } = update;
}

public sealed record TranslationSessionOptions(string Endpoint, string ApiKey, TargetLanguage TargetLanguage);

public interface ITranslationProvider
{
    ValueTask<ITranslationSession> CreateSessionAsync(
        TranslationSessionOptions options,
        CancellationToken cancellationToken);
}

public interface ITranslationSession : IAsyncDisposable
{
    event EventHandler<TranslationUpdateEventArgs>? UpdateReceived;
    ValueTask StartAsync(CancellationToken cancellationToken);
    ValueTask WriteAudioAsync(ReadOnlyMemory<byte> audio, CancellationToken cancellationToken);
    ValueTask StopAsync(CancellationToken cancellationToken);
}
```

Split the snippet into the three files named above without changing public names.

- [ ] **Step 3: Run targeted and full tests**

Run:

```powershell
dotnet test tests/RealTimeTranslation.Core.Tests --filter "LanguageMappingTests|AudioFrameTests"
dotnet test RealTimeTranslation.slnx
```

Expected: all tests pass.

- [ ] **Step 4: Commit**

```powershell
git add src/RealTimeTranslation.Core tests/RealTimeTranslation.Core.Tests
git commit -m "feat: define streaming translation contracts"
```

---

### Task 3: 实现按语句关联的字幕状态规约

**Files:**
- Create: `src/RealTimeTranslation.Core/SubtitleState.cs`
- Test: `tests/RealTimeTranslation.Core.Tests/SubtitleStateTests.cs`

**Interfaces:**
- Consumes: atomic `PartialTranscriptUpdate` and `FinalUtteranceUpdate` from Task 2.
- Produces: immutable `SubtitleState.Apply(TranslationUpdate)` used by coordinator and WPF view model.

- [ ] **Step 1: Write failing association and stale-event tests**

```csharp
public sealed class SubtitleStateTests
{
    [Fact]
    public void Final_original_and_translation_are_applied_atomically()
    {
        var state = SubtitleState.Empty.Apply(new FinalUtteranceUpdate(
            "result-1", 1, "Hello", "你好", "en-US"));

        Assert.Equal("result-1", state.UtteranceId);
        Assert.Equal("Hello", state.OriginalText);
        Assert.Equal("你好", state.TranslatedText);
        Assert.False(state.IsOriginalPartial);
    }

    [Fact]
    public void New_partial_clears_translation_instead_of_pairing_it_with_wrong_sentence()
    {
        var state = SubtitleState.Empty
            .Apply(new FinalUtteranceUpdate("result-1", 1, "Hello", "你好", "en-US"))
            .Apply(new PartialTranscriptUpdate("result-2", 2, "How are"));

        Assert.Equal("result-2", state.UtteranceId);
        Assert.Equal("How are", state.OriginalText);
        Assert.Equal(string.Empty, state.TranslatedText);
        Assert.True(state.IsOriginalPartial);
    }

    [Fact]
    public void Late_final_from_older_sequence_is_ignored()
    {
        var state = SubtitleState.Empty
            .Apply(new PartialTranscriptUpdate("result-2", 2, "How are"))
            .Apply(new FinalUtteranceUpdate("result-1", 1, "Hello", "你好", "en-US"));

        Assert.Equal("result-2", state.UtteranceId);
        Assert.Equal("How are", state.OriginalText);
        Assert.Equal(string.Empty, state.TranslatedText);
    }

    [Fact]
    public void Final_text_is_limited_to_two_display_lines()
    {
        var state = SubtitleState.Empty.Apply(new FinalUtteranceUpdate(
            "result-1", 1, "one\ntwo\nthree", "一\n二\n三", "en-US"));

        Assert.Equal("two\nthree", state.OriginalText.Replace("\r\n", "\n"));
        Assert.Equal("二\n三", state.TranslatedText.Replace("\r\n", "\n"));
    }
}
```

Run: `dotnet test tests/RealTimeTranslation.Core.Tests --filter SubtitleStateTests`

Expected: FAIL because `SubtitleState` does not exist.

- [ ] **Step 2: Implement sequence-aware atomic subtitle state**

```csharp
namespace RealTimeTranslation.Core;

public sealed record SubtitleState(
    string UtteranceId,
    long Sequence,
    string OriginalText,
    string TranslatedText,
    string DetectedLanguage,
    bool IsOriginalPartial)
{
    public static SubtitleState Empty { get; } = new(
        string.Empty, -1, string.Empty, string.Empty, string.Empty, false);

    public SubtitleState Apply(TranslationUpdate update)
    {
        if (update.Sequence < Sequence) return this;

        return update switch
        {
            PartialTranscriptUpdate partial => new(
                partial.UtteranceId,
                partial.Sequence,
                LastTwoLines(partial.Text),
                string.Empty,
                string.Empty,
                true),
            FinalUtteranceUpdate final => new(
                final.UtteranceId,
                final.Sequence,
                LastTwoLines(final.OriginalText),
                LastTwoLines(final.TranslatedText),
                final.DetectedLanguage,
                false),
            _ => this
        };
    }

    private static string LastTwoLines(string text)
    {
        var lines = text.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return string.Join(Environment.NewLine, lines.TakeLast(2));
    }
}
```

- [ ] **Step 3: Verify and commit**

Run: `dotnet test RealTimeTranslation.slnx`

Expected: all tests pass and no test can display a translation whose `UtteranceId` differs from the displayed original.

```powershell
git add src/RealTimeTranslation.Core/SubtitleState.cs tests/RealTimeTranslation.Core.Tests/SubtitleStateTests.cs
git commit -m "feat: associate subtitles by utterance"
```

---

### Task 4: 实现有限重试和单会话 Coordinator

**Files:**
- Create: `src/RealTimeTranslation.Core/RetryPolicy.cs`
- Create: `src/RealTimeTranslation.Core/BoundedAudioFrameQueue.cs`
- Create: `src/RealTimeTranslation.Core/TranslationCoordinator.cs`
- Test: `tests/RealTimeTranslation.Core.Tests/RetryPolicyTests.cs`
- Test: `tests/RealTimeTranslation.Core.Tests/BoundedAudioFrameQueueTests.cs`
- Test: `tests/RealTimeTranslation.Core.Tests/TranslationCoordinatorTests.cs`
- Test support: `tests/RealTimeTranslation.Core.Tests/Fakes/FakeAudioSource.cs`
- Test support: `tests/RealTimeTranslation.Core.Tests/Fakes/FakeTranslationProvider.cs`

**Interfaces:**
- Consumes: all Task 2 contracts and `SubtitleState`.
- Produces: a 20-frame bounded queue, one sequential Azure writer, and `TranslationCoordinator.StartAsync`, `PauseAsync`, `ResumeAsync`, `StopAsync`, `StateChanged`, and `SubtitleChanged`.

- [ ] **Step 1: Write retry policy tests**

```csharp
public sealed class RetryPolicyTests
{
    [Fact]
    public void Delays_are_one_two_four_seconds()
    {
        var policy = RetryPolicy.NetworkDefault;
        Assert.Equal([TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(4)], policy.Delays);
    }
}
```

Implement:

```csharp
public sealed record RetryPolicy(IReadOnlyList<TimeSpan> Delays)
{
    public static RetryPolicy NetworkDefault { get; } = new([
        TimeSpan.FromSeconds(1),
        TimeSpan.FromSeconds(2),
        TimeSpan.FromSeconds(4)]);
}
```

- [ ] **Step 2: Write the failing bounded-queue test, then implement it**

```csharp
public sealed class BoundedAudioFrameQueueTests
{
    [Fact]
    public async Task Capacity_twenty_drops_oldest_frames_and_preserves_order()
    {
        var queue = new BoundedAudioFrameQueue(20);
        for (byte value = 0; value < 25; value++)
            Assert.True(queue.TryWrite(new AudioFrame([value])));
        queue.Complete();

        var values = new List<byte>();
        await foreach (var frame in queue.ReadAllAsync(CancellationToken.None))
            values.Add(frame.Data[0]);

        Assert.Equal(20, values.Count);
        Assert.Equal(5, values[0]);
        Assert.Equal(24, values[^1]);
    }
}
```

Run: `dotnet test tests/RealTimeTranslation.Core.Tests --filter BoundedAudioFrameQueueTests`

Expected: FAIL because `BoundedAudioFrameQueue` does not exist.

Implement:

```csharp
using System.Threading.Channels;

namespace RealTimeTranslation.Core;

public sealed class BoundedAudioFrameQueue
{
    public const int DefaultCapacity = 20;
    private readonly Channel<AudioFrame> channel;

    public BoundedAudioFrameQueue(int capacity = DefaultCapacity)
    {
        if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
        channel = Channel.CreateBounded<AudioFrame>(new BoundedChannelOptions(capacity)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.DropOldest
        });
    }

    public bool TryWrite(AudioFrame frame) => channel.Writer.TryWrite(frame);
    public IAsyncEnumerable<AudioFrame> ReadAllAsync(CancellationToken cancellationToken) =>
        channel.Reader.ReadAllAsync(cancellationToken);
    public void Complete() => channel.Writer.TryComplete();
}
```

Run the test again. Expected: PASS; only values 5 through 24 are read.

- [ ] **Step 3: Write failing lifecycle and sequential-write tests**

```csharp
public sealed class TranslationCoordinatorTests
{
    [Fact]
    public async Task Start_creates_exactly_one_source_and_session()
    {
        var audio = new FakeAudioSource();
        var provider = new FakeTranslationProvider();
        await using var coordinator = new TranslationCoordinator(provider, _ => audio, _ => Task.CompletedTask);

        await coordinator.StartAsync(TestOptions.Value, CancellationToken.None);

        Assert.Equal(SessionState.Listening, coordinator.State);
        Assert.Equal(1, provider.CreateCount);
        Assert.Equal(1, audio.StartCount);
    }

    [Fact]
    public async Task Pause_stops_audio_and_session_and_cancels_retries()
    {
        var audio = new FakeAudioSource();
        var provider = new FakeTranslationProvider();
        await using var coordinator = new TranslationCoordinator(provider, _ => audio, _ => Task.CompletedTask);
        await coordinator.StartAsync(TestOptions.Value, CancellationToken.None);

        await coordinator.PauseAsync(CancellationToken.None);

        Assert.Equal(SessionState.Paused, coordinator.State);
        Assert.Equal(1, audio.StopCount);
        Assert.Equal(1, provider.LastSession.StopCount);
    }

    [Fact]
    public async Task Audio_frames_are_written_sequentially_through_one_consumer()
    {
        var audio = new FakeAudioSource();
        var provider = new FakeTranslationProvider(writeDelay: TimeSpan.FromMilliseconds(10));
        await using var coordinator = new TranslationCoordinator(provider, _ => audio, _ => Task.CompletedTask);
        await coordinator.StartAsync(TestOptions.Value, CancellationToken.None);

        for (var index = 0; index < 30; index++)
            audio.Emit(new AudioFrame([checked((byte)index)]));
        await provider.LastSession.WaitForWritesAsync(20, TimeSpan.FromSeconds(2));

        Assert.Equal(1, provider.LastSession.MaximumConcurrentWrites);
    }
}
```

`TestOptions.Value` must contain a fake endpoint, fake key, `AudioSourceKind.SystemAudio`, and `TargetLanguage.SimplifiedChinese`; no real secret is used.

Run: `dotnet test tests/RealTimeTranslation.Core.Tests --filter TranslationCoordinatorTests`

Expected: FAIL because coordinator types do not exist.

- [ ] **Step 4: Implement the explicit state machine and audio pump**

```csharp
public enum SessionState { Idle, Connecting, Listening, Paused, Reconnecting, Failed, Stopping }

public sealed record StartTranslationOptions(
    string Endpoint,
    string ApiKey,
    AudioSourceKind Source,
    TargetLanguage TargetLanguage);

public sealed class CoordinatorStateChangedEventArgs(SessionState state, string? message = null) : EventArgs
{
    public SessionState State { get; } = state;
    public string? Message { get; } = message;
}

public sealed class SubtitleChangedEventArgs(SubtitleState state) : EventArgs
{
    public SubtitleState State { get; } = state;
}
```

Implement `TranslationCoordinator` with these invariants:

```csharp
public sealed class TranslationCoordinator : IAsyncDisposable
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly ITranslationProvider provider;
    private readonly Func<AudioSourceKind, IAudioCaptureSource> audioFactory;
    private readonly Func<TimeSpan, Task> delay;
    private CancellationTokenSource? sessionCts;
    private IAudioCaptureSource? audio;
    private ITranslationSession? session;
    private SubtitleState subtitleState = SubtitleState.Empty;
    private BoundedAudioFrameQueue? audioQueue;
    private Task? audioPump;

    public TranslationCoordinator(
        ITranslationProvider provider,
        Func<AudioSourceKind, IAudioCaptureSource> audioFactory,
        Func<TimeSpan, Task> delay)
    {
        this.provider = provider;
        this.audioFactory = audioFactory;
        this.delay = delay;
    }

    public SessionState State { get; private set; } = SessionState.Idle;
    public event EventHandler<CoordinatorStateChangedEventArgs>? StateChanged;
    public event EventHandler<SubtitleChangedEventArgs>? SubtitleChanged;

    public async ValueTask StartAsync(StartTranslationOptions options, CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            if (State is not SessionState.Idle and not SessionState.Paused and not SessionState.Failed)
                throw new InvalidOperationException($"Cannot start from {State}.");
            await StartCoreAsync(options, cancellationToken);
        }
        finally { gate.Release(); }
    }

    public async ValueTask PauseAsync(CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            await StopResourcesAsync(cancellationToken);
            SetState(SessionState.Paused);
        }
        finally { gate.Release(); }
    }

    public ValueTask ResumeAsync(StartTranslationOptions options, CancellationToken cancellationToken) =>
        StartAsync(options, cancellationToken);

    public async ValueTask StopAsync(CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            SetState(SessionState.Stopping);
            await StopResourcesAsync(cancellationToken);
            SetState(SessionState.Idle);
        }
        finally { gate.Release(); }
    }

    private async Task StartCoreAsync(StartTranslationOptions options, CancellationToken externalToken)
    {
        SetState(SessionState.Connecting);
        sessionCts = CancellationTokenSource.CreateLinkedTokenSource(externalToken);
        session = await provider.CreateSessionAsync(
            new(options.Endpoint, options.ApiKey, options.TargetLanguage), sessionCts.Token);
        audio = audioFactory(options.Source);
        session.UpdateReceived += OnTranslationUpdate;
        audio.FrameAvailable += OnAudioFrame;
        audio.Faulted += OnAudioFault;
        await session.StartAsync(sessionCts.Token);
        audioQueue = new BoundedAudioFrameQueue();
        audioPump = PumpAudioAsync(audioQueue, session, sessionCts.Token);
        await audio.StartAsync(sessionCts.Token);
        SetState(SessionState.Listening);
    }

    private void OnAudioFrame(object? sender, AudioFrameEventArgs e) =>
        audioQueue?.TryWrite(e.Frame);

    private async Task PumpAudioAsync(
        BoundedAudioFrameQueue queue,
        ITranslationSession activeSession,
        CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var frame in queue.ReadAllAsync(cancellationToken))
                await activeSession.WriteAudioAsync(frame.Data, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private void OnTranslationUpdate(object? sender, TranslationUpdateEventArgs e)
    {
        subtitleState = subtitleState.Apply(e.Update);
        SubtitleChanged?.Invoke(this, new(subtitleState));
    }

    private void OnAudioFault(object? sender, AudioSourceFaultEventArgs e) =>
        SetState(SessionState.Failed, "音频设备不可用");

    private async Task StopResourcesAsync(CancellationToken cancellationToken)
    {
        sessionCts?.Cancel();
        if (audio is not null) await audio.StopAsync(cancellationToken);
        audioQueue?.Complete();
        if (audioPump is not null) await audioPump;
        if (session is not null) await session.StopAsync(cancellationToken);
        if (audio is not null) await audio.DisposeAsync();
        if (session is not null) await session.DisposeAsync();
        audio = null;
        session = null;
        audioQueue = null;
        audioPump = null;
        sessionCts?.Dispose();
        sessionCts = null;
    }

    private void SetState(SessionState state, string? message = null)
    {
        State = state;
        StateChanged?.Invoke(this, new(state, message));
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync(CancellationToken.None);
        gate.Dispose();
    }
}
```

After the basic tests pass, add a test that raises three transient cancellation updates and verifies delays 1/2/4, then a fourth failure enters `Failed`; implement retry in a private `ReconnectAsync` guarded by the current session ID. User pause must cancel that loop. The queue and pump belong to one session ID and are recreated on every reconnect.

- [ ] **Step 5: Verify queue, race, and lifecycle tests**

Run:

```powershell
dotnet test tests/RealTimeTranslation.Core.Tests --filter "RetryPolicyTests|BoundedAudioFrameQueueTests|TranslationCoordinatorTests"
dotnet test RealTimeTranslation.slnx
```

Expected: all tests pass; fake counts prove only one source and one session exist.

- [ ] **Step 6: Commit**

```powershell
git add src/RealTimeTranslation.Core tests/RealTimeTranslation.Core.Tests
git commit -m "feat: coordinate a single translation session"
```

---

### Task 5: 实现设置和 Windows Credential Manager

**Files:**
- Create: `src/RealTimeTranslation.Core/SettingsContracts.cs`
- Create: `src/RealTimeTranslation.Infrastructure/Settings/JsonAppSettingsStore.cs`
- Create: `src/RealTimeTranslation.Infrastructure/Credentials/WindowsCredentialStore.cs`
- Test: `tests/RealTimeTranslation.Infrastructure.Tests/JsonAppSettingsStoreTests.cs`
- Test: `tests/RealTimeTranslation.Infrastructure.Tests/WindowsCredentialStoreTests.cs`

**Interfaces:**
- Produces: `AppSettings`, `IAppSettingsStore`, `ICredentialStore`.
- Security invariant: JSON never contains the API Key.

- [ ] **Step 1: Write failing settings security tests**

```csharp
public sealed class JsonAppSettingsStoreTests
{
    [Fact]
    public async Task Saved_json_contains_only_non_sensitive_values()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        var store = new JsonAppSettingsStore(path);
        await store.SaveAsync(new("https://example.test", AudioSourceKind.SystemAudio,
            TargetLanguage.SimplifiedChinese, 100, 200), CancellationToken.None);

        var json = await File.ReadAllTextAsync(path);
        Assert.Contains("example.test", json);
        Assert.DoesNotContain("ApiKey", json, StringComparison.OrdinalIgnoreCase);
    }
}
```

Define the exact core contracts:

```csharp
public sealed record AppSettings(
    string Endpoint,
    AudioSourceKind AudioSource,
    TargetLanguage TargetLanguage,
    double SubtitleLeft,
    double SubtitleTop);

public interface IAppSettingsStore
{
    ValueTask<AppSettings?> LoadAsync(CancellationToken cancellationToken);
    ValueTask SaveAsync(AppSettings settings, CancellationToken cancellationToken);
}

public interface ICredentialStore
{
    ValueTask<string?> ReadApiKeyAsync(CancellationToken cancellationToken);
    ValueTask WriteApiKeyAsync(string apiKey, CancellationToken cancellationToken);
    ValueTask DeleteApiKeyAsync(CancellationToken cancellationToken);
}
```

- [ ] **Step 2: Implement JSON settings atomically**

Use `System.Text.Json` with `WriteIndented = true`. Write to `<path>.tmp`, then call `File.Move(temp, path, true)`. Default path:

```csharp
Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
    "RealTimeTranslation",
    "settings.json")
```

Run: `dotnet test tests/RealTimeTranslation.Infrastructure.Tests --filter JsonAppSettingsStoreTests`

Expected: PASS and no key-related property appears in JSON.

- [ ] **Step 3: Implement Credential Manager using Win32 APIs**

Use target name `RealTimeTranslation/AzureSpeech` and credential type `CRED_TYPE_GENERIC`. Define private P/Invoke bindings for `CredWriteW`, `CredReadW`, `CredDeleteW`, and `CredFree` from `advapi32.dll`. Store the UTF-16 API Key in `CredentialBlob`; zero the unmanaged buffer in `finally` before freeing it.

The public implementation must enforce:

```csharp
public async ValueTask WriteApiKeyAsync(string apiKey, CancellationToken cancellationToken)
{
    cancellationToken.ThrowIfCancellationRequested();
    if (string.IsNullOrWhiteSpace(apiKey))
        throw new ArgumentException("API Key cannot be empty.", nameof(apiKey));
    await Task.Run(() => WriteCore(apiKey), cancellationToken);
}

public ValueTask<string?> ReadApiKeyAsync(CancellationToken cancellationToken) =>
    new(Task.Run(ReadCore, cancellationToken));

public ValueTask DeleteApiKeyAsync(CancellationToken cancellationToken) =>
    new(Task.Run(DeleteCore, cancellationToken));
```

`ReadCore` returns `null` for Win32 error 1168 (`ERROR_NOT_FOUND`); every other failure throws `Win32Exception`.

- [ ] **Step 4: Add a local Windows integration test**

```csharp
[Trait("Category", "WindowsCredentialManager")]
public async Task Credential_round_trip_and_delete()
{
    var store = new WindowsCredentialStore("RealTimeTranslation.Tests/" + Guid.NewGuid());
    await store.WriteApiKeyAsync("not-a-real-secret", CancellationToken.None);
    Assert.Equal("not-a-real-secret", await store.ReadApiKeyAsync(CancellationToken.None));
    await store.DeleteApiKeyAsync(CancellationToken.None);
    Assert.Null(await store.ReadApiKeyAsync(CancellationToken.None));
}
```

Run locally: `dotnet test tests/RealTimeTranslation.Infrastructure.Tests --filter Category=WindowsCredentialManager`

Expected: PASS; the test deletes its temporary credential.

- [ ] **Step 5: Verify and commit**

Run: `dotnet test RealTimeTranslation.slnx`

```powershell
git add src/RealTimeTranslation.Core/SettingsContracts.cs src/RealTimeTranslation.Infrastructure tests/RealTimeTranslation.Infrastructure.Tests
git commit -m "feat: store settings and Azure credentials securely"
```

---

### Task 6: 实现并验证 Azure 连续翻译会话

**Files:**
- Create: `src/RealTimeTranslation.Infrastructure/Azure/AzureTranslationProvider.cs`
- Create: `src/RealTimeTranslation.Infrastructure/Azure/AzureTranslationSession.cs`
- Create: `tests/RealTimeTranslation.IntegrationTests/AzureTranslationSessionTests.cs`
- Modify: `tools/RealTimeTranslation.Feasibility/Program.cs`

**Interfaces:**
- Consumes: `ITranslationProvider`, `ITranslationSession`, `LanguageCatalog`.
- Produces: Azure-backed streaming session accepting canonical PCM.

- [ ] **Step 1: Write a credential-gated failing integration test**

```csharp
public sealed class AzureTranslationSessionTests
{
    [Fact]
    public async Task Can_start_and_stop_continuous_session()
    {
        var endpoint = Environment.GetEnvironmentVariable("AZURE_SPEECH_ENDPOINT");
        var key = Environment.GetEnvironmentVariable("AZURE_SPEECH_KEY");
        if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(key)) return;

        var provider = new AzureTranslationProvider();
        await using var session = await provider.CreateSessionAsync(
            new(endpoint, key, TargetLanguage.SimplifiedChinese), CancellationToken.None);
        await session.StartAsync(CancellationToken.None);
        await session.StopAsync(CancellationToken.None);
    }
}
```

Run with configured environment variables: `dotnet test tests/RealTimeTranslation.IntegrationTests --filter AzureTranslationSessionTests`

Expected before implementation: FAIL because `AzureTranslationProvider` does not exist.

- [ ] **Step 2: Implement Azure session construction**

The constructor path must use:

```csharp
var config = SpeechTranslationConfig.FromEndpoint(new Uri(options.Endpoint), options.ApiKey);
config.SetProperty(PropertyId.SpeechServiceConnection_LanguageIdMode, "Continuous");
config.AddTargetLanguage(LanguageCatalog.GetTargetCode(options.TargetLanguage));

var autoDetect = AutoDetectSourceLanguageConfig.FromLanguages(LanguageCatalog.SourceLocales);
var streamFormat = AudioStreamFormat.GetWaveFormatPCM(16_000, 16, 1);
var pushStream = AudioInputStream.CreatePushStream(streamFormat);
var audioConfig = AudioConfig.FromStreamInput(pushStream);
var recognizer = new TranslationRecognizer(config, autoDetect, audioConfig);
```

Map Azure callbacks so every final subtitle is one atomic, sentence-associated update:

```csharp
var sequences = new ConcurrentDictionary<string, long>(StringComparer.Ordinal);
long nextSequence = 0;
long SequenceFor(string resultId) =>
    sequences.GetOrAdd(resultId, _ => Interlocked.Increment(ref nextSequence));

recognizer.Recognizing += (_, e) =>
{
    var resultId = e.Result.ResultId;
    Raise(new PartialTranscriptUpdate(resultId, SequenceFor(resultId), e.Result.Text));
};
recognizer.Recognized += (_, e) =>
{
    var resultId = e.Result.ResultId;
    var sequence = SequenceFor(resultId);
    var detected = e.Result.Properties.GetProperty(
        PropertyId.SpeechServiceConnection_AutoDetectSourceLanguageResult);
    var target = LanguageCatalog.GetTargetCode(options.TargetLanguage);
    e.Result.Translations.TryGetValue(target, out var translated);
    Raise(new FinalUtteranceUpdate(
        resultId,
        sequence,
        e.Result.Text,
        translated ?? string.Empty,
        detected ?? string.Empty));
    sequences.TryRemove(resultId, out _);
};
recognizer.Canceled += (_, e) =>
    Raise(new TranslationCanceledUpdate(
        "session",
        Interlocked.Increment(ref nextSequence),
        $"{e.Reason}: {e.ErrorCode}"));

void Raise(TranslationUpdate update) =>
    UpdateReceived?.Invoke(this, new TranslationUpdateEventArgs(update));
```

`WriteAudioAsync` calls `pushStream.Write(audio.ToArray())` only while started. `StopAsync` calls `StopContinuousRecognitionAsync`, closes the push stream, and is idempotent. `DisposeAsync` disposes recognizer, audio config, stream, and format exactly once.

- [ ] **Step 3: Verify start/stop and prerecorded samples**

Add one self-recorded 16 kHz mono PCM WAV sample per source language under `tests/fixtures/audio/`, with matching `.license.txt` declaring project ownership and MIT redistribution. At least one fixture must contain a valid non-audio RIFF chunk before `data`, proving the test path does not assume a 44-byte header.

Read WAV files with NAudio's RIFF parser and stream only the parsed data chunk:

```csharp
private static async Task StreamWaveAsync(
    string path,
    ITranslationSession session,
    CancellationToken cancellationToken)
{
    using var reader = new WaveFileReader(path);
    Assert.Equal(WaveFormatEncoding.Pcm, reader.WaveFormat.Encoding);
    Assert.Equal(16_000, reader.WaveFormat.SampleRate);
    Assert.Equal(16, reader.WaveFormat.BitsPerSample);
    Assert.Equal(1, reader.WaveFormat.Channels);

    var buffer = new byte[3_200];
    int read;
    while ((read = reader.Read(buffer, 0, buffer.Length)) > 0)
    {
        await session.WriteAudioAsync(buffer.AsMemory(0, read), cancellationToken);
        await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);
    }
}
```

Add `Wav_with_extra_chunks_streams_only_pcm_data` and assert the number of bytes sent equals `WaveFileReader.Length`. No code may seek to offset 44 or subtract 44 from file length.

Run each target direction explicitly:

```powershell
$env:AZURE_SPEECH_ENDPOINT='configured-locally'
$env:AZURE_SPEECH_KEY='configured-locally'
dotnet test tests/RealTimeTranslation.IntegrationTests --filter Category=AzureLive
Remove-Item Env:AZURE_SPEECH_KEY
Remove-Item Env:AZURE_SPEECH_ENDPOINT
```

Expected: source language detected as the corresponding candidate and translation is non-empty. Never print the key.

- [ ] **Step 4: Add the feasibility CLI mode**

`Program.cs` accepts:

```text
--wav <path> --target zh-Hans|en|ru
```

It reads Endpoint and Key from environment variables, streams the file, prints timestamps for first partial and final translation, and exits `0` on success or `1` on failure. It must never print credential values.

- [ ] **Step 5: Gate the project on real evidence**

Run all six cross-language directions. If continuous language identification, target codes, or callbacks differ from the design, stop here and write the exact SDK version, input file, exception/result payload, and reproduction command to `docs/feasibility/azure-audio-report.md`. Do not continue to UI work until the design is amended and approved.

- [ ] **Step 6: Commit**

```powershell
git add src/RealTimeTranslation.Infrastructure/Azure tests/RealTimeTranslation.IntegrationTests tools/RealTimeTranslation.Feasibility tests/fixtures docs/feasibility
git commit -m "feat: add Azure streaming translation session"
```

---

### Task 7: 实现统一 PCM 转换和两个 Windows 音源

**Files:**
- Create: `src/RealTimeTranslation.Infrastructure/Audio/DownmixToMonoSampleProvider.cs`
- Create: `src/RealTimeTranslation.Infrastructure/Audio/Pcm16MonoConverter.cs`
- Create: `src/RealTimeTranslation.Infrastructure/Audio/SystemLoopbackAudioSource.cs`
- Create: `src/RealTimeTranslation.Infrastructure/Audio/MicrophoneAudioSource.cs`
- Test: `tests/RealTimeTranslation.Infrastructure.Tests/Pcm16MonoConverterTests.cs`
- Test: `tests/RealTimeTranslation.Infrastructure.Tests/AudioSourceLifecycleTests.cs`
- Test support: `tests/RealTimeTranslation.Infrastructure.Tests/TestSupport/SineWave.cs`

**Interfaces:**
- Consumes: `IAudioCaptureSource`, `AudioFormat.AzurePcm`.
- Produces: owned `byte[]` frames in 16 kHz, 16-bit, mono PCM.

- [ ] **Step 1: Write a failing deterministic conversion test**

Generate one second of 48 kHz stereo floating-point sine wave in memory. Feed it to `Pcm16MonoConverter`; assert output format equals `AudioFormat.AzurePcm`, output is non-empty, and byte count is within 31,800–32,200 bytes.

```csharp
[Fact]
public void Converts_48k_stereo_float_to_16k_mono_pcm16()
{
    var input = WaveFormat.CreateIeeeFloatWaveFormat(48_000, 2);
    using var converter = new Pcm16MonoConverter(input);
    var output = new List<byte>();
    converter.FrameReady += (_, frame) => output.AddRange(frame.Data);
    converter.Write(SineWave.CreateFloatStereo(48_000, TimeSpan.FromSeconds(1), 440));
    Assert.InRange(output.Count, 31_800, 32_200);
}
```

Create the deterministic helper used by the test:

```csharp
internal static class SineWave
{
    public static byte[] CreateFloatStereo(int sampleRate, TimeSpan duration, double frequency)
    {
        var frameCount = checked((int)(sampleRate * duration.TotalSeconds));
        var samples = new float[frameCount * 2];
        for (var frame = 0; frame < frameCount; frame++)
        {
            var sample = (float)Math.Sin(2 * Math.PI * frequency * frame / sampleRate);
            samples[frame * 2] = sample;
            samples[frame * 2 + 1] = sample;
        }

        var bytes = new byte[samples.Length * sizeof(float)];
        Buffer.BlockCopy(samples, 0, bytes, 0, bytes.Length);
        return bytes;
    }
}
```

Run: `dotnet test tests/RealTimeTranslation.Infrastructure.Tests --filter Pcm16MonoConverterTests`

Expected: FAIL because converter types do not exist.

- [ ] **Step 2: Implement conversion pipeline**

Construct this NAudio pipeline once per source:

```csharp
buffered = new BufferedWaveProvider(inputFormat)
{
    DiscardOnBufferOverflow = false,
    ReadFully = false,
    BufferDuration = TimeSpan.FromSeconds(2)
};
ISampleProvider samples = buffered.ToSampleProvider();
samples = new DownmixToMonoSampleProvider(samples);
samples = new WdlResamplingSampleProvider(samples, 16_000);
output = new SampleToWaveProvider16(samples);
```

`Write(byte[] data)` adds bytes to `buffered`, drains `output` into 3,200-byte owned frames (100 ms at 16 kHz PCM16 mono), and raises `FrameReady`. `DownmixToMonoSampleProvider` averages all input channels per sample frame, so stereo and surround devices follow one deterministic rule.

- [ ] **Step 3: Implement system loopback source**

Use `WasapiLoopbackCapture` on the default render device. In `DataAvailable`, pass the recorded bytes through one `Pcm16MonoConverter`; relay owned frames through `FrameAvailable`. In `RecordingStopped`, raise `Faulted` only when `Exception` is non-null. `StartAsync`, `StopAsync`, and `DisposeAsync` must be idempotent.

- [ ] **Step 4: Implement microphone source**

Use `WasapiCapture` on the default capture device and the same conversion pipeline and lifecycle semantics. Do not open loopback and microphone simultaneously; that invariant remains in the coordinator and UI.

- [ ] **Step 5: Run local device tests**

Add traits `Category=WindowsAudioManual`. Each test captures five seconds and asserts at least one non-empty frame. For loopback, instruct the test operator to play the repository-owned fixture; for microphone, instruct them to speak. Manual tests are excluded from default CI through an environment guard `RUN_WINDOWS_AUDIO_TESTS=1`.

Run:

```powershell
$env:RUN_WINDOWS_AUDIO_TESTS='1'
dotnet test tests/RealTimeTranslation.Infrastructure.Tests --filter Category=WindowsAudioManual
Remove-Item Env:RUN_WINDOWS_AUDIO_TESTS
```

Expected: both sources emit canonical PCM frames and stop cleanly.

- [ ] **Step 6: Verify and commit**

Run: `dotnet test RealTimeTranslation.slnx`

```powershell
git add src/RealTimeTranslation.Infrastructure/Audio tests/RealTimeTranslation.Infrastructure.Tests
git commit -m "feat: capture canonical Windows audio"
```

---

### Task 8: 完成端到端技术验证并形成报告

**Files:**
- Modify: `tools/RealTimeTranslation.Feasibility/Program.cs`
- Create: `docs/feasibility/azure-audio-report.md`

**Interfaces:**
- Consumes: Azure session and both real audio sources.
- Produces: evidence gate required before UI implementation.

- [ ] **Step 1: Extend the CLI with live sources**

Supported commands:

```text
--source system --target zh-Hans|en|ru --duration 00:05:00
--source microphone --target zh-Hans|en|ru --duration 00:05:00
```

The CLI prints only timestamps, detected language codes, event kinds, latency, and non-sensitive error categories. It does not print transcripts by default; `--show-text` explicitly enables local console text for manual validation.

- [ ] **Step 2: Run the required matrix**

Execute:

- system audio: English→Chinese, Russian→Chinese, Chinese→English;
- microphone: one direction per source language;
- mixed sequence: separate Chinese, English, Russian sentences in one continuous session;
- pause for ten seconds, resume, then translate another sentence;
- disconnect network once and observe 1/2/4-second retries;
- continuous system audio for at least 30 minutes.

- [ ] **Step 3: Record evidence, not conclusions without data**

`azure-audio-report.md` must contain:

```markdown
# Azure and Windows Audio Feasibility Report

## Environment
- OS build:
- .NET SDK: 10.0.301
- Speech SDK: 1.53.0
- NAudio: 3.3.0
- Azure region:

## Test Matrix
| Source | Spoken language | Target | Detected | First partial ms | Final translation ms | Result |

## Stability
| Duration | Peak private bytes | Reconnect count | Duplicate session count | Result |

## Findings
- Confirmed capabilities
- Reproducible limitations
- Design changes required: none, or an explicitly approved change reference
```

All rows must contain measured values; the report cannot contain placeholders when committed.

- [ ] **Step 4: Apply the gate**

Proceed only if system loopback, all three languages, 2–4 second typical experience, pause/resume, and 30-minute stability are acceptable. Otherwise stop and request a design decision with the completed report attached.

- [ ] **Step 5: Commit**

```powershell
git add tools/RealTimeTranslation.Feasibility docs/feasibility/azure-audio-report.md
git commit -m "docs: record translation feasibility results"
```

Stop after this commit. Present `docs/feasibility/azure-audio-report.md`, measured results, failures, and the Task 1–8 commit list to the project owner. Do not begin Task 9 or any UI implementation until the owner explicitly approves the feasibility gate.

---

### Task 9: 实现设置窗口与启动编排

**Files:**
- Modify: `src/RealTimeTranslation.Desktop/App.xaml`
- Modify: `src/RealTimeTranslation.Desktop/App.xaml.cs`
- Create: `src/RealTimeTranslation.Desktop/ViewModels/SettingsViewModel.cs`
- Create: `src/RealTimeTranslation.Desktop/Services/WindowOrchestrator.cs`
- Rename: `src/RealTimeTranslation.Desktop/MainWindow.xaml` to `src/RealTimeTranslation.Desktop/SettingsWindow.xaml`
- Rename: `src/RealTimeTranslation.Desktop/MainWindow.xaml.cs` to `src/RealTimeTranslation.Desktop/SettingsWindow.xaml.cs`
- Test: `tests/RealTimeTranslation.Desktop.Tests/SettingsViewModelTests.cs`

**Interfaces:**
- Consumes: settings store, credential store, coordinator.
- Produces: first-run and repeat-run flows without automatic audio upload.

- [ ] **Step 1: Write view-model tests before XAML**

Test exact behaviors:

```csharp
[Fact]
public async Task Start_is_disabled_until_endpoint_key_source_and_target_are_valid();

[Fact]
public async Task Test_connection_saves_key_only_after_success();

[Fact]
public async Task Loading_settings_never_starts_translation();

[Fact]
public async Task Selecting_system_audio_clears_microphone_selection();
```

Use fake settings, credentials, and connection tester. Run the test class and verify failure before implementation.

- [ ] **Step 2: Implement SettingsViewModel**

Expose these exact bindable members:

```csharp
string Endpoint
string ApiKey
AudioSourceKind SelectedAudioSource
TargetLanguage SelectedTargetLanguage
string CurrentDeviceName
string StatusMessage
bool IsBusy
bool CanStart
ICommand TestConnectionCommand
ICommand StartTranslationCommand
ICommand DeleteCredentialCommand
```

`StartTranslationCommand` saves non-sensitive settings, calls coordinator once, then asks `WindowOrchestrator` to show subtitles. It never persists `ApiKey` outside `ICredentialStore`.

- [ ] **Step 3: Build the settings XAML**

The window contains, in order: title, cloud privacy/cost notice, Endpoint field, masked API Key field, “测试连接”, source radio buttons, current device label, target language combo box, status text, and primary “开始翻译” button. Bind controls to the exact members above; no additional MVP features are added.

- [ ] **Step 4: Add single-instance startup**

In `App.xaml.cs`, create a named mutex `RealTimeTranslation.MVP.Singleton`. If ownership is false, show “实时翻译已经在运行” and exit before constructing windows or services. On normal shutdown, dispose the mutex after coordinator shutdown.

- [ ] **Step 5: Verify**

Run:

```powershell
dotnet test RealTimeTranslation.slnx
dotnet build src/RealTimeTranslation.Desktop --configuration Release
dotnet run --project src/RealTimeTranslation.Desktop
```

Expected: first run shows configuration; repeat run loads settings but does not capture audio; a second process exits with a message.

- [ ] **Step 6: Commit**

```powershell
git add src/RealTimeTranslation.Desktop tests/RealTimeTranslation.Core.Tests
git commit -m "feat: add secure translation setup flow"
```

---

### Task 10: 实现置顶悬浮字幕条

**Files:**
- Create: `src/RealTimeTranslation.Desktop/SubtitleWindow.xaml`
- Create: `src/RealTimeTranslation.Desktop/SubtitleWindow.xaml.cs`
- Create: `src/RealTimeTranslation.Desktop/ViewModels/SubtitleViewModel.cs`
- Modify: `src/RealTimeTranslation.Desktop/Services/WindowOrchestrator.cs`
- Create: `src/RealTimeTranslation.Desktop/Services/IUiDispatcher.cs`
- Create: `src/RealTimeTranslation.Desktop/Services/WpfUiDispatcher.cs`
- Test: `tests/RealTimeTranslation.Desktop.Tests/SubtitleViewModelTests.cs`

**Interfaces:**
- Consumes: coordinator subtitle/state events and settings store.
- Produces: always-on-top, non-activating, draggable overlay with pause, settings, close.

- [ ] **Step 1: Write view-model tests**

```csharp
[Fact]
public void Partial_original_uses_partial_visual_state();

[Fact]
public async Task Pause_command_calls_coordinator_once_and_changes_label_to_resume();

[Fact]
public async Task Return_to_settings_stops_before_switching_windows();

[Fact]
public async Task Close_stops_before_requesting_application_shutdown();
```

Run and confirm failure before implementation.

- [ ] **Step 2: Implement SubtitleViewModel**

Expose:

```csharp
string OriginalText
string TranslatedText
bool IsOriginalPartial
string ConnectionStatus
bool AreControlsVisible
string PauseButtonText
ICommand PauseResumeCommand
ICommand ReturnToSettingsCommand
ICommand CloseCommand
```

Subscribe once to coordinator events and unsubscribe on disposal. Marshal updates to the WPF dispatcher through an injected `IUiDispatcher` so core code remains UI-independent.

```csharp
public interface IUiDispatcher
{
    void Invoke(Action action);
}

public sealed class WpfUiDispatcher(Dispatcher dispatcher) : IUiDispatcher
{
    public void Invoke(Action action) => dispatcher.Invoke(action);
}
```

- [ ] **Step 3: Implement exact window behavior**

Use `WindowStyle="None"`, `AllowsTransparency="True"`, `Background="Transparent"`, `Topmost="True"`, `ShowInTaskbar="False"`, and `ResizeMode="NoResize"`. The content is a rounded semi-transparent dark panel with two wrapping text blocks limited to two lines. Controls appear only while the mouse is over the window.

In `SourceInitialized`, apply `WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW` using `GetWindowLongPtr`/`SetWindowLongPtr`. Drag only from the non-button background using `DragMove()`. Save `Left` and `Top` after drag completes.

- [ ] **Step 4: Wire window transitions**

`WindowOrchestrator` owns exactly one settings window and one subtitle window. Starting translation hides settings and shows subtitles. Returning to settings awaits coordinator stop, hides subtitles, and shows settings. Closing awaits stop before `Application.Current.Shutdown()`.

- [ ] **Step 5: Verify manually and automatically**

Run all tests, then confirm:

- overlay remains above browser/video;
- clicking the underlying application does not make the overlay steal focus;
- dragging persists position after restart;
- long original and translation text never exceed two visible lines;
- hover controls work;
- pause stops new Azure traffic before the UI reports Paused.

- [ ] **Step 6: Commit**

```powershell
git add src/RealTimeTranslation.Desktop tests/RealTimeTranslation.Core.Tests
git commit -m "feat: add always-on-top subtitle overlay"
```

---

### Task 11: 完成安全日志、错误分类和恢复体验

**Files:**
- Create: `src/RealTimeTranslation.Core/AppError.cs`
- Create: `src/RealTimeTranslation.Infrastructure/Logging/SafeRollingFileLogger.cs`
- Modify: `src/RealTimeTranslation.Infrastructure/Azure/AzureTranslationSession.cs`
- Modify: `src/RealTimeTranslation.Core/TranslationCoordinator.cs`
- Modify: `src/RealTimeTranslation.Desktop/ViewModels/SettingsViewModel.cs`
- Modify: `src/RealTimeTranslation.Desktop/ViewModels/SubtitleViewModel.cs`
- Test: `tests/RealTimeTranslation.Infrastructure.Tests/SafeRollingFileLoggerTests.cs`
- Test: `tests/RealTimeTranslation.Core.Tests/ErrorRecoveryTests.cs`

**Interfaces:**
- Produces: user-safe `AppErrorCode` values and bounded local diagnostics.

- [ ] **Step 1: Write redaction and recovery tests**

```csharp
[Fact]
public void Logger_removes_keys_query_strings_transcripts_and_translations();

[Fact]
public async Task Three_transient_failures_use_one_two_four_second_delays_then_fail();

[Fact]
public async Task Pause_cancels_pending_retry();

[Fact]
public async Task Old_session_callbacks_cannot_update_new_session();
```

Use literal markers `secret-key`, `sensitive-original`, and `sensitive-translation`; assert none appears in the output file.

- [ ] **Step 2: Define stable error categories**

```csharp
public enum AppErrorCode
{
    AuthenticationFailed,
    InvalidEndpoint,
    NetworkUnavailable,
    ServiceUnavailable,
    RateLimited,
    AudioDeviceUnavailable,
    UnsupportedAudioFormat,
    Unknown
}

public sealed record AppError(AppErrorCode Code, string UserMessage, bool IsTransient);
```

Map Azure cancellation codes and audio exceptions to these categories in Infrastructure; core and UI never display raw SDK error details.

- [ ] **Step 3: Implement bounded safe logging**

Write logs under `%LOCALAPPDATA%/RealTimeTranslation/logs`. Default maximum is 1 MiB per file and three files. Public logging methods accept event name, error code, timestamp, and numeric metrics only; they do not accept transcript or translation arguments. Redact case-insensitive patterns `key=`, `token=`, `authorization=`, and URI query strings before writing.

- [ ] **Step 4: Finish retry integration**

Coordinator retries only transient `NetworkUnavailable` and `ServiceUnavailable` errors. Authentication, endpoint, audio format and device errors transition immediately to `Failed`. A new session GUID is captured by every callback; callbacks whose GUID differs from the active session are ignored.

- [ ] **Step 5: Verify and commit**

Run:

```powershell
dotnet test tests/RealTimeTranslation.Core.Tests --filter ErrorRecoveryTests
dotnet test tests/RealTimeTranslation.Infrastructure.Tests --filter SafeRollingFileLoggerTests
dotnet test RealTimeTranslation.slnx
```

Expected: all tests pass and test logs contain no sensitive markers.

```powershell
git add src tests
git commit -m "feat: harden errors retries and diagnostics"
```

---

### Task 12: 完成开源文档、发布构建与 v0.1.0 验收

**Files:**
- Create: `README.md`
- Create: `README.zh-CN.md`
- Create: `LICENSE`
- Create: `CONTRIBUTING.md`
- Create: `SECURITY.md`
- Create: `CHANGELOG.md`
- Create: `.github/ISSUE_TEMPLATE/bug_report.yml`
- Create: `.github/ISSUE_TEMPLATE/feature_request.yml`
- Create: `.github/pull_request_template.md`
- Create: `.github/workflows/release.yml`
- Create: `docs/privacy.md`
- Create: `docs/release-checklist.md`

**Interfaces:**
- Consumes: completed app and feasibility evidence.
- Produces: reproducible win-x64 release artifact and complete public repository surface.

- [ ] **Step 1: Add standard public files**

README documents exact capabilities, non-goals, Windows requirements, Azure resource setup, Endpoint/Key configuration, privacy behavior, possible Azure cost, build commands, screenshots, troubleshooting, and links to MIT/CONTRIBUTING/SECURITY. English and Chinese versions must communicate identical security and cost facts.

Use the standard MIT license text with copyright holder supplied by the repository owner before the release commit. `CHANGELOG.md` begins with `## [0.1.0] - 2026-10-27` only when all release checks pass; otherwise keep the section under `Unreleased`.

- [ ] **Step 2: Add reproducible release workflow**

Create `.github/workflows/release.yml`:

```yaml
name: release
on:
  push:
    tags: ['v*']
jobs:
  publish:
    runs-on: windows-latest
    permissions:
      contents: write
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: 10.0.301
      - run: dotnet restore RealTimeTranslation.slnx --locked-mode
      - run: dotnet test RealTimeTranslation.slnx --configuration Release --no-restore
      - run: dotnet publish src/RealTimeTranslation.Desktop/RealTimeTranslation.Desktop.csproj --configuration Release --runtime win-x64 --self-contained true --output artifacts/win-x64
      - shell: pwsh
        run: |
          Compress-Archive -Path artifacts/win-x64/* -DestinationPath artifacts/RealTimeTranslation-win-x64.zip
          (Get-FileHash artifacts/RealTimeTranslation-win-x64.zip -Algorithm SHA256).Hash | Set-Content artifacts/RealTimeTranslation-win-x64.sha256
      - uses: softprops/action-gh-release@v2
        with:
          files: |
            artifacts/RealTimeTranslation-win-x64.zip
            artifacts/RealTimeTranslation-win-x64.sha256
```

Folder-based self-contained publishing is intentional because Azure Speech contains native assets; do not enable single-file publishing without a separate successful validation.

- [ ] **Step 3: Run the full release checklist**

Run:

```powershell
dotnet restore RealTimeTranslation.slnx --locked-mode
dotnet format RealTimeTranslation.slnx --verify-no-changes
dotnet build RealTimeTranslation.slnx --configuration Release --no-restore
dotnet test RealTimeTranslation.slnx --configuration Release --no-build
dotnet publish src/RealTimeTranslation.Desktop/RealTimeTranslation.Desktop.csproj --configuration Release --runtime win-x64 --self-contained true --output artifacts/win-x64
git grep -n -i -E "(speech[_-]?key|api[_-]?key).{0,20}[=:].{8,}" -- ':!docs/superpowers/**'
```

Expected: restore/build/test/publish succeed; secret scan reports no real credential. Manually verify all P0/P1 cases from the design, including 30-minute run, network loss, audio disconnect, pause/resume, credential delete, and clean exit.

- [ ] **Step 4: Review repository diff before release**

Run:

```powershell
git status --short
git diff --check
git log --oneline --decorate -15
```

Expected: only intended project files are tracked; no credential, fixture without license, generated log, local settings, `artifacts/`, or unrelated shortcut is included.

- [ ] **Step 5: Commit the release-ready repository**

```powershell
git add README.md README.zh-CN.md LICENSE CONTRIBUTING.md SECURITY.md CHANGELOG.md docs .github
git commit -m "docs: prepare v0.1.0 open source release"
```

Do not create or push tag `v0.1.0` until the owner explicitly authorizes publication.

---

## Implementation Checkpoints

- After Task 1: solution baseline review.
- After Task 6: Azure API behavior review; stop on mismatch.
- After Task 8: mandatory stop. Submit the committed feasibility report and wait for explicit approval; no UI work before approval.
- After Task 10: interactive UX review.
- After Task 12: release readiness review; publishing remains a separate explicit authorization.

## Success Traceability

| Design requirement | Implemented by |
| --- | --- |
| Windows/.NET/WPF baseline | Tasks 1, 9 |
| 中英俄自动识别与三种目标语言 | Tasks 2, 6, 8 |
| 系统声音与麦克风二选一 | Tasks 2, 4, 7, 9 |
| Azure 用户自备凭据 | Tasks 5, 6, 9 |
| 悬浮双语字幕 | Tasks 3, 10 |
| 暂停、恢复、返回、关闭 | Tasks 4, 10, 11 |
| 有限重试和错误提示 | Tasks 4, 11 |
| 不保存音频和字幕 | Tasks 3, 5, 11 |
| 2～4 秒体验与 30 分钟稳定性 | Tasks 8, 12 |
| MIT 开源和 GitHub 标准结构 | Tasks 1, 12 |
| v0.1.0 可复现发布物 | Task 12 |
