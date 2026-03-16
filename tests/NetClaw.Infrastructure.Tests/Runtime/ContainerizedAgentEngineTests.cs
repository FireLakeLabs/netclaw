using System.Text.Json;
using System.Text.Json.Serialization;
using NetClaw.Domain.Contracts.Agents;
using NetClaw.Domain.Contracts.Containers;
using NetClaw.Domain.Contracts.Services;
using NetClaw.Domain.Entities;
using NetClaw.Domain.Enums;
using NetClaw.Domain.ValueObjects;
using NetClaw.Infrastructure.Configuration;
using NetClaw.Infrastructure.FileSystem;
using NetClaw.Infrastructure.Paths;
using NetClaw.Infrastructure.Runtime;
using NetClaw.Infrastructure.Runtime.Agents;
using Microsoft.Extensions.Logging.Abstractions;

namespace NetClaw.Infrastructure.Tests.Runtime;

public sealed class ContainerizedAgentEngineTests : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    private readonly string tempRoot;
    private readonly StorageOptions storageOptions;
    private readonly GroupPathResolver groupPathResolver;
    private readonly PhysicalFileSystem fileSystem;

    public ContainerizedAgentEngineTests()
    {
        tempRoot = Path.Combine(Path.GetTempPath(), $"netclaw-container-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRoot);
        storageOptions = StorageOptions.Create(tempRoot);
        fileSystem = new PhysicalFileSystem();
        groupPathResolver = new GroupPathResolver(storageOptions, fileSystem);

        Directory.CreateDirectory(storageOptions.GroupsDirectory);
        Directory.CreateDirectory(storageOptions.DataDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(tempRoot))
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void BuildMounts_IncludesGroupAndSessionAndIpcDirectories()
    {
        ContainerizedAgentEngine engine = CreateEngine();
        RegisteredGroup group = new("Team", new GroupFolder("team"), "@Andy", DateTimeOffset.UtcNow);

        AgentExecutionRequest request = new(
            AgentProviderKind.Copilot,
            group,
            new ContainerInput("Prompt", null, new GroupFolder("team"), new ChatJid("team@jid"), false, false, "Andy"),
            new AgentWorkspaceContext(group.Folder, "/workspace/group", "/workspace/sessions/team", "/workspace/runtime/team", false, [], new AgentInstructionSet([])),
            null,
            []);

        IReadOnlyList<ContainerMount> mounts = engine.BuildMounts(request);

        Assert.Contains(mounts, m => m.ContainerPath == "/workspace/group" && !m.IsReadOnly);
        Assert.Contains(mounts, m => m.ContainerPath == "/home/user/.copilot" && !m.IsReadOnly);
        Assert.Contains(mounts, m => m.ContainerPath == "/workspace/ipc" && !m.IsReadOnly);
    }

    [Fact]
    public void BuildMounts_NonMainGroup_IncludesGlobalReadOnly()
    {
        string globalDir = Path.Combine(storageOptions.GroupsDirectory, "global");
        Directory.CreateDirectory(globalDir);

        ContainerizedAgentEngine engine = CreateEngine();
        RegisteredGroup group = new("Team", new GroupFolder("team"), "@Andy", DateTimeOffset.UtcNow);

        AgentExecutionRequest request = new(
            AgentProviderKind.Copilot,
            group,
            new ContainerInput("Prompt", null, new GroupFolder("team"), new ChatJid("team@jid"), false, false, "Andy"),
            new AgentWorkspaceContext(group.Folder, "/workspace/group", "/workspace/sessions/team", "/workspace/runtime/team", false, [], new AgentInstructionSet([])),
            null,
            []);

        IReadOnlyList<ContainerMount> mounts = engine.BuildMounts(request);

        Assert.Contains(mounts, m => m.ContainerPath == "/workspace/global" && m.IsReadOnly);
    }

    [Fact]
    public void BuildMounts_MainGroup_IncludesProjectReadOnly()
    {
        ContainerizedAgentEngine engine = CreateEngine();
        RegisteredGroup group = new("Main", new GroupFolder("main"), "@Andy", DateTimeOffset.UtcNow, isMain: true);

        AgentExecutionRequest request = new(
            AgentProviderKind.Copilot,
            group,
            new ContainerInput("Prompt", null, new GroupFolder("main"), new ChatJid("main@jid"), true, false, "Andy"),
            new AgentWorkspaceContext(group.Folder, "/workspace/group", "/workspace/sessions/main", "/workspace/runtime/main", true, [], new AgentInstructionSet([])),
            null,
            []);

        IReadOnlyList<ContainerMount> mounts = engine.BuildMounts(request);

        Assert.Contains(mounts, m => m.ContainerPath == "/workspace/project" && m.IsReadOnly);
        Assert.DoesNotContain(mounts, m => m.ContainerPath == "/workspace/global");
    }

    [Fact]
    public void BuildMounts_ClaudeCode_UsesClaudeSessionPath()
    {
        ContainerizedAgentEngine engine = CreateEngine(defaultProvider: "claude-code");
        RegisteredGroup group = new("Team", new GroupFolder("team"), "@Andy", DateTimeOffset.UtcNow);

        AgentExecutionRequest request = new(
            AgentProviderKind.ClaudeCode,
            group,
            new ContainerInput("Prompt", null, new GroupFolder("team"), new ChatJid("team@jid"), false, false, "Andy"),
            new AgentWorkspaceContext(group.Folder, "/workspace/group", "/workspace/sessions/team", "/workspace/runtime/team", false, [], new AgentInstructionSet([])),
            null,
            []);

        IReadOnlyList<ContainerMount> mounts = engine.BuildMounts(request);

        Assert.Contains(mounts, m => m.ContainerPath == "/home/user/.claude" && !m.IsReadOnly);
        Assert.DoesNotContain(mounts, m => m.ContainerPath == "/home/user/.copilot");
    }

    [Fact]
    public void ContainerRuntimeOptions_Validates_NewFields()
    {
        ContainerRuntimeOptions options = new()
        {
            ImageName = "netclaw-agent:latest",
            ExecutionTimeout = TimeSpan.FromMinutes(5),
            MaxOutputBytes = 1024
        };

        options.Validate();
        Assert.Equal("netclaw-agent:latest", options.ImageName);
    }

    [Fact]
    public void ContainerRuntimeOptions_RejectsEmptyImageName()
    {
        ContainerRuntimeOptions options = new() { ImageName = "" };
        Assert.Throws<InvalidOperationException>(() => options.Validate());
    }

    [Fact]
    public void ContainerRuntimeOptions_RejectsNegativeTimeout()
    {
        ContainerRuntimeOptions options = new() { ExecutionTimeout = TimeSpan.FromSeconds(-1) };
        Assert.Throws<InvalidOperationException>(() => options.Validate());
    }

    [Fact]
    public void CredentialProxyOptions_ValidatesNewFields()
    {
        CredentialProxyOptions options = new()
        {
            CopilotUpstreamUrl = "https://api.githubcopilot.com",
            ClaudeUpstreamUrl = "https://api.anthropic.com",
            AuthMode = "api-key"
        };

        options.Validate();
    }

    [Fact]
    public void CredentialProxyOptions_RejectsInvalidAuthMode()
    {
        CredentialProxyOptions options = new() { AuthMode = "invalid" };
        Assert.Throws<InvalidOperationException>(() => options.Validate());
    }

    private ContainerizedAgentEngine CreateEngine(string defaultProvider = "copilot")
    {
        return new ContainerizedAgentEngine(
            new FakeContainerRuntime(),
            new ContainerRuntimeOptions { ImageName = "netclaw-agent:test" },
            new CredentialProxyOptions(),
            new AgentRuntimeOptions { DefaultProvider = defaultProvider, CopilotConfigDirectory = "/tmp/config" },
            storageOptions,
            groupPathResolver,
            fileSystem,
            new PlatformInfo(PlatformKind.Linux, IsWsl: false, HasSystemd: true, IsRoot: false, HomeDirectory: "/home/user"),
            NullLogger<ContainerizedAgentEngine>.Instance);
    }

    private sealed class FakeContainerRuntime : IContainerRuntime
    {
        public Task CleanupOrphansAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task EnsureRunningAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public IReadOnlyList<string> GetHostGatewayArguments() => ["--add-host=host.docker.internal:host-gateway"];

        public Task StopContainerAsync(ContainerName containerName, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
