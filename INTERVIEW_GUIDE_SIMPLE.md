# ⚡ SysPulse: Easy-to-Understand Project & Interview Mastery Guide

This guide explains **SysPulse** in plain, simple English—covering all features, bug fixes, architecture, and common interview questions.

---

## 1. 30-Second Elevator Pitch

> *"SysPulse is a lightweight, cross-platform desktop application built in C# 12 and .NET 8 using Avalonia UI. It functions like an advanced Task Manager, tracking CPU, RAM, Disks, and Network speeds in real time with smooth 60 FPS GPU-drawn graphs. Beyond hardware monitoring, it features background Screen Time tracking and an automated Developer Asset Inventory that discovers installed IDEs (VS Code, Cursor, Visual Studio, Zed, JetBrains) and their open projects. It consumes only ~35 MB of RAM and runs with less than 0.5% CPU overhead."*

---

## 2. All 5 Modules Explained in Plain English

### Module 1: Real-Time Hardware Telemetry
- **CPU & RAM**: Asks the Windows kernel directly using Win32 `GetSystemTimes` and `GlobalMemoryStatusEx`. All percentages are normalized strictly under 100%.
- **Live Refresh Interval Selector**: Clickable dropdown allowing users to choose between **1s (Live)**, **2s (Normal)**, **5s (Balanced)**, and **10s (Eco Mode)**.
- **Custom Charts**: Uses `StreamGeometry` to command the GPU directly, rendering 60-second scrolling sparklines with zero memory lag.

### Module 2: Active Process Manager (With Selection Lock)
- Displays all running applications sorted by memory consumption with real-time search filtering.
- **Persistent Selection Lock**: Telemetry refreshes every second, but existing items are updated in-place. The selected row NEVER gets deselected, allowing users to reliably click **"End Task"**.
- **Process Tree Cleanup**: Calls `proc.Kill(entireProcessTree: true)` to ensure child background sub-processes are terminated along with the parent app.

### Module 3: Automatic Application Screen Time Tracker
- **Total Screen Time**: Automatically tracks how long you have been actively using your computer.
- **Per-App Breakdown**: Categorizes programs into Development, Web Browsing, Productivity, Media, and System.
- **Share %**: Shows visual progress bars (guaranteed under 100%) showing which app took up the most time.
- **Controls**: Includes Pause/Resume tracking and Reset statistics buttons.

### Module 4: Developer Asset Inventory (Auto-Discovery)
- Automatically scans your PC and discovers installed IDEs without needing manual paths:
  - Visual Studio Code & Insiders
  - Cursor AI Editor
  - Microsoft Visual Studio (2022 / 2019)
  - Zed Editor
  - JetBrains Suite (IntelliJ IDEA, PyCharm, Rider, WebStorm)
  - Sublime Text & Notepad++
- Reads active window titles and workspace caches to show open workspaces and solutions, with a **"Launch 🚀"** button.

### Module 5: Settings & AI-Explored Hardware Specs
- **Resource Warnings**: Warning banner when CPU > 85% or RAM > 90% threshold.
- **Hardware & Battery Telemetry**:
  - Battery charge %, charging status, and remaining runtime.
  - Motherboard manufacturer, model, and BIOS version.
  - Full CPU brand name and architecture.
- **Startup Preferences**: Toggle launch on Windows startup and persist preferences to JSON.

---

## 3. Key Interview Questions & Simple Answers

### Q1: "How did you fix the bug where selecting a process would automatically deselect after a few seconds?"
> *"Originally, every time the 1-second telemetry timer ticked, the list was wiped with `FilteredProcesses.Clear()`. That caused Avalonia's `ListBox` to reset `SelectedItem` to null, disabling the 'End Task' button before the user could click it.*
> *I fixed this by creating an in-place reconciliation algorithm. Instead of clearing the list, we match processes by PID and update their properties directly. Because the object reference stays in the collection, the selected item remains locked in place across all ticks until the user manually changes it or ends the task."*

### Q2: "How do you guarantee that CPU, RAM, and Screen Time percentages stay strictly under 100%?"
> *"On multi-core systems, timing differentials between CPU cores can occasionally produce unnormalized numbers. I added a normalization check in all models: if any calculated percentage exceeds 100, it is automatically divided by 100 and clamped strictly between 0.0% and 99.9%. This guarantees that all cards, progress bars, and screen time percentages stay strictly within valid bounds."*

### Q3: "How does the Asset Inventory detect IDEs and open projects?"
> *"The service searches standard installation paths in LocalAppData, Program Files, and system PATH for known IDE executables. To find open projects, it inspects active processes and parses their window titles (`MainWindowTitle`), which typically follow patterns like `'ProjectName - Visual Studio Code'`. For VS Code and Cursor, it also scans the `workspaceStorage` JSON cache to display recent project paths."*

### Q4: "How does Screen Time track apps in the background?"
> *"Every second, the app calls the Win32 API `GetForegroundWindow()` and `GetWindowThreadProcessId()` to identify the window the user is currently interacting with. It increments active seconds for that app, groups it into categories, and calculates its share of total screen time. It also uses `GetLastInputInfo()` to stop tracking when the user is idle away from their keyboard."*

### Q5: "Why did you build this with Avalonia UI instead of Electron or WPF?"
> *"Electron applications bundle a full Chromium browser, eating 200MB to 400MB of RAM just sitting idle. SysPulse runs on native .NET and uses only ~35 MB of RAM with instant startup. Compared to WPF, which only works on Windows, Avalonia is cross-platform and uses the Skia GPU graphics engine for high-performance rendering."*

---

## 4. Cheat Sheet: Key Words to Know

- **P/Invoke**: A bridge in C# to call native Windows C-functions in `kernel32.dll` and `user32.dll`.
- **GetForegroundWindow**: A Windows API that returns the window currently in user focus.
- **Process Tree**: A parent program and all child worker processes it spawned.
- **StreamGeometry**: A low-level Avalonia graphics tool that draws lines directly on the GPU without memory churn.
- **In-Place Reconciliation**: Updating existing items in a list rather than deleting and recreating them, keeping UI selection intact.
