# Design QA

- source visual truth paths: `image-1.jpg` through `image-5.png` under `C:\Users\浪白\.codex\attachments\4d93550e-ae38-4e88-97c6-62bb213d7c0e\`
- installed implementation: `LangBai.BatchRename` version `1.1.0.0` (x64)
- main screenshot: `artifacts\ui-v1.1-installed-main.png`
- final-preview screenshot: `artifacts\ui-v1.1-installed-final-preview.png`
- minimum-size screenshot: `artifacts\ui-v1.1-minimum.png` at 980 x 680
- combined main comparison: `artifacts\design-qa-v1.1-installed-main.png`
- combined final-preview comparison: `artifacts\design-qa-v1.1-installed-final-preview.png`

## Findings

No actionable P0, P1, or P2 visual findings remain in the checked states.

- Saved schemes: the empty control states that no scheme exists, while Save and Delete remain adjacent and subordinate to the rule field. Save status explains local-only persistence and overwrite behavior.
- Sorting: name/date/size/type and reverse controls are grouped as one setting. The helper line changes with the selected strategy and documents folder-size behavior.
- Original/new distinction: original names are muted and read-only, proposed names use blue semibold text, and a directional arrow connects each pair. Status remains icon + text + color.
- Final preview: the separate confirmation window clearly states that disk changes happen only after confirmation, preserves visible Back and Copy actions, and keeps one blue primary action.
- Complex-field guidance: start number, zero-padding width, and time format have persistent examples rather than tooltip-only help.
- Minimum size: long names use ellipsis and tooltips; the preview status and primary action remain visible at 980 x 680.
- Iconography: all action and file-type glyphs remain in the Segoe Fluent family and use the established blue identity.

## Interaction Evidence

- `scripts\Test-UiInteractions.ps1` sends a real mouse-wheel event to the focused time-format combo box and verifies that its value does not change.
- The same test opens the real right-click edit menu, finds Cut, Copy, Paste, and Select All, invokes Paste, and verifies the textbox value.
- `scripts\Test-ExplorerCommand.ps1` invokes the packaged `IExplorerCommand` with two files and one folder and verifies the selection summary received by the installed app.
- `scripts\Test-FinalPreviewFlow.ps1` drives the installed final-preview window, confirms the action, verifies two files and one folder were renamed, and verifies that undo history was persisted.
- Core tests cover local preset save/overwrite/delete, all four sort strategies, reverse ordering, preview planning, collisions, locked-item rollback, long-name rejection, locked-history recovery, execution, persistence, and undo.
- An independent deep-user pass rechecked the Windows 11 first-level menu for file-only, folder-only, and mixed selections; it also built a 1,000-item preview in 1.33 seconds and confirmed the UI remained ready.

## Comparison History

1. Earlier P2: long names hard-clipped at minimum size. Fixed with ellipsis and full-name tooltips.
2. Earlier P2: preview gridlines rendered nearly black. Fixed with explicit `#E8EDF3` row borders and no native gridlines.
3. Earlier P2: type glyphs and status badges were vertically offset. Fixed with centered row content and matched padding.
4. Earlier P2: original and proposed names had insufficient visual distinction. Fixed with read-only gray, editable blue, explicit headers, and arrows.
5. Earlier P2: confirmation used a generic message box. Fixed with a dedicated, inspectable final-preview table based on the MT reference.
6. Earlier P2: complex numeric/time settings depended on terse labels and tooltips. Fixed with persistent dynamic examples.

## Residual Test Gaps

- Dark mode is not offered in version 1.1, so no dark-theme claim is made.
- Windows text scaling above the current system setting was not separately captured; the rule rail scrolls and the window enforces minimum dimensions.
- Screenshot review cannot establish full screen-reader compatibility; UI Automation names and keyboard paths were checked, but Narrator was not run end to end.
- Non-NTFS/network shares, a standard non-administrator account, touch input, and Windows high-contrast mode were not separately tested on real target systems.

## Implementation Checklist

- [x] Reference and installed implementation viewed in combined comparison images.
- [x] Default, populated-valid, minimum-size, final-preview, and interaction states checked.
- [x] Fonts, spacing, colors, icons, copy, focusability, truncation, and action hierarchy reviewed.
- [x] Build completed with zero warnings and zero errors.
- [x] Unit/integration tests passed 12/12.
- [x] Installed package, Explorer selection transfer, mouse-wheel behavior, right-click paste, and final confirmation flow verified.

final result: passed
