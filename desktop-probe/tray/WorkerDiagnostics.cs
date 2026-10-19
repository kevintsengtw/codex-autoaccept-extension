using System;
using System.Text.RegularExpressions;

// Metadata only. Never retain raw stderr, exception messages, UI text or paths.
internal sealed class WorkerDiagnostics
{
    public const string ExpectedVersion = "0.3.10";
    readonly object sync = new object();
    string version, errorType, failure, probeLevel, targetLevel, probeQuery, targetQuery;
    string logName;
    bool attempted;
    string logFailureStage;
    int logFailureCode=-1;
    LogWriteDiagnostic logFailureRecord;
    readonly Func<LogWriteDiagnostic,string,bool> saveLogDiagnostic;
    bool? diagnosticSaved;
    public WorkerDiagnostics(Func<LogWriteDiagnostic,string,bool> saveLogDiagnostic=null)
    { this.saveLogDiagnostic=saveLogDiagnostic; }
    public string LogFailureDetails
    {
        get { lock(sync) return logFailureRecord!=null ? logFailureRecord.Description
            : "失敗時間（本機）：未確認\r\n階段："+(logFailureStage??"未確認")+"；Windows 錯誤碼："
                +(logFailureCode<0 ? "未確認" : logFailureCode.ToString(System.Globalization.CultureInfo.InvariantCulture))+"。"; }
    }
    public bool Compatible { get { lock (sync) return version == ExpectedVersion; } }
    public string IntegritySummary
    {
        get { lock (sync) return "後端權限：" + (probeLevel ?? "未取得") + "；App 權限：" + (targetLevel ?? "未取得")
            + (probeQuery != null && (probeQuery != "none:0" || targetQuery != "none:0") ? "\n權限查詢：後端 " + probeQuery + "；App " + targetQuery : ""); }
    }
    public string ErrorType { get { lock (sync) return errorType ?? "未取得"; } }
    public string LogName { get { lock(sync) return logName; } }
    public void Read(string line)
    {
        lock (sync)
        {
            var record=LogWriteDiagnostic.Parse(line);
            if(record!=null)
            {
                if(record.Kind=="FAILURE")
                { logFailureRecord=record; logFailureStage=record.Stage; logFailureCode=record.NativeError; errorType="LocalLogWriteFailure"; }
                if(saveLogDiagnostic!=null) diagnosticSaved=saveLogDiagnostic(record,logName);
                return;
            }
            var logFailure = Regex.Match(line,@"\ALOG_WRITE_FAILURE: (MAIN_OPEN|MAIN_APPEND|SUMMARY_WRITE|SUMMARY_REPLACE):(-1|[0-9]{1,5})\z");
            if(logFailure.Success && int.Parse(logFailure.Groups[2].Value,System.Globalization.CultureInfo.InvariantCulture)<=65535)
            { logFailureStage=logFailure.Groups[1].Value; logFailureCode=int.Parse(logFailure.Groups[2].Value,System.Globalization.CultureInfo.InvariantCulture); errorType="LocalLogWriteFailure"; }
            var logMatch=Regex.Match(line,@"^LOG_NAME: (auto-\d{8}-\d{6}-[a-f0-9]{32}\.txt)$");
            if(logMatch.Success) logName=logMatch.Groups[1].Value;
            if (line.StartsWith("BACKEND_VERSION: "))
            {
                var v = line.Substring("BACKEND_VERSION: ".Length);
                if (Regex.IsMatch(v, @"^\d{1,3}\.\d{1,3}\.\d{1,5}$")) version = v;
            }
            var levels = Regex.Match(line, @"^INTEGRITY: probe=(medium|medium-plus|low|untrusted|high|system|unavailable); target=(medium|medium-plus|low|untrusted|high|system|unavailable)$");
            if (levels.Success) { probeLevel = levels.Groups[1].Value; targetLevel = levels.Groups[2].Value; }
            var queries = Regex.Match(line, @"^INTEGRITY_QUERY: probe=((?:none|OpenProcess|OpenProcessToken|TokenLength|GetTokenInformation|SidCount):\d{1,10}); target=((?:none|OpenProcess|OpenProcessToken|TokenLength|GetTokenInformation|SidCount):\d{1,10})$");
            if (queries.Success) { probeQuery = queries.Groups[1].Value; targetQuery = queries.Groups[2].Value; }
            if (line == "ATTEMPT: AUTO_INVOKE_ONCE") attempted = true;
            if (line.StartsWith("FAILURE_CODE: "))
            {
                var f = line.Substring("FAILURE_CODE: ".Length);
                if (f == "ARGUMENTS_OR_WINDOW" || f == "TARGET_OR_PERMISSION" || f == "PROBE_ELEVATED" || f == "TARGET_ELEVATED" || f == "IO" || f == "OTHER") failure = f;
            }
            foreach (var type in new[] { "IOException", "UnauthorizedAccessException", "SecurityException", "ArgumentException", "FileNotFoundException", "BadImageFormatException", "TypeInitializationException", "ManualRefusalException", "ElementNotAvailableException", "ElementNotEnabledException", "COMException", "Win32Exception" })
                if (Regex.IsMatch(line, @"\b" + type + @"\b")) { errorType = type; break; }
        }
    }
    public string Describe(int exitCode)
    {
        lock (sync)
        {
            if(logFailureStage!=null)
                return "後端退出碼 "+exitCode+"；"+DescribeLogFailure();
            string reason = version != ExpectedVersion ? "後端版本未確認或不符，請完整解壓同一版 ZIP。"
                : failure == "PROBE_ELEVATED" ? "後端已提升權限，請退出後以一般使用者重新啟動。"
                : failure == "TARGET_ELEVATED" ? "App 是已提升權限的目標，此一般使用者工具不處理。"
                : failure == "TARGET_OR_PERMISSION" ? "目標視窗已變更，或前後端與 App 權限不符。"
                : failure == "IO" ? "後端輸入輸出或本機紀錄初始化失敗。"
                : failure == "ARGUMENTS_OR_WINDOW" ? "後端啟動參數或目標視窗無效。" : "後端執行未完成。";
            return "後端退出碼 " + exitCode + "；錯誤類型 " + (errorType ?? "未取得") + "。\n" + reason
                + "\n" + IntegritySummary + (attempted ? " 核准可能已送出，請先查看 App，再重新連接。" : " 本次未記錄核准嘗試，可重新選擇視窗或重新連接。");
        }
    }
    public string DescribeLogFailure()
    {
        lock(sync) return "錯誤類型 LocalLogWriteFailure。\r\n"+LogFailureDetails
            +"\r\n本機 log 寫入失敗，已停止新的核准嘗試；短摘要可能過期。"
            +"\r\n"+(diagnosticSaved==true ? "本機診斷已另存，可按「開啟本次失敗診斷」。"
                : "本機診斷未另存成功；請保留介面上的錯誤資訊。")
            +"\r\n"+IntegritySummary+(attempted ? " 核准可能已送出，請先查看 App。" : " 本次未記錄核准嘗試。");
    }
}
