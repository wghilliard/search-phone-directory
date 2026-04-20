# Phone Search Bar — Behavioral Tests

Manual test checklist to run before publishing. Requires a Rust server with the plugin loaded and at least a few named phones placed in the world.

## Prerequisites

- [ ] Server running with PhoneSearchBar plugin loaded
- [ ] At least 3 named phones (e.g. "Gas Station", "Gas Outpost", "Shop East")
- [ ] Access to both a landline and a mobile phone

## Core Search

- [ ] **T1: Basic search** — Open phone → Directory tab → type "gas" → Enter. Only phones with "gas" in the name appear.
- [ ] **T2: Case insensitive** — Search "GAS", "Gas", and "gas". All return the same results.
- [ ] **T3: Multi-word search** — Search "gas station". Matches phones containing "gas station" as a substring.
- [ ] **T4: No matches** — Search a string that matches nothing (e.g. "zzzzz"). Directory shows an empty list.
- [ ] **T5: Empty submit** — With a filter active, clear the input and press Enter. Default directory is restored.
- [ ] **T6: Sorted results** — Filtered results appear in alphabetical order by phone name.

## Search Bar UI

- [ ] **T7: Appears on open** — Open a phone. Search bar is visible above the phone panel.
- [ ] **T8: Placeholder text** — Before typing, "Search..." placeholder is visible in the search bar.
- [ ] **T9: Search bar persists after Enter** — Type "gas" → Enter. Search bar remains visible with "gas" in the input.
- [ ] **T10: Clear button** — Click ✕. Filter is cleared, default directory is restored, input is empty, placeholder reappears.
- [ ] **T11: Disappears on close** — Close the phone. Search bar is gone.

## Phone Calls

- [ ] **T12: Hides during call** — With search bar visible, dial a number. Search bar disappears while calling/in call.
- [ ] **T13: Restores after hangup** — After the call ends (hangup, busy, timeout), search bar reappears.
- [ ] **T14: Search text persists across call** — Type "gas" → Enter → dial a number → hang up. Search bar reappears with "gas" in the input and directory shows filtered results.

## Tab Switching

- [ ] **T15: Tab switch resets directory** — Search "gas" → switch to Contacts tab → switch back to Directory tab. Directory shows unfiltered vanilla results. Search text is still in the input.
- [ ] **T16: Re-apply after tab switch** — From T15, press Enter. Filtered results reappear.

## Phone Types

- [ ] **T17: Landline** — Repeat T1 on a wall-mounted telephone. Works identically.
- [ ] **T18: Mobile phone** — Repeat T1 on a mobile phone. Works identically.

## Edge Cases

- [ ] **T19: Walk away from phone** — While searching, walk out of range. Search bar disappears. No errors in server console.
- [ ] **T20: Rapid searches** — Type and Enter quickly several times in a row. No errors, no duplicate UI elements. Some commands may be silently dropped by cooldown.
- [ ] **T21: Long input** — Type more than 30 characters and press Enter. Input is truncated, search works, no errors.
- [ ] **T22: Plugin reload while phone open** — Open phone → `oxide.reload PhoneSearchBar` in server console. Search bar disappears. Close and reopen phone — search bar returns.
- [ ] **T23: Two players searching simultaneously** — Two players each open a phone and search for different terms. Each sees their own filtered results independently.

## Server Console

- [ ] **T24: No errors during normal use** — Run through T1–T18 and check server console. No exceptions or warnings from PhoneSearchBar.
- [ ] **T25: No errors on disconnect** — While the search bar is visible, disconnect. Server console shows no errors.
