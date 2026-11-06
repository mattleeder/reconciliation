namespace Reconciliation.Web.Services;

public static class SampleFiles
{
    // The CSVs live in the repo's samples/ folder and are embedded into this assembly
    // (see Reconciliation.Web.csproj), so there is one copy and it works wherever the app is hosted.
    public static async Task<string> ReadAsync(string fileName)
    {
        await using var stream = typeof(SampleFiles).Assembly.GetManifestResourceStream($"Samples.{fileName}")
            ?? throw new InvalidOperationException($"Embedded sample file '{fileName}' was not found.");
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync();
    }
}
