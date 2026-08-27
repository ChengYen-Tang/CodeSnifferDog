using CodeSnifferDog.Modules.Tools.Shell;

namespace CodeSnifferDog.SingleFileSmoke;

internal static class Program
{
    private const string ManagementCommand = "(Get-Command Set-Location).Source";

    private static async Task<int> Main()
    {
        try
        {
            string workingDirectory = Directory.GetCurrentDirectory();
            var result = await new PowerShellCommandRunner().RunAsync(
                ManagementCommand,
                workingDirectory,
                CancellationToken.None);

            if (result.ExitCode != 0)
            {
                Console.Error.WriteLine($"PowerShell runner failed with exit code {result.ExitCode}.");
                Console.Error.WriteLine(result.StandardError);
                return 1;
            }

            string moduleName = result.StandardOutput.Trim();

            if (!string.Equals(moduleName, "Microsoft.PowerShell.Management", StringComparison.Ordinal))
            {
                Console.Error.WriteLine(
                    $"Expected Set-Location from Microsoft.PowerShell.Management, but received '{moduleName}'.");
                return 1;
            }

            if (!string.IsNullOrEmpty(result.StandardError))
            {
                Console.Error.WriteLine(result.StandardError);
                return 1;
            }

            Console.WriteLine("Single-file PowerShell module smoke test passed.");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
    }
}
