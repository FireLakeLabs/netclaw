namespace FireLakeLabs.NetClaw.Domain.Tests;

public sealed class AssemblySmokeTests
{
    [Fact]
    public void DomainAssemblyMarker_ComesFromDomainAssembly()
    {
        Assert.Equal(
            "FireLakeLabs.NetClaw.Domain",
            typeof(FireLakeLabs.NetClaw.Domain.DomainAssemblyMarker).Assembly.GetName().Name);
    }

    [Fact]
    public void DomainAssembly_ExposesCoreContractNamespaces()
    {
        Type[] contractTypes =
        [
            typeof(FireLakeLabs.NetClaw.Domain.Contracts.Persistence.IMessageRepository),
            typeof(FireLakeLabs.NetClaw.Domain.Contracts.Services.IMessageFormatter),
            typeof(FireLakeLabs.NetClaw.Domain.Contracts.Containers.ContainerInput),
            typeof(FireLakeLabs.NetClaw.Domain.Contracts.Ipc.IpcMessageCommand),
            typeof(FireLakeLabs.NetClaw.Domain.Contracts.Agents.AgentExecutionRequest),
            typeof(FireLakeLabs.NetClaw.Domain.Enums.AgentProviderKind)
        ];

        Assert.All(
            contractTypes,
            contractType => Assert.Equal("FireLakeLabs.NetClaw.Domain", contractType.Assembly.GetName().Name));
    }
}
