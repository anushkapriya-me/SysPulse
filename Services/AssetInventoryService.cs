using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using SysPulse.Models;

namespace SysPulse.Services;

public class AssetInventoryService : IAssetInventoryService
{
    public Task<List<IdeAsset>> DiscoverInstalledIdesAsync()
    {
        return Task.Run(() =>
        {
            var results = new List<IdeAsset>();

            // 1. VS Code
            results.Add(ScanVsCode());

            // 2. Cursor
            results.Add(ScanCursor());

            // 3. Visual Studio (Community/Professional/Enterprise)
            results.Add(ScanVisualStudio());

            // 4. Zed
            results.Add(ScanZed());

            // 5. JetBrains (IntelliJ, PyCharm, Rider)
            results.AddRange(ScanJetBrains());

            // 6. Sublime Text / Notepad++
            var sublime = ScanSublimeText();
            if (sublime.IsInstalled || sublime.IsRunning) results.Add(sublime);

            var npp = ScanNotepadPlusPlus();
            if (npp.IsInstalled || npp.IsRunning) results.Add(npp);

            return results.OrderByDescending(i => i.IsRunning)
                          .ThenByDescending(i => i.IsInstalled)
                          .ThenByDescending(i => i.ProjectCount)
                          .ToList();
        });
    }

    private static IdeAsset ScanVsCode()
    {
        var asset = new IdeAsset
        {
            Name = "Visual Studio Code",
            Publisher = "Microsoft Corporation",
            IconEmoji = "💻"
        };

        // Potential paths
        string localApp = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string progFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);

        string[] potentialPaths = [
            Path.Combine(localApp, @"Programs\Microsoft VS Code\Code.exe"),
            Path.Combine(progFiles, @"Microsoft VS Code\Code.exe"),
            Path.Combine(progFiles, @"Microsoft VS Code Insiders\Code - Insiders.exe")
        ];

        foreach (var p in potentialPaths)
        {
            if (File.Exists(p))
            {
                asset.IsInstalled = true;
                asset.ExecutablePath = p;
                try
                {
                    var vi = FileVersionInfo.GetVersionInfo(p);
                    asset.Version = vi.ProductVersion ?? vi.FileVersion ?? "1.x";
                }
                catch {}
                break;
            }
        }

        // Running processes & open projects
        var procs = Process.GetProcessesByName("Code");
        if (procs.Length > 0)
        {
            asset.IsRunning = true;
            asset.ProcessId = procs[0].Id;

            foreach (var p in procs)
            {
                try
                {
                    string title = p.MainWindowTitle;
                    if (!string.IsNullOrWhiteSpace(title))
                    {
                        // Window title format: "ProjectName - Visual Studio Code" or "File - ProjectName - Visual Studio Code"
                        var parts = title.Split(['-'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                        if (parts.Length >= 2)
                        {
                            string projName = parts.Length == 2 ? parts[0] : parts[^2];
                            if (!projName.Equals("Visual Studio Code", StringComparison.OrdinalIgnoreCase) &&
                                !asset.OpenProjects.Any(op => op.Name.Equals(projName, StringComparison.OrdinalIgnoreCase)))
                            {
                                asset.OpenProjects.Add(new IdeWorkspaceProject
                                {
                                    Name = projName,
                                    Path = "Active Window Workspace",
                                    IsActiveNow = true,
                                    LastOpenedText = "Open in Editor"
                                });
                            }
                        }
                    }
                }
                catch {}
                finally { p.Dispose(); }
            }
        }

        // Check recent workspaces in %APPDATA%\Code\User\workspaceStorage
        try
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string wsDir = Path.Combine(appData, @"Code\User\workspaceStorage");
            if (Directory.Exists(wsDir))
            {
                foreach (var folder in Directory.GetDirectories(wsDir).Take(8))
                {
                    string wsFile = Path.Combine(folder, "workspace.json");
                    if (File.Exists(wsFile))
                    {
                        string json = File.ReadAllText(wsFile);
                        var folderPath = ExtractJsonPath(json, "folder");
                        if (!string.IsNullOrWhiteSpace(folderPath))
                        {
                            string cleaned = folderPath.Replace("file:///", "").Replace("/", "\\");
                            string name = Path.GetFileName(cleaned.TrimEnd('\\'));
                            if (!string.IsNullOrWhiteSpace(name) && !asset.OpenProjects.Any(op => op.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
                            {
                                asset.OpenProjects.Add(new IdeWorkspaceProject
                                {
                                    Name = name,
                                    Path = cleaned,
                                    IsActiveNow = false,
                                    LastOpenedText = "Recent Project"
                                });
                            }
                        }
                    }
                }
            }
        }
        catch {}

        return asset;
    }

    private static IdeAsset ScanCursor()
    {
        var asset = new IdeAsset
        {
            Name = "Cursor AI",
            Publisher = "Anysphere Inc.",
            IconEmoji = "⚡"
        };

        string localApp = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string[] potentialPaths = [
            Path.Combine(localApp, @"Programs\cursor\Cursor.exe"),
            Path.Combine(localApp, @"cursor\Cursor.exe")
        ];

        foreach (var p in potentialPaths)
        {
            if (File.Exists(p))
            {
                asset.IsInstalled = true;
                asset.ExecutablePath = p;
                try
                {
                    var vi = FileVersionInfo.GetVersionInfo(p);
                    asset.Version = vi.ProductVersion ?? vi.FileVersion ?? "0.x";
                }
                catch {}
                break;
            }
        }

        var procs = Process.GetProcessesByName("Cursor");
        if (procs.Length > 0)
        {
            asset.IsRunning = true;
            asset.ProcessId = procs[0].Id;

            foreach (var p in procs)
            {
                try
                {
                    string title = p.MainWindowTitle;
                    if (!string.IsNullOrWhiteSpace(title))
                    {
                        var parts = title.Split(['-'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                        if (parts.Length >= 2)
                        {
                            string projName = parts.Length == 2 ? parts[0] : parts[^2];
                            if (!projName.Equals("Cursor", StringComparison.OrdinalIgnoreCase) &&
                                !asset.OpenProjects.Any(op => op.Name.Equals(projName, StringComparison.OrdinalIgnoreCase)))
                            {
                                asset.OpenProjects.Add(new IdeWorkspaceProject
                                {
                                    Name = projName,
                                    Path = "Active Window Workspace",
                                    IsActiveNow = true,
                                    LastOpenedText = "Open in Editor"
                                });
                            }
                        }
                    }
                }
                catch {}
                finally { p.Dispose(); }
            }
        }

        try
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string wsDir = Path.Combine(appData, @"Cursor\User\workspaceStorage");
            if (Directory.Exists(wsDir))
            {
                foreach (var folder in Directory.GetDirectories(wsDir).Take(6))
                {
                    string wsFile = Path.Combine(folder, "workspace.json");
                    if (File.Exists(wsFile))
                    {
                        string json = File.ReadAllText(wsFile);
                        var folderPath = ExtractJsonPath(json, "folder");
                        if (!string.IsNullOrWhiteSpace(folderPath))
                        {
                            string cleaned = folderPath.Replace("file:///", "").Replace("/", "\\");
                            string name = Path.GetFileName(cleaned.TrimEnd('\\'));
                            if (!string.IsNullOrWhiteSpace(name) && !asset.OpenProjects.Any(op => op.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
                            {
                                asset.OpenProjects.Add(new IdeWorkspaceProject
                                {
                                    Name = name,
                                    Path = cleaned,
                                    IsActiveNow = false,
                                    LastOpenedText = "Recent Project"
                                });
                            }
                        }
                    }
                }
            }
        }
        catch {}

        return asset;
    }

    private static IdeAsset ScanVisualStudio()
    {
        var asset = new IdeAsset
        {
            Name = "Microsoft Visual Studio",
            Publisher = "Microsoft Corporation",
            IconEmoji = "🟣"
        };

        string progFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        string[] searchRoots = [
            Path.Combine(progFiles, @"Microsoft Visual Studio\2022"),
            Path.Combine(progFiles, @"Microsoft Visual Studio\2019")
        ];

        foreach (var root in searchRoots)
        {
            if (Directory.Exists(root))
            {
                foreach (var ed in Directory.GetDirectories(root)) // Community, Enterprise, Professional
                {
                    string devenv = Path.Combine(ed, @"Common7\IDE\devenv.exe");
                    if (File.Exists(devenv))
                    {
                        asset.IsInstalled = true;
                        asset.ExecutablePath = devenv;
                        asset.Version = Path.GetFileName(root) + " (" + Path.GetFileName(ed) + ")";
                        break;
                    }
                }
            }
            if (asset.IsInstalled) break;
        }

        var procs = Process.GetProcessesByName("devenv");
        if (procs.Length > 0)
        {
            asset.IsRunning = true;
            asset.ProcessId = procs[0].Id;
            foreach (var p in procs)
            {
                try
                {
                    string title = p.MainWindowTitle;
                    if (!string.IsNullOrWhiteSpace(title) && title.Contains("Microsoft Visual Studio"))
                    {
                        var parts = title.Split(['-'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                        if (parts.Length > 0 && !parts[0].Contains("Visual Studio"))
                        {
                            asset.OpenProjects.Add(new IdeWorkspaceProject
                            {
                                Name = parts[0],
                                Path = "Loaded Solution",
                                IsActiveNow = true,
                                LastOpenedText = "Open Solution"
                            });
                        }
                    }
                }
                catch {}
                finally { p.Dispose(); }
            }
        }

        return asset;
    }

    private static IdeAsset ScanZed()
    {
        var asset = new IdeAsset
        {
            Name = "Zed Editor",
            Publisher = "Zed Industries",
            IconEmoji = "⚡"
        };

        string[] potentialPaths = [
            @"D:\Zed\bin\zed.exe",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Programs\Zed\zed.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), @"Zed\zed.exe")
        ];

        foreach (var p in potentialPaths)
        {
            if (File.Exists(p))
            {
                asset.IsInstalled = true;
                asset.ExecutablePath = p;
                asset.Version = "Zed (High Performance)";
                break;
            }
        }

        var procs = Process.GetProcessesByName("zed");
        if (procs.Length > 0)
        {
            asset.IsRunning = true;
            asset.ProcessId = procs[0].Id;
            foreach (var p in procs)
            {
                try
                {
                    if (!string.IsNullOrWhiteSpace(p.MainWindowTitle))
                    {
                        asset.OpenProjects.Add(new IdeWorkspaceProject
                        {
                            Name = p.MainWindowTitle,
                            Path = "Active Workspace",
                            IsActiveNow = true,
                            LastOpenedText = "Open in Zed"
                        });
                    }
                }
                catch {}
                finally { p.Dispose(); }
            }
        }

        return asset;
    }

    private static List<IdeAsset> ScanJetBrains()
    {
        var list = new List<IdeAsset>();

        var jbConfigs = new (string Name, string ProcName, string Emoji, string FolderPattern)[]
        {
            ("IntelliJ IDEA", "idea64", "☕", "IntelliJ IDEA*"),
            ("PyCharm", "pycharm64", "🐍", "PyCharm*"),
            ("JetBrains Rider", "rider64", "🔷", "JetBrains Rider*"),
            ("WebStorm", "webstorm64", "🌐", "WebStorm*")
        };

        string progFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        string jbDir = Path.Combine(progFiles, "JetBrains");

        foreach (var cfg in jbConfigs)
        {
            var asset = new IdeAsset
            {
                Name = cfg.Name,
                Publisher = "JetBrains s.r.o.",
                IconEmoji = cfg.Emoji
            };

            if (Directory.Exists(jbDir))
            {
                var matches = Directory.GetDirectories(jbDir, cfg.FolderPattern);
                if (matches.Length > 0)
                {
                    string exePath = Path.Combine(matches[0], "bin", $"{cfg.ProcName}.exe");
                    if (File.Exists(exePath))
                    {
                        asset.IsInstalled = true;
                        asset.ExecutablePath = exePath;
                        asset.Version = Path.GetFileName(matches[0]);
                    }
                }
            }

            var procs = Process.GetProcessesByName(cfg.ProcName);
            if (procs.Length > 0)
            {
                asset.IsRunning = true;
                asset.ProcessId = procs[0].Id;
                foreach (var p in procs)
                {
                    try
                    {
                        string title = p.MainWindowTitle;
                        if (!string.IsNullOrWhiteSpace(title))
                        {
                            var parts = title.Split(['–', '-'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                            if (parts.Length > 0)
                            {
                                asset.OpenProjects.Add(new IdeWorkspaceProject
                                {
                                    Name = parts[0],
                                    Path = "Active Project",
                                    IsActiveNow = true,
                                    LastOpenedText = "Open in IDE"
                                });
                            }
                        }
                    }
                    catch {}
                    finally { p.Dispose(); }
                }
            }

            if (asset.IsInstalled || asset.IsRunning)
            {
                list.Add(asset);
            }
        }

        return list;
    }

    private static IdeAsset ScanSublimeText()
    {
        var asset = new IdeAsset { Name = "Sublime Text", Publisher = "Sublime HQ", IconEmoji = "📄" };
        string p1 = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), @"Sublime Text\sublime_text.exe");
        if (File.Exists(p1)) { asset.IsInstalled = true; asset.ExecutablePath = p1; asset.Version = "Sublime Text 4"; }
        var procs = Process.GetProcessesByName("sublime_text");
        if (procs.Length > 0) { asset.IsRunning = true; asset.ProcessId = procs[0].Id; foreach (var p in procs) p.Dispose(); }
        return asset;
    }

    private static IdeAsset ScanNotepadPlusPlus()
    {
        var asset = new IdeAsset { Name = "Notepad++", Publisher = "Don Ho", IconEmoji = "📝" };
        string p1 = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), @"Notepad++\notepad++.exe");
        if (File.Exists(p1)) { asset.IsInstalled = true; asset.ExecutablePath = p1; asset.Version = "v8.x"; }
        var procs = Process.GetProcessesByName("notepad++");
        if (procs.Length > 0) { asset.IsRunning = true; asset.ProcessId = procs[0].Id; foreach (var p in procs) p.Dispose(); }
        return asset;
    }

    private static string ExtractJsonPath(string json, string propertyName)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty(propertyName, out var prop))
            {
                return prop.GetString() ?? string.Empty;
            }
        }
        catch {}
        return string.Empty;
    }
}
