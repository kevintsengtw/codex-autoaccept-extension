static void Check(bool b, string s) { if (!b) throw new Exception(s); }
var state = new DesktopState();
Check(!state.DesiredOn && !state.ActualOn && !state.Ready, "always starts OFF");
state.BeginConnection(); Check(state.Connecting && !state.Ready && state.Caption.Contains("正在連接"), "cannot enable while connecting");
state.Disconnected(false);
state.Request(true); Check(state.Caption.Contains("等待"), "ON intent can wait for App");
state.Connected(); Check(state.DesiredOn && !state.ActualOn && state.Caption == "開啟中", "new worker never assumed ON before acknowledgement");
state.Acknowledge(true); Check(state.Caption.StartsWith("ON"), "ON acknowledgement");
state.Request(false); Check(state.Caption == "停止中", "pending OFF must not claim OFF");
state.Acknowledge(false); Check(state.Caption.StartsWith("OFF"), "OFF acknowledged");
state.Request(true); state.Disconnected(false); Check(state.DesiredOn && !state.Ready, "normal App loss keeps current-session intent");
state.Connected(); state.Acknowledge(true); state.Disconnected(true);
Check(state.Faulted && !state.Ready && !state.ActualOn, "worker fault never silently re-enables");
state.Acknowledge(true); Check(!state.ActualOn, "late acknowledgement ignored while disconnected");
state.Connected(); Check(state.Faulted && !state.Ready, "late READY cannot resurrect failed worker");
state.Reset(); Check(!state.DesiredOn && !state.Faulted, "explicit reconnect resets OFF");
state.Request(true); var restarted = new DesktopState(); Check(!restarted.DesiredOn, "process restart never restores ON");
Console.WriteLine("PASS: 13 desktop lifecycle checks (default OFF, intent, worker ack, pending OFF, reconnect, fault and restart).");
