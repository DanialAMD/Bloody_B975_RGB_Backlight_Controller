using System.Threading;
using System.Windows.Forms;
using B975RgbApp.Services;

namespace B975RgbApp;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        AppLanguage.SetLanguage(SettingsStore.Load().Language);
        using var instanceMutex = new Mutex(true, "Local\\B975RgbApp.SingleInstance", out var isFirstInstance);
        if (!isFirstInstance)
        {
            MessageBox.Show(
                AppLanguage.T(
                    "برنامه از قبل در حال اجراست. کنار ساعت ویندوز را بررسی کن.",
                    "The application is already running. Check the Windows system tray."),
                "Bloody B975 RGB",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information,
                MessageBoxDefaultButton.Button1,
                AppLanguage.IsEnglish
                    ? (MessageBoxOptions)0
                    : MessageBoxOptions.RtlReading | MessageBoxOptions.RightAlign);
            return;
        }

        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}
