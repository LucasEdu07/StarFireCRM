using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Animation;

namespace ExtintorCrm.App.Presentation;

public partial class SplashScreenWindow : Window
{
    public SplashScreenWindow()
    {
        InitializeComponent();
    }

    public void SetStatus(string message)
    {
        StatusText.Text = string.IsNullOrWhiteSpace(message) ? "Inicializando..." : message.Trim();
    }

    public Task FadeOutAndCloseAsync(int durationMs = 170)
    {
        if (!IsVisible)
        {
            return Task.CompletedTask;
        }

        var closeTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var fade = new DoubleAnimation
        {
            From = Opacity,
            To = 0,
            Duration = TimeSpan.FromMilliseconds(durationMs),
            FillBehavior = FillBehavior.Stop
        };

        fade.Completed += (_, _) =>
        {
            try
            {
                Close();
            }
            finally
            {
                closeTcs.TrySetResult(true);
            }
        };

        BeginAnimation(OpacityProperty, fade);
        return closeTcs.Task;
    }
}
