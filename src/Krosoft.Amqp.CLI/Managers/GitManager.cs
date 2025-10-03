using System.Diagnostics;
using Krosoft.Amqp.CLI.Interfaces;

namespace Krosoft.Amqp.CLI.Managers;

//internal class GitManager : IGitManager
//{
//    public async Task<int> Pull()
//    {
//        try
//        {
//            DisplayHeader("Pull repository");

//            var result = await ExecuteGitCommand("pull");

//            Console.ForegroundColor = ConsoleColor.Green;
//            Console.WriteLine();
//            Console.ResetColor();

//            return result;
//        }
//        catch (Exception ex)
//        {
//            Console.ForegroundColor = ConsoleColor.Red;
//            Console.WriteLine($"Erreur lors du pull : {ex.Message}");
//            Console.ResetColor();
//            return 1;
//        }
//    }

//    public async Task<int> Clean()
//    {
//        try
//        {
//            DisplayHeader("Clean up all old branches");

internal class GitManager : IGitManager
{
    public async Task<int> Pull()
    {
        DisplayHeader("Pull repository");
        return await ExecuteGitCommand("pull");
    }

    public async Task<int> Clean()
    {
        DisplayHeader("Clean up all old branches");

        var pullResult = await ExecuteGitCommand("pull");
        if (pullResult != 0)
        {
            return pullResult;
        }

        var pruneResult = await ExecuteGitCommand("fetch origin --prune");
        if (pruneResult != 0)
        {
            return pruneResult;
        }

        await CleanGoneBranches();

        DisplayFooter();
        return 0;
    }

    private void DisplayHeader(string title)
    {
        WriteColoredLine(ConsoleColor.Green, "==========================================");
        WriteColoredLine(ConsoleColor.Green, title);
        WriteColoredLine(ConsoleColor.Green, "==========================================");

        WriteColored(ConsoleColor.Blue, "Path : ");
        Console.WriteLine(Directory.GetCurrentDirectory());

        WriteColoredLine(ConsoleColor.Green, "==========================================");
        Console.ResetColor();
    }

    private void DisplayFooter()
    {
        WriteColoredLine(ConsoleColor.Green, "==========================================");
    }

    private void WriteColoredLine(ConsoleColor color, string text)
    {
        Console.ForegroundColor = color;
        Console.WriteLine(text);
        Console.ResetColor();
    }

    private void WriteColored(ConsoleColor color, string text)
    {
        Console.ForegroundColor = color;
        Console.Write(text);
        Console.ResetColor();
    }

    private async Task<int> ExecuteGitCommand(string arguments)
    {
        try
        {
            var processInfo = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(processInfo);
            if (process == null)
            {
                return HandleError("Impossible de démarrer git");
            }

            await Task.WhenAll(RedirectOutput(process.StandardOutput),
                               RedirectOutput(process.StandardError, ConsoleColor.Red));

            await process.WaitForExitAsync();
            return process.ExitCode;
        }
        catch (Exception ex)
        {
            return HandleError($"Erreur lors de l'exécution de 'git {arguments}' : {ex.Message}");
        }
    }

    private async Task RedirectOutput(StreamReader reader, ConsoleColor? color = null)
    {
        while (!reader.EndOfStream)
        {
            var line = await reader.ReadLineAsync();
            if (!string.IsNullOrEmpty(line))
            {
                if (color.HasValue)
                {
                    WriteColoredLine(color.Value, line);
                }
                else
                {
                    Console.WriteLine(line);
                }
            }
        }
    }

    private async Task CleanGoneBranches()
    {
        try
        {
            var output = await GetGitBranchOutput();
            if (string.IsNullOrEmpty(output))
            {
                return;
            }

            var goneBranches = ParseGoneBranches(output);
            await DeleteBranches(goneBranches);
        }
        catch (Exception ex)
        {
            HandleError($"Erreur lors du nettoyage des branches : {ex.Message}");
        }
    }

    private async Task<string> GetGitBranchOutput()
    {
        var processInfo = new ProcessStartInfo
        {
            FileName = "git",
            Arguments = "branch -vv",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            CreateNoWindow = true
        };

        using var process = Process.Start(processInfo);
        if (process == null)
        {
            return string.Empty;
        }

        var output = await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();

        return process.ExitCode == 0 ? output : string.Empty;
    }

    private string[] ParseGoneBranches(string branchOutput)
    {
        var goneBranches = new List<string>();
        var lines = branchOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        foreach (var line in lines)
        {
            if (IsGoneBranch(line))
            {
                var branchName = ExtractBranchName(line);
                if (!string.IsNullOrEmpty(branchName) && !branchName.StartsWith("*"))
                {
                    goneBranches.Add(branchName);
                }
            }
        }

        return goneBranches.ToArray();
    }

    private bool IsGoneBranch(string line) =>
        line.Contains("[origin/") && line.Contains(": gone]");

    private string ExtractBranchName(string line)
    {
        var parts = line.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length > 0 ? parts[0] : string.Empty;
    }

    private async Task DeleteBranches(string[] branches)
    {
        foreach (var branch in branches)
        {
            Console.WriteLine($"Suppression de la branche locale : {branch}");
            await ExecuteGitCommand($"branch -D {branch}");
        }
    }

    private int HandleError(string message)
    {
        WriteColoredLine(ConsoleColor.Red, message);
        return 1;
    }
}