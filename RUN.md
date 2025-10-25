# RUN.md – Internals and Pseudocode

This document explains how the system works at a high level and the pseudocode for each feature.

## Startup Flow

```
Main:
  ui.ShowWelcome()
  vaultService = new VaultService("vault.dat")
  if !vaultService.VaultExists():
      master = ui.PromptSetMasterPassword()
      vaultService.CreateNewVault(master)
      payload = new VaultPayload()
  else:
      loop until unlocked:
         master = ui.PromptUnlock()
         try payload = vaultService.LoadVault(master); break
         catch -> ui.AnyMessage("red", "Unlock failed")

  loop running:
    choice = ui.ShowMainMenu()
    switch choice:
      Add Entry -> addEntry()
      Get Entry -> getEntry()
      List Entries -> listEntries()
      Search Entries (fuzzy) -> searchEntries()
      Delete Entry -> deleteEntry()
      Generate Password -> genPassword()
      Change Master Password -> changeMaster()
      Export Vault -> exportVault()
      Import Vault -> importVault()
      Save & Lock -> saveAndExit()
      Quit -> exit
```

## VaultService

```
CreateNewVault(master):
  payload = new VaultPayload()
  SaveVault(master, payload)

LoadVault(master):
  raw = read file bytes("vault.dat")
  vf = deserialize JSON to VaultFile
  key = PBKDF2(master, vf.Salt, vf.Iterations)
  plaintext = AES-GCM-Decrypt(key, vf.Nonce, vf.Ciphertext, vf.Tag)
  payload = deserialize JSON to VaultPayload
  return payload

SaveVault(master, payload):
  json = JSON(payload)
  plaintext = UTF8(json)
  salt = RNG(16)
  iter = 210000
  key = PBKDF2(master, salt, iter)
  (cipher, nonce, tag) = AES-GCM-Encrypt(key, plaintext)
  vf = VaultFile{ Salt=salt, Iterations=iter, Nonce=nonce, Ciphertext=cipher, Tag=tag }
  write file bytes("vault.dat", UTF8(JSON(vf)))
```

## CryptoService

```
DeriveKey(password, salt, iter): PBKDF2-HMAC-SHA256 -> 32 bytes key
Encrypt(key, plaintext):
  nonce = RNG(12)
  tagSize = 16
  AesGcm(key, tagSize).Encrypt(nonce, plaintext) -> (cipher, tag)
  return (cipher, nonce, tag)
Decrypt(key, nonce, cipher, tag):
  AesGcm(key, tagSize).Decrypt(nonce, cipher, tag) -> plaintext
```

## CRUD Handlers

```
addEntry():
  entry = ui.PromptNewEntry()
  existing = payload.Entries.FirstOrDefault(Name equals entry.Name, case-insensitive)
  if existing != null: update fields + UpdatedAt
  else: payload.Entries.Add(entry)

getEntry():
  name = ui.PromptEntryName("Entry name to view:")
  found = find by name
  if found: ui.ShowEntry(found, offerCopy=true)
  else: ui.AnyMessage("red", "Not found")

listEntries():
  ui.ShowNamesList(Entries.Select(e => e.Name).OrderBy(name))

deleteEntry():
  name = ui.PromptEntryName("Entry name to delete:")
  found = find by name
  if found && ui.ConfirmDelete(found.Name): remove and notify
```

## Generate Password

```
genPassword():
  len = ui.PromptPasswordLength()  // default 16
  pwd = PasswordGenerator.Generate(len)
  ui.AnyMessage("green", $"Generated: {pwd}")
```

## Change Master Password

```
changeMaster():
  newMaster = ui.PromptSetMasterPassword()
  master = newMaster
  vaultService.SaveVault(master, payload)  // re-encrypts with new derivation params
```

## Export / Import

```
exportVault():
  path = ui.PromptFilePath("Export to file path:")
  vaultService.SaveVault(master, payload)  // ensure latest state
  copy("vault.dat", path, overwrite if confirmed)

importVault():
  path = ui.PromptFilePath("Import from file path:")
  copy(path, "vault.dat", overwrite if confirmed)
  // Force unlock again to load imported state
  loop until unlocked:
    master = ui.PromptUnlock()
    payload = vaultService.LoadVault(master)
```

## Fuzzy Search

```
searchEntries():
  q = ui.PromptSearchQuery()
  corpus = Entries.Select(Name)
  results = FuzzySearch.Search(q, corpus, maxResults=10)  // returns (name, score [0..1]) sorted desc
  ui.ShowSearchResults(results)
  if results not empty:
     pick = ui.PromptEntryName("View which result (enter name):")
     chosen = find by name
     if chosen: ui.ShowEntry(chosen, offerCopy=true)
```

### Fuzzy scoring (FuzzySearch)
- Score = 1 - Levenshtein(query, name) / max(len(query), len(name))
- +0.1 if query is a subsequence of name
- +0.1 if name contains query as substring
- Clamped to [0, 1], sorted descending
