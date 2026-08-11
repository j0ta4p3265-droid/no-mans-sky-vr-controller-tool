namespace NMSOpenCompositeConfigurator;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
#if DEBUG
        var settingsSelfTestIndex = Array.FindIndex(args, value =>
            value.Equals("--settings-self-test", StringComparison.OrdinalIgnoreCase));
        if (settingsSelfTestIndex >= 0 && settingsSelfTestIndex + 3 < args.Length)
        {
            SelfTest.RunSettingsAndLocator(
                args[settingsSelfTestIndex + 1],
                args[settingsSelfTestIndex + 2],
                args[settingsSelfTestIndex + 3]);
            return;
        }
        var selfTestIndex = Array.FindIndex(args, value => value.Equals("--self-test", StringComparison.OrdinalIgnoreCase));
        if (selfTestIndex >= 0 && selfTestIndex + 2 < args.Length)
        {
            SelfTest.Run(args[selfTestIndex + 1], args[selfTestIndex + 2]);
            return;
        }
#endif
        var form = new MainForm();
#if DEBUG
        var screenshotIndex = Array.FindIndex(args, value => value.Equals("--screenshot", StringComparison.OrdinalIgnoreCase));
        var tabIndex = Array.FindIndex(args, value => value.Equals("--tab", StringComparison.OrdinalIgnoreCase));
        if (tabIndex >= 0 && tabIndex + 1 < args.Length)
            form.SelectPreviewTab(args[tabIndex + 1]);
        if (screenshotIndex >= 0 && screenshotIndex + 1 < args.Length)
        {
            form.Shown += async (_, _) =>
            {
                await Task.Delay(1200);
                using var bitmap = new Bitmap(form.ClientSize.Width, form.ClientSize.Height);
                form.DrawToBitmap(bitmap, form.ClientRectangle);
                bitmap.Save(args[screenshotIndex + 1], System.Drawing.Imaging.ImageFormat.Png);
                form.Close();
            };
        }
#endif
        Application.Run(form);
    }
}
