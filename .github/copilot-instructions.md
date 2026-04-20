# Copilot Instructions — Phone Search Bar

## Build

```sh
cd src
dotnet build
```

Targets .NET Framework 4.8. Assembly references point to a local Oxide.Rust build at `../../Oxide.Rust/src/bin/Debug/net48/` (controlled by the `OxideDir` property in `src/PhoneSearchBar.csproj`). There are no tests or linters configured.

## Architecture

This is a single-file Oxide.Rust plugin (`src/PhoneSearchBar.cs`) that adds a CUI search bar overlay to the in-game phone directory. All logic lives in one class — hooks, commands, filtering, and UI — which is standard for Oxide plugins.

**Data flow:** Player opens phone → `OnActiveTelephoneUpdated` hook fires → CUI search bar is drawn as an Overlay → player types and presses Enter → `CuiInputFieldComponent.Command` fires a console command → plugin scans `TelephoneManager.allTelephones` server-side → filtered results sent via `ReceivePhoneDirectory` ClientRPC (same RPC the vanilla game uses).

**Per-player state** is tracked in three dictionaries keyed by `player.userID`:
- `_activePhones` — the PhoneController the player is currently using
- `_activeSearchText` — the current filter string (persists across phone calls)
- `_lastCommandTime` — cooldown tracking

All state is cleaned up on phone close, disconnect, and plugin unload.

## Key Conventions

### Unity Object Safety
Never use `?.` (null-conditional) on Unity objects — they override `== null` to detect destroyed objects. Always use explicit `== null` checks. The `IsPhoneValid()` method is the standard guard; call it before touching any PhoneController reference.

### ProtoBuf Pooling
Always use `Pool.Get<T>()` instead of `new T()` for ProtoBuf types (`PhoneDirectory`, `DirectoryEntry`). Always wrap in `try/finally` with `Dispose()` in the finally block — pool leaks are silent and cumulative.

### CUI Element Names
All CUI element names are prefixed with `PhoneSearchBar.` to avoid collisions with other plugins. `DestroySearchBar()` must be called before `ShowSearchBar()` to prevent duplicate overlays.

### CUI Positioning
Anchor values are normalized screen coordinates (0–1) relative to the `Overlay` parent (full screen). The phone panel's position is not exposed server-side, so alignment is done by eye and may need tuning per resolution. `AnchorMin` = bottom-left corner, `AnchorMax` = top-right corner.

### Decompiling Game Code
Use `ilspycmd` (installed as a dotnet global tool) to inspect game types from `Assembly-CSharp.dll` and `Rust.Data.dll`:
```sh
ilspycmd "../../Oxide.Rust/src/bin/Debug/net48/Assembly-CSharp.dll" -t PhoneController
```

### Hook Discovery
Phone hooks (`OnPhoneDial`, `OnPhoneDialFailed`, `OnActiveTelephoneUpdated`, etc.) are defined by `Interface.CallHook()` calls *inside* `Assembly-CSharp.dll`, not in the Oxide.Rust source. Grep for `CallHook` in decompiled output to find available hooks. Not all RPCs have hooks — notably `Server_RequestPhoneDirectory` has none.

## Docs Reference

- `docs/UX.md` — expected player experience, behavior table, known limitations
- `docs/VALIDATION.md` — safety checklist, edge cases, scaling, post-Rust-update verification steps
- `docs/IDEA.md` — original project concept and Oxide resource links
