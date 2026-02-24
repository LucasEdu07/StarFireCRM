using System.Windows;
using System.Windows.Input;

namespace ExtintorCrm.App.Presentation;

public partial class CobrancaWindow : Window
{
    public CobrancaAction SelectedAction { get; private set; } = CobrancaAction.None;

    public CobrancaWindow(CobrancaWindowModel model, Window? owner)
    {
        InitializeComponent();
        DataContext = model;
        Owner = owner;
    }

    private void WhatsApp_Click(object sender, RoutedEventArgs e)
    {
        SelectedAction = CobrancaAction.WhatsApp;
        DialogResult = true;
        Close();
    }

    private void Email_Click(object sender, RoutedEventArgs e)
    {
        SelectedAction = CobrancaAction.Email;
        DialogResult = true;
        Close();
    }

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        SelectedAction = CobrancaAction.Copy;
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        SelectedAction = CobrancaAction.None;
        Close();
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }
}

public enum CobrancaAction
{
    None,
    Copy,
    Email,
    WhatsApp
}

public sealed class CobrancaWindowModel
{
    public string ClienteNome { get; init; } = string.Empty;
    public string ContatoInfo { get; init; } = string.Empty;
    public string Mensagem { get; init; } = string.Empty;
    public bool CanSendEmail { get; init; }
    public bool CanSendWhatsApp { get; init; }
}
