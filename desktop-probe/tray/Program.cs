using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using Microsoft.Win32;

internal static class TrayProgram
{
    [STAThread]
    static void Main()
    {
        Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
        bool owner;
        using (var mutex = new Mutex(true, "Local\\CodexAutoAcceptDesktop-" + WindowsIdentity.GetCurrent().User.Value, out owner))
        {
            if (!owner) { MessageBox.Show("程式已在執行，請查看右下角系統匣（可能在隱藏圖示裡）。", "Codex Auto-Accept"); return; }
            try { Application.Run(new TrayContext()); }
            catch (Exception e) { MessageBox.Show("啟動失敗：" + e.GetType().Name + "。請確認完整解壓及公司允許程式執行。", "Codex Auto-Accept"); }
        }
    }
}
internal sealed class TargetWindow
{
    public long Hwnd, Started; public int Pid; public string Path, Name, Title;
    public override string ToString() { return Name + " · PID " + Pid + " · " + Title; }
}
internal sealed class Settings { public string TargetPath; public BrowserHost TargetHost; }
internal sealed class TrayContext : ApplicationContext
{
    const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run", RunName = "CodexAutoAcceptDesktop";
    readonly DesktopState state = new DesktopState();
    readonly Form panel = new Form();
    readonly NotifyIcon tray = new NotifyIcon();
    readonly Label status = new Label(), target = new Label(), detail = new Label(), launchStatus = new Label();
    readonly Button toggle = new Button(), choose = new Button(), reconnect = new Button(), launch = new Button(), openLog = new Button();
    readonly CheckBox startup = new CheckBox();
    readonly ToolStripMenuItem toggleMenu = new ToolStripMenuItem();
    readonly System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer();
    readonly string settingsFile = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CodexAutoAcceptDesktop", "settings.json");
    Settings settings = new Settings();
    Process worker;
    TargetWindow selected;
    Icon icon;
    bool busy, exiting, refreshStartup;
    string currentLogName;
    LocalLogDiagnostics currentLocalDiagnostics;
    public TrayContext()
    {
        if (File.Exists(settingsFile))
            try { settings = new JavaScriptSerializer().Deserialize<Settings>(File.ReadAllText(settingsFile)) ?? new Settings(); } catch { }
        panel.Text = "Codex Auto-Accept 0.3.10"; panel.ClientSize = new Size(620, 530);
        panel.FormBorderStyle = FormBorderStyle.FixedDialog; panel.MaximizeBox = false; panel.StartPosition = FormStartPosition.CenterScreen;
        panel.Font = new Font("Microsoft JhengHei UI", 10);
        status.SetBounds(20, 20, 500, 35); status.Font = new Font(panel.Font.FontFamily, 16, FontStyle.Bold);
        target.SetBounds(20, 65, 500, 50); target.AutoEllipsis = true;
        detail.SetBounds(20, 120, 580, 140);
        toggle.Text = "開啟 ON"; toggle.SetBounds(195, 205, 145, 40); toggle.Click += (s, e) => Toggle();
        choose.Text = "1. 選擇 App 視窗"; choose.SetBounds(20, 205, 165, 40); choose.Click += async (s, e) => await Choose();
        reconnect.Text = "重新連接"; reconnect.SetBounds(350, 205, 165, 40); reconnect.Click += async (s, e) => await Reconnect();
        startup.Text = "登入 Windows 時啟動（啟動後仍為 OFF）"; startup.SetBounds(20, 250, 495, 30); startup.CheckedChanged += (s, e) => ChangeStartup();
        launch.Text = "測試背景相容啟動"; launch.SetBounds(20, 290, 190, 38); launch.Click += async (s, e) => await LaunchCompatible();
        launchStatus.Text = "本次尚未由工具送出相容啟動。"; launchStatus.SetBounds(220, 285, 380, 75);
        openLog.Text = "開啟本次 log 摘要"; openLog.SetBounds(20,370,220,38); openLog.Click += (s,e) => OpenLog();
        var logTip = new Label { Text = "紀錄自動保留在本機，可稍後查看。\n完整 log 保留原因與核准前後階段。", Bounds = new Rectangle(260,365,340,50) };
        var tip = new Label { Text = "先選擇 App，再開啟 ON。關閉視窗仍常駐；右鍵系統匣可退出。", AutoSize = false };
        tip.SetBounds(20, 430, 580,30);
        foreach(Control control in new Control[] { choose,toggle,reconnect,startup,launch,launchStatus,openLog,logTip,tip }) control.Top+=60;
        panel.Controls.AddRange(new Control[] { status, target, detail, choose, toggle, reconnect, startup, launch, launchStatus, openLog, logTip, tip });
        choose.TabIndex = 0; toggle.TabIndex = 1; reconnect.TabIndex = 2; startup.TabIndex = 3; launch.TabIndex = 4; openLog.TabIndex = 5;
        panel.FormClosing += (s, e) => { if (e.CloseReason == CloseReason.WindowsShutDown) return; if (!exiting) { e.Cancel = true; panel.Hide(); tray.ShowBalloonTip(2000, "Codex Auto-Accept", "程式仍在系統匣執行，雙擊圖示可開啟設定。", ToolTipIcon.Info); } };
        var menu = new ContextMenuStrip();
        menu.Items.Add(toggleMenu); toggleMenu.Click += (s, e) => Toggle();
        menu.Items.Add("開啟設定", null, (s, e) => Show());
        menu.Items.Add("重新連接（回到 OFF）", null, async (s, e) => await Reconnect());
        menu.Items.Add(new ToolStripSeparator()); menu.Items.Add("退出", null, async (s, e) => await Exit());
        tray.ContextMenuStrip = menu; tray.Visible = true; tray.DoubleClick += (s, e) => Show();
        var uiHandle = panel.Handle; // Create marshal target even when launch stays entirely in the tray.
        ReadStartup(); Render();
        timer.Interval = 2000; timer.Tick += async (s, e) => { if (!busy && !exiting) await Attach(); }; timer.Start();
        if (string.IsNullOrWhiteSpace(settings.TargetPath)) Show();
    }
    void Show() { panel.Show(); panel.WindowState = FormWindowState.Normal; panel.Activate(); }
    void Ui(Action action) { if (!exiting && panel.IsHandleCreated) try { panel.BeginInvoke(action); } catch (InvalidOperationException) { } }
    void Toggle()
    {
        if (!state.Ready || busy) { Show(); return; }
        if (state.Faulted) { Show(); MessageBox.Show(panel, "後端已停止。請先查看 App 的核准狀態，再按「重新連接」。", "Codex Auto-Accept"); return; }
        state.Request(!state.DesiredOn);
        if (state.Ready) Send(state.DesiredOn ? "ON" : "OFF");
        Render();
    }
    void Render()
    {
        status.Text = state.Caption; toggle.Text = state.DesiredOn ? "2. 關閉 OFF" : "2. 開啟 ON"; toggleMenu.Text = state.DesiredOn ? "關閉 OFF" : "開啟 ON";
        toggle.Enabled = toggleMenu.Enabled = state.Ready && !state.Faulted && !busy;
        openLog.Enabled = !busy && currentLogName != null;
        openLog.Text = state.Faulted ? (currentLocalDiagnostics!=null && File.Exists(currentLocalDiagnostics.FilePath)
            ? "開啟本次失敗診斷" : "開啟本次完整 log") : "開啟本次 log 摘要";
        choose.Enabled = !busy; reconnect.Enabled = !busy && (selected != null || !string.IsNullOrEmpty(settings.TargetPath));
        launch.Enabled = !busy && !string.IsNullOrEmpty(settings.TargetPath);
        target.Text = selected == null ? "尚未連接 App。第一次請選擇視窗；之後會記住 App 路徑。" : "目標：" + selected.Name + " · PID " + selected.Pid + "\n" + selected.Title;
        if (string.IsNullOrEmpty(detail.Text)) detail.Text = "每次啟動皆為 OFF。已送出的核准不會因切 OFF 而撤回。";
        tray.Text = "Codex Auto-Accept · " + state.Caption;
        using (var bitmap = new Bitmap(32, 32))
        using (var graphics = Graphics.FromImage(bitmap))
        using (var brush = new SolidBrush(state.Faulted ? Color.DarkOrange : state.Ready && state.ActualOn ? Color.ForestGreen : Color.DimGray))
        {
            graphics.Clear(Color.Transparent); graphics.FillEllipse(brush, 1, 1, 30, 30);
            using (var font = new Font("Arial", 16, FontStyle.Bold)) graphics.DrawString("A", font, Brushes.White, 5, 3);
            IntPtr handle = bitmap.GetHicon(); Icon next;
            try { next = (Icon)Icon.FromHandle(handle).Clone(); } finally { DestroyIcon(handle); }
            tray.Icon = next; if (icon != null) icon.Dispose(); icon = next;
        }
    }
    async Task Choose()
    {
        if (busy) return; busy = true; Render();
        try
        {
            var windows = await Task.Run(() => Windows());
            using (var picker = new Form { Text = "選擇 ChatGPT／Codex App", ClientSize = new Size(650, 280), StartPosition = FormStartPosition.CenterParent, MinimizeBox = false, MaximizeBox = false })
            {
                var list = new ListBox { Dock = DockStyle.Top, Height = 220, HorizontalScrollbar = true };
                list.Items.AddRange(windows.Cast<object>().ToArray());
                var ok = new Button { Text = "連接所選視窗", Dock = DockStyle.Bottom, Height = 40, DialogResult = DialogResult.OK };
                picker.Controls.Add(list); picker.Controls.Add(ok); picker.AcceptButton = ok;
                if (windows.Count == 0) { MessageBox.Show(panel, "未找到 App 視窗。請先開啟 ChatGPT／Codex App，再試一次。", "Codex Auto-Accept"); return; }
                if (picker.ShowDialog(panel) != DialogResult.OK || list.SelectedItem == null || exiting) return;
                var chosen = (TargetWindow)list.SelectedItem;
                state.Reset(); await Disconnect();
                settings.TargetPath = chosen.Path;
                settings.TargetHost = await Task.Run(() => DetectHost(chosen));
                if (exiting) return;
                SaveSettings(); StartWorker(chosen);
            }
        }
        catch (Exception e) { MessageBox.Show(panel, "選擇／連接失敗：" + e.GetType().Name, "Codex Auto-Accept"); }
        finally { busy = false; choose.Enabled = true; Render(); }
    }
    async Task Reconnect()
    {
        if (busy || exiting) return;
        var previous = selected;
        busy = true; state.Reset(); Render();
        try { await Disconnect(); if (previous != null && SameWindow(previous)) StartWorker(previous); }
        finally { busy = false; if (!exiting) Render(); }
        if (worker == null) await Attach(); Render();
    }
    static BrowserHost DetectHost(TargetWindow window)
    {
        var modules = new List<string>();
        try { using (var p = Process.GetProcessById(window.Pid)) foreach (ProcessModule m in p.Modules) modules.Add(m.ModuleName); } catch { }
        try { return BackgroundLaunch.Detect(window.Path, modules); } catch { return BrowserHost.Unknown; }
    }
    void OpenLog()
    {
        if (currentLogName == null) return;
        try
        {
            string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"CodexUiaProbe",currentLogName);
            string summary=Path.ChangeExtension(path,null)+"-summary.txt";
            if(state.Faulted && currentLocalDiagnostics!=null && File.Exists(currentLocalDiagnostics.FilePath)) path=currentLocalDiagnostics.FilePath;
            else if(!state.Faulted && File.Exists(summary)) path=summary;
            if(!File.Exists(path)) { MessageBox.Show(panel,"本次 log 尚未建立。","執行紀錄"); return; }
            using(var process=Process.Start(new ProcessStartInfo(path){UseShellExecute=true})) { }
        }
        catch { MessageBox.Show(panel,"無法開啟本次 log；可在本機 CodexUiaProbe 紀錄資料夾查看。","執行紀錄"); }
    }
    async Task LaunchCompatible()
    {
        if (busy || exiting || string.IsNullOrEmpty(settings.TargetPath)) return;
        busy = true; Render();
        try
        {
            BrowserHost host;
            using (var dialog = new Form { Text = "測試背景相容啟動", ClientSize = new Size(540, 265), StartPosition = FormStartPosition.CenterParent, MinimizeBox = false, MaximizeBox = false, Font = panel.Font })
            {
                var info = new Label { Text = "請先儲存工作並從 App 選單完整退出（不能只關閉視窗）。\n此功能會以背景／輔助使用參數啟動已選過的 App，\n啟動後為 OFF。參數是否有效，仍需實測。\n不會替你關閉 App；下次從原捷徑開啟即恢復一般啟動。", Bounds = new Rectangle(20,15,500,110) };
                var mode = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Bounds = new Rectangle(20,140,500,30) };
                mode.Items.AddRange(new object[] { "WebView2（Windows 原生 App）", "Chromium／Electron" });
                mode.SelectedIndex = settings.TargetHost == BrowserHost.Chromium ? 1 : 0;
                var evidence = new Label { Text = settings.TargetHost == BrowserHost.Unknown ? "引擎尚未辨識：先測 WebView2，無效時再測 Chromium／Electron。" : "已找到 " + settings.TargetHost + " 引擎線索；這不代表參數已生效。", Bounds = new Rectangle(20,178,500,35) };
                var start = new Button { Text = "以此模式啟動（OFF）", DialogResult = DialogResult.OK, Bounds = new Rectangle(20,220,290,35) };
                var cancel = new Button { Text = "取消", DialogResult = DialogResult.Cancel, Bounds = new Rectangle(330,220,190,35) };
                dialog.Controls.AddRange(new Control[] { info, mode, evidence, start, cancel }); dialog.AcceptButton = start; dialog.CancelButton = cancel;
                if (dialog.ShowDialog(panel) != DialogResult.OK || exiting) return;
                host = mode.SelectedIndex == 0 ? BrowserHost.WebView2 : BrowserHost.Chromium;
            }
            state.Reset(); await Disconnect(); Render();
            string file = settings.TargetPath;
            bool running = await Task.Run(() =>
            {
                bool blocked = false;
                foreach (var p in Process.GetProcessesByName(Path.GetFileNameWithoutExtension(file)))
                    using (p)
                    {
                        if (p.HasExited) continue;
                        string path = null; bool available = false;
                        try { path = p.MainModule.FileName; available = !string.IsNullOrEmpty(path); } catch { }
                        if (BackgroundLaunch.BlocksLaunch(file, path, available)) blocked = true;
                    }
                return blocked;
            });
            if (exiting) return;
            if (running)
            {
                launchStatus.Text = "APP_STILL_RUNNING：請從 App 選單完整退出，再重試。未啟動新 App。";
                MessageBox.Show(panel, "App 仍在執行，或同名程序的路徑無法確認。請從 App 選單完整退出後再試。\n已有 App 執行時，新參數可能無效。工具目前為 OFF。", "背景相容啟動"); return;
            }
            var startInfo = BackgroundLaunch.Create(file, host, Environment.GetEnvironmentVariable(BackgroundLaunch.EnvironmentKey));
            await Task.Run(() => { using (var process = Process.Start(startInfo)) { if (process == null) throw new InvalidOperationException(); } });
            if (exiting) return;
            launchStatus.Text = "已送出 " + host + " 相容啟動（效果未確認）。連接後請手動開 ON，再測背景／最小化。";
        }
        catch (InvalidOperationException) { launchStatus.Text = "LAUNCH_BLOCKED：啟動受限或已有 WebView2 參數。未覆寫原有參數，請改用原捷徑。"; }
        catch (Exception e) { launchStatus.Text = "LAUNCH_FAILED：" + e.GetType().Name + "。請改用原捷徑開啟 App。"; }
        finally { busy = false; Render(); }
        if (!exiting) await Attach();
    }
    async Task Attach()
    {
        if (busy || exiting || state.Faulted) return;
        // A healthy attached worker needs no UI update on each timer tick.
        if (selected != null && SameWindow(selected)) return;
        busy = true;
        try
        {
            if (worker != null) await Disconnect();
            if (string.IsNullOrEmpty(settings.TargetPath)) { detail.Text = "請選擇 App 視窗。若路徑讀取受限，下次需重新選擇。"; Render(); return; }
            var windows = await Task.Run(() => Windows().Where(w => string.Equals(w.Path, settings.TargetPath, StringComparison.OrdinalIgnoreCase)).ToList());
            if (exiting) return;
            if (windows.Count == 1) StartWorker(windows[0]);
            else { detail.Text = windows.Count > 1 ? "找到多個符合視窗，請用「選擇 App 視窗」指定。" : "等待已選過的 App 開啟。"; Render(); }
        }
        catch (Exception e) { detail.Text = "連接失敗：" + e.GetType().Name; state.Disconnected(true); Render(); }
        // Render() inside the operation disabled the picker while busy. Restore
        // controls even on the first-run/missing/multiple-window return paths.
        finally { busy = false; if (!exiting) Render(); }
    }
    void StartWorker(TargetWindow window)
    {
        var file = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "CodexUiaManualInvoke.exe");
        if (!File.Exists(file)) throw new FileNotFoundException();
        selected = window;
        currentLogName=null;
        var p = new Process { StartInfo = new ProcessStartInfo(file, "--headless --hwnd " + window.Hwnd + " --max-depth 60")
            { UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true,
              StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8 }, EnableRaisingEvents = true };
        worker = p; state.BeginConnection(); detail.Text = "正在連接…";
        var localDiagnostics=new LocalLogDiagnostics(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CodexAutoAcceptDesktop","diagnostics"));
        currentLocalDiagnostics=localDiagnostics;
        var diagnostics = new WorkerDiagnostics(localDiagnostics.Append);
        p.OutputDataReceived += (s, e) => {
            if (e.Data == null) return;
            diagnostics.Read(e.Data); // Retain safe diagnostics even if exit races ahead of UI delivery.
            Ui(() => { if (ReferenceEquals(worker, p) && !p.HasExited) Receive(e.Data, diagnostics); });
        };
        p.ErrorDataReceived += (s, e) => { if (e.Data != null) diagnostics.Read(e.Data); };
        p.Exited += async (s, e) => {
            try
            {
                int code = p.ExitCode;
                await Task.Run(() => p.WaitForExit()); // Drain redirected output before describing the exit.
                Ui(() => { if (ReferenceEquals(worker, p)) { currentLogName=diagnostics.LogName??currentLogName; state.Disconnected(true); detail.Text = diagnostics.Describe(code); Render(); } });
            }
            catch (InvalidOperationException) { }
        };
        try { p.Start(); p.BeginOutputReadLine(); p.BeginErrorReadLine(); }
        catch { worker = null; selected = null; p.Dispose(); state.Disconnected(true); throw; }
        Render();
    }
    void Receive(string message, WorkerDiagnostics diagnostics)
    {
        var logDiagnostic=LogWriteDiagnostic.Parse(message);
        if(logDiagnostic!=null)
        {
            if(logDiagnostic.Kind=="FAILURE") { state.Disconnected(true); detail.Text=diagnostics.DescribeLogFailure(); Render(); }
            return;
        }
        if (message.StartsWith("LOG_WRITE_WARNING: ")) { detail.Text = "短摘要更新失敗；繼續使用完整主 log，原因已保留在本機。"; Render(); return; }
        if (message.StartsWith("LOG_WRITE_FAILURE: ")) { state.Disconnected(true); detail.Text = diagnostics.DescribeLogFailure(); Render(); return; }
        if (message.StartsWith("UIA_SCAN: ")) return;
        if (message.StartsWith("LOG_NAME: ")) { currentLogName=diagnostics.LogName; openLog.Enabled=!busy && currentLogName!=null; return; }
        if (message == "READY: OFF") {
            if (!diagnostics.Compatible) { state.Disconnected(true); detail.Text = "前後端版本不符，請完整解壓 0.3.10 ZIP。"; Send("QUIT"); Render(); return; }
            state.Connected(); if (state.DesiredOn) Send("ON"); detail.Text = "已連接。每次啟動仍為 OFF；執行紀錄自動保留在本機。"; }
        else if (message == "STATUS: ON" || message == "STATUS: OFF") {
            bool on=message=="STATUS: ON"; state.Acknowledge(on);
            detail.Text=on?"正在監聽，執行紀錄自動保留在本機。":"已關閉自動核准，後續請求保留人工處理。";
        }
        else if (message == "ATTEMPT: AUTO_INVOKE_ONCE") detail.Text = "已開始一次核准嘗試；結果須看後續工作是否完成。";
        else if (message == "UIA_STATE: BACKGROUND_PROVIDER_LIMITED") detail.Text = "背景監聽中，等待 App 提供可用按鈕；紀錄已保留。";
        else if (message == "UIA_STATE: BACKGROUND_CANDIDATE_READY") detail.Text = "已在背景辨識核准按鈕，不會主動切換前景。";
        else if (message == "UIA_STATE: CLOAKED" || message == "UIA_STATE: WINDOW_HIDDEN") detail.Text = "App 視窗已隱藏或位於其他桌面；等待恢復。";
        else if (message == "UIA_STATE: NATIVE_STATE_UNAVAILABLE") detail.Text = "視窗狀態無法確認或被對話框阻擋；等待重掃。";
        else if (message == "UIA_STATE: WINDOW_TRANSITION") detail.Text = "掃描期間視窗狀態改變；等待狀態穩定後重掃。";
        else if (message == "UIA_STATE: MINIMIZED") detail.Text = "最小化視窗狀態尚無法確認；保持開關，等待重掃。";
        else if (message == "UIA_STATE: MINIMIZED_PROVIDER_LIMITED") detail.Text = "最小化監聽中，等待 App 提供可用按鈕；紀錄已保留。";
        else if (message == "UIA_STATE: MINIMIZED_CANDIDATE_READY") detail.Text = "已在最小化視窗辨識核准按鈕；不主動還原視窗。";
        else if (message == "UIA_STATE: ROOT_OFFSCREEN") detail.Text = "App 視窗暫時不可讀；保持開關，等待更新。";
        else if (message == "UIA_STATE: BUTTON_OFFSCREEN") detail.Text = "找到按鈕但目前不可用，等待畫面更新。";
        else if (message == "UIA_STATE: BUTTON_NOT_READY") detail.Text = "找到按鈕，但尚未啟用或無法 Invoke；等待更新。";
        else if (message == "UIA_STATE: NO_APPROVAL_BUTTON") detail.Text = "正在監聽，等待可辨識的核准請求。";
        else if (message.StartsWith("AUTO: READ/ACTION ERROR")) detail.Text = "讀取／操作未完成，原因已寫入本機 log；等待重掃。";
        else if (message.StartsWith("AUTO: WAITING")) detail.Text = "等待核准區塊離開或畫面穩定；保持開關狀態。";
        else if (message.StartsWith("AUTO: CONTINUING")) detail.Text = "本次核准觀察已結束，繼續監聽。";
        else if (message.StartsWith("RESULT: TIMEOUT")) { state.Disconnected(true); detail.Text = "後端逾時，結果可能已生效。請先檢查 App，再重新連接。"; }
        Render();
    }
    void Send(string command)
    {
        try { if (worker == null || worker.HasExited) throw new InvalidOperationException(); worker.StandardInput.WriteLine(command); worker.StandardInput.Flush(); }
        catch { state.Disconnected(true); detail.Text = "無法聯絡後端，請重新連接。"; }
    }
    async Task Disconnect()
    {
        var old = worker; worker = null; selected = null; state.Disconnected(false);
        if (old != null)
        {
            await Task.Run(() => {
                try { if (!old.HasExited) { try { old.StandardInput.WriteLine("QUIT"); old.StandardInput.Flush(); } catch { } if (!old.WaitForExit(2500)) { old.Kill(); old.WaitForExit(); } } } catch { }
                old.Dispose();
            });
        }
    }
    async Task Exit()
    {
        if (exiting) return; exiting = true; timer.Stop(); state.Request(false); tray.Visible = false;
        await Disconnect(); panel.Close(); tray.Dispose(); if (icon != null) icon.Dispose(); timer.Dispose(); ExitThread();
    }
    void SaveSettings()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(settingsFile));
        var temp = settingsFile + ".tmp"; File.WriteAllText(temp, new JavaScriptSerializer().Serialize(settings), new UTF8Encoding(false));
        if (File.Exists(settingsFile)) File.Replace(temp, settingsFile, null); else File.Move(temp, settingsFile);
    }
    string StartupCommand => "\"" + Application.ExecutablePath + "\"";
    void ReadStartup()
    {
        refreshStartup = true;
        try { using (var key = Registry.CurrentUser.OpenSubKey(RunKey)) startup.Checked = string.Equals(key?.GetValue(RunName) as string, StartupCommand, StringComparison.OrdinalIgnoreCase); }
        catch { startup.Checked = false; startup.Enabled = false; }
        finally { refreshStartup = false; }
    }
    void ChangeStartup()
    {
        if (refreshStartup) return;
        try
        {
            using (var key = Registry.CurrentUser.CreateSubKey(RunKey))
            {
                string old = key.GetValue(RunName) as string;
                if (old != null && !string.Equals(old, StartupCommand, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Existing startup entry belongs to a different location.");
                if (startup.Checked && StartupCommand.Length > 260) throw new InvalidOperationException("Startup path is too long.");
                if (startup.Checked) key.SetValue(RunName, StartupCommand, RegistryValueKind.String);
                else key.DeleteValue(RunName, false);
            }
        }
        catch { MessageBox.Show(panel, "無法變更登入啟動。公司政策可能限制此設定，或同名啟動項目使用了另一個路徑。程式仍可手動啟動。", "Codex Auto-Accept"); }
        ReadStartup();
    }
    static bool SameWindow(TargetWindow w)
    {
        if (!IsWindow(new IntPtr(w.Hwnd))) return false;
        uint pid; GetWindowThreadProcessId(new IntPtr(w.Hwnd), out pid);
        if (pid != w.Pid) return false;
        try { using (var p = Process.GetProcessById(w.Pid)) return p.StartTime.ToUniversalTime().Ticks == w.Started; }
        catch { return false; }
    }
    static List<TargetWindow> Windows()
    {
        var windows = new List<TargetWindow>();
        EnumWindows((h, l) => {
            if (!IsWindowVisible(h)) return true;
            uint id; GetWindowThreadProcessId(h, out id);
            try
            {
                using (var p = Process.GetProcessById((int)id))
                {
                    string name = p.ProcessName;
                    if (name.StartsWith("CodexUia", StringComparison.OrdinalIgnoreCase) || name == "CodexAutoAcceptDesktop"
                        || (name.IndexOf("ChatGPT", StringComparison.OrdinalIgnoreCase) < 0 && name.IndexOf("Codex", StringComparison.OrdinalIgnoreCase) < 0)) return true;
                    var title = new StringBuilder(2048); GetWindowText(h, title, title.Capacity);
                    string path = null; try { path = p.MainModule.FileName; } catch { }
                    windows.Add(new TargetWindow { Hwnd = h.ToInt64(), Started = p.StartTime.ToUniversalTime().Ticks, Pid = (int)id, Path = path, Name = name, Title = title.ToString() });
                }
            }
            catch { }
            return true;
        }, IntPtr.Zero);
        return windows;
    }
    delegate bool EnumProc(IntPtr hwnd, IntPtr param);
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc callback, IntPtr param);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr hwnd);
    [DllImport("user32.dll")] static extern bool IsWindow(IntPtr hwnd);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetWindowText(IntPtr hwnd, StringBuilder text, int max);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint pid);
    [DllImport("user32.dll")] static extern bool DestroyIcon(IntPtr icon);
}
