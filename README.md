# PasswordVault

PasswordVault is a minimal, secure, cross-platform console password manager written in C#. It stores all entries in a single encrypted file (`vault.dat`) protected by a master password. It demonstrates good CLI UX, secure key derivation, authenticated encryption, simple persistence, and easily testable service boundaries.

## Key Features
- Create/unlock a vault with a master password
- Add, get, list, delete entries (name, username, password, notes)
- Strong password generator
- Fuzzy search over entry names
- Clipboard copy of passwords (opt-in)
- Change master password (re-encrypt vault)
- Export/import encrypted vault file
- Autosave on changes

## How It Works (TL;DR)
- On first run you set a master password and an empty encrypted vault file is created (`vault.dat`).
- On subsequent runs you unlock using the master password; the vault is decrypted into memory.
- Entries are managed in-memory and persisted back to the encrypted file.
- Salt/nonce/tag are stored with the file (non-secret); only the key derived from your master password remains secret.

### Current Cryptography
- Key derivation: PBKDF2-HMAC-SHA256 with 210,000 iterations and a 16-byte random salt
- Encryption: AES-GCM (12-byte random nonce, 16-byte authentication tag)
- Encrypted contents: JSON payload of entries (see Data Model). Decrypted only in-memory while running.

## Architecture Overview
```mermaid
flowchart TD
    UI[TerminalUI (Spectre.Console)] -->|prompts & menus| Program
    Program[Program.cs (Orchestrator)] -->|create/load/save| VaultService
    Program -->|generate| PasswordGenerator
    Program -->|search| FuzzySearch
    VaultService -->|encrypt/decrypt| CryptoService
    VaultService -->|serialize/deserialize| Models
    VaultService -->|read/write| FileSystem[(vault.dat)]

    subgraph Models
      VaultEntry
      VaultPayload
      VaultFile (v1)
    end

    subgraph Services
      VaultService
      CryptoService
      PasswordGenerator
      FuzzySearch
    end
```

## Data Model
- VaultEntry: `Name`, `Username`, `Password`, `Notes`, `CreatedAt`, `UpdatedAt`
- VaultPayload: `Entries: List<VaultEntry>` (the decrypted in-memory model)
- VaultFile (v1 on disk): `Salt`, `Iterations`, `Nonce`, `Ciphertext`, `Tag` (JSON envelope; `Ciphertext` holds the encrypted payload JSON)

## CLI Flow
1. Welcome screen and detection of an existing vault.
2. Create or Unlock:
   - Create Vault → set a master password → write an empty encrypted vault.
   - Unlock Vault → prompt for master password → decrypt the vault into memory.
3. Main menu:
   - Add Entry → prompt fields; generates a password if left blank. Autosaves.
   - Get Entry → display entry; optional clipboard copy.
   - List Entries → show names only.
   - Search Entries (fuzzy) → Levenshtein/subsequence scoring over names.
   - Delete Entry → confirm then remove. Autosaves.
   - Generate Password → quick secure password generation.
   - Change Master Password → re-encrypt vault with a new master password.
   - Export/Import Vault → copy the encrypted file safely.
   - Save & Lock / Quit → persist and exit.

## Fuzzy Search
- Simple in-memory search using Levenshtein distance with subsequence and substring bonuses.
- Results displayed with normalized score [0..1] and can be opened directly from results.

## Clipboard Handling
- Copy-to-clipboard via TextCopy with explicit user confirmation from the entry view.
- Note: Clipboard auto-clear timers are planned in the security upgrade (see Roadmap).

## Autosave Behavior
- Add/Update/Delete actions persist immediately to `vault.dat` to prevent data loss between sessions.

## Export / Import
- Export: saves the current state and copies `vault.dat` to a user-specified path.
- Import: replaces `vault.dat` with a selected file and prompts to unlock again.

## Technologies Used
- .NET 9 (C#)
- Spectre.Console for rich TUI prompts and tables
- Newtonsoft.Json for JSON serialization
- System.Security.Cryptography (PBKDF2, AES-GCM)
- TextCopy for clipboard support

## Project Structure
```
PasswordVault/
├── Program.cs                 # App entry & menu orchestration
├── PasswordVault.csproj
├── Models/
│   ├── User.cs                # (placeholder for auth requests)
│   ├── VaultEntry.cs          # Entry model
│   └── VaultFile.cs           # VaultFile (v1) + VaultPayload
├── Services/
│   ├── CryptoService.cs       # PBKDF2 + AES-GCM helpers
│   ├── PasswordGenerator.cs   # Secure random passwords
│   ├── VaultService.cs        # Create/Load/Save vault file
│   └── FuzzySearch.cs         # Name search
└── ui/
    └── TerminalUI.cs          # Spectre.Console UI
```

## Building & Running
- Build
```bash path=null start=null
dotnet build
```
- Run
```bash path=null start=null
dotnet run
```

The application creates/uses `vault.dat` in the working directory.

## Roadmap (Security Upgrades)
We plan a v2 vault format and enhancements to harden against offline attacks and tampering:
- Argon2id KDF option and policy-based upgrades
- AEAD AAD binding of header metadata
- Device-bound pepper via OS keystore (optional)
- Atomic writes + strict file permissions
- Clipboard auto-clear timers, idle autolock, strength meter

Details: see PROJECTV2.md. Implementation will be delivered in small PRs.
# password-vault
