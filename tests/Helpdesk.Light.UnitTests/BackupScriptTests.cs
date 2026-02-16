namespace Helpdesk.Light.UnitTests;

public sealed class BackupScriptTests
{
    [Fact]
    public void BackupScript_UsesSqliteBackupCommandInsteadOfRawCopy()
    {
        string repositoryRoot = FindRepositoryRoot();
        string scriptPath = Path.Combine(repositoryRoot, "scripts", "backup-helpdesk.sh");

        Assert.True(File.Exists(scriptPath), $"Expected backup script at '{scriptPath}'.");

        string script = File.ReadAllText(scriptPath);

        Assert.Contains("sqlite3 \"$DB_PATH\" \".backup '$PAYLOAD_DIR/helpdesk.db'\"", script, StringComparison.Ordinal);
        Assert.DoesNotContain("cp \"$DB_PATH\" \"$PAYLOAD_DIR/helpdesk.db\"", script, StringComparison.Ordinal);
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "Helpdesk.Light.slnx")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException("Unable to locate repository root from test execution path.");
    }
}
