namespace NetClaw.Host.Configuration;

public sealed record DashboardOptions
{
    public int Port { get; init; } = 5080;

    public bool Enabled { get; init; } = true;

    public string BindAddress { get; init; } = "0.0.0.0";

    public void Validate()
    {
        if (Port is < 1 or > 65535)
        {
            throw new InvalidOperationException("Dashboard port must be between 1 and 65535.");
        }

        if (string.IsNullOrWhiteSpace(BindAddress))
        {
            throw new InvalidOperationException("Dashboard bind address is required.");
        }

        if (!System.Net.IPAddress.TryParse(BindAddress, out _))
        {
            throw new InvalidOperationException($"Dashboard bind address '{BindAddress}' is not a valid IP address. Use an IPv4 (e.g. 0.0.0.0) or IPv6 (e.g. ::) address.");
        }
    }
}
