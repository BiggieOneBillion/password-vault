
using System;
using System.Linq;
using PasswordVault.Models;
using PasswordVault.Services;
using PasswordVault.ui;

public class Program
{
    public static void Main()
    {
        var ui = new TerminalUI();
        ui.ShowWelcome();

        string vaultPath = "vault.dat"; // single-file encrypted vault in working directory
        var vaultService = new VaultService(vaultPath);
        string masterPassword;
        VaultPayload payload = new();

        var firstStep = ui.PromptFirstRunOrUnlock(vaultService.VaultExists());
        if (firstStep == "Create Vault")
        {
            masterPassword = ui.PromptSetMasterPassword();
            vaultService.CreateNewVault(masterPassword);
            payload = new VaultPayload();
            ui.AnyMessage("green", "Vault created.");
        }
        else if (firstStep == "Recreate Vault")
        {
            if (ui.Confirm("This will erase the current vault file. Continue?"))
            {
                masterPassword = ui.PromptSetMasterPassword();
                vaultService.CreateNewVault(masterPassword);
                payload = new VaultPayload();
                ui.AnyMessage("green", "Vault recreated.");
            }
            else
            {
                // fall through to unlock existing vault
                while (true)
                {
                    try
                    {
                        masterPassword = ui.PromptUnlock();
                        payload = vaultService.LoadVault(masterPassword);
                        break;
                    }
                    catch
                    {
                        ui.AnyMessage("red", "Unlock failed. Try again.");
                    }
                }
            }
        }
        else
        {
            while (true)
            {
                try
                {
                    masterPassword = ui.PromptUnlock();
                    payload = vaultService.LoadVault(masterPassword);

                    // Migration prompt: if v1, offer upgrade to v2 (PBKDF2 600k + AAD)
                    if (vaultService.GetVaultFormatVersion() == 1)
                    {
                        if (ui.PromptUpgradeKdf("PBKDF2 210k (v1)", "PBKDF2 600k + AAD (v2)"))
                        {
                            vaultService.SaveVaultV2(masterPassword, payload);
                            ui.AnyMessage("green", "Vault upgraded to v2.");
                        }
                    }
                    break;
                }
                catch
                {
                    ui.AnyMessage("red", "Unlock failed. Try again.");
                }
            }
        }

        bool running = true;
        while (running)
        {
            var choice = ui.ShowMainMenu();
            switch (choice)
            {
                case "Add Entry":
                    var entry = ui.PromptNewEntry();
                    var existing = payload.Entries.FirstOrDefault(e => string.Equals(e.Name, entry.Name, StringComparison.OrdinalIgnoreCase));
                    if (existing != null)
                    {
                        existing.Username = entry.Username;
                        existing.Password = entry.Password;
                        existing.Notes = entry.Notes;
                        existing.UpdatedAt = DateTimeOffset.UtcNow;
                        ui.AnyMessage("yellow", "Entry updated.");
                    }
                    else
                    {
                        payload.Entries.Add(entry);
                        ui.AnyMessage("green", "Entry added.");
                    }
                    // autosave after mutation
                    vaultService.SaveVault(masterPassword, payload);
                    ui.PressReturnToContinue();
                    break;

                case "Get Entry":
                    var name = ui.PromptEntryName("Entry name to view:");
                    var found = payload.Entries.FirstOrDefault(e => string.Equals(e.Name, name, StringComparison.OrdinalIgnoreCase));
                    if (found == null) {
                        ui.AnyMessage("red", "Not found.");
                        ui.PressReturnToContinue();
                    }
                    else ui.ShowEntry(found, offerCopy: true);
                    break;

                case "List Entries":
                    ui.ShowNamesList(payload.Entries.Select(e => e.Name).OrderBy(n => n).ToArray());
                    break;

                case "Delete Entry":
                    var del = ui.PromptEntryName("Entry name to delete:");
                    var target = payload.Entries.FirstOrDefault(e => string.Equals(e.Name, del, StringComparison.OrdinalIgnoreCase));

                    if (target == null) { ui.AnyMessage("red", "Not found."); break; }
                    // ask the user to enter their password before authorising delete

                    int count = 0;
                    int maximumTries = 3;

                    while (true)
                    {
                        count++;
                        string checkUserMasterPassword = ui.PromptUnlock();
                        try
                        {
                            payload = vaultService.LoadVault(checkUserMasterPassword);
                            break;
                        }
                        catch
                        {
                            if (count == maximumTries)
                            {
                                ui.AnyMessage("red", "You have exhausted your trials. Try later.");
                                Environment.Exit(1);
                            }
                            ui.AnyMessage(
                                "red",
                                $"Wrong master password. Try again. You have {maximumTries - count} trials left."
                            );
                            continue;
                        }
                    }

                    // Re-locate the target in the freshly reloaded payload, then remove by name
                    var refreshedTarget = payload.Entries.FirstOrDefault(e => string.Equals(e.Name, del, StringComparison.OrdinalIgnoreCase));
                    if (refreshedTarget == null) {
                        ui.AnyMessage("red", "Not found.");
                        ui.PressReturnToContinue();
                        break; 
                    }

                    if (ui.ConfirmDelete(refreshedTarget.Name))
                    {
                        payload.Entries.RemoveAll(e => string.Equals(e.Name, refreshedTarget.Name, StringComparison.OrdinalIgnoreCase));
                        ui.AnyMessage("green", "Deleted.");
                        // autosave after mutation
                        vaultService.SaveVault(masterPassword, payload);
                        ui.PressReturnToContinue();
                    }
                    break;

                case "Search Entries (fuzzy)":
                    var query = ui.PromptSearchQuery();
                    var corpus = payload.Entries.Select(e => e.Name);
                    var results = FuzzySearch.Search(query, corpus, maxResults: 10);
                    ui.ShowSearchResults(results);
                    if (results.Count > 0)
                    {
                        var pick = ui.PromptEntryName("View which result (enter name):");
                        var chosen = payload.Entries.FirstOrDefault(e => string.Equals(e.Name, pick, StringComparison.OrdinalIgnoreCase));
                        if (chosen != null) ui.ShowEntry(chosen, offerCopy: true);
                        else ui.AnyMessage("red", "Not found.");
                    }
                    break;

                case "Generate Password":
                    var len = ui.PromptPasswordLength();
                    var pwd = PasswordGenerator.Generate(len);
                    ui.AnyMessage("green", $"Generated: {pwd}");
                    ui.CopyToClipboard(pwd);
                    ui.PressReturnToContinue();
                    break;

                case "Change Master Password":
                    var newPw = ui.PromptSetMasterPassword();
                    masterPassword = newPw;
                    vaultService.SaveVault(masterPassword, payload);
                    ui.AnyMessage("green", "Master password changed and vault re-encrypted.");
                    break;

                case "Export Vault":
                    try
                    {
                        var exportPath = ui.PromptFilePath("Export to file path:");
                        if (System.IO.File.Exists(exportPath) && !ui.ConfirmOverwrite(exportPath)) break;
                        // Save current state first
                        vaultService.SaveVault(masterPassword, payload);
                        System.IO.File.Copy(vaultPath, exportPath, overwrite: true);
                        ui.AnyMessage("green", $"Exported to {exportPath}.");
                    }
                    catch (Exception ex)
                    {
                        ui.AnyMessage("red", $"Export failed: {ex.Message}");
                    }
                    break;

                case "Import Vault":
                    try
                    {
                        var importPath = ui.PromptFilePath("Import from file path:");
                        if (!System.IO.File.Exists(importPath)) { ui.AnyMessage("red", "File not found."); break; }
                        if (System.IO.File.Exists(vaultPath) && !ui.ConfirmOverwrite(vaultPath)) break;
                        System.IO.File.Copy(importPath, vaultPath, overwrite: true);
                        ui.AnyMessage("green", "Imported. Please unlock again.");
                        // force re-unlock cycle
                        while (true)
                        {
                            try
                            {
                                masterPassword = ui.PromptUnlock();
                                payload = vaultService.LoadVault(masterPassword);
                                break;
                            }
                            catch
                            {
                                ui.AnyMessage("red", "Unlock failed. Try again.");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        ui.AnyMessage("red", $"Import failed: {ex.Message}");
                    }
                    break;

                case "Delete Account":
                    // Re-authenticate user
                    int tries = 0;
                    int maxTries = 3;
                    while (true)
                    {
                        tries++;
                        var pw = ui.PromptUnlock();
                        try
                        {
                            // If this succeeds, the password is valid
                            payload = vaultService.LoadVault(pw);
                            break;
                        }
                        catch
                        {
                            if (tries == maxTries)
                            {
                                ui.AnyMessage("red", "You have exhausted your trials. Try later.");
                                Environment.Exit(1);
                            }
                            ui.AnyMessage("red", $"Wrong master password. Try again. You have {maxTries - tries} trials left.");
                        }
                    }

                    // Require exact phrase
                    var phrase = ui.PromptExactInput("Type 'delete my account' to confirm intent:");
                    if (!string.Equals(phrase.Trim(), "delete my account", StringComparison.OrdinalIgnoreCase))
                    {
                        ui.AnyMessage("red", "Phrase mismatch. Aborting.");
                        ui.PressReturnToContinue();
                        break;
                    }

                    // Final confirmation
                    if (!ui.Confirm("This will permanently delete your vault file and cannot be undone. Proceed?"))
                    {
                        ui.AnyMessage("yellow", "Cancelled.");
                        break;
                    }

                    try
                    {
                        if (System.IO.File.Exists(vaultPath)) System.IO.File.Delete(vaultPath);
                        ui.AnyMessage("green", "Account deleted. Exiting.");
                        Environment.Exit(0);
                    }
                    catch (Exception ex)
                    {
                        ui.AnyMessage("red", $"Delete failed: {ex.Message}");
                    }
                    break;

                case "Save & Lock":
                    vaultService.SaveVault(masterPassword, payload);
                    ui.AnyMessage("green", "Saved and locked.");
                    running = false;
                    break;

                case "Quit":
                    running = false;
                    break;
            }
        }
    }
}
