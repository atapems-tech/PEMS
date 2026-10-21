using PEMS.Persistence.Initialization;

namespace PEMS.Tests.Deployment;

/// <summary>
/// Code-first bootstrap: the full-text script ships inside the assembly and is split into
/// executable batches (no hand-run SSMS step, no hardcoded database name).
/// </summary>
public class DatabaseInitializerTests
{
    [Fact]
    public void SplitBatches_removes_USE_statements_and_splits_on_GO()
    {
        var script = "USE PEMSDB;\nGO\n\nSELECT 1;\nGO\n  go  \nSELECT 2;\nGO\n";

        var batches = DatabaseInitializer.SplitBatches(script);

        Assert.Equal(new[] { "SELECT 1;", "SELECT 2;" }, batches);
    }

    [Fact]
    public void SplitBatches_does_not_split_on_words_that_merely_start_with_GO()
    {
        var batches = DatabaseInitializer.SplitBatches("SELECT 'GOOD';\nGOTO_LABEL: SELECT 2;\n");

        Assert.Single(batches);
    }

    [Fact]
    public void Embedded_full_text_script_is_present_database_agnostic_and_idempotent()
    {
        var assembly = typeof(DatabaseInitializer).Assembly;
        using var stream = assembly.GetManifestResourceStream(DatabaseInitializer.FullTextResourceName);
        Assert.NotNull(stream);

        var script = new StreamReader(stream!).ReadToEnd();
        var batches = DatabaseInitializer.SplitBatches(script);

        Assert.NotEmpty(batches);
        Assert.DoesNotContain(batches, b => b.Contains("USE PEMSDB", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(batches, b => b.Contains("FULLTEXTSERVICEPROPERTY", StringComparison.OrdinalIgnoreCase));
        Assert.All(batches.Where(b => b.Contains("CREATE FULLTEXT INDEX", StringComparison.OrdinalIgnoreCase)),
            b => Assert.Contains("NOT EXISTS", b, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Options_default_to_a_fully_automatic_bootstrap()
    {
        var options = new DatabaseInitializationOptions();

        Assert.True(options.CreateIfMissing);
        Assert.True(options.MigrateOnStartup);
        Assert.True(options.ApplyFullTextSearch);
        Assert.True(options.CommandTimeoutSeconds >= 60);
    }
}
