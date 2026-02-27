using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using ExtintorCrm.App.Domain;
using ExtintorCrm.App.Infrastructure;
using ExtintorCrm.App.Infrastructure.Documents;
using Microsoft.Win32;

namespace ExtintorCrm.App.Presentation
{
    public partial class ClienteDetalhesWindow : Window
    {
        private readonly CultureInfo _ptBr = new("pt-BR");
        private readonly DocumentoAnexoRepository _documentoAnexoRepository = new();
        private readonly DocumentoStorageService _documentoStorageService = new();
        private readonly ObservableCollection<DocumentoAnexoItem> _alvaraAnexos = new();
        private bool _isFormattingValor;
        private bool _isSavingChanges;
        private readonly DispatcherTimer _saveFeedbackTimer = new() { Interval = TimeSpan.FromSeconds(2.4) };
        private static readonly Regex OrderedListRegex = new(@"^\d+\.\s+", RegexOptions.Compiled);
        private static readonly Regex MarkdownLinkRegex = new(@"\[(?<label>[^\]]+)\]\((?<url>[^)]+)\)", RegexOptions.Compiled);
        public bool SaveRequested { get; private set; }
        public Func<Task<bool>>? SaveChangesAsync { get; set; }

        public ClienteDetalhesWindow(ClienteDetalhesViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
            AlvaraAnexosDataGrid.ItemsSource = _alvaraAnexos;
            UpdateAlvaraAnexosUi();
            _saveFeedbackTimer.Tick += SaveFeedbackTimer_Tick;
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            PerfilClienteStatusRockerControl?.PrepareForDisplay();
            await LoadAlvaraAnexosAsync();
        }

        private void Editar_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is ClienteDetalhesViewModel vm)
            {
                vm.IsEditMode = true;
            }
        }

        private async void Salvar_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not ClienteDetalhesViewModel vm)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(vm.NomeFantasia))
            {
                DialogService.Info("Validação", "Nome é obrigatório.", this);
                return;
            }

            if (string.IsNullOrWhiteSpace(vm.CPF))
            {
                DialogService.Info("Validação", "CPF/CNPJ é obrigatório.", this);
                return;
            }

            if (_isSavingChanges)
            {
                return;
            }

            _isSavingChanges = true;
            var saveButton = sender as Button;
            if (saveButton != null)
            {
                saveButton.IsEnabled = false;
            }
            SetSaveFeedback("Salvando...", "TextSecondary", autoHide: false);

            try
            {
                if (SaveChangesAsync != null)
                {
                    var saved = await SaveChangesAsync();
                    if (saved)
                    {
                        vm.AcceptCurrentStateAsSaved();
                        vm.IsEditMode = true;
                        SetSaveFeedback("Salvo", "Success", autoHide: true);
                    }
                    else
                    {
                        SetSaveFeedback("Não foi possível salvar", "Warning", autoHide: true);
                    }

                    return;
                }

                SaveRequested = true;
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                SetSaveFeedback("Falha ao salvar", "Danger", autoHide: true);
                DialogService.Error("Salvar", $"Falha ao salvar alterações: {ex.Message}", this);
            }
            finally
            {
                _isSavingChanges = false;
                if (saveButton != null)
                {
                    saveButton.IsEnabled = true;
                }
            }
        }

        private void CancelarEdicao_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is ClienteDetalhesViewModel vm)
            {
                vm.CancelEdit();
            }
        }

        private void MarkdownTool_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button { Tag: string command } || ObservacoesMarkdownEditor == null)
            {
                return;
            }

            ExecuteMarkdownCommand(ObservacoesMarkdownEditor, command, showFeedback: true);
        }

        private void MarkdownToolModal_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button { Tag: string command } || ObservacoesMarkdownEditorModal == null)
            {
                return;
            }

            ExecuteMarkdownCommand(ObservacoesMarkdownEditorModal, command, showFeedback: true);
        }

        private void OpenMarkdownEditorModal_Click(object sender, RoutedEventArgs e)
        {
            MarkdownEditorOverlay.Visibility = Visibility.Visible;
            ObservacoesMarkdownEditorModal?.Focus();
            if (ObservacoesMarkdownEditorModal != null)
            {
                ObservacoesMarkdownEditorModal.CaretIndex = ObservacoesMarkdownEditorModal.Text?.Length ?? 0;
            }
        }

        private void CloseMarkdownEditorModal_Click(object sender, RoutedEventArgs e)
        {
            MarkdownEditorOverlay.Visibility = Visibility.Collapsed;
            ObservacoesMarkdownEditor?.Focus();
        }

        private void MarkdownOverlay_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource == MarkdownEditorOverlay)
            {
                MarkdownEditorOverlay.Visibility = Visibility.Collapsed;
                ObservacoesMarkdownEditor?.Focus();
            }
        }

        private void MarkdownModalCard_MouseDown(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
        }

        private void ObservacoesEditor_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (sender is not TextBox editor)
            {
                return;
            }

            if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.Z)
            {
                ExecuteMarkdownCommand(editor, "undo", showFeedback: true);
                e.Handled = true;
                return;
            }

            if ((Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift)) && e.Key == Key.Z)
            {
                ExecuteMarkdownCommand(editor, "redo", showFeedback: true);
                e.Handled = true;
                return;
            }

            if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.Y)
            {
                ExecuteMarkdownCommand(editor, "redo", showFeedback: true);
                e.Handled = true;
                return;
            }

            if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.D)
            {
                ExecuteMarkdownCommand(editor, "duplicate_line", showFeedback: true);
                e.Handled = true;
                return;
            }

            if (Keyboard.Modifiers == ModifierKeys.Alt && e.Key == Key.Up)
            {
                ExecuteMarkdownCommand(editor, "move_line_up", showFeedback: true);
                e.Handled = true;
                return;
            }

            if (Keyboard.Modifiers == ModifierKeys.Alt && e.Key == Key.Down)
            {
                ExecuteMarkdownCommand(editor, "move_line_down", showFeedback: true);
                e.Handled = true;
                return;
            }

            if ((Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift)) &&
                (e.Key == Key.D7 || e.Key == Key.NumPad7))
            {
                ExecuteMarkdownCommand(editor, "toggle_ul", showFeedback: true);
                e.Handled = true;
                return;
            }

            if ((Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift)) &&
                (e.Key == Key.OemPeriod || e.Key == Key.Decimal))
            {
                ExecuteMarkdownCommand(editor, "toggle_quote", showFeedback: true);
                e.Handled = true;
                return;
            }

            if (Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Alt) &&
                (e.Key == Key.D1 || e.Key == Key.NumPad1))
            {
                ExecuteMarkdownCommand(editor, "toggle_h1", showFeedback: true);
                e.Handled = true;
                return;
            }

            if (Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift) && e.Key == Key.X)
            {
                ExecuteMarkdownCommand(editor, "clear_formatting", showFeedback: true);
                e.Handled = true;
                return;
            }

            if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.B)
            {
                ExecuteMarkdownCommand(editor, "bold", showFeedback: false);
                e.Handled = true;
                return;
            }

            if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.I)
            {
                ExecuteMarkdownCommand(editor, "italic", showFeedback: false);
                e.Handled = true;
                return;
            }

            if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.K)
            {
                ExecuteMarkdownCommand(editor, "link", showFeedback: false);
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Escape && MarkdownEditorOverlay.Visibility == Visibility.Visible)
            {
                MarkdownEditorOverlay.Visibility = Visibility.Collapsed;
                ObservacoesMarkdownEditor?.Focus();
                e.Handled = true;
            }
        }

        private void SaveFeedbackTimer_Tick(object? sender, EventArgs e)
        {
            _saveFeedbackTimer.Stop();
            SaveStatusText.Visibility = Visibility.Collapsed;
        }

        private void SetSaveFeedback(string message, string brushResourceKey, bool autoHide)
        {
            SaveStatusText.Text = message;
            SaveStatusText.Foreground = ResolveBrush(brushResourceKey);
            SaveStatusText.Visibility = Visibility.Visible;
            _saveFeedbackTimer.Stop();

            if (autoHide)
            {
                _saveFeedbackTimer.Start();
            }
        }

        private void SetEditorActionFeedback(string message)
        {
            if (_isSavingChanges)
            {
                return;
            }

            SetSaveFeedback(message, "TextSecondary", autoHide: true);
        }

        private Brush ResolveBrush(string resourceKey)
        {
            if (TryFindResource(resourceKey) is Brush brush)
            {
                return brush;
            }

            return Brushes.Gray;
        }

        private void ExecuteMarkdownCommand(TextBox editor, string command, bool showFeedback)
        {
            var result = ApplyMarkdownCommand(editor, command);
            if (showFeedback && !string.IsNullOrWhiteSpace(result.Feedback))
            {
                SetEditorActionFeedback(result.Feedback!);
            }
        }

        private static (bool Handled, string? Feedback) ApplyMarkdownCommand(TextBox editor, string command)
        {
            switch (command)
            {
                case "undo":
                    if (!editor.CanUndo)
                    {
                        return (true, "Nada para desfazer");
                    }

                    editor.Undo();
                    editor.Focus();
                    return (true, "Desfeito");
                case "redo":
                    if (!editor.CanRedo)
                    {
                        return (true, "Nada para refazer");
                    }

                    editor.Redo();
                    editor.Focus();
                    return (true, "Refeito");
                case "h1":
                    PrefixSelectedLines(editor, "# ", orderedList: false);
                    return (true, null);
                case "h2":
                    PrefixSelectedLines(editor, "## ", orderedList: false);
                    return (true, null);
                case "toggle_h1":
                    return ToggleLinePrefix(editor, "# ", "Título aplicado", "Título removido");
                case "bold":
                    WrapSelection(editor, "**", "**", "texto");
                    return (true, null);
                case "italic":
                    WrapSelection(editor, "*", "*", "texto");
                    return (true, null);
                case "ul":
                    PrefixSelectedLines(editor, "- ", orderedList: false);
                    return (true, null);
                case "toggle_ul":
                    return ToggleLinePrefix(editor, "- ", "Lista aplicada", "Lista removida");
                case "ol":
                    PrefixSelectedLines(editor, string.Empty, orderedList: true);
                    return (true, null);
                case "link":
                    InsertMarkdownLink(editor);
                    return (true, null);
                case "quote":
                    PrefixSelectedLines(editor, "> ", orderedList: false);
                    return (true, null);
                case "toggle_quote":
                    return ToggleLinePrefix(editor, "> ", "Citação aplicada", "Citação removida");
                case "code":
                    WrapSelection(editor, "`", "`", "codigo");
                    return (true, null);
                case "codeblock":
                    WrapSelection(editor, "```\r\n", "\r\n```", "codigo");
                    return (true, null);
                case "duplicate_line":
                    return DuplicateSelectedLines(editor);
                case "move_line_up":
                    return MoveSelectedLines(editor, moveUp: true);
                case "move_line_down":
                    return MoveSelectedLines(editor, moveUp: false);
                case "clear_formatting":
                    return ClearFormatting(editor);
                default:
                    return (false, null);
            }
        }

        private static void WrapSelection(TextBox editor, string prefix, string suffix, string placeholder)
        {
            var text = editor.Text ?? string.Empty;
            var start = editor.SelectionStart;
            var length = editor.SelectionLength;

            if (length > 0)
            {
                var selected = text.Substring(start, length);
                var replacement = $"{prefix}{selected}{suffix}";
                var updated = text.Remove(start, length).Insert(start, replacement);
                editor.Text = updated;
                editor.SelectionStart = start + prefix.Length;
                editor.SelectionLength = selected.Length;
                editor.Focus();
                return;
            }

            var inserted = $"{prefix}{placeholder}{suffix}";
            editor.Text = text.Insert(start, inserted);
            editor.SelectionStart = start + prefix.Length;
            editor.SelectionLength = placeholder.Length;
            editor.Focus();
        }

        private static void InsertMarkdownLink(TextBox editor)
        {
            var text = editor.Text ?? string.Empty;
            var start = editor.SelectionStart;
            var length = editor.SelectionLength;
            var selected = length > 0 ? text.Substring(start, length) : "texto";
            var replacement = $"[{selected}](https://)";

            if (length > 0)
            {
                editor.Text = text.Remove(start, length).Insert(start, replacement);
            }
            else
            {
                editor.Text = text.Insert(start, replacement);
            }

            var urlSelectionStart = start + selected.Length + 3;
            editor.SelectionStart = urlSelectionStart;
            editor.SelectionLength = "https://".Length;
            editor.Focus();
        }

        private static void PrefixSelectedLines(TextBox editor, string prefix, bool orderedList)
        {
            var state = BuildEditorLineState(editor);
            if (state.Lines.Count == 0)
            {
                editor.Text = orderedList ? "1. item" : $"{prefix}item";
                editor.SelectionStart = editor.Text.Length;
                editor.SelectionLength = 0;
                return;
            }

            var itemNumber = 1;
            for (var i = state.StartLine; i <= state.EndLine; i++)
            {
                var line = state.Lines[i];
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                state.Lines[i] = orderedList ? $"{itemNumber++}. {line}" : $"{prefix}{line}";
            }

            ApplyLines(editor, state.Lines, state.NewLine, state.StartLine, state.EndLine);
        }

        private static (bool Handled, string? Feedback) ToggleLinePrefix(
            TextBox editor,
            string prefix,
            string appliedFeedback,
            string removedFeedback)
        {
            var state = BuildEditorLineState(editor);
            var anyContent = false;
            var allPrefixed = true;

            for (var i = state.StartLine; i <= state.EndLine; i++)
            {
                if (string.IsNullOrWhiteSpace(state.Lines[i]))
                {
                    continue;
                }

                anyContent = true;
                if (!GetLineBody(state.Lines[i]).StartsWith(prefix, StringComparison.Ordinal))
                {
                    allPrefixed = false;
                }
            }

            if (!anyContent)
            {
                return (true, "Nada para formatar");
            }

            for (var i = state.StartLine; i <= state.EndLine; i++)
            {
                if (string.IsNullOrWhiteSpace(state.Lines[i]))
                {
                    continue;
                }

                state.Lines[i] = allPrefixed
                    ? RemoveLinePrefix(state.Lines[i], prefix)
                    : AddLinePrefix(state.Lines[i], prefix);
            }

            ApplyLines(editor, state.Lines, state.NewLine, state.StartLine, state.EndLine);
            return (true, allPrefixed ? removedFeedback : appliedFeedback);
        }

        private static (bool Handled, string? Feedback) DuplicateSelectedLines(TextBox editor)
        {
            var state = BuildEditorLineState(editor);
            if (state.Lines.Count == 0)
            {
                return (true, "Nada para duplicar");
            }

            var count = state.EndLine - state.StartLine + 1;
            var block = state.Lines.Skip(state.StartLine).Take(count).ToList();
            state.Lines.InsertRange(state.EndLine + 1, block);

            var newStart = state.EndLine + 1;
            var newEnd = newStart + count - 1;
            ApplyLines(editor, state.Lines, state.NewLine, newStart, newEnd);
            return (true, count == 1 ? "Linha duplicada" : "Linhas duplicadas");
        }

        private static (bool Handled, string? Feedback) MoveSelectedLines(TextBox editor, bool moveUp)
        {
            var state = BuildEditorLineState(editor);
            if (state.Lines.Count == 0)
            {
                return (true, "Nada para mover");
            }

            var count = state.EndLine - state.StartLine + 1;
            if (moveUp && state.StartLine == 0)
            {
                return (true, "Não há linha acima");
            }

            if (!moveUp && state.EndLine >= state.Lines.Count - 1)
            {
                return (true, "Não há linha abaixo");
            }

            var block = state.Lines.Skip(state.StartLine).Take(count).ToList();
            state.Lines.RemoveRange(state.StartLine, count);

            var insertIndex = moveUp ? state.StartLine - 1 : state.StartLine + 1;
            state.Lines.InsertRange(insertIndex, block);

            var newStart = insertIndex;
            var newEnd = newStart + count - 1;
            ApplyLines(editor, state.Lines, state.NewLine, newStart, newEnd);
            return (true, moveUp ? "Linha movida para cima" : "Linha movida para baixo");
        }

        private static (bool Handled, string? Feedback) ClearFormatting(TextBox editor)
        {
            var state = BuildEditorLineState(editor);
            var changed = false;

            for (var i = state.StartLine; i <= state.EndLine; i++)
            {
                var stripped = StripMarkdownFormatting(state.Lines[i]);
                if (!string.Equals(stripped, state.Lines[i], StringComparison.Ordinal))
                {
                    state.Lines[i] = stripped;
                    changed = true;
                }
            }

            if (!changed)
            {
                return (true, "Nada para limpar");
            }

            ApplyLines(editor, state.Lines, state.NewLine, state.StartLine, state.EndLine);
            return (true, "Formatação limpa");
        }

        private static string StripMarkdownFormatting(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                return line;
            }

            var body = GetLineBody(line);
            var leading = line[..(line.Length - body.Length)];

            var removePrefix = true;
            while (removePrefix)
            {
                removePrefix = false;
                if (body.StartsWith("###### ", StringComparison.Ordinal))
                {
                    body = body[7..];
                    removePrefix = true;
                    continue;
                }

                if (body.StartsWith("##### ", StringComparison.Ordinal))
                {
                    body = body[6..];
                    removePrefix = true;
                    continue;
                }

                if (body.StartsWith("#### ", StringComparison.Ordinal))
                {
                    body = body[5..];
                    removePrefix = true;
                    continue;
                }

                if (body.StartsWith("### ", StringComparison.Ordinal))
                {
                    body = body[4..];
                    removePrefix = true;
                    continue;
                }

                if (body.StartsWith("## ", StringComparison.Ordinal))
                {
                    body = body[3..];
                    removePrefix = true;
                    continue;
                }

                if (body.StartsWith("# ", StringComparison.Ordinal))
                {
                    body = body[2..];
                    removePrefix = true;
                    continue;
                }

                if (body.StartsWith("> ", StringComparison.Ordinal))
                {
                    body = body[2..];
                    removePrefix = true;
                    continue;
                }

                if (body.StartsWith("- ", StringComparison.Ordinal) ||
                    body.StartsWith("* ", StringComparison.Ordinal) ||
                    body.StartsWith("+ ", StringComparison.Ordinal))
                {
                    body = body[2..];
                    removePrefix = true;
                    continue;
                }

                var orderedMatch = OrderedListRegex.Match(body);
                if (orderedMatch.Success)
                {
                    body = body[orderedMatch.Length..];
                    removePrefix = true;
                }
            }

            body = MarkdownLinkRegex.Replace(body, "${label}");
            body = body
                .Replace("**", string.Empty, StringComparison.Ordinal)
                .Replace("__", string.Empty, StringComparison.Ordinal)
                .Replace("*", string.Empty, StringComparison.Ordinal)
                .Replace("_", string.Empty, StringComparison.Ordinal)
                .Replace("`", string.Empty, StringComparison.Ordinal);
            return leading + body;
        }

        private static string AddLinePrefix(string line, string prefix)
        {
            var body = GetLineBody(line);
            var leading = line[..(line.Length - body.Length)];
            return leading + prefix + body;
        }

        private static string RemoveLinePrefix(string line, string prefix)
        {
            var body = GetLineBody(line);
            if (!body.StartsWith(prefix, StringComparison.Ordinal))
            {
                return line;
            }

            var leading = line[..(line.Length - body.Length)];
            return leading + body[prefix.Length..];
        }

        private static string GetLineBody(string line)
        {
            var body = line.TrimStart();
            return body;
        }

        private static (List<string> Lines, string NewLine, int StartLine, int EndLine) BuildEditorLineState(TextBox editor)
        {
            var text = editor.Text ?? string.Empty;
            var newline = text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
            var lines = text.Length == 0
                ? new List<string> { string.Empty }
                : text.Replace("\r\n", "\n").Split('\n').ToList();

            var startLine = 0;
            var endLine = 0;
            if (text.Length > 0)
            {
                var selectionStart = Math.Clamp(editor.SelectionStart, 0, text.Length);
                var selectionEndInclusive = editor.SelectionLength > 0
                    ? Math.Clamp(editor.SelectionStart + editor.SelectionLength - 1, 0, text.Length - 1)
                    : selectionStart;

                for (var i = 0; i < selectionStart; i++)
                {
                    if (text[i] == '\n')
                    {
                        startLine++;
                    }
                }

                for (var i = 0; i < selectionEndInclusive; i++)
                {
                    if (text[i] == '\n')
                    {
                        endLine++;
                    }
                }
            }

            startLine = Math.Clamp(startLine, 0, Math.Max(lines.Count - 1, 0));
            endLine = Math.Clamp(endLine, startLine, Math.Max(lines.Count - 1, 0));
            return (lines, newline, startLine, endLine);
        }

        private static void ApplyLines(TextBox editor, IReadOnlyList<string> lines, string newline, int startLine, int endLine)
        {
            var updatedText = string.Join(newline, lines);
            var selectionStart = GetLineStartIndex(lines, newline, startLine);
            var selectionEnd = GetLineStartIndex(lines, newline, endLine + 1);

            editor.Text = updatedText;
            editor.SelectionStart = Math.Clamp(selectionStart, 0, editor.Text.Length);
            editor.SelectionLength = Math.Clamp(selectionEnd - selectionStart, 0, editor.Text.Length - editor.SelectionStart);
            editor.Focus();
        }

        private static int GetLineStartIndex(IReadOnlyList<string> lines, string newline, int lineIndex)
        {
            var clampedIndex = Math.Clamp(lineIndex, 0, lines.Count);
            var index = 0;
            for (var i = 0; i < clampedIndex; i++)
            {
                index += lines[i].Length;
                if (i < lines.Count - 1)
                {
                    index += newline.Length;
                }
            }

            return index;
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private async Task LoadAlvaraAnexosAsync()
        {
            if (DataContext is not ClienteDetalhesViewModel vm)
            {
                return;
            }

            try
            {
                var anexos = await _documentoAnexoRepository.ListByClienteAlvaraAsync(vm.ClienteId);
                _alvaraAnexos.Clear();
                foreach (var anexo in anexos)
                {
                    _alvaraAnexos.Add(new DocumentoAnexoItem(anexo));
                }

                AlvaraAnexosDataGrid.SelectedIndex = _alvaraAnexos.Count > 0 ? 0 : -1;
                UpdateAlvaraAnexosUi();
            }
            catch (Exception ex)
            {
                DialogService.Error("Anexos", $"Falha ao carregar documentos do alvará: {ex.Message}", this);
            }
        }

        private void UpdateAlvaraAnexosUi()
        {
            AlvaraAnexosCountText.Text = _alvaraAnexos.Count == 0
                ? "Nenhum documento anexado"
                : $"{_alvaraAnexos.Count} documento(s) anexado(s)";

            var hasSelection = AlvaraAnexosDataGrid.SelectedItem is DocumentoAnexoItem;
            AbrirAlvaraButton.IsEnabled = hasSelection;
            ExcluirAlvaraButton.IsEnabled = hasSelection;
        }

        private void AlvaraAnexosDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateAlvaraAnexosUi();
        }

        private async void AnexarAlvaraDocumento_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not ClienteDetalhesViewModel vm)
            {
                return;
            }

            var dialog = new OpenFileDialog
            {
                Title = "Selecionar documentos do alvará",
                Filter = "Documentos (*.*)|*.*",
                CheckFileExists = true,
                Multiselect = true
            };

            if (dialog.ShowDialog() != true || dialog.FileNames.Length == 0)
            {
                return;
            }

            foreach (var fileName in dialog.FileNames)
            {
                StoredDocumentoArquivo? storedFile = null;
                try
                {
                    storedFile = _documentoStorageService.StoreForAlvara(vm.ClienteId, fileName);
                    await _documentoAnexoRepository.AddAsync(new DocumentoAnexo
                    {
                        Id = storedFile.DocumentoId,
                        ClienteId = vm.ClienteId,
                        Contexto = "Alvara",
                        TipoDocumento = ResolveAlvaraDocumentType(Path.GetFileName(fileName)),
                        NomeOriginal = storedFile.NomeOriginal,
                        CaminhoRelativo = storedFile.CaminhoRelativo,
                        TamanhoBytes = storedFile.TamanhoBytes,
                        CriadoEm = DateTime.UtcNow,
                        AtualizadoEm = DateTime.UtcNow
                    });
                }
                catch (Exception ex)
                {
                    if (storedFile != null)
                    {
                        _documentoStorageService.DeleteByRelativePath(storedFile.CaminhoRelativo);
                    }

                    DialogService.Error(
                        "Anexos",
                        $"Falha ao anexar '{Path.GetFileName(fileName)}': {ex.Message}",
                        this);
                }
            }

            await LoadAlvaraAnexosAsync();
        }

        private void AbrirAlvaraDocumento_Click(object sender, RoutedEventArgs e)
        {
            if (AlvaraAnexosDataGrid.SelectedItem is not DocumentoAnexoItem selected)
            {
                return;
            }

            try
            {
                var absolutePath = _documentoStorageService.ResolveAbsolutePath(selected.CaminhoRelativo);
                if (!File.Exists(absolutePath))
                {
                    DialogService.Info("Anexos", "Arquivo não encontrado no armazenamento local.", this);
                    return;
                }

                Process.Start(new ProcessStartInfo
                {
                    FileName = absolutePath,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                DialogService.Error("Anexos", $"Falha ao abrir arquivo: {ex.Message}", this);
            }
        }

        private async void ExcluirAlvaraDocumento_Click(object sender, RoutedEventArgs e)
        {
            if (AlvaraAnexosDataGrid.SelectedItem is not DocumentoAnexoItem selected)
            {
                return;
            }

            var confirmed = DialogService.Confirm(
                "Excluir anexo",
                $"Deseja realmente excluir o anexo '{selected.NomeOriginal}'?",
                this);
            if (!confirmed)
            {
                return;
            }

            try
            {
                await _documentoAnexoRepository.DeleteAsync(selected.Id);
                _documentoStorageService.DeleteByRelativePath(selected.CaminhoRelativo);
                await LoadAlvaraAnexosAsync();
            }
            catch (Exception ex)
            {
                DialogService.Error("Anexos", $"Falha ao excluir anexo: {ex.Message}", this);
            }
        }

        private static string ResolveAlvaraDocumentType(string? fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return "Alvará";
            }

            var lower = fileName.ToLowerInvariant();
            if (lower.Contains("laudo"))
            {
                return "Laudo";
            }

            if (lower.Contains("licenca") || lower.Contains("licença"))
            {
                return "Licença";
            }

            return "Alvará";
        }

        private void ValorEditor_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = e.Text.Any(ch => !char.IsDigit(ch) && ch != ',' && ch != '.');
        }

        private void ValorEditor_Pasting(object sender, DataObjectPastingEventArgs e)
        {
            if (!e.DataObject.GetDataPresent(DataFormats.Text))
            {
                e.CancelCommand();
                return;
            }

            var text = e.DataObject.GetData(DataFormats.Text) as string ?? string.Empty;
            if (!TryParseMoneyFlexible(text, out _))
            {
                e.CancelCommand();
            }
        }

        private void ValorEditor_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isFormattingValor || sender is not TextBox textBox || textBox.DataContext is not PagamentoPerfilItem item)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(textBox.Text))
            {
                item.Valor = 0m;
                return;
            }

            if (!TryParseMoneyFlexible(textBox.Text, out var value))
            {
                return;
            }

            item.Valor = value;
        }

        private void ValorEditor_GotFocus(object sender, RoutedEventArgs e)
        {
            if (sender is not TextBox textBox || textBox.DataContext is not PagamentoPerfilItem item)
            {
                return;
            }

            _isFormattingValor = true;
            try
            {
                textBox.Text = item.Valor > 0 ? item.Valor.ToString("N2", _ptBr) : string.Empty;
                textBox.CaretIndex = textBox.Text.Length;
            }
            finally
            {
                _isFormattingValor = false;
            }
        }

        private void ValorEditor_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is not TextBox textBox || textBox.DataContext is not PagamentoPerfilItem item)
            {
                return;
            }

            textBox.Text = item.Valor > 0 ? item.Valor.ToString("C2", _ptBr) : string.Empty;
            textBox.CaretIndex = textBox.Text.Length;
        }

        private bool TryParseMoneyFlexible(string input, out decimal value)
        {
            value = 0m;
            if (string.IsNullOrWhiteSpace(input))
            {
                return false;
            }

            var sanitized = input.Replace("R$", string.Empty, StringComparison.OrdinalIgnoreCase)
                .Replace(" ", string.Empty)
                .Trim();

            var digitsOnly = new string(sanitized.Where(char.IsDigit).ToArray());
            if (string.IsNullOrEmpty(digitsOnly))
            {
                return false;
            }

            if (decimal.TryParse(
                    sanitized,
                    NumberStyles.Number | NumberStyles.AllowCurrencySymbol,
                    _ptBr,
                    out value))
            {
                return true;
            }

            var lastComma = sanitized.LastIndexOf(',');
            var lastDot = sanitized.LastIndexOf('.');
            var hasExplicitDecimal = lastComma >= 0 || lastDot >= 0;

            if (!hasExplicitDecimal)
            {
                if (!decimal.TryParse(digitsOnly, out var integerValue))
                {
                    return false;
                }

                value = integerValue;
                return true;
            }

            var normalized = sanitized;
            if (lastComma >= 0 && lastDot >= 0)
            {
                var decimalSep = lastComma > lastDot ? ',' : '.';
                var thousandSep = decimalSep == ',' ? '.' : ',';
                normalized = normalized.Replace(thousandSep.ToString(), string.Empty);
                normalized = normalized.Replace(decimalSep, '.');
            }
            else if (lastDot >= 0)
            {
                var dotCount = normalized.Count(ch => ch == '.');
                if (dotCount > 1)
                {
                    normalized = normalized.Replace(".", string.Empty);
                }
                else
                {
                    var digitsAfter = normalized.Length - lastDot - 1;
                    if (digitsAfter == 3)
                    {
                        normalized = normalized.Replace(".", string.Empty);
                    }
                }
            }
            else if (lastComma >= 0)
            {
                var commaCount = normalized.Count(ch => ch == ',');
                if (commaCount > 1)
                {
                    var firstComma = normalized.IndexOf(',');
                    normalized = normalized[..(firstComma + 1)]
                        + normalized[(firstComma + 1)..].Replace(",", string.Empty);
                }
                normalized = normalized.Replace(',', '.');
            }

            normalized = new string(normalized.Where(ch => char.IsDigit(ch) || ch == '.').ToArray());
            return decimal.TryParse(
                normalized,
                NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture,
                out value);
        }
    }
}
