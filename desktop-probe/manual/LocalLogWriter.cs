using System;
using System.IO;
using System.Security;
using System.Text;
using System.Threading;

internal sealed class LogWriteFailure : IOException
{
    public readonly string Stage;
    public readonly int NativeError;
    public readonly DateTimeOffset Time;
    public readonly int Attempts;
    public LogWriteFailure(string stage, Exception cause, DateTimeOffset? time=null, int attempts=1) : base("Local log write failed.", cause)
    {
        Stage = stage;
        Time = time ?? DateTimeOffset.Now;
        Attempts = attempts;
        // Security and general managed I/O errors need not contain a Win32 code.
        // Do not mislabel the low 16 bits of another HRESULT facility as Win32.
        NativeError = ((uint)cause.HResult & 0xffff0000u) == 0x80070000u ? cause.HResult & 0xffff : -1;
    }
    public LogWriteDiagnostic Diagnostic(string kind) => LogWriteDiagnostic.Create(kind,Stage,NativeError,Time,Attempts);
}

internal static class LocalLogWriter
{
    static bool IsFileWriteFailure(Exception error) => error is IOException
        || error is UnauthorizedAccessException || error is SecurityException;
    internal static void AppendMain(string path, string block, Action<LogWriteDiagnostic> report=null,
        Func<DateTimeOffset> localNow=null, Func<string,Stream> open=null)
    {
        var now=localNow ?? (() => DateTimeOffset.Now);
        var openStream=open ?? (p => new FileStream(p,FileMode.Append,FileAccess.Write,FileShare.Read));
        Stream stream;
        LogWriteFailure lastConflict=null;
        int attempts=0;
        while (true)
        {
            attempts++;
            try { stream=openStream(path); break; }
            catch (Exception e) when (IsFileWriteFailure(e))
            {
                var failure=new LogWriteFailure("MAIN_OPEN",e,now(),attempts);
                if (!(e is IOException) || (failure.NativeError!=32 && failure.NativeError!=33) || attempts>=5) throw failure;
                if (lastConflict==null) report?.Invoke(failure.Diagnostic("RETRY"));
                lastConflict=failure;
                Thread.Sleep(100);
            }
        }
        // Only opening an untouched stream is retried. A write/flush/dispose
        // failure may have persisted part of a record and must never be replayed.
        try
        {
            using (stream)
            {
                byte[] bytes=new UTF8Encoding(false).GetBytes(block);
                stream.Write(bytes,0,bytes.Length);
                stream.Flush();
            }
        }
        catch (Exception e) when (IsFileWriteFailure(e)) { throw new LogWriteFailure("MAIN_APPEND",e,now(),attempts); }
        if (lastConflict!=null) report?.Invoke(LogWriteDiagnostic.Create("RECOVERED","MAIN_OPEN",lastConflict.NativeError,now(),attempts));
    }
    public static void AppendWithSummaryFallback(string path, string block, string summaryText,
        Func<LogWriteFailure, string> describe, Action<LogWriteFailure> warn, Action<LogWriteDiagnostic> report=null)
    {
        try { Append(path, block, summaryText,report); }
        catch (LogWriteFailure e)
        {
            if (e.Stage == "MAIN_APPEND" || e.Stage == "MAIN_OPEN") throw;
            // Preserve the original summary cause even if its main-log warning
            // subsequently fails. The frontend uses a separate local directory.
            report?.Invoke(e.Diagnostic("WARNING"));
            // The complete record already exists. Persist the summary warning in
            // the authoritative main log; never replay the original append/action.
            string warning = describe(e);
            if (warning != null) AppendMain(path, warning,report);
            warn(e);
        }
    }
    public static void Append(string path, string block, string summaryText, Action<LogWriteDiagnostic> report=null)
    {
        // Never retry the append: a partial write must not duplicate evidence.
        AppendMain(path, block,report);
        string summary = Path.ChangeExtension(path, null) + "-summary.txt", temp = summary + ".tmp";
        try { File.WriteAllText(temp, summaryText, new UTF8Encoding(true)); }
        catch (Exception e) when (IsFileWriteFailure(e)) { throw new LogWriteFailure("SUMMARY_WRITE", e); }
        // Readers/scanners can briefly deny delete-sharing on Windows. Only
        // retry the atomic summary replacement, never an approval or main append.
        for (int attempt = 0; ; attempt++)
        {
            try
            {
                if (File.Exists(summary)) File.Replace(temp, summary, null);
                else File.Move(temp, summary);
                return;
            }
            catch (Exception e) when (IsFileWriteFailure(e))
            {
                var failure = new LogWriteFailure("SUMMARY_REPLACE", e,attempts:attempt+1);
                if (!(e is IOException) || (failure.NativeError != 32 && failure.NativeError != 33) || attempt >= 4)
                    throw failure;
                Thread.Sleep(100);
            }
        }
    }
}
