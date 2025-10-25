using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using PasswordVault.Models;
using Spectre.Console;
using TextCopy;

namespace PasswordVault.ui;

public class TerminalUI
{
    public void ShowWelcome()
    {
        Console.Clear();
        var panel = new Panel("[bold blue]Password Vault[/]")
            .Border(BoxBorder.Rounded)
            .BorderColor(Color.Blue);
        AnsiConsole.Write(panel);
        AnsiConsole.WriteLine();
    }

    public string PromptFirstRunOrUnlock(bool vaultExists)
    {
        if (!vaultExists)
        {
            return "Create Vault";
        }
        return AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("[yellow]Vault detected. Choose an option:[/]")
                .AddChoices("Unlock Vault", "Recreate Vault"));
    }

    public string PromptSetMasterPassword()
    {
        while (true)
        {
            var pw1 = AnsiConsole.Prompt(new TextPrompt<string>("[green]Set master password:[/]").Secret());
            var score = PasswordVault.Services.PasswordStrength.EstimateScore(pw1);
            var minScore = PasswordVault.Services.SecurityPolicy.Current.MinPasswordScore;
            var minLen = PasswordVault.Services.SecurityPolicy.Current.MinPasswordLength;
            if (pw1.Length < minLen || score < minScore)
            {
                AnsiConsole.MarkupLine($"[red]Password too weak (len>={minLen}, score>={minScore}). Try a longer passphrase with mixed characters.[/]");
                continue;
            }
            var pw2 = AnsiConsole.Prompt(new TextPrompt<string>("[green]Confirm master password:[/]").Secret());
            if (pw1 == pw2 && !string.IsNullOrWhiteSpace(pw1)) return pw1;
            AnsiConsole.MarkupLine("[red]Passwords do not match or empty. Try again.[/]");
        }
    }

    public string PromptUnlock()
    {
        return AnsiConsole.Prompt(new TextPrompt<string>("[green]Enter master password:[/]").Secret());
    }

    public string ShowMainMenu()
    {
        Console.Clear();
        return AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("[green]Choose an action[/]")
                .AddChoices("Add Entry", "Get Entry", "List Entries", "Search Entries (fuzzy)", "Delete Entry", "Generate Password", "Change Master Password", "Export Vault", "Import Vault", "Delete Account", "Save & Lock", "Quit"));
    }

    public VaultEntry PromptNewEntry()
    {
        var name = AnsiConsole.Ask<string>("[yellow]Name (e.g., github):[/]");
        var username = AnsiConsole.Ask<string>("[yellow]Username:[/]");
        var password = AnsiConsole.Prompt(new TextPrompt<string>("[yellow]Password (leave blank to generate):[/]").AllowEmpty());
        if (string.IsNullOrEmpty(password))
        {
            password = PasswordVault.Services.PasswordGenerator.Generate();
            Console.WriteLine($"GENERATED PASSWORD: {password}");
            // AnsiConsole.MarkupLine($"[gray]Generated password:[/] {password.ToString()}");
        }
        var notes = AnsiConsole.Prompt(new TextPrompt<string>("[yellow]Notes (optional):[/]").AllowEmpty());
        return new VaultEntry { Name = name, Username = username, Password = password, Notes = notes, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow };
    }

    public string PromptEntryName(string title = "Which entry?")
    {
        return AnsiConsole.Ask<string>($"[yellow]{title}[/]");
    }

    public void ShowEntry(VaultEntry entry, bool offerCopy = true)
    {
        var table = new Table().Border(TableBorder.Rounded);
        table.AddColumn("Field");
        table.AddColumn("Value");
        table.AddRow("Name", entry.Name);
        table.AddRow("Username", entry.Username);
        table.AddRow("Password", entry.Password);
        table.AddRow("Notes", entry.Notes);
        table.AddRow("Updated", entry.UpdatedAt.ToString("u"));
        AnsiConsole.Write(table);

        if (offerCopy && AnsiConsole.Confirm("Copy password to clipboard?"))
        {
            PasswordVault.Services.ClipboardHelper.StartCopyWithAutoClear(entry.Password, TimeSpan.FromSeconds(PasswordVault.Services.SecurityPolicy.Current.ClipboardClearSeconds));
            AnsiConsole.MarkupLine($"[green]Password copied. Will clear in {PasswordVault.Services.SecurityPolicy.Current.ClipboardClearSeconds}s.[/]");
        }
    }

    public void ShowNamesList(string[] names)
    {
        if (names.Length == 0)
        {
            AnsiConsole.MarkupLine("[gray]No entries yet.[/]");
            AnsiConsole.WriteLine();
            PressReturnToContinue();
            return;
        }
        var table = new Table().Border(TableBorder.Minimal);
        table.AddColumn("Entries");
        foreach (var n in names)
            table.AddRow(n);
        AnsiConsole.Write(table);
        PressReturnToContinue();
    }

    public bool ConfirmDelete(string name)
    {
        return AnsiConsole.Confirm($"Delete '{name}'?");
    }

    public string PromptSearchQuery()
    {
        return AnsiConsole.Ask<string>("[yellow]Search query:[/]");
    }

    public void ShowSearchResults(List<(string name, double score)> results)
    {
        if (results.Count == 0)
        {
            AnsiConsole.MarkupLine("[gray]No matches.[/]");
            return;
        }
        var table = new Table().Border(TableBorder.Rounded);
        table.AddColumn("Name");
        table.AddColumn("Score");
        foreach (var r in results)
        {
            table.AddRow(r.name, r.score.ToString("0.000"));
        }
        AnsiConsole.Write(table);
    }

    public int PromptPasswordLength()
    {
        return AnsiConsole.Prompt(new TextPrompt<int>("[yellow]Length (>=8):[/]").DefaultValue(16));
    }

    public string PromptFilePath(string title)
    {
        return AnsiConsole.Ask<string>($"[yellow]{title}[/]");
    }

    public bool ConfirmOverwrite(string path)
    {
        return AnsiConsole.Confirm($"'{path}' exists. Overwrite?");
    }

    public void AnyMessage(string color, string message)
    {
        AnsiConsole.MarkupLine($"[{color}]{message}[/]");
    }

    public bool Confirm(string message)
    {
        return AnsiConsole.Confirm(message);
    }

    public string PromptExactInput(string title)
    {
        return AnsiConsole.Ask<string>($"[yellow]{title}[/]");
    }

    public bool PromptUpgradeKdf(string currentSpec, string recommendedSpec)
    {
        var choice = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title($"[yellow]Upgrade vault encryption?\nCurrent: {currentSpec}\nRecommended: {recommendedSpec}[/]")
                .AddChoices("Upgrade now", "Not now"));
        return choice == "Upgrade now";
    }

    public void PressReturnToContinue()
    {
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[red]Press enter to continue.[/]");
        Console.ReadLine();
    }

    public void CopyToClipboard(string copy)
    {
        if (AnsiConsole.Confirm("Copy password to clipboard?"))
        {
            ClipboardService.SetText(copy);
            AnsiConsole.MarkupLine("[green]Password copied to clipboard.[/]");
        }
    }
}
