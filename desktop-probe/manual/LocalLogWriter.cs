using System;
using System.IO;
using System.Security;
using System.Text;
using System.Threading;

internal sealed class LogWriteFailure : IOException
{
    public readonly string Stage;
    public readonly int NativeError;
    public LogWriteFailure(string stage, Exception cause) : base("Local log write failed.", cause)
    {
        Stage = stage;
        // Security and general managed I/O errors need not contain a Win32 code.
        // Do not mislabel the low 16 bits of another HRESULT facility as Win32.
        NativeError = ((uint)cause.HResult & 0xffff0000u) == 0x80070000u ? cause.HResult & 0xffff : -1;
    }
}

internal static class LocalLogWriter
{
    static bool IsFileWriteFailure(Exception error) => error is IOException
        || error is UnauthorizedAccessException || error is SecurityException;
    static void AppendMain(string path, string block)
    {
        try { File.AppendAllText(path, block, new UTF8Encoding(false)); }
        catch (Exception e) when (IsFileWriteFailure(e)) { throw new LogWriteFailure("MAIN_APPEND", e); }
    }
    public static void AppendWithSummaryFallback(string path, string block, string summaryText,
        Func<LogWriteFailure, string> describe, Action<LogWriteFailure> warn)
    {
        try { Append(path, block, summaryText); }
        catch (LogWriteFailure e)
        {
            if (e.Stage == "MAIN_APPEND") throw;
            // The complete record already exists. Persist the summary warning in
            // the authoritative main log; never replay the original append/action.
            string warning = describe(e);
            if (warning != null) AppendMain(path, warning);
            warn(e);
        }
    }
    public static void Append(string path, string block, string summaryText)
    {
        // Never retry the append: a partial write must not duplicate evidence.
        AppendMain(path, block);
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
                var failure = new LogWriteFailure("SUMMARY_REPLACE", e);
                if (!(e is IOException) || (failure.NativeError != 32 && failure.NativeError != 33) || attempt >= 4)
                    throw failure;
                Thread.Sleep(100);
            }
        }
    }
}
