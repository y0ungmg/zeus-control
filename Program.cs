using System.Runtime.InteropServices;

namespace ZeusControl;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            if (args.Contains("--self-test", StringComparer.OrdinalIgnoreCase))
            {
                using var form = new MainForm(selfTest: true);
                form.CreateControl();
                if (!form.RunSelfTest(out var failure))
                {
                    Console.Error.WriteLine(failure);
                    return 2;
                }
                return 0;
            }

            Application.Run(new MainForm(selfTest: false));
            return 0;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Program nie mógł się uruchomić.\n\n{ex}", "ZEUS CONTROL — błąd startu", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return 1;
        }
    }
}
