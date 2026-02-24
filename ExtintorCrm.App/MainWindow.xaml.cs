using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using ExtintorCrm.App.Infrastructure.Logging;
using ExtintorCrm.App.Presentation;

namespace ExtintorCrm.App;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private const int WmNcLButtonDblClk = 0x00A3;
    private const int WmSysCommand = 0x0112;
    private const int ScSize = 0xF000;
    private const int ScMove = 0xF010;
    private const int ScKeyMenu = 0xF100;
    private const int ScRestore = 0xF120;

    private readonly ClientesViewModel _viewModel;
    private readonly List<TourStep> _tourSteps = [];
    private int _tourStepIndex = -1;
    private int _lastAutoScrolledStepIndex = -1;
    private bool _allowCloseWithoutConfirmation;

    public MainWindow()
    {
        InitializeComponent();
        _viewModel = new ClientesViewModel();
        DataContext = _viewModel;
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        var source = PresentationSource.FromVisual(this) as HwndSource;
        source?.AddHook(WndProc);

        if (_viewModel.LoadCommand.CanExecute(null))
        {
            _viewModel.LoadCommand.Execute(null);
        }

        SizeChanged += (_, _) =>
        {
            if (TourOverlay.Visibility == Visibility.Visible)
            {
                UpdateTourVisual();
            }
        };
    }

    private void ClientesDataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (_viewModel.DetailsCommand.CanExecute(null))
        {
            _viewModel.DetailsCommand.Execute(null);
        }
    }

    private void ClientesDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is not DataGrid grid)
        {
            return;
        }

        var selected = grid.SelectedItems
            .OfType<Domain.Cliente>()
            .ToList();

        _viewModel.UpdateSelectedClientes(selected);
    }

    private void ClientesSelectionCheckBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not CheckBox checkBox || checkBox.DataContext is not Domain.Cliente cliente)
        {
            return;
        }

        var grid = FindAncestor<DataGrid>(checkBox);
        if (grid == null)
        {
            return;
        }

        if (grid.SelectedItems.Contains(cliente))
        {
            grid.SelectedItems.Remove(cliente);
        }
        else
        {
            grid.SelectedItems.Add(cliente);
        }

        grid.SelectedItem = grid.SelectedItems.Cast<object>().FirstOrDefault();
        _viewModel.UpdateSelectedClientes(grid.SelectedItems.OfType<Domain.Cliente>().ToList());
        e.Handled = true;
    }

    private void ClientesDataGrid_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not DataGrid grid)
        {
            return;
        }

        var row = FindAncestor<DataGridRow>(e.OriginalSource as DependencyObject);
        if (row?.Item is not Domain.Cliente cliente)
        {
            return;
        }

        if (FindAncestor<CheckBox>(e.OriginalSource as DependencyObject) is not null)
        {
            return;
        }

        var modifiers = Keyboard.Modifiers;
        if (modifiers.HasFlag(ModifierKeys.Control) || modifiers.HasFlag(ModifierKeys.Shift))
        {
            return;
        }

        if (row.IsSelected)
        {
            grid.SelectedItems.Remove(cliente);
            if (ReferenceEquals(grid.SelectedItem, cliente))
            {
                grid.SelectedItem = null;
            }

            _viewModel.UpdateSelectedClientes(grid.SelectedItems.OfType<Domain.Cliente>().ToList());
            e.Handled = true;
        }
    }

    private void CriticalItemsDataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is not DataGrid dataGrid || dataGrid.SelectedItem is not DashboardAlertItem item)
        {
            return;
        }

        if (_viewModel.OpenDashboardItemCommand.CanExecute(item))
        {
            _viewModel.OpenDashboardItemCommand.Execute(item);
        }
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (_viewModel.IsFullscreen && msg == WmNcLButtonDblClk)
        {
            handled = true;
            return IntPtr.Zero;
        }

        if (_viewModel.IsFullscreen && msg == WmSysCommand)
        {
            var command = wParam.ToInt32() & 0xFFF0;
            if (command == ScMove || command == ScSize || command == ScRestore || command == ScKeyMenu)
            {
                handled = true;
                return IntPtr.Zero;
            }
        }

        return IntPtr.Zero;
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_viewModel.IsFullscreen)
        {
            return;
        }

        if (e.LeftButton == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (_allowCloseWithoutConfirmation)
        {
            return;
        }

        var confirm = DialogService.Confirm(
            "Confirmar saída",
            "Deseja realmente fechar o Star Fire CRM?",
            this);

        if (!confirm)
        {
            e.Cancel = true;
            return;
        }

        _allowCloseWithoutConfirmation = true;
    }

    private void HelpButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: string target })
        {
            return;
        }

        StartInteractiveTour(target);
    }

    private void StartInteractiveTour(string target)
    {
        _tourSteps.Clear();
        _tourSteps.AddRange(CreateInteractiveSteps(target).Where(s => s.Target is { IsVisible: true }));

        if (_tourSteps.Count == 0)
        {
            return;
        }

        _tourStepIndex = 0;
        _lastAutoScrolledStepIndex = -1;
        TourOverlay.Visibility = Visibility.Visible;
        UpdateTourVisual();
    }

    private IEnumerable<TourStep> CreateInteractiveSteps(string target)
    {
        return target switch
        {
            "Dashboard" =>
            [
                new TourStep(DashboardQuickActionsPanel, "Ações rápidas", "Use estes botões para ir direto para Clientes e Pagamentos."),
                new TourStep(DashboardKpiPanel, "KPIs principais", "Aqui ficam os indicadores executivos de vencidos e vencendo."),
                new TourStep(DashboardCriticalCard, "Críticos agora", "Lista os itens mais urgentes. Duplo clique abre o perfil do cliente.")
            ],
            "Clientes" =>
            [
                new TourStep(ClientesActionBar, "Barra de ações", "Novo, detalhes, edição e ações de sistema (Importar/Exportar/Backup)."),
                new TourStep(ClientesSearchCard, "Busca", "Filtre por nome, CPF/CNPJ ou telefone para localizar rapidamente."),
                new TourStep(ClientesStatusTabsCard, "Ativos e inativos", "Alterne a visualização para gerenciar a base por status."),
                new TourStep(ClientesGridCard, "Listagem", "Grade principal com situação e vencimentos dos clientes.")
            ],
            "Pagamentos" =>
            [
                new TourStep(PagamentosActionBar, "Filtro e ações", "Filtre por status e execute criar/editar/excluir pagamentos."),
                new TourStep(PagamentosGridCard, "Tabela de pagamentos", "Acompanhe vencimento, valor, status e situação.")
            ],
            "Configuracoes" =>
            [
                new TourStep(ConfigUpdateCard, "Atualização do aplicativo", "Selecione o instalador da nova versão para atualizar com segurança sem perder dados."),
                new TourStep(ConfigAppearanceCard, "Aparência", "Controle o tema claro/escuro e o modo de janela."),
                new TourStep(ConfigBackupCard, "Backup", "Defina pasta, frequência e execute backup/restauração."),
                new TourStep(ConfigAlertsCard, "Alertas", "Configure os prazos de aviso para vencimentos."),
                new TourStep(ConfigSaveButton, "Salvar", "Persista as alterações de configuração.")
            ],
            _ => []
        };
    }

    private void UpdateTourVisual()
    {
        if (_tourStepIndex < 0 || _tourStepIndex >= _tourSteps.Count)
        {
            return;
        }

        var step = _tourSteps[_tourStepIndex];
        if (step.Target is not FrameworkElement target || !target.IsVisible)
        {
            return;
        }

        if (EnsureTargetVisibleOnce(target))
        {
            Dispatcher.InvokeAsync(UpdateTourVisual, DispatcherPriority.Background);
            return;
        }

        target.UpdateLayout();
        MainRootGrid.UpdateLayout();
        TourCanvas.UpdateLayout();

        var bounds = GetElementBounds(target, TourCanvas);
        if (bounds.Width < 1 || bounds.Height < 1)
        {
            return;
        }

        TourTitleText.Text = step.Title;
        TourDescriptionText.Text = step.Description;
        TourStepCounterText.Text = $"Etapa {_tourStepIndex + 1} de {_tourSteps.Count}";
        TourPrevButton.IsEnabled = _tourStepIndex > 0;
        TourNextButton.IsEnabled = _tourStepIndex < _tourSteps.Count - 1;

        Canvas.SetLeft(TourTargetHighlight, bounds.X - 4);
        Canvas.SetTop(TourTargetHighlight, bounds.Y - 4);
        TourTargetHighlight.Width = bounds.Width + 8;
        TourTargetHighlight.Height = bounds.Height + 8;

        var canvasWidth = Math.Max(TourCanvas.ActualWidth, ActualWidth);
        var canvasHeight = Math.Max(TourCanvas.ActualHeight, ActualHeight);

        TourCallout.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var calloutWidth = Math.Max(300, TourCallout.DesiredSize.Width);
        var calloutHeight = Math.Max(170, TourCallout.DesiredSize.Height);

        const double margin = 16;
        const double gap = 20;

        var rightSpace = canvasWidth - bounds.Right - margin;
        var leftSpace = bounds.Left - margin;
        var bottomSpace = canvasHeight - bounds.Bottom - margin;
        var topSpace = bounds.Top - margin;

        var placement = TourPlacement.Right;
        if (rightSpace < calloutWidth + gap)
        {
            if (leftSpace >= calloutWidth + gap)
            {
                placement = TourPlacement.Left;
            }
            else if (bottomSpace >= calloutHeight + gap)
            {
                placement = TourPlacement.Bottom;
            }
            else if (topSpace >= calloutHeight + gap)
            {
                placement = TourPlacement.Top;
            }
            else
            {
                placement = rightSpace >= leftSpace
                    ? (bottomSpace >= topSpace ? TourPlacement.Bottom : TourPlacement.Top)
                    : TourPlacement.Left;
            }
        }

        double calloutLeft;
        double calloutTop;

        switch (placement)
        {
            case TourPlacement.Right:
                calloutLeft = Math.Clamp(bounds.Right + gap, margin, Math.Max(margin, canvasWidth - calloutWidth - margin));
                calloutTop = Math.Clamp(bounds.Top + (bounds.Height / 2) - (calloutHeight / 2), 56, Math.Max(56, canvasHeight - calloutHeight - margin));
                TourArrowText.Text = ">";
                Canvas.SetLeft(TourArrowText, bounds.Right + 3);
                Canvas.SetTop(TourArrowText, bounds.Top + (bounds.Height / 2) - 12);
                break;
            case TourPlacement.Left:
                calloutLeft = Math.Clamp(bounds.Left - calloutWidth - gap, margin, Math.Max(margin, canvasWidth - calloutWidth - margin));
                calloutTop = Math.Clamp(bounds.Top + (bounds.Height / 2) - (calloutHeight / 2), 56, Math.Max(56, canvasHeight - calloutHeight - margin));
                TourArrowText.Text = "<";
                Canvas.SetLeft(TourArrowText, bounds.Left - 14);
                Canvas.SetTop(TourArrowText, bounds.Top + (bounds.Height / 2) - 12);
                break;
            case TourPlacement.Bottom:
                calloutLeft = Math.Clamp(bounds.Left + (bounds.Width / 2) - (calloutWidth / 2), margin, Math.Max(margin, canvasWidth - calloutWidth - margin));
                calloutTop = Math.Clamp(bounds.Bottom + gap, 56, Math.Max(56, canvasHeight - calloutHeight - margin));
                TourArrowText.Text = "v";
                Canvas.SetLeft(TourArrowText, bounds.Left + (bounds.Width / 2) - 6);
                Canvas.SetTop(TourArrowText, bounds.Bottom + 2);
                break;
            default:
                calloutLeft = Math.Clamp(bounds.Left + (bounds.Width / 2) - (calloutWidth / 2), margin, Math.Max(margin, canvasWidth - calloutWidth - margin));
                calloutTop = Math.Clamp(bounds.Top - calloutHeight - gap, 56, Math.Max(56, canvasHeight - calloutHeight - margin));
                TourArrowText.Text = "^";
                Canvas.SetLeft(TourArrowText, bounds.Left + (bounds.Width / 2) - 6);
                Canvas.SetTop(TourArrowText, bounds.Top - 16);
                break;
        }

        Canvas.SetLeft(TourCallout, calloutLeft);
        Canvas.SetTop(TourCallout, calloutTop);
        TourCallout.Width = calloutWidth;
    }

    private static Rect GetElementBounds(FrameworkElement target, Visual relativeTo)
    {
        var transform = target.TransformToVisual(relativeTo);
        var topLeft = transform.Transform(new Point(0, 0));
        return new Rect(topLeft.X, topLeft.Y, target.ActualWidth, target.ActualHeight);
    }

    private static T? FindAncestor<T>(DependencyObject? start) where T : DependencyObject
    {
        var current = start;
        while (current is not null)
        {
            if (current is T found)
            {
                return found;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return null;
    }

    private bool EnsureTargetVisibleOnce(FrameworkElement target)
    {
        if (_lastAutoScrolledStepIndex == _tourStepIndex)
        {
            return false;
        }

        var scrollViewer = FindAncestor<ScrollViewer>(target);
        if (scrollViewer is null)
        {
            return false;
        }

        target.BringIntoView(new Rect(0, 0, Math.Max(1, target.ActualWidth), Math.Max(1, target.ActualHeight)));
        scrollViewer.UpdateLayout();

        if (scrollViewer.ViewportHeight > 0)
        {
            var targetRect = target.TransformToAncestor(scrollViewer)
                .TransformBounds(new Rect(0, 0, Math.Max(1, target.ActualWidth), Math.Max(1, target.ActualHeight)));

            const double topComfort = 36;
            const double bottomComfort = 28;

            var desiredOffset = scrollViewer.VerticalOffset;
            var viewportBottom = scrollViewer.ViewportHeight - bottomComfort;

            if (targetRect.Top < topComfort)
            {
                desiredOffset += targetRect.Top - topComfort;
            }
            else if (targetRect.Bottom > viewportBottom)
            {
                desiredOffset += targetRect.Bottom - viewportBottom;
            }

            desiredOffset = Math.Max(0, desiredOffset);
            if (Math.Abs(desiredOffset - scrollViewer.VerticalOffset) > 0.5)
            {
                scrollViewer.ScrollToVerticalOffset(desiredOffset);
                scrollViewer.UpdateLayout();
            }
        }

        _lastAutoScrolledStepIndex = _tourStepIndex;
        return true;
    }

    private void TourPrevButton_Click(object sender, RoutedEventArgs e)
    {
        if (_tourStepIndex <= 0)
        {
            return;
        }

        _tourStepIndex--;
        UpdateTourVisual();
    }

    private void TourNextButton_Click(object sender, RoutedEventArgs e)
    {
        if (_tourStepIndex >= _tourSteps.Count - 1)
        {
            return;
        }

        _tourStepIndex++;
        UpdateTourVisual();
    }

    private void TourCloseButton_Click(object sender, RoutedEventArgs e)
    {
        CloseTour();
    }

    private void CloseTour()
    {
        TourOverlay.Visibility = Visibility.Collapsed;
        _tourSteps.Clear();
        _tourStepIndex = -1;
        _lastAutoScrolledStepIndex = -1;
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (TourOverlay.Visibility != Visibility.Visible)
        {
            return;
        }

        switch (e.Key)
        {
            case Key.Escape:
                CloseTour();
                e.Handled = true;
                break;
            case Key.Left:
                TourPrevButton_Click(sender, e);
                e.Handled = true;
                break;
            case Key.Right:
                TourNextButton_Click(sender, e);
                e.Handled = true;
                break;
        }
    }

    private void UpdateAppButton_Click(object sender, RoutedEventArgs e)
    {
        var picker = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Selecione o instalador da nova versão",
            Filter = "Executável (*.exe)|*.exe",
            CheckFileExists = true,
            Multiselect = false
        };

        if (picker.ShowDialog() != true)
        {
            return;
        }

        var selectedInstaller = picker.FileName;
        if (string.IsNullOrWhiteSpace(selectedInstaller) || !File.Exists(selectedInstaller))
        {
            DialogService.Info("Atualização", "Arquivo selecionado inválido.", this);
            return;
        }

        var fileName = Path.GetFileName(selectedInstaller);
        if (string.IsNullOrWhiteSpace(fileName) ||
            !fileName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ||
            !fileName.StartsWith("StarFire-Setup-", StringComparison.OrdinalIgnoreCase))
        {
            DialogService.Info(
                "Atualização",
                "Selecione um instalador válido no padrão 'StarFire-Setup-<versão>.exe'.",
                this);
            return;
        }

        var confirm = DialogService.Confirm(
            "Atualizar aplicativo",
            "O Star Fire CRM será fechado para executar o instalador selecionado. Deseja continuar?",
            this);

        if (!confirm)
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = selectedInstaller,
                UseShellExecute = true,
                WorkingDirectory = Path.GetDirectoryName(selectedInstaller) ?? string.Empty
            });

            _allowCloseWithoutConfirmation = true;
            Application.Current.Shutdown();
        }
        catch (Exception ex)
        {
            AppLogger.Error("Falha ao iniciar instalador de atualização.", ex);
            DialogService.Error(
                "Atualização",
                $"Não foi possível iniciar o instalador: {ex.Message}",
                this);
        }
    }

    private enum TourPlacement
    {
        Right,
        Left,
        Bottom,
        Top
    }

    private sealed record TourStep(FrameworkElement Target, string Title, string Description);
}




