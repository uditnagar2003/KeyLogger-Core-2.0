
namespace VisualKeyloggerDetector
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            // To customize application configuration such as set high DPI settings or default font,
            // see https://aka.ms/applicationconfiguration.
            VisualKeyloggerDetector.ApplicationConfiguration.Initialize();
            Application.Run(new MainForm());
        }
    }
}