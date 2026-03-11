namespace NetClaw.Setup;

public static class SetupStatusWriter
{
    public static void Write(SetupResult result)
    {
        Console.WriteLine($"=== NETCLAW SETUP: {result.StepName.ToUpperInvariant()} ===");
        foreach (KeyValuePair<string, string> item in result.Status)
        {
            Console.WriteLine($"{item.Key}: {item.Value}");
        }

        Console.WriteLine("=== END ===");
    }
}
