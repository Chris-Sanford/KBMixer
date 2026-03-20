namespace KBMixer
{
    internal static class Program
    {
        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            // To customize application configuration such as set high DPI settings or default font,
            // see https://aka.ms/applicationconfiguration.
            ApplicationConfiguration.Initialize();

            if (args.Any(a => string.Equals(a, "--list-audio-sessions", StringComparison.OrdinalIgnoreCase)))
            {
                string path = Audio.WriteSessionDiagnosticFile();
                MessageBox.Show(
                    "Wrote a dump of every render device and WASAPI session to:\n\n" + path,
                    "KBMixer — audio session diagnostics",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            bool startMinimized = args.Any(a =>
                string.Equals(a, StartupRegistration.MinimizedCommandLineFlag, StringComparison.OrdinalIgnoreCase));

            Application.Run(new Form1(startMinimized));
        }
    }
}