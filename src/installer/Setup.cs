using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace XtuCnSetup
{
    static class Program
    {
        [STAThread]
        static int Main(string[] args)
        {
            if (args.Length > 0 && (args[0] == "/install" || args[0] == "/restore"))
            {
                var f = new MainForm();
                f.Headless = true;
                f.Detect(false);
                return f.RunHeadless(args[0] == "/install") ? 0 : 1;
            }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
            return 0;
        }
    }

    class MainForm : Form
    {
        const string TargetVersion = "7.14.2.135";
        const string XtuProductName = "Intel(R) Extreme Tuning Utility";

        static readonly string[] Payload =
        {
            "PerfTune.exe", "XtuCommon.dll", "IntelOverclockingSDK.dll",
            "XtuCoreClient.dll", "PerfTuneCommon.dll"
        };
        static readonly string[] ResourceNames =
        {
            "PerfTune", "XtuCommon", "IntelOverclockingSDK", "XtuCoreClient", "PerfTuneCommon"
        };
        static readonly string[] OrigResourceNames =
        {
            "Orig_PerfTune", "Orig_XtuCommon", "Orig_IntelOverclockingSDK", "Orig_XtuCoreClient", "Orig_PerfTuneCommon"
        };

        TextBox log;
        Label status;
        ProgressBar bar;
        Button btnInstall, btnRestore, btnOpen, btnClose;
        string installDir = null, installedVersion = null;
        volatile bool busy = false;
        public bool Headless = false;
        readonly StringBuilder headlessLog = new StringBuilder();

        public bool RunHeadless(bool install)
        {
            try
            {
                if (install) InstallWorker(); else RestoreCore();
                File.WriteAllText(LogPath(), headlessLog.ToString());
                return true;
            }
            catch (Exception ex)
            {
                headlessLog.AppendLine("ERROR " + ex.Message);
                try { File.WriteAllText(LogPath(), headlessLog.ToString()); } catch { }
                return false;
            }
        }

        static string LogPath()
        {
            string d = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "XTU-CN");
            Directory.CreateDirectory(d);
            return Path.Combine(d, "setup.log");
        }

        public MainForm()
        {
            Text = "Intel XTU 中文版 安装程序";
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            SuspendLayout();
            Font = new Font("Microsoft YaHei UI", 9F);
            ClientSize = new Size(660, 500);
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            BackColor = Color.White;

            var header = new Panel { Dock = DockStyle.Top, Height = 74, BackColor = Color.FromArgb(0, 104, 181) };
            var title = new Label
            {
                Text = "Intel® Extreme Tuning Utility 中文版",
                ForeColor = Color.White,
                Font = new Font("Microsoft YaHei UI", 15F, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(18, 12)
            };
            var sub = new Label
            {
                Text = "汉化补丁安装程序 · 适用于 XTU " + TargetVersion + " · 一键安装 / 一键还原",
                ForeColor = Color.FromArgb(220, 235, 250),
                AutoSize = true,
                Location = new Point(20, 44)
            };
            header.Controls.Add(title);
            header.Controls.Add(sub);

            status = new Label
            {
                Location = new Point(20, 88),
                Size = new Size(620, 46),
                Text = "正在检测...",
                Font = new Font("Microsoft YaHei UI", 9.5F)
            };

            btnInstall = MakeButton("安装汉化", 20, 140, 190, 40, Color.FromArgb(0, 120, 212), Color.White);
            btnRestore = MakeButton("还原为英文原版", 226, 140, 160, 40, Color.FromArgb(240, 240, 240), Color.Black);
            btnOpen = MakeButton("打开 XTU", 400, 140, 110, 40, Color.FromArgb(240, 240, 240), Color.Black);
            btnClose = MakeButton("退出", 524, 140, 96, 40, Color.FromArgb(240, 240, 240), Color.Black);

            bar = new ProgressBar { Location = new Point(20, 196), Size = new Size(600, 14), Style = ProgressBarStyle.Continuous };

            log = new TextBox
            {
                Location = new Point(20, 222),
                Size = new Size(620, 190),
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                BackColor = Color.FromArgb(250, 250, 250),
                Font = new Font("Consolas", 9F)
            };

            var foot = new Label
            {
                Location = new Point(20, 424),
                Size = new Size(620, 60),
                ForeColor = Color.Gray,
                Text = "说明：本程序会先备份原始文件（%ProgramData%\\XTU-CN\\backup），可随时还原。\r\n安装后程序文件将失去 Intel 数字签名，属正常现象。"
            };

            Controls.Add(header);
            Controls.Add(status);
            Controls.Add(btnInstall);
            Controls.Add(btnRestore);
            Controls.Add(btnOpen);
            Controls.Add(btnClose);
            Controls.Add(bar);
            Controls.Add(log);
            Controls.Add(foot);

            btnInstall.Click += (s, e) => RunInstall();
            btnRestore.Click += (s, e) => RunRestore();
            btnOpen.Click += (s, e) => OpenXtu();
            btnClose.Click += (s, e) => Close();

            ResumeLayout(false);
            PerformLayout();
            Load += (s, e) => { Detect(false); };
        }

        static Button MakeButton(string text, int x, int y, int w, int h, Color back, Color fore)
        {
            var b = new Button
            {
                Text = text,
                Location = new Point(x, y),
                Size = new Size(w, h),
                FlatStyle = FlatStyle.Flat,
                BackColor = back,
                ForeColor = fore,
                Font = new Font("Microsoft YaHei UI", 9.5F)
            };
            b.FlatAppearance.BorderColor = Color.FromArgb(200, 200, 200);
            return b;
        }

        void Log(string text)
        {
            if (Headless) { lock (headlessLog) headlessLog.AppendLine(text); return; }
            if (log.InvokeRequired) { log.BeginInvoke((Action)(() => Log(text))); return; }
            log.AppendText(text + "\r\n");
        }

        void SetBusy(bool state)
        {
            if (InvokeRequired) { BeginInvoke((Action)(() => SetBusy(state))); return; }
            busy = state;
            btnInstall.Enabled = !state;
            btnRestore.Enabled = !state;
            btnOpen.Enabled = !state;
            btnClose.Enabled = !state;
            Cursor = state ? Cursors.WaitCursor : Cursors.Default;
        }

        void SetBar(int v)
        {
            if (bar.InvokeRequired) { bar.BeginInvoke((Action)(() => SetBar(v))); return; }
            bar.Value = Math.Max(0, Math.Min(100, v));
        }

        void SetStatus(string text)
        {
            if (status.InvokeRequired) { status.BeginInvoke((Action)(() => SetStatus(text))); return; }
            status.Text = text;
        }

        internal bool Detect(bool verbose)
        {
            installDir = null;
            installedVersion = null;
            try
            {
                using (var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
                using (var uninst = baseKey.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"))
                {
                    if (uninst != null)
                    {
                        foreach (var name in uninst.GetSubKeyNames())
                        {
                            using (var k = uninst.OpenSubKey(name))
                            {
                                if (k == null) continue;
                                var dn = k.GetValue("DisplayName") as string;
                                if (dn == null || dn.IndexOf("Extreme Tuning Utility", StringComparison.OrdinalIgnoreCase) < 0) continue;
                                var loc = k.GetValue("InstallLocation") as string;
                                var ver = k.GetValue("DisplayVersion") as string;
                                foreach (var cand in new[] { loc, DefaultDir() })
                                {
                                    if (string.IsNullOrEmpty(cand)) continue;
                                    var exe = Path.Combine(cand, "Client", "PerfTune.exe");
                                    if (File.Exists(exe))
                                    {
                                        installDir = cand.TrimEnd('\\');
                                        installedVersion = ver;
                                        break;
                                    }
                                }
                                if (installDir != null) break;
                            }
                        }
                    }
                }
            }
            catch { }

            if (installDir == null && File.Exists(Path.Combine(DefaultDir(), "Client", "PerfTune.exe")))
            {
                installDir = DefaultDir();
                try { installedVersion = FileVersionInfo.GetVersionInfo(Path.Combine(installDir, "Client", "PerfTune.exe")).FileVersion; }
                catch { }
            }

            if (installDir != null && string.IsNullOrEmpty(installedVersion))
            {
                try { installedVersion = FileVersionInfo.GetVersionInfo(Path.Combine(installDir, "Client", "PerfTune.exe")).FileVersion; }
                catch { }
            }

            if (!string.IsNullOrEmpty(installedVersion))
            {
                // normalize e.g. 7.14.2.135 (may include trailing .0)
                installedVersion = installedVersion.Trim();
            }

            if (installDir == null)
            {
                SetStatus("未检测到已安装的 XTU。\r\n点击“安装汉化”可先运行随附的官方安装包，装完再自动汉化。");
                if (verbose) Log("未检测到 XTU。");
            }
            else
            {
                SetStatus("已检测到 XTU " + installedVersion + "\r\n安装位置：" + installDir);
                if (verbose) Log("检测到 XTU " + installedVersion + " @ " + installDir);
            }
            return installDir != null;
        }

        static string DefaultDir()
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), @"Intel\Intel(R) Extreme Tuning Utility");
        }

        // ---------------- install ----------------
        void RunInstall()
        {
            if (busy) return;
            SetBusy(true);
            SetBar(0);
            log.Clear();
            var t = new Thread(() =>
            {
                try { InstallWorker(); }
                catch (Exception ex) { Log("[错误] " + ex.Message); SetStatus("安装失败：" + ex.Message); }
                finally { SetBusy(false); SetBar(0); }
            });
            t.IsBackground = true;
            t.Start();
        }

        void InstallWorker()
        {
            if (!Detect(true))
            {
                Log("未检测到 XTU，准备运行官方安装包...");
                if (!RunBundledSetup()) { SetStatus("未完成 XTU 安装，已取消汉化。"); return; }
                if (!Detect(true)) { SetStatus("仍未检测到 XTU，请安装完成后重新运行本程序。"); return; }
            }

            if (installedVersion != null && installedVersion != TargetVersion
                && !installedVersion.StartsWith("7.14.2", StringComparison.Ordinal))
            {
                if (!Headless)
                {
                    var r = MessageBox.Show(this,
                        "检测到 XTU 版本为 " + installedVersion + "，本汉化针对 " + TargetVersion + " 制作。\r\n继续可能导致界面异常，是否继续？",
                        "版本不匹配", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                    if (r != DialogResult.Yes) { SetStatus("已取消。"); return; }
                }
                else Log("[警告] 版本不匹配：" + installedVersion);
            }

            string client = Path.Combine(installDir, "Client");
            string backup = BackupDir();
            Directory.CreateDirectory(backup);

            Log("目标目录：" + client);
            Log("备份目录：" + backup);
            SetBar(5);

            // 1. stop app
            KillPerfTune();
            SetBar(12);

            // 2. backup originals (only if not present)
            for (int i = 0; i < Payload.Length; i++)
            {
                string src = Path.Combine(client, Payload[i]);
                string bak = Path.Combine(backup, Payload[i]);
                if (!File.Exists(src)) { Log("[跳过] 找不到 " + Payload[i]); continue; }
                if (!File.Exists(bak))
                {
                    bool fromRes = false;
                    try
                    {
                        ExtractResource("res." + OrigResourceNames[i] + ".gz", bak);
                        fromRes = true;
                    }
                    catch { }
                    if (!fromRes) File.Copy(src, bak, true);
                    Log("已备份 " + Payload[i] + (fromRes ? "（内置原版）" : "（当前文件）"));
                }
                else
                {
                    Log("备份已存在 " + Payload[i]);
                }
                SetBar(12 + (i + 1) * 6);
            }

            // 3. extract & replace
            for (int i = 0; i < Payload.Length; i++)
            {
                string dst = Path.Combine(client, Payload[i]);
                if (!File.Exists(dst)) { Log("[跳过] 目标不存在 " + Payload[i]); continue; }
                string tmp = dst + ".xtucn.tmp";
                ExtractResource("res." + ResourceNames[i] + ".gz", tmp);
                ApplyAttributes(tmp);
                KillIfExists(dst);
                File.Copy(tmp, dst, true);
                File.Delete(tmp);
                Log("已替换 " + Payload[i]);
                SetBar(48 + (i + 1) * 9);
            }

            Log("安装完成，正在启动 XTU ...");
            SetBar(100);
            OpenXtu();
            SetStatus("汉化安装完成 ✔  可随时点击“还原为英文原版”恢复。");
            if (!Headless)
                MessageBox.Show(this, "汉化安装完成。\r\nXTU 已重新启动，界面应为中文。", "完成",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        bool RunBundledSetup()
        {
            string tmpDir = Path.Combine(Path.GetTempPath(), "XTU-CN-Setup");
            Directory.CreateDirectory(tmpDir);
            string setup = Path.Combine(tmpDir, "XTUSetup_" + TargetVersion + ".exe");
            try
            {
                if (!File.Exists(setup))
                {
                    Log("正在释放官方安装包（约 43MB）...");
                    ExtractResource("res.XTUSetup.gz", setup);
                    Log("已释放到 " + setup);
                }
                SetBar(20);
                Log("即将启动官方安装程序，请按提示完成安装（安装期间请勿关闭本窗口）。");
                var psi = new ProcessStartInfo(setup) { UseShellExecute = true };
                var p = Process.Start(psi);
                p.WaitForExit();
                Log("官方安装程序已退出，等待安装完成检测...");

                for (int i = 0; i < 60; i++)
                {
                    if (Detect(false)) { SetBar(40); return true; }
                    SetBar(20 + i);
                    Thread.Sleep(2000);
                }
                return Detect(false);
            }
            catch (Exception ex)
            {
                Log("[错误] 运行官方安装包失败：" + ex.Message);
                return false;
            }
        }

        // ---------------- restore ----------------
        void RunRestore()
        {
            if (busy) return;
            if (!Detect(true)) { MessageBox.Show(this, "未检测到 XTU。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
            string backup = BackupDir();
            if (!Directory.Exists(backup)) { MessageBox.Show(this, "没有找到备份，无法还原。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }

            SetBusy(true);
            log.Clear();
            var t = new Thread(() =>
            {
                try
                {
                    RestoreCore();
                    if (!Headless)
                        MessageBox.Show(this, "已还原为英文原版，XTU 已重新启动。", "完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    Log("[错误] " + ex.Message);
                    SetStatus("还原失败：" + ex.Message);
                }
                finally { SetBusy(false); SetBar(0); }
            });
            t.IsBackground = true;
            t.Start();
        }

        void RestoreCore()
        {
            string client = Path.Combine(installDir, "Client");
            string backup = BackupDir();
            KillPerfTune();
            for (int i = 0; i < Payload.Length; i++)
            {
                string src = Path.Combine(backup, Payload[i]);
                string dst = Path.Combine(client, Payload[i]);
                if (!File.Exists(src)) { Log("[跳过] 备份中缺少 " + Payload[i]); continue; }
                File.Copy(src, dst, true);
                Log("已还原 " + Payload[i]);
                SetBar(20 + (i + 1) * 15);
            }
            OpenXtu();
            SetStatus("已还原为英文原版。");
        }

        // ---------------- helpers ----------------
        static string BackupDir()
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), @"XTU-CN\backup");
        }

        static void ExtractResource(string name, string targetPath)
        {
            var asm = Assembly.GetExecutingAssembly();
            using (var s = asm.GetManifestResourceStream(name))
            {
                if (s == null) throw new Exception("缺少内置资源：" + name);
                using (var ds = new DeflateStream(s, CompressionMode.Decompress))
                using (var fs = File.Create(targetPath))
                {
                    var buf = new byte[81920];
                    int n;
                    while ((n = ds.Read(buf, 0, buf.Length)) > 0) fs.Write(buf, 0, n);
                }
            }
        }

        static void ApplyAttributes(string path)
        {
            try { File.SetAttributes(path, FileAttributes.Normal); } catch { }
        }

        static void KillIfExists(string path) { }

        static void KillPerfTune()
        {
            foreach (var name in new[] { "PerfTune", "XtuUiLauncher" })
            {
                foreach (var p in Process.GetProcessesByName(name))
                {
                    try { p.Kill(); p.WaitForExit(5000); } catch { }
                }
            }
            Thread.Sleep(1200);
        }

        void OpenXtu()
        {
            try
            {
                if (installDir == null) Detect(false);
                if (installDir == null) return;
                var exe = Path.Combine(installDir, "Client", "PerfTune.exe");
                if (File.Exists(exe)) Process.Start(new ProcessStartInfo(exe) { UseShellExecute = true });
            }
            catch (Exception ex) { Log("[提示] 启动 XTU 失败：" + ex.Message); }
        }
    }
}
