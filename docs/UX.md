# Phone Search Bar — Expected UX

## Overview
When a player picks up or opens a phone (landline or mobile), a search bar appears above the phone panel. Typing a name and pressing Enter filters the directory list, making it easy to find a specific phone without scrolling through pages.

## Player Flow

1. **Player uses a phone** — the standard Rust phone UI opens (dial pad, directory, etc.)
2. **Search bar appears** — an olive-brown bar with "Search..." placeholder text is overlaid just above the phone panel tabs
3. **Player types a name and presses Enter** — e.g. `outpost` or `Shop East`
4. **Directory updates** — only phones whose names contain the search text (case-insensitive) are shown, replacing the default paginated list
5. **Player clicks ✕** — the filter is cleared, the default directory is restored, and the input field resets
6. **Player closes the phone** — the search bar disappears and all filter state is reset

## Behavior Details

| Action | Result |
|---|---|
| Open phone | Search bar appears with "Search..." placeholder |
| Type `shop` + Enter | Directory shows only phones with "shop" in the name |
| Type `Shop East` + Enter | Matches phones containing "shop east" (case-insensitive) |
| Submit empty input | Default directory (page 1) is restored |
| Click ✕ button | Clears filter, restores default directory, resets input |
| Close phone / walk away | Search bar is removed, filter state is cleared |
| Disconnect | All state is cleaned up server-side |
| Spam commands | 250ms per-player cooldown silently drops excess requests |

## Limitations

- **Results are capped at 12** — matches the vanilla directory page size. If more than 12 phones match, only the first 12 are shown.
- **No live-as-you-type filtering** — the filter is applied when the player presses Enter, not on every keystroke. This is a constraint of how Rust's `CuiInputFieldComponent` works.
- **Search text max 30 characters** — matches the max phone name length in Rust. Longer input is truncated server-side.
- **Placeholder text doesn't auto-hide** — the "Search..." label sits behind the input. It's covered by typed text but may faintly show through depending on text length.
- **Tab switching resets the directory** — if you search on the Directory tab, switch to another tab (e.g. Contacts), then switch back, the directory reverts to the unfiltered vanilla list. Your search text is preserved in the input — press Enter again to re-apply the filter. This happens because tab switching triggers a vanilla directory request that the plugin cannot intercept (no Oxide hook exists for `Server_RequestPhoneDirectory`).
- **Positioning may need tuning** — the search bar is placed just above the phone panel tab bar. The exact position may need adjustment depending on screen resolution.
- **Pagination while filtering** — the vanilla page navigation buttons won't paginate through filtered results. The filtered view always shows the first page of matches.
