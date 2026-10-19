using System;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;

// Windows .NET Framework verification of the built binaries. Never calls their
// entry points, initializes UIA, connects to an App, or invokes an approval.
internal static class LogRuntimeVerification
{
    const BindingFlags StaticPrivate=BindingFlags.Static|BindingFlags.NonPublic;
    static void Check(bool value,string message) { if(!value) throw new Exception(message); }
    static string CaptureAppend(MethodInfo append,string block,out Exception failure)
    {
        TextWriter original=Console.Out;
        var captured=new StringWriter(CultureInfo.InvariantCulture);
        failure=null;
        try
        {
            Console.SetOut(captured);
            try { append.Invoke(null,new object[] { block }); }
            catch(TargetInvocationException error) { failure=error.InnerException; }
        }
        finally { Console.SetOut(original); }
        return captured.ToString();
    }
    static void Feed(object diagnostics,string output)
    {
        MethodInfo read=diagnostics.GetType().GetMethod("Read");
        foreach(string line in output.Split(new char[] {'\r','\n'},StringSplitOptions.RemoveEmptyEntries))
            read.Invoke(diagnostics,new object[] { line });
    }
    public static int Main(string[] args)
    {
        if(args.Length!=3) throw new ArgumentException("Expected helper, tray and isolated fixture directory.");
        string root=Path.GetFullPath(args[2]);
        if(!root.StartsWith(@"C:\Temp\",StringComparison.OrdinalIgnoreCase)) throw new Exception("Unsafe runtime fixture directory");
        Directory.CreateDirectory(root);
        Type backend=Assembly.LoadFrom(Path.GetFullPath(args[0])).GetType("ManualProgram",true);
        Assembly frontend=Assembly.LoadFrom(Path.GetFullPath(args[1]));
        Type storeType=frontend.GetType("LocalLogDiagnostics",true), diagnosticType=frontend.GetType("WorkerDiagnostics",true);
        string logName="auto-20261019-000000-"+Guid.NewGuid().ToString("N")+".txt";
        string main=Path.Combine(root,logName);
        File.WriteAllText(main,"original\n");
        backend.GetField("log",StaticPrivate).SetValue(null,main);
        backend.GetField("lastScanMode",StaticPrivate).SetValue(null,"BACKGROUND");
        backend.GetField("lastScanWindowState",StaticPrivate).SetValue(null,"NORMAL");
        MethodInfo append=backend.GetMethod("AppendLog",StaticPrivate);
        Exception failure;
        var reader=new FileStream(main,FileMode.Open,FileAccess.Read,FileShare.Read);
        var release=new Thread(delegate() { Thread.Sleep(150); reader.Dispose(); });
        release.Start();
        string recovered;
        try { recovered=CaptureAppend(append,"once\n",out failure); }
        finally { release.Join(); reader.Dispose(); }
        Check(failure==null && File.ReadAllText(main)=="original\nonce\n"
            && recovered.Contains("RETRY|MAIN_OPEN|32|") && recovered.Contains("RECOVERED|MAIN_OPEN|32|"),
            "built net48 backend did not recover a transient main-file conflict exactly once");
        File.WriteAllText(Path.Combine(root,"recovered-protocol.txt"),recovered);
        Console.WriteLine("PASS: built net48 AppendLog recovered a Windows sharing conflict; one main record, first-failure/recovery protocol.");

        object store=Activator.CreateInstance(storeType,new object[] {Path.Combine(root,"independent-diagnostics")});
        ConstructorInfo ctor=diagnosticType.GetConstructors()[0];
        Delegate sink=Delegate.CreateDelegate(ctor.GetParameters()[0].ParameterType,store,storeType.GetMethod("Append"));
        object diagnostics=ctor.Invoke(new object[] {sink});
        Feed(diagnostics,"BACKEND_VERSION: 0.3.10\nLOG_NAME: "+logName);
        DateTimeOffset started=DateTimeOffset.Now;
        string failed;
        using(var locked=new FileStream(main,FileMode.Open,FileAccess.Read,FileShare.Read))
        {
            failed=CaptureAppend(append,"must not appear\n",out failure);
            Check(failure!=null && failure.GetType().Name=="LogWriteFailure"
                && failed.Contains("FAILURE|MAIN_OPEN|32|") && failed.Contains("LOG_WRITE_FAILURE: MAIN_OPEN:32"),
                "built backend did not report a bounded persistent open failure");
            Feed(diagnostics,failed);
            string diagnosticPath=(string)storeType.GetField("FilePath").GetValue(store);
            string disk=File.ReadAllText(diagnosticPath);
            Check(disk.Contains("kind=FAILURE stage=MAIN_OPEN win32=32 attempts=5 log="+logName),
                "built frontend did not save the failure while the main log remained locked");
            DateTimeOffset actual=DateTimeOffset.ParseExact(disk.Substring(disk.LastIndexOf("local_time=",StringComparison.Ordinal)+11,29),
                "yyyy-MM-dd'T'HH:mm:ss.fffzzz",CultureInfo.InvariantCulture);
            Check(actual>=started.AddMilliseconds(-1) && actual<=DateTimeOffset.Now,"failure time is not the actual backend failure time");
        }
        Check(File.ReadAllText(main)=="original\nonce\n","persistent failure modified or duplicated the main record");
        File.WriteAllText(Path.Combine(root,"failed-protocol.txt"),failed);
        string description=(string)diagnosticType.GetMethod("Describe").Invoke(diagnostics,new object[] {0});
        Check(description.Contains("MAIN_OPEN") && description.Contains("Windows 錯誤碼：32")
            && description.Contains("本機診斷已另存") && !description.Contains("失敗時間（本機）：未確認"),
            "built frontend omitted time, phase, native code or persistence status");
        File.WriteAllText(Path.Combine(root,"gui-description.txt"),description);
        using(var font=new Font("Microsoft JhengHei UI",10))
        {
            Size measured=TextRenderer.MeasureText(description,font,new Size(580,int.MaxValue),TextFormatFlags.WordBreak|TextFormatFlags.TextBoxControl|TextFormatFlags.NoPadding);
            Check(measured.Height<=140,"failure details exceed the expanded GUI label at this machine's font/DPI");
            Console.WriteLine("PASS: windowless failure-text measurement: "+measured.Height+" <= 140 pixels; this is not visual GUI acceptance.");
        }
        Console.WriteLine("PASS: built net48 backend-to-frontend protocol, exact local failure time, MAIN_OPEN/Win32 32 and independent on-disk diagnostics.");
        Console.WriteLine("No product entry point, App/UIA connection or approval action was executed.");
        return 0;
    }
}
