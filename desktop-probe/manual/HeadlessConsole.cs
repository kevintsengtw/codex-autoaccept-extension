using System;
using System.IO;
using System.Text;

internal static class HeadlessConsole
{
    // Do not set Console.OutputEncoding/InputEncoding: Framework setters call
    // SetConsoleOutputCP/SetConsoleCP even when a console is absent.
    public static void Configure()
    {
        var utf8 = new UTF8Encoding(false);
        Console.SetIn(new StreamReader(Console.OpenStandardInput(), utf8, true));
        Console.SetOut(new StreamWriter(Console.OpenStandardOutput(), utf8) { AutoFlush = true });
        Console.SetError(new StreamWriter(Console.OpenStandardError(), utf8) { AutoFlush = true });
    }
}
