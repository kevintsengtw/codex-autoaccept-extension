using System;
using System.Globalization;
using System.IO;
using System.Security;
using System.Text;
using System.Threading;

// Independent of the worker's main/summary files. Failure to save diagnostics
// must not erase the metadata already retained by WorkerDiagnostics in memory.
internal sealed class LocalLogDiagnostics
{
    readonly object sync=new object();
    public readonly string FilePath;
    public LocalLogDiagnostics(string directory)
    {
        FilePath=Path.Combine(directory,"worker-"+DateTimeOffset.Now.ToString("yyyyMMdd-HHmmss",CultureInfo.InvariantCulture)
            +"-"+Guid.NewGuid().ToString("N")+"-diagnostics.txt");
    }
    public bool Append(LogWriteDiagnostic record,string logName)
    {
        lock(sync)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                FileStream stream;
                for(int attempt=0;;attempt++)
                {
                    try { stream=new FileStream(FilePath,FileMode.Append,FileAccess.Write,FileShare.Read); break; }
                    catch(IOException error)
                    {
                        int code=((uint)error.HResult & 0xffff0000u)==0x80070000u ? error.HResult & 0xffff : -1;
                        if((code!=32 && code!=33) || attempt>=4) throw;
                        Thread.Sleep(100);
                    }
                }
                using(stream)
                {
                    byte[] bytes=new UTF8Encoding(false).GetBytes(record.LocalText(logName));
                    stream.Write(bytes,0,bytes.Length);
                    stream.Flush();
                }
                return true;
            }
            catch(IOException) { return false; }
            catch(UnauthorizedAccessException) { return false; }
            catch(SecurityException) { return false; }
        }
    }
}
