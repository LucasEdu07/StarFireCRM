using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;

namespace ExtintorCrm.App.Presentation;

public partial class HelpTourWindow : Window
{
    private readonly IReadOnlyList<HelpTourStep> _steps;
    private int _currentIndex;

    public HelpTourWindow(string telaNome, IReadOnlyList<HelpTourStep> steps, Window? owner)
    {
        InitializeComponent();

        _steps = steps.Count > 0
            ? steps
            : new List<HelpTourStep> { new("Ajuda", "Nenhuma orientação disponível para esta tela.") };

        Owner = owner;
        WindowTitleText.Text = $"Ajuda - {telaNome}";
        TourTitleText.Text = $"Tour guiado: {telaNome}";
        UpdateStep();
    }

    private void UpdateStep()
    {
        var step = _steps[_currentIndex];
        StepCounterText.Text = $"Etapa {_currentIndex + 1} de {_steps.Count}";
        StepDescriptionText.Text = $"{step.Titulo}\n\n{step.Descricao}";
        PreviousButton.IsEnabled = _currentIndex > 0;
        NextButton.IsEnabled = _currentIndex < _steps.Count - 1;
    }

    private void PreviousButton_Click(object sender, RoutedEventArgs e)
    {
        if (_currentIndex <= 0)
        {
            return;
        }

        _currentIndex--;
        UpdateStep();
    }

    private void NextButton_Click(object sender, RoutedEventArgs e)
    {
        if (_currentIndex >= _steps.Count - 1)
        {
            return;
        }

        _currentIndex++;
        UpdateStep();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Escape:
                Close();
                e.Handled = true;
                break;
            case Key.Left:
                PreviousButton_Click(sender, e);
                e.Handled = true;
                break;
            case Key.Right:
                NextButton_Click(sender, e);
                e.Handled = true;
                break;
        }
    }
}

public sealed record HelpTourStep(string Titulo, string Descricao);
