using System.Runtime.InteropServices;

namespace ZeusControl;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        var isSelfTest = args.Contains("--self-test", StringComparer.OrdinalIgnoreCase);
        try
        {
            Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            if (isSelfTest)
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
            if (isSelfTest)
            {
                Console.Error.WriteLine(ex);
                return 1;
            }
            MessageBox.Show($"Program nie mógł się uruchomić.\n\n{ex}", "ZEUS CONTROL — błąd startu", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return 1;
        }
    }
}
