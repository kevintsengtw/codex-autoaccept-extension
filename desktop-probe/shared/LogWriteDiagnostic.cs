using System;
using System.Globalization;
using System.Text.RegularExpressions;

// Fixed local metadata only: never serialize exception messages, paths or UI text.
internal sealed class LogWriteDiagnostic
{
    public const string Prefix = "LOG_WRITE_DIAGNOSTIC: ";
    public const string TimeFormat = "yyyy-MM-dd'T'HH:mm:ss.fffzzz";
    public readonly string Kind, Stage;
    public readonly int NativeError, Attempts;
    public readonly DateTimeOffset Time;
    LogWriteDiagnostic(string kind, string stage, int nativeError, DateTimeOffset time, int attempts)
    { Kind=kind; Stage=stage; NativeError=nativeError; Time=time; Attempts=attempts; }
    public static bool SafeLogName(string name) => name != null && Regex.IsMatch(name,
        @"\Aauto-[0-9]{8}-[0-9]{6}-[a-f0-9]{32}\.txt\z");
    public static LogWriteDiagnostic Create(string kind, string stage, int nativeError, DateTimeOffset time, int attempts=1)
    {
        var value = new LogWriteDiagnostic(kind,stage,nativeError,time,attempts);
        if (Parse(value.Protocol) == null) throw new ArgumentException("Invalid fixed log diagnostic metadata.");
        return value;
    }
    public string Protocol => Prefix + Kind + "|" + Stage + "|" + NativeError.ToString(CultureInfo.InvariantCulture)
        + "|" + Time.ToString(TimeFormat,CultureInfo.InvariantCulture) + "|" + Attempts.ToString(CultureInfo.InvariantCulture);
    public string LocalText(string logName) => "local_time=" + Time.ToString(TimeFormat,CultureInfo.InvariantCulture)
        + " kind=" + Kind + " stage=" + Stage + " win32=" + NativeError.ToString(CultureInfo.InvariantCulture)
        + " attempts=" + Attempts.ToString(CultureInfo.InvariantCulture)
        + " log=" + (SafeLogName(logName) ? logName : "unconfirmed") + "\r\n";
    public string Description => "失敗時間（本機）：" + Time.ToString("yyyy-MM-dd HH:mm:ss.fff zzz",CultureInfo.InvariantCulture)
        + "\r\n階段：" + Stage + "；Windows 錯誤碼：" + (NativeError < 0 ? "未確認" : NativeError.ToString(CultureInfo.InvariantCulture))
        + "；嘗試次數：" + Attempts.ToString(CultureInfo.InvariantCulture) + "。";
    public static LogWriteDiagnostic Parse(string line)
    {
        if (line == null || line.Length > 160) return null;
        var match=Regex.Match(line,@"\ALOG_WRITE_DIAGNOSTIC: (RETRY|RECOVERED|WARNING|FAILURE)\|(MAIN_OPEN|MAIN_APPEND|SUMMARY_WRITE|SUMMARY_REPLACE)\|(-1|[0-9]{1,5})\|([0-9T:.+\-]+)\|([1-5])\z");
        if (!match.Success) return null;
        int code;
        DateTimeOffset time;
        if (!int.TryParse(match.Groups[3].Value,NumberStyles.AllowLeadingSign,CultureInfo.InvariantCulture,out code)
            || code < -1 || code > 65535 || !DateTimeOffset.TryParseExact(match.Groups[4].Value,TimeFormat,
                CultureInfo.InvariantCulture,DateTimeStyles.None,out time)) return null;
        return new LogWriteDiagnostic(match.Groups[1].Value,match.Groups[2].Value,code,time,int.Parse(match.Groups[5].Value,CultureInfo.InvariantCulture));
    }
}
