using System;

internal enum RefusalCode
{
    READ, INTEGRITY, INTERACTIVE_REQUIRED, TARGET_CHANGED, ROOT_CHANGED,
    NODE_LIMIT, DEPTH_LIMIT, TREE_REPEAT, NO_CANDIDATE, MULTIPLE_CANDIDATES,
    CONTEXT_MISSING, CONTEXT_LIMIT, INVOKE_UNAVAILABLE
}

internal sealed class ManualRefusalException : InvalidOperationException
{
    public readonly RefusalCode Code;
    public ManualRefusalException(RefusalCode code) : base(code.ToString()) { Code = code; }
}

// Budget recovery only schedules another full scan. It never selects a candidate,
// clears AutoGate, or retries an Invoke. Explicit limits remain fixed.
internal sealed class ScanRecovery
{
    public int Nodes { get; private set; }
    public int Depth { get; private set; }
    readonly bool growNodes, growDepth;
    public ScanRecovery(int nodes, int depth, bool growNodes, bool growDepth)
    {
        if (nodes < 1 || nodes > 10000 || depth < 1 || depth > 100) throw new ArgumentOutOfRangeException();
        Nodes = nodes; Depth = depth; this.growNodes = growNodes; this.growDepth = growDepth;
    }
    public bool TryGrow(RefusalCode code)
    {
        if (code == RefusalCode.NODE_LIMIT && growNodes && Nodes < 10000)
        { Nodes = Math.Min(10000, Nodes * 2); return true; }
        if (code == RefusalCode.DEPTH_LIMIT && growDepth && Depth < 100)
        { Depth = Math.Min(100, Depth + 20); return true; }
        return false;
    }
    public static bool RequiresReselection(RefusalCode code)
    { return code == RefusalCode.TARGET_CHANGED || code == RefusalCode.ROOT_CHANGED; }
    public static string Guidance(RefusalCode code, bool expanded)
    {
        if (expanded) return "已提高完整掃描上限，下一輪重新掃描；本輪未開始新的核准嘗試。";
        switch (code)
        {
            case RefusalCode.NODE_LIMIT: return "控制項節點超過完整掃描上限，本輪未核准。重開 PowerShell 不會縮小 App 的控制項樹；原因與上限已保留於本機 log。";
            case RefusalCode.DEPTH_LIMIT: return "控制項深度超過完整掃描上限，本輪未核准。原因與上限已保留於本機 log。";
            case RefusalCode.ROOT_CHANGED: case RefusalCode.TARGET_CHANGED:
                return "目標視窗或 UIA 根元素已改變，已切 OFF。先確認 App 的核准結果，再按 Q 退出本工具並重新選取視窗；不需關閉 Codex Desktop。";
            case RefusalCode.TREE_REPEAT: return "App 回傳重複控制項，掃描不完整，本輪未核准；保持 App 開啟，工具將重新掃描。";
            case RefusalCode.CONTEXT_LIMIT: return "核准按鈕的同層控制項超過局部驗證上限，本輪未核准；完整原因已保留於本機 log。";
            case RefusalCode.CONTEXT_MISSING: return "核准按鈕缺少可驗證的父層，本輪未核准；工具將重新掃描。";
            case RefusalCode.NO_CANDIDATE: return "沒有唯一可操作的「允許一次」按鈕，本輪未核准。";
            case RefusalCode.MULTIPLE_CANDIDATES: return "找到多個可用的「允許一次」按鈕，本輪未核准。";
            case RefusalCode.INTEGRITY: return "工具與目標須使用一般使用者的 medium 權限，不會提權。";
            case RefusalCode.INTERACTIVE_REQUIRED: return "此控制台模式需要可輸入 O／F／Q 的互動式終端機。";
            case RefusalCode.INVOKE_UNAVAILABLE: return "按鈕的 Invoke 能力已消失，本輪未核准。";
            default: return "UIA 讀取或驗證未完成，等待重新掃描；不重送已開始的核准嘗試。";
        }
    }
}

// The diagnostic record accepts only enums, bounded counters and a boolean.
// Provider exception messages, UI text, IDs and fingerprints are never retained.
internal sealed class FailureReport
{
    string last; DateTime lastAt;
    public string Next(RefusalCode code, int count, int observedDepth, ScanRecovery budget,
        bool expanded, bool attempted, DateTime now)
    {
        string key = code + "/" + budget.Nodes + "/" + budget.Depth + "/" + expanded + "/" + attempted;
        if (last == key && now - lastAt < TimeSpan.FromSeconds(10)) return null;
        last = key; lastAt = now;
        return "AUTO_REASON: " + code + "\n結果：" + ScanRecovery.Guidance(code, expanded)
            + "\n已讀節點：" + Bound(count, 10000) + "；已讀深度：" + Bound(observedDepth, 100)
            + "；下輪上限：" + budget.Nodes + " 節點／" + budget.Depth + " 層"
            + (attempted ? "\n本輪已開始核准嘗試，結果可能已生效；保留去重，不重送。" : "\n本輪沒有開始新的 Invoke。")
            + "\n";
    }
    static int Bound(int n, int maximum) { return Math.Max(0, Math.Min(maximum, n)); }
}
