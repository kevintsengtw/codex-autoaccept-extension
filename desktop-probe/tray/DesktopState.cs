internal sealed class DesktopState
{
    public bool DesiredOn { get; private set; } // Never loaded from disk.
    public bool Ready { get; private set; }
    public bool ActualOn { get; private set; }
    public bool Connecting { get; private set; }
    public void BeginConnection() { Ready = false; ActualOn = false; Faulted = false; Connecting = true; }
    public bool Faulted { get; private set; }
    public void Request(bool on) { DesiredOn = on; }
    public void Connected() { if (Faulted) return; Connecting = false; Ready = true; ActualOn = false; Faulted = false; }
    public void Acknowledge(bool on) { if (Ready) ActualOn = on; }
    public void Disconnected(bool fault) { Ready = false; ActualOn = false; Faulted = fault; Connecting = false; }
    public void Reset() { DesiredOn = false; Ready = false; ActualOn = false; Faulted = false; Connecting = false; }
    public string Caption => Faulted ? "暫停：請重新連接" : Connecting ? "正在連接（OFF）" : !Ready ? (DesiredOn ? "ON · 等待 App" : "OFF · 等待 App")
        : DesiredOn != ActualOn ? (DesiredOn ? "開啟中" : "停止中") : ActualOn ? "ON · 自動核准中" : "OFF · 人工核准";
}
