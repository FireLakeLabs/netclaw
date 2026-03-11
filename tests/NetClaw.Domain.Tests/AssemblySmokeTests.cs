namespace NetClaw.Domain.Tests;

public sealed class AssemblySmokeTests
{
    [Fact]
    public void DomainAssemblyMarker_ComesFromDomainAssembly()
    {
        Assert.Equal(
            "NetClaw.Domain",
            typeof(NetClaw.Domain.DomainAssemblyMarker).Assembly.GetName().Name);
    }

    [Fact]
    public void DomainAssembly_ExposesCoreContractNamespaces()
    {
        Type[] contractTypes =
        [
            typeof(NetClaw.Domain.Contracts.Persistence.IMessageRepository),
            typeof(NetClaw.Domain.Contracts.Services.IMessageFormatter),
            typeof(NetClaw.Domain.Contracts.Containers.ContainerInput),
            typeof(NetClaw.Domain.Contracts.Ipc.IpcMessageCommand),
            typeof(NetClaw.Domain.Contracts.Agents.AgentExecutionRequest),
            typeof(NetClaw.Domain.Enums.AgentProviderKind)
        ];

        Assert.All(
            contractTypes,
            contractType => Assert.Equal("NetClaw.Domain", contractType.Assembly.GetName().Name));
    }
}
