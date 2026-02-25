using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
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

    private void ApplyTemplate_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is CobrancaWindowModel model)
        {
            model.ApplySuggestedMessage();
        }
    }

    private void MensagemTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (DataContext is CobrancaWindowModel model)
        {
            model.MarkMessageAsEdited();
        }
    }

    private void Register_Click(object sender, RoutedEventArgs e)
    {
        SelectedAction = CobrancaAction.Register;
        DialogResult = true;
        Close();
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
    Register,
    Copy,
    Email,
    WhatsApp
}

public sealed class CobrancaWindowModel : ViewModelBase
{
    private readonly Func<string, string, string> _messageFactory;
    private string _etapaSelecionada;
    private string _tomSelecionado;
    private string _mensagem;
    private bool _messageEdited;
    private bool _isApplyingSuggestion;
    private bool _suppressFirstTextChanged;

    public CobrancaWindowModel(
        string clienteNome,
        string contatoInfo,
        string valorResumo,
        string vencimentoResumo,
        string prazoResumo,
        bool canSendEmail,
        bool canSendWhatsApp,
        IReadOnlyCollection<string> etapaOptions,
        string etapaSelecionada,
        IReadOnlyCollection<string> tomOptions,
        string tomSelecionado,
        IReadOnlyCollection<string>? historicoItens,
        Func<string, string, string> messageFactory)
    {
        ClienteNome = clienteNome;
        ContatoInfo = contatoInfo;
        ValorResumo = valorResumo;
        VencimentoResumo = vencimentoResumo;
        PrazoResumo = prazoResumo;
        CanSendEmail = canSendEmail;
        CanSendWhatsApp = canSendWhatsApp;
        _messageFactory = messageFactory;

        EtapaOptions = new ObservableCollection<string>(
            etapaOptions != null && etapaOptions.Count > 0
                ? etapaOptions
                : new[] { "Lembrete preventivo", "Vencimento hoje", "Atraso leve", "Atraso critico", "Negociacao" });

        TomOptions = new ObservableCollection<string>(
            tomOptions != null && tomOptions.Count > 0
                ? tomOptions
                : new[] { "Cordial", "Profissional", "Urgente" });

        HistoricoItens = new ObservableCollection<string>(historicoItens ?? Array.Empty<string>());

        _etapaSelecionada = string.IsNullOrWhiteSpace(etapaSelecionada)
            ? EtapaOptions.FirstOrDefault() ?? "Lembrete preventivo"
            : etapaSelecionada;

        _tomSelecionado = string.IsNullOrWhiteSpace(tomSelecionado)
            ? TomOptions.FirstOrDefault() ?? "Profissional"
            : tomSelecionado;

        _mensagem = string.Empty;
        _suppressFirstTextChanged = true;
        ApplySuggestedMessage();
    }

    public string ClienteNome { get; }
    public string ContatoInfo { get; }
    public string ValorResumo { get; }
    public string VencimentoResumo { get; }
    public string PrazoResumo { get; }
    public bool CanSendEmail { get; }
    public bool CanSendWhatsApp { get; }
    public ObservableCollection<string> EtapaOptions { get; }
    public ObservableCollection<string> TomOptions { get; }
    public ObservableCollection<string> HistoricoItens { get; }

    public bool HasHistorico => HistoricoItens.Count > 0;

    public string EtapaSelecionada
    {
        get => _etapaSelecionada;
        set
        {
            var normalized = string.IsNullOrWhiteSpace(value) ? _etapaSelecionada : value;
            if (string.Equals(_etapaSelecionada, normalized, StringComparison.Ordinal))
            {
                return;
            }

            _etapaSelecionada = normalized;
            OnPropertyChanged();
            AutoRefreshSuggestedMessage();
        }
    }

    public string TomSelecionado
    {
        get => _tomSelecionado;
        set
        {
            var normalized = string.IsNullOrWhiteSpace(value) ? _tomSelecionado : value;
            if (string.Equals(_tomSelecionado, normalized, StringComparison.Ordinal))
            {
                return;
            }

            _tomSelecionado = normalized;
            OnPropertyChanged();
            AutoRefreshSuggestedMessage();
        }
    }

    public string Mensagem
    {
        get => _mensagem;
        set
        {
            var normalized = value ?? string.Empty;
            if (string.Equals(_mensagem, normalized, StringComparison.Ordinal))
            {
                return;
            }

            _mensagem = normalized;
            OnPropertyChanged();

            if (_suppressFirstTextChanged)
            {
                _suppressFirstTextChanged = false;
                return;
            }

            if (!_isApplyingSuggestion)
            {
                _messageEdited = true;
            }
        }
    }

    public void ApplySuggestedMessage()
    {
        _isApplyingSuggestion = true;
        try
        {
            Mensagem = _messageFactory(EtapaSelecionada, TomSelecionado);
            _messageEdited = false;
        }
        finally
        {
            _isApplyingSuggestion = false;
        }
    }

    public void MarkMessageAsEdited()
    {
        if (_isApplyingSuggestion)
        {
            return;
        }

        if (_suppressFirstTextChanged)
        {
            _suppressFirstTextChanged = false;
            return;
        }

        _messageEdited = true;
    }

    private void AutoRefreshSuggestedMessage()
    {
        if (_messageEdited)
        {
            return;
        }

        ApplySuggestedMessage();
    }
}
