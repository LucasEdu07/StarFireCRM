using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace ExtintorCrm.App.Presentation;

public partial class CobrancaWindow : Window
{
    private bool _allowCloseWithoutPrompt;

    public CobrancaAction SelectedAction { get; private set; } = CobrancaAction.None;

    public CobrancaWindow(CobrancaWindowModel model, Window? owner)
    {
        InitializeComponent();
        DataContext = model;
        Owner = owner;
        Title = "Cobran\u00E7a Inteligente";
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
        _allowCloseWithoutPrompt = true;
        DialogResult = true;
        Close();
    }

    private void WhatsApp_Click(object sender, RoutedEventArgs e)
    {
        SelectedAction = CobrancaAction.WhatsApp;
        _allowCloseWithoutPrompt = true;
        DialogResult = true;
        Close();
    }

    private void WhatsAppAutoPreview_Click(object sender, RoutedEventArgs e)
    {
        DialogService.Info(
            "Em breve",
            "A funcionalidade de cobran\u00E7a WhatsApp autom\u00E1tica est\u00E1 em desenvolvimento e ser\u00E1 liberada em uma pr\u00F3xima vers\u00E3o.",
            this);
    }

    private void Email_Click(object sender, RoutedEventArgs e)
    {
        SelectedAction = CobrancaAction.Email;
        _allowCloseWithoutPrompt = true;
        DialogResult = true;
        Close();
    }

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not CobrancaWindowModel model)
        {
            return;
        }

        var message = string.IsNullOrWhiteSpace(model.Mensagem)
            ? string.Empty
            : model.Mensagem.Trim();

        if (string.IsNullOrWhiteSpace(message))
        {
            model.SetCopyFeedback("Digite uma mensagem antes de copiar.");
            return;
        }

        try
        {
            Clipboard.SetText(message);
            model.SetCopyFeedback("Mensagem copiada para a \u00E1rea de transfer\u00EAncia.");
        }
        catch
        {
            model.SetCopyFeedback("N\u00E3o foi poss\u00EDvel copiar a mensagem agora.");
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        if (!CanDiscardChanges())
        {
            return;
        }

        SelectedAction = CobrancaAction.None;
        _allowCloseWithoutPrompt = true;
        Close();
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (_allowCloseWithoutPrompt)
        {
            return;
        }

        if (!CanDiscardChanges())
        {
            e.Cancel = true;
            return;
        }

        SelectedAction = CobrancaAction.None;
        _allowCloseWithoutPrompt = true;
    }

    private bool CanDiscardChanges()
    {
        if (DataContext is not CobrancaWindowModel model)
        {
            return true;
        }

        if (!model.HasUnsavedEdits)
        {
            return true;
        }

        return DialogService.Confirm(
            "Descartar altera\u00E7\u00F5es",
            "Voc\u00EA editou a mensagem de cobran\u00E7a. Deseja sair sem salvar essa edi\u00E7\u00E3o?",
            this);
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
    private string _copyFeedback = string.Empty;

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

        HistoricoItens = new ObservableCollection<CobrancaHistoryItemViewModel>(
            (historicoItens ?? Array.Empty<string>())
            .Select(ParseHistoryItem));

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
    public ObservableCollection<CobrancaHistoryItemViewModel> HistoricoItens { get; }

    public bool HasHistorico => HistoricoItens.Count > 0;

    public string ClienteInitials
    {
        get
        {
            if (string.IsNullOrWhiteSpace(ClienteNome))
            {
                return "??";
            }

            var parts = ClienteNome
                .Trim()
                .Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length == 0)
            {
                return "??";
            }

            if (parts.Length == 1)
            {
                return parts[0].Substring(0, Math.Min(2, parts[0].Length)).ToUpperInvariant();
            }

            var first = parts[0][0];
            var last = parts[^1][0];
            return string.Concat(char.ToUpperInvariant(first), char.ToUpperInvariant(last));
        }
    }

    public int MessageLength => string.IsNullOrEmpty(Mensagem) ? 0 : Mensagem.Trim().Length;

    public bool IsMessageEmpty => MessageLength == 0;

    public bool IsMessageTooShort => !IsMessageEmpty && MessageLength < 35;

    public bool IsMessageTooLong => MessageLength > 500;

    public string MessageQualityHint
    {
        get
        {
            if (IsMessageEmpty)
            {
                return "Escreva a mensagem para habilitar os canais.";
            }

            if (IsMessageTooShort)
            {
                return "Mensagem curta: adicione contexto para maior clareza.";
            }

            if (IsMessageTooLong)
            {
                return "Mensagem longa: tente resumir para facilitar a leitura.";
            }

            return "Mensagem pronta para envio.";
        }
    }

    public bool CanExecuteChannelActions => !IsMessageEmpty;

    public bool CanRegisterAction => CanExecuteChannelActions;

    public bool CanCopyAction => CanExecuteChannelActions;

    public bool CanEmailAction => CanSendEmail && CanExecuteChannelActions;

    public bool CanWhatsAppAction => CanSendWhatsApp && CanExecuteChannelActions;

    public bool HasUnsavedEdits => _messageEdited;

    public string CopyFeedback
    {
        get => _copyFeedback;
        private set
        {
            if (string.Equals(_copyFeedback, value, StringComparison.Ordinal))
            {
                return;
            }

            _copyFeedback = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasCopyFeedback));
        }
    }

    public bool HasCopyFeedback => !string.IsNullOrWhiteSpace(CopyFeedback);

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

            var isInitialBindingSync = _suppressFirstTextChanged;
            if (_suppressFirstTextChanged)
            {
                _suppressFirstTextChanged = false;
            }

            if (!_isApplyingSuggestion && !isInitialBindingSync)
            {
                _messageEdited = true;
                OnPropertyChanged(nameof(HasUnsavedEdits));
            }

            if (!isInitialBindingSync)
            {
                CopyFeedback = string.Empty;
            }

            NotifyMessageDerivedStateChanged();
        }
    }

    public void ApplySuggestedMessage()
    {
        _isApplyingSuggestion = true;
        try
        {
            Mensagem = _messageFactory(EtapaSelecionada, TomSelecionado);
            _messageEdited = false;
            CopyFeedback = string.Empty;
            OnPropertyChanged(nameof(HasUnsavedEdits));
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
        OnPropertyChanged(nameof(HasUnsavedEdits));
        NotifyMessageDerivedStateChanged();
    }

    public void SetCopyFeedback(string feedback)
    {
        CopyFeedback = feedback ?? string.Empty;
    }

    private void AutoRefreshSuggestedMessage()
    {
        if (_messageEdited)
        {
            return;
        }

        ApplySuggestedMessage();
    }

    private void NotifyMessageDerivedStateChanged()
    {
        OnPropertyChanged(nameof(MessageLength));
        OnPropertyChanged(nameof(IsMessageEmpty));
        OnPropertyChanged(nameof(IsMessageTooShort));
        OnPropertyChanged(nameof(IsMessageTooLong));
        OnPropertyChanged(nameof(MessageQualityHint));
        OnPropertyChanged(nameof(CanExecuteChannelActions));
        OnPropertyChanged(nameof(CanRegisterAction));
        OnPropertyChanged(nameof(CanCopyAction));
        OnPropertyChanged(nameof(CanEmailAction));
        OnPropertyChanged(nameof(CanWhatsAppAction));
    }

    private static CobrancaHistoryItemViewModel ParseHistoryItem(string rawEntry)
    {
        if (string.IsNullOrWhiteSpace(rawEntry))
        {
            return CobrancaHistoryItemViewModel.FromRaw(string.Empty);
        }

        var entry = rawEntry.Trim();
        var timestamp = string.Empty;
        var canal = string.Empty;
        var etapa = string.Empty;
        var tom = string.Empty;
        var mensagem = string.Empty;

        if (entry.StartsWith("[COBRANCA ", StringComparison.OrdinalIgnoreCase))
        {
            var closeBracketIndex = entry.IndexOf(']');
            if (closeBracketIndex > 10)
            {
                timestamp = entry.Substring(10, closeBracketIndex - 10).Trim();
            }

            if (closeBracketIndex >= 0 && closeBracketIndex < entry.Length - 1)
            {
                var payload = entry[(closeBracketIndex + 1)..].Trim();
                var parts = payload.Split('|', StringSplitOptions.RemoveEmptyEntries);
                foreach (var part in parts)
                {
                    var token = part.Trim();
                    var separatorIndex = token.IndexOf('=');
                    if (separatorIndex <= 0 || separatorIndex >= token.Length - 1)
                    {
                        continue;
                    }

                    var key = token[..separatorIndex].Trim();
                    var value = token[(separatorIndex + 1)..].Trim().Trim('"');
                    switch (key)
                    {
                        case "Canal":
                            canal = value;
                            break;
                        case "Etapa":
                            etapa = value;
                            break;
                        case "Tom":
                            tom = value;
                            break;
                        case "Msg":
                            mensagem = value;
                            break;
                    }
                }
            }
        }

        return CobrancaHistoryItemViewModel.FromStructured(entry, timestamp, canal, etapa, tom, mensagem);
    }
}

public sealed class CobrancaHistoryItemViewModel
{
    private CobrancaHistoryItemViewModel(
        string rawText,
        string timestamp,
        string canal,
        string etapa,
        string tom,
        string mensagem,
        bool isStructured)
    {
        RawText = rawText;
        Timestamp = timestamp;
        Canal = canal;
        Etapa = etapa;
        Tom = tom;
        Mensagem = mensagem;
        IsStructured = isStructured;
    }

    public string RawText { get; }
    public string Timestamp { get; }
    public string Canal { get; }
    public string Etapa { get; }
    public string Tom { get; }
    public string Mensagem { get; }
    public bool IsStructured { get; }

    public string TimestampLabel => string.IsNullOrWhiteSpace(Timestamp)
        ? "Data não informada"
        : Timestamp;

    public string CanalLabel => Canal switch
    {
        "Copia" => "Cópia",
        _ => string.IsNullOrWhiteSpace(Canal) ? "Registro" : Canal
    };

    public string EtapaLabel => string.IsNullOrWhiteSpace(Etapa)
        ? "Etapa não informada"
        : Etapa;

    public string TomLabel => string.IsNullOrWhiteSpace(Tom)
        ? "Tom não informado"
        : Tom;

    public string MensagemLabel => string.IsNullOrWhiteSpace(Mensagem)
        ? RawText
        : Mensagem;

    public static CobrancaHistoryItemViewModel FromRaw(string rawText)
    {
        return new CobrancaHistoryItemViewModel(
            rawText,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            rawText,
            false);
    }

    public static CobrancaHistoryItemViewModel FromStructured(
        string rawText,
        string timestamp,
        string canal,
        string etapa,
        string tom,
        string mensagem)
    {
        var hasStructuredValues =
            !string.IsNullOrWhiteSpace(timestamp) ||
            !string.IsNullOrWhiteSpace(canal) ||
            !string.IsNullOrWhiteSpace(etapa) ||
            !string.IsNullOrWhiteSpace(tom);

        return new CobrancaHistoryItemViewModel(
            rawText,
            timestamp,
            canal,
            etapa,
            tom,
            string.IsNullOrWhiteSpace(mensagem) ? rawText : mensagem,
            hasStructuredValues);
    }
}
