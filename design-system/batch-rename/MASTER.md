# Batch Rename Design System

This file is the visual source of truth for the Windows desktop utility.

## Product fit

- Product category: File Manager and Productivity Tool.
- Usage context: short, high-attention desktop operation with potentially destructive filesystem effects.
- Style: Windows-native flat minimalism, information-dense but calm.
- Primary outcome: understand the rule, inspect every proposed name, then execute with confidence.

The generator's Newsletter pattern, Exaggerated Minimalism, oversized display type, Lora/Raleway pairing, and web scroll animation were rejected because they do not fit a native file utility.

## Color tokens

| Role | Value | Use |
|---|---:|---|
| Primary | `#2563EB` | Main action, focus, selected state |
| Primary hover | `#1D4ED8` | Hover and pressed action |
| Background | `#F8FAFC` | App canvas |
| Surface | `#FFFFFF` | Header, panels, data area |
| Subtle surface | `#F1F5F9` | Table headers, helper zones |
| Foreground | `#0F172A` | Main text |
| Muted text | `#64748B` | Supporting copy |
| Border | `#E2E8F0` | Dividers and card boundaries |
| Success | `#067647` | Valid changed names |
| Success surface | `#ECFDF3` | Success status badge |
| Error | `#B42318` | Invalid name and collision |
| Error surface | `#FEF3F2` | Error status badge |
| Warning | `#B54708` | Review-required state |

Do not rely on color alone; every status includes an icon and text.

## Typography

- UI: `Segoe UI Variable Text`, `Microsoft YaHei UI`, `Segoe UI`.
- Display headings: `Segoe UI Variable Display` with Semibold only.
- Tokens and filename rules: `Cascadia Mono`, then `Consolas`.
- Screen title: 26 px; section title: 18 px; body: 13-14 px; helper text: 12-13 px.
- Avoid web fonts: the app must feel native and work offline on Windows 10/11.

## Layout and density

- Spacing scale: 4, 8, 12, 16, 20, 24, 32.
- Rule rail: 360-380 px. Preview fills remaining width.
- Panel radius: 10 px. Input/button radius: 6 px. Status pill radius: 12 px.
- Data rows: 42-46 px. Header: 38-42 px. Keep filenames scannable.
- Use 1 px neutral borders. Shadows are optional and very subtle; never stack heavy card shadows.

## Interaction rules

- The default `{P}{S}` rule performs no changes.
- Rule edits update preview immediately; manual target-name edits are explicitly labelled.
- Invalid input appears inline or in the persistent validation bar before submission.
- Execute stays disabled while any item is invalid or unchanged.
- Keyboard access: Ctrl+O add files, Ctrl+Shift+O add folders, F5 refresh, Ctrl+H history, Ctrl+Enter execute.
- All actions have visible focus and descriptive tooltips.
- The filesystem operation always requires a confirmation and writes an undo record.

## Iconography and motion

- Use the Windows Segoe Fluent Icons set consistently; never use emoji.
- Use functional file/folder colors sparingly: folder blue, file slate.
- Native hover/press feedback only. No decorative reveal, bounce, parallax, or continuous animation.

## Pre-delivery checklist

- Contrast meets 4.5:1 for normal text.
- Status is communicated by icon, label, and color.
- Tab order reaches all fields, the editable preview, and all actions.
- Focus rings remain visible.
- No primary action is hidden by scrolling at the minimum window size.
- Long filenames truncate or scroll without pushing status/actions off screen.
- Empty, valid, invalid, manually edited, success, history, and undo-conflict states are verified.
