# Getting a Windows 11 / “Fluent” look in desktop apps

The shell you see in **Settings** and many inbox apps is **WinUI 3** (Windows App SDK): Fluent Design, Mica, Segoe UI Variable, and control templates that track the OS. There is **no official drop-in** that makes a random HWND look *identical* to Settings without using those stacks or re‑implementing styles.

## Closest match (same tech as inbox apps)

| Approach | What you get | Cost |
|----------|----------------|------|
| **WinUI 3** (Windows App SDK) | Same control set and theming direction as modern Windows UI | **Large migration** from WPF: new project type, XAML differences, packaging/WinAppSDK concerns |
| **Windows UI Library (WinUI 2)** | Fluent controls, but **UWP**-oriented; not the default path for new desktop .exe apps | Usually not worth it vs WinUI 3 for greenfield |

## Stay on WPF (incremental)

| Library | Notes |
|---------|--------|
| **[WPF-UI](https://github.com/lepoco/wpfui)** | Actively maintained Fluent-style WPF controls (navigation, cards, buttons). Good fit if you want **less custom XAML** and closer defaults to Win11. |
| **[ModernWpf](https://github.com/Kinnara/ModernWpf)** | Fluent-ish styling for standard WPF controls; lighter than a full control suite. |
| **MahApps.Metro** | Older “Metro” era; **less** like current Windows 11 Settings. |

KBMixer today uses **hand-tuned WPF** (`Themes/FluentSettingsDark.xaml`) plus **Mica** (`MicaHelper`) to approximate clarity without impersonating Settings copy (e.g. no fake breadcrumb path).

## Recommendation

- **Pixel-parity with Settings:** plan a **WinUI 3** port or accept “close enough” with WPF + a library like **WPF-UI**.
- **Same cleanliness, honest branding:** keep WPF, refine spacing/typography, and avoid UI copy that mimics Microsoft navigation.
