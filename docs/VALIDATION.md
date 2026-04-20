# Phone Search Bar — Safety & Validation Checklist

This document captures the safety checks, engine-specific concerns, and edge cases identified during development. Use it as a validation reference when testing in-game or after Rust/Oxide updates.

## Memory Safety

### ProtoBuf Pooling
- [ ] `Pool.Get<PhoneDirectory>()` is always disposed via `try/finally`
- [ ] `directory.entries` (allocated via `Pool.Get<List<DirectoryEntry>>()`) and individual `DirectoryEntry` objects are released automatically by `PhoneDirectory.Dispose()` → `ResetToPool()`
- [ ] No `new PhoneDirectory()` or `new DirectoryEntry()` — always use the pool
- [ ] `directory.Dispose()` is called even if `ClientRPC` throws

### GC Pressure
- [ ] `IndexOf(..., OrdinalIgnoreCase)` is used instead of `ToLower().Contains()` to avoid string allocations per entry
- [ ] No LINQ allocations in hot paths

## Null Reference / Unity Object Safety

### Unity `== null` vs C# `null`
- [ ] All Unity object comparisons use `==` operator (not `?.` null-conditional)
- [ ] `phone == null`, `phone.ParentEntity == null`, `phone.ParentEntity.IsDestroyed` are checked via `IsPhoneValid()` before any use
- [ ] Destroyed Unity objects throw `MissingReferenceException` (not `NullReferenceException`) — our guards prevent reaching that point

### Null Guards in Filter Loop
- [ ] `kvp.Value` (PhoneController) is null-checked before access
- [ ] `GetDirectoryName()` return value is null-checked before `IndexOf`
- [ ] No assumption that `TelephoneManager.allTelephones` values are always valid

## Exploit / Abuse Prevention

### Command Spam
- [ ] 250ms per-player cooldown on `phonesearch.filter` and `phonesearch.clear`
- [ ] Cooldown uses `Time.realtimeSinceStartup` (unaffected by server time scale)

### Input Validation
- [ ] Search text is server-side truncated to 30 characters (matches CUI `CharsLimit`)
- [ ] A malicious client bypassing the CUI and calling the console command directly is handled
- [ ] `arg.Player() == null` check rejects RCON/server console calls
- [ ] Player must have an active phone in `_activePhones` to use either command

### Resource Limits
- [ ] Results capped at 12 entries (DirectoryPageSize) — no unbounded iteration output
- [ ] Dictionary scan of `allTelephones` is O(n) where n = registered phones — acceptable for servers with hundreds/low thousands of phones

## State Consistency

### Player-Phone Tracking (`_activePhones`)
- [ ] Added on `OnActiveTelephoneUpdated(player, controller)` when controller is non-null
- [ ] Removed on phone close (controller is null), player disconnect, and plugin unload
- [ ] Stale references caught by `IsPhoneValid()` on next command — state cleaned + CUI destroyed
- [ ] Dictionary keyed by `player.userID` (ulong) — no risk of player object identity issues

### CUI Lifecycle
- [ ] `DestroySearchBar()` is called before `ShowSearchBar()` to prevent duplicate elements
- [ ] CUI destroyed on: phone close, player disconnect, plugin unload, invalid phone detection
- [ ] `CuiHelper.DestroyUi` is safe to call on already-destroyed or disconnected players

## Edge Cases

### Phone Destroyed While In Use
- **Normal path**: Game calls `ClearCurrentUser()` → `SetActiveTelephone(null)` → our hook fires → clean cleanup
- **Abnormal path** (admin destroy, server kill): Hook may not fire. Stale entry persists in `_activePhones` until:
  - Player submits a search → `IsPhoneValid()` catches it → cleanup + CUI destroy
  - Player disconnects → `OnPlayerDisconnected` cleanup
  - Plugin unloads → `Unload()` cleanup
- **Worst case**: Orphaned search bar CUI with no functional impact (no crash, no leak)

### Phone Destroyed During Active Call
- `OnPhoneDialFailed` now validates the phone via `IsPhoneValid()` before restoring
- If invalid: all player state is cleaned up, search bar is not re-shown
- No crash or stale RPC possible

### Destroyed Phones in allTelephones
- During rapid teardown or admin destroy, `TelephoneManager.allTelephones` may briefly contain destroyed entries
- Filter loop checks `otherPhone.ParentEntity == null || IsDestroyed` before accessing name/number
- Destroyed entries are silently skipped

### More Than 12 Filtered Matches
- Only the first 12 matching phones are returned (matches vanilla page size)
- `atEnd` is set to `true` — vanilla page buttons will not attempt to fetch more
- This is an intentional design choice: search is "first 12 matches only", not paginated

### Plugin Hot Reload
- `Unload()` destroys all CUIs and clears all state
- Players currently using a phone will NOT get the search bar re-added until they close and reopen the phone
- No rehydration on load (acceptable — phone sessions are short-lived)

### Rapid Phone Switching
- Each `OnActiveTelephoneUpdated` call with a new controller overwrites the previous entry in `_activePhones`
- `ShowSearchBar()` calls `DestroySearchBar()` first — no duplicate CUIs
- Cooldown timer is per-player, not per-phone — switching phones doesn't bypass the cooldown

### Multiple Players on Same Phone
- `PhoneController.currentPlayer` can only be one player at a time (game enforces this)
- `IsPhoneValid()` checks `phone.currentPlayer != player` — rejects stale references if another player took over

## Scaling

| Players | Expected Phones | Filter Scan Time | Verdict |
|---------|----------------|------------------|---------|
| 100     | ~200-500       | Microseconds     | No concern |
| 500     | ~1,000-3,000   | Sub-millisecond  | No concern |
| 1,000+  | ~5,000+        | Low milliseconds | Monitor, but cooldown limits frequency |

## Post-Update Validation

After a Rust or Oxide update, verify:
- [ ] `OnActiveTelephoneUpdated` hook still fires (check `BasePlayer.SetActiveTelephone`)
- [ ] `TelephoneManager.allTelephones` is still public static
- [ ] `PhoneController.ParentEntity`, `.currentPlayer`, `.PhoneNumber`, `.GetDirectoryName()` signatures unchanged
- [ ] `ReceivePhoneDirectory` RPC name unchanged (check `PhoneController.Server_RequestPhoneDirectory`)
- [ ] `PhoneDirectory` and `DirectoryEntry` protobuf fields (`phoneName`, `phoneNumber`, `atEnd`) unchanged
- [ ] `Pool.Get<PhoneDirectory>()` and `Dispose()` behavior unchanged
