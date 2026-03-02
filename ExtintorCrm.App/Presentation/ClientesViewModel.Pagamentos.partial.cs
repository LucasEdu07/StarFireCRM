using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using ExtintorCrm.App.Domain;
using ExtintorCrm.App.Presentation;

namespace ExtintorCrm.App.Presentation
{
    public partial class ClientesViewModel
    {
        private async Task LoadPagamentosAsync()
        {
            IsLoadingPagamentos = true;
            try
            {
                var pagamentos = await _pagamentoRepository.GetAllAsync();
                var clientes = await _clienteRepository.GetAllAsync();
                var clientesById = clientes.ToDictionary(c => c.Id, c => c);
                var clientesByCpf = clientes
                    .Where(c => !string.IsNullOrWhiteSpace(c.CPF))
                    .Select(c => new { Key = NormalizeDigits(c.CPF), Cliente = c })
                    .Where(x => !string.IsNullOrWhiteSpace(x.Key))
                    .GroupBy(x => x.Key!)
                    .ToDictionary(g => g.Key, g => g.First().Cliente);

                foreach (var pagamento in pagamentos)
                {
                    Cliente? cliente = null;
                    var pagamentoCpf = NormalizeDigits(pagamento.CpfCnpjCliente);
                    if (!string.IsNullOrWhiteSpace(pagamentoCpf) && clientesByCpf.TryGetValue(pagamentoCpf!, out var byCpf))
                    {
                        cliente = byCpf;
                    }
                    else if (clientesById.TryGetValue(pagamento.ClienteId, out var byId))
                    {
                        cliente = byId;
                    }

                    if (cliente != null)
                    {
                        pagamento.ClienteId = cliente.Id;
                        pagamento.CpfCnpjCliente = cliente.CPF ?? cliente.Documento;
                        pagamento.ClienteNome = cliente.NomeFantasia;
                    }
                    else
                    {
                        pagamento.ClienteNome = "Cliente nao identificado";
                    }
                }

                _allPagamentos.Clear();
                _allPagamentos.AddRange(pagamentos);
                PagamentosLoadErrorMessage = string.Empty;
                _alertService.ApplyAlerts(_allPagamentos);
                ApplyPagamentoFilter();
                _exportCommand.RaiseCanExecuteChanged();
                UpdateDashboardPaymentCounters();
                RefreshCriticalAlerts();
                RefreshDashboardExecutiveData();
            }
            catch (Exception ex)
            {
                PagamentosLoadErrorMessage = "Não foi possível carregar os pagamentos. Tente novamente.";
                await LogAndToastErrorAsync("Falha ao carregar pagamentos.", "Falha ao carregar pagamentos", ex);
            }
            finally
            {
                IsLoadingPagamentos = false;
            }
        }

        private void ApplyPagamentoFilter()
        {
            IEnumerable<Pagamento> query = _allPagamentos;
            if (PagamentoFilter == "Em aberto")
            {
                query = query.Where(p => !p.Pago);
            }
            else if (PagamentoFilter == "Pago")
            {
                query = query.Where(p => p.Pago);
            }

            if (!string.IsNullOrWhiteSpace(PagamentoSearchTerm))
            {
                var term = PagamentoSearchTerm.Trim();
                query = query.Where(p =>
                    ContainsIgnoreCase(p.ClienteNome, term) ||
                    ContainsIgnoreCase(p.Descricao, term) ||
                    ContainsIgnoreCase(p.CpfCnpjCliente, term));
            }

            Pagamentos.Clear();
            foreach (var pagamento in query.OrderBy(p => p.DataVencimento))
            {
                Pagamentos.Add(pagamento);
            }
            SelectedPagamento = null;
            OnPropertyChanged(nameof(CanResetPagamentoFilters));
            OnPropertyChanged(nameof(ShowPagamentosEmptyState));
            OnPropertyChanged(nameof(PagamentosStateTitle));
            OnPropertyChanged(nameof(PagamentosStateDescription));
            _resetPagamentoFiltersCommand.RaiseCanExecuteChanged();
        }

        private void ResetPagamentoFilters()
        {
            _pagamentoSearchDebounceCts?.Cancel();
            _pagamentoSearchDebounceCts?.Dispose();
            _pagamentoSearchDebounceCts = null;

            _pagamentoSearchTerm = string.Empty;
            _pagamentoFilter = "Todos";
            ApplyPagamentoFilter();
            OnPropertyChanged(nameof(PagamentoSearchTerm));
            OnPropertyChanged(nameof(PagamentoFilter));
            OnPropertyChanged(nameof(CanResetPagamentoFilters));
            _resetPagamentoFiltersCommand.RaiseCanExecuteChanged();
        }

        private void QueuePagamentoSearch()
        {
            _pagamentoSearchDebounceCts?.Cancel();
            _pagamentoSearchDebounceCts?.Dispose();
            _pagamentoSearchDebounceCts = new CancellationTokenSource();
            var token = _pagamentoSearchDebounceCts.Token;

            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(220, token);
                    if (token.IsCancellationRequested)
                    {
                        return;
                    }

                    await Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        if (token.IsCancellationRequested)
                        {
                            return;
                        }

                        ApplyPagamentoFilter();
                    });
                }
                catch (TaskCanceledException)
                {
                    // debounce cancelado
                }
            }, token);
        }

        private static bool ContainsIgnoreCase(string? value, string term)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            return value.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private async Task NewPagamentoAsync()
        {
            var clientes = await _clienteRepository.GetAllAsync();
            if (!clientes.Any())
            {
                await ShowToastAsync("Cadastre ao menos um cliente antes de criar pagamentos.", "Info");
                return;
            }

            var vm = new PagamentoFormViewModel(clientes);
            var form = new PagamentoFormWindow(vm) { Owner = Application.Current?.MainWindow };
            if (form.ShowDialog() != true)
            {
                return;
            }

            await _pagamentoRepository.AddAsync(vm.ToPagamento());
            await LoadPagamentosAsync();
            await ShowToastAsync("Pagamento criado com sucesso.", "Success");
        }

        private async Task EditPagamentoAsync()
        {
            if (SelectedPagamento == null)
            {
                return;
            }

            var clientes = await _clienteRepository.GetAllAsync();
            var vm = new PagamentoFormViewModel(clientes, SelectedPagamento);
            var form = new PagamentoFormWindow(vm) { Owner = Application.Current?.MainWindow };
            if (form.ShowDialog() != true)
            {
                return;
            }

            await _pagamentoRepository.UpdateAsync(vm.ToPagamento());
            await LoadPagamentosAsync();
            await ShowToastAsync("Pagamento atualizado com sucesso.", "Success");
        }

        private async Task DeletePagamentoAsync()
        {
            if (SelectedPagamento == null)
            {
                return;
            }

            var confirmed = DialogService.Confirm(
                "Excluir pagamento",
                $"Deseja realmente excluir o pagamento '{SelectedPagamento.Descricao}'?",
                Application.Current?.MainWindow);

            if (!confirmed)
            {
                return;
            }

            await _pagamentoRepository.DeleteAsync(SelectedPagamento.Id);
            await LoadPagamentosAsync();
            await ShowToastAsync("Pagamento excluido com sucesso.", "Success");
        }

        private async Task SendCobrancaAsync()
        {
            if (SelectedPagamento == null)
            {
                return;
            }

            var cliente = await _clienteRepository.GetByIdAsync(SelectedPagamento.ClienteId);
            if (cliente == null && !string.IsNullOrWhiteSpace(SelectedPagamento.CpfCnpjCliente))
            {
                var cpf = NormalizeDigits(SelectedPagamento.CpfCnpjCliente);
                if (!string.IsNullOrWhiteSpace(cpf))
                {
                    cliente = (await _clienteRepository.GetAllAsync())
                        .FirstOrDefault(c => NormalizeDigits(c.CPF ?? c.Documento) == cpf);
                }
            }

            if (cliente == null)
            {
                await ShowToastAsync("Cliente nao encontrado para este pagamento.", "Error");
                return;
            }

            var clienteNome = string.IsNullOrWhiteSpace(cliente.NomeFantasia) ? "Cliente" : cliente.NomeFantasia.Trim();
            var telefoneRaw = FirstNotEmpty(cliente.Telefone1, cliente.Telefone, cliente.Telefone2, cliente.Telefone3);
            var whatsappPhone = NormalizePhoneForWhatsApp(telefoneRaw);
            var email = (cliente.Email ?? string.Empty).Trim();
            var canSendWhatsApp = IsValidWhatsAppPhone(whatsappPhone);
            var canSendEmail = IsValidEmail(email);

            var etapaDefault = ResolveCobrancaEtapa(SelectedPagamento);
            var tomDefault = "Profissional";
            var payload = BuildCobrancaMessage(clienteNome, SelectedPagamento, etapaDefault, tomDefault);
            var contactInfo = BuildContatoInfo(telefoneRaw, email, canSendWhatsApp, canSendEmail);
            var historico = ExtractCobrancaHistory(SelectedPagamento.Observacoes);

            var model = new CobrancaWindowModel(
                clienteNome: clienteNome,
                contatoInfo: contactInfo,
                valorResumo: SelectedPagamento.Valor.ToString("C2", new CultureInfo("pt-BR")),
                vencimentoResumo: SelectedPagamento.DataVencimento.ToString("dd/MM/yyyy"),
                prazoResumo: ResolvePrazoResumo(SelectedPagamento),
                canSendEmail: canSendEmail,
                canSendWhatsApp: canSendWhatsApp,
                etapaOptions: BuildCobrancaEtapaOptions(SelectedPagamento),
                etapaSelecionada: etapaDefault,
                tomOptions: BuildCobrancaToneOptions(),
                tomSelecionado: tomDefault,
                historicoItens: historico,
                messageFactory: (etapa, tom) => BuildCobrancaMessage(clienteNome, SelectedPagamento, etapa, tom).Message);

            var dialog = new CobrancaWindow(model, Application.Current?.MainWindow);
            if (dialog.ShowDialog() != true || dialog.SelectedAction == CobrancaAction.None)
            {
                return;
            }

            var messageToSend = string.IsNullOrWhiteSpace(model.Mensagem)
                ? payload.Message
                : model.Mensagem.Trim();

            try
            {
                var canalRegistrado = string.Empty;
                var interactionExecuted = false;
                var subject = BuildCobrancaMessage(clienteNome, SelectedPagamento, model.EtapaSelecionada, model.TomSelecionado).Subject;

                switch (dialog.SelectedAction)
                {
                    case CobrancaAction.Register:
                        canalRegistrado = "Registro manual";
                        interactionExecuted = true;
                        await ShowToastAsync("Contato registrado no historico da cobranca.", "Success");
                        break;

                    case CobrancaAction.Copy:
                        Clipboard.SetText(messageToSend);
                        canalRegistrado = "Copia";
                        interactionExecuted = true;
                        await ShowToastAsync("Mensagem de cobranca copiada.", "Success");
                        break;

                    case CobrancaAction.Email:
                        if (!canSendEmail)
                        {
                            await ShowToastAsync("Cliente sem e-mail valido.", "Info");
                            break;
                        }

                        OpenUri(BuildMailToUri(email, subject, messageToSend));
                        canalRegistrado = "E-mail";
                        interactionExecuted = true;
                        await ShowToastAsync("Cliente de e-mail aberto com a cobranca.", "Info");
                        break;

                    case CobrancaAction.WhatsApp:
                        if (!canSendWhatsApp)
                        {
                            await ShowToastAsync("Cliente sem telefone valido para WhatsApp.", "Info");
                            break;
                        }

                        OpenUri($"https://wa.me/{whatsappPhone}?text={Uri.EscapeDataString(messageToSend)}");
                        canalRegistrado = "WhatsApp";
                        interactionExecuted = true;
                        await ShowToastAsync("WhatsApp aberto com a mensagem pronta.", "Info");
                        break;
                }

                if (interactionExecuted)
                {
                    await RegisterCobrancaInteractionAsync(
                        SelectedPagamento,
                        canalRegistrado,
                        model.EtapaSelecionada,
                        model.TomSelecionado,
                        messageToSend);

                    await LoadPagamentosAsync();
                }
            }
            catch (Exception ex)
            {
                await LogAndToastErrorAsync("Falha ao abrir canal de cobranca.", "Falha ao abrir canal de cobranca", ex);
            }
        }

        private async Task RegisterCobrancaInteractionAsync(
            Pagamento pagamento,
            string canal,
            string etapa,
            string tom,
            string mensagem)
        {
            var historicoEntry = BuildCobrancaHistoryEntry(canal, etapa, tom, mensagem);
            pagamento.Observacoes = AppendCobrancaHistory(pagamento.Observacoes, historicoEntry);
            await _pagamentoRepository.UpdateAsync(pagamento);
        }

        private Task OpenPagamentoAttachmentsAsync()
        {
            if (SelectedPagamento == null)
            {
                return Task.CompletedTask;
            }

            var referencia = string.IsNullOrWhiteSpace(SelectedPagamento.Descricao)
                ? $"Pagamento {SelectedPagamento.DataVencimento:dd/MM/yyyy}"
                : SelectedPagamento.Descricao;
            var context = DocumentoAnexosContext.ForPagamento(SelectedPagamento.Id, referencia);
            var vm = new DocumentoAnexosViewModel(_documentoAnexoRepository, _documentoStorageService, context);
            var window = new DocumentoAnexosWindow(vm)
            {
                Owner = Application.Current?.MainWindow
            };
            window.ShowDialog();
            return Task.CompletedTask;
        }
    }
}
