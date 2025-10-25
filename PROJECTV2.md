# PROJECTV2 – Security Upgrade Plan

This document specifies the security hardening for the console password vault and a migration path from v1 to v2 with no data loss.

## 1) Goals and Threat Model
- Protect against offline cracking if vault.dat is exfiltrated.
- Prevent undetected header/ciphertext tampering.
- Minimize plaintext exposure in memory/clipboard/logs.
- Maintain cross-platform usability with optional device-bound protection.

Out of scope: Compromise of a running session or keylogger-level malware.

## 2) Vault file format v2 (JSON envelope)
```
{
  "FormatVersion": 2,
  "CipherSpec": { "alg": "aes-256-gcm", "tagBytes": 16 },
  "Kdf": {
    "type": "argon2id", // or "pbkdf2"
    "params": {
      // argon2id example
      "memoryMB": 128,
      "iterations": 3,
      "parallelism": 2
      // pbkdf2 example: { "iterations": 600000, "hash": "sha256" }
    }
  },
  "Salt": "base64-16+ bytes",
  "Nonce": "base64-12 bytes",
  "Ciphertext": "base64",
  "Tag": "base64-16 bytes",
  "CreatedAt": "2025-01-01T00:00:00Z",
  "HeaderAADHash": "base64-32 bytes sha256(header-without-ciphertext-tag-aadhash)"
}
```
- AAD binding: The canonical header JSON (everything except Ciphertext/Tag/HeaderAADHash) is the AEAD AAD during encryption/decryption.
- Back-compat: v1 loader remains; v1 → v2 auto-migration after unlock.

## 3) Cryptography
- AES-GCM (256-bit key, 12-byte nonce, 16-byte tag).
- KDF choices (policy-driven):
  - Preferred: Argon2id (memory-hard). Defaults: memory 128 MB, iterations 3, parallelism min(cores, 4).
  - Legacy: PBKDF2-HMAC-SHA256. Default: ≥600k iterations.
- Fresh random salt per save; fresh random nonce per encryption. Never reuse nonces.
- Key combining with optional pepper (see §6): finalKey = HKDF-Expand(HKDF-Extract(pepper, derivedKey), info="vault-enc-key", L=32).

## 4) Migration & KDF upgrade policy
- On unlock, compare stored KDF (type/params) with current policy.
- If weaker, prompt user to upgrade; re-encrypt payload under new KDF params, new salt/nonce, write v2.
- Keep a .bak of the previous file with strict perms.

## 5) Master password UX
- Enforce passphrase strength: zxcvbn score ≥ 3, length ≥ 12–16; show live meter in TerminalUI.
- Exponential backoff on failed unlocks (0.5s → 1s → 2s … cap 10s).
- Idle autolock: after N minutes idle, save + wipe payload and return to unlock screen.

## 6) Optional device-bound pepper
- Generate a 32-byte random pepper on first run.
- Store pepper in OS keystore:
  - macOS: Keychain
  - Windows: DPAPI ProtectedData
  - Linux: libsecret/gnome-keyring (fallback: user-env-provided secret)
- Portable mode toggle (no pepper) for moving vaults across machines.

## 7) Clipboard hygiene
- After copy, start a countdown (20–60s) then overwrite clipboard with benign text and clear.
- Show timer and allow configurable duration; handle failures gracefully.

## 8) Memory hygiene
- Minimize lifetime of plaintext: decrypt only on unlock; avoid long-lived strings of secrets.
- Prefer byte[] for sensitive values; Array.Clear after use.
- On lock/exit: null out payload, clear buffers, best-effort GC.Collect(); document limitations.

## 9) File-system protections
- Strict permissions on vault.dat and backups:
  - Unix/macOS: chmod 600
  - Windows: owner-only ACL (remove inherited access)
- Atomic writes: write to temp, fsync/Flush(true), set perms, atomic replace.
- Warn if stored in commonly backed-up/synced dirs; provide .gitignore hint.

## 10) Defense-in-depth
- Logging: never print secrets; generic decrypt errors.
- Tamper detection: AAD binding covers header; include payload checksum (SHA-256) inside the encrypted JSON for diagnostics.
- Export/import: apply same ACL hardening; warn about portability when pepper is enabled; “convert to portable” re-encrypts without pepper.

## 11) Policy management
- Central SecurityPolicy with:
  - KDF selection and parameters (argon2id/pbkdf2)
  - Minimum password strength rules
  - Idle/clipboard timeouts
- UI: “About/Security” to display effective policy; on load, auto-migrate if needed.

## 12) Testing & validation
- Crypto round-trip tests (including AAD mismatch and nonce tampering should fail).
- Migration tests: v1 (PBKDF2) → v2 (Argon2id) preserves entries.
- Permission tests (best-effort per OS).
- Fuzz header fields: invalid combos rejected.

## 13) Operational UX
- Add “Lock now” menu item.
- Indicate KDF and parameters in an info screen; badge if upgrade recommended.
- Performance calibration on first run to pick Argon2 memory/iterations within acceptable latency.

## 14) Implementation checklist (code touchpoints)
- Models
  - New: VaultFileV2 schema (header, Kdf, CipherSpec).
  - Update: VaultPayload to include optional payload checksum.
- Services
  - CryptoService: add Encrypt/Decrypt with AAD parameter; HKDF helper; Argon2id support; PBKDF2 and Argon2 strategy.
  - VaultService: detect v1 vs v2, parse header, derive key via Kdf strategy, pass AAD, atomic save with perms, migration flow, backups.
  - PasswordStrength (zxcvbn wrapper), PepperService (Keychain/DPAPI/libsecret), FilePermissions helper, ClipboardService with timer.
- UI (TerminalUI)
  - Master password setup: strength meter; block weak.
  - Menu: Lock now; Security/About; settings for timeouts; migrate prompt.
  - Unlock backoff and idle autolock feedback.
- Config
  - SecurityPolicy defaults; user overrides in a non-secret config file.
- Build
  - Add packages: Argon2 (e.g., Konscious.Security.Cryptography), Zxcvbn.Core, optionally a HKDF implementation (or implement via HMACSHA256), platform-specific secret stores.

## 15) Migration plan
1. Detect v1 on load; unlock using existing PBKDF2 path.
2. Build v2 header with current policy (argon2id preferred), create AAD from canonical header.
3. Re-encrypt payload, write v2 atomically; chmod/ACL; create .bak.
4. Verify readable by new loader; delete .bak based on user confirmation.

## 16) Rollout & Backward Compatibility
- v2 loader supports reading v1 (for migration) and v2.
- v1 code cannot read v2; warn users to upgrade the app alongside the vault.

## 17) Open Questions / Options
- Exact canonicalization for AAD: fixed property ordering + UTF-8 without BOM.
- Pepper portability UX: export-without-pepper flow for moving vaults.
- Argon2 calibration target latency (e.g., 250–500ms) and memory caps per device.

## 18) Acceptance Criteria
- Stolen vault.dat alone is insufficient to recover keys without master password and (if enabled) device-bound pepper.
- Tampering with header or ciphertext reliably fails decryption.
- Weak master passwords are rejected; users can still generate strong passphrases easily.
- Autosave remains atomic and private; clipboard clears automatically.
