using System;
using System.Linq;
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
                    pagamento.ClienteNome = "Cliente não identificado";
                }
            }

            _allPagamentos.Clear();
            _allPagamentos.AddRange(pagamentos);
            _alertService.ApplyAlerts(_allPagamentos);
            ApplyPagamentoFilter();
            _exportCommand.RaiseCanExecuteChanged();
            UpdateDashboardPaymentCounters();
            RefreshCriticalAlerts();
            RefreshDashboardExecutiveData();
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

            Pagamentos.Clear();
            foreach (var pagamento in query.OrderBy(p => p.DataVencimento))
            {
                Pagamentos.Add(pagamento);
            }
            SelectedPagamento = null;
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
            await ShowToastAsync("Pagamento excluído com sucesso.", "Success");
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
                await ShowToastAsync("Cliente não encontrado para este pagamento.", "Error");
                return;
            }

            var clienteNome = string.IsNullOrWhiteSpace(cliente.NomeFantasia) ? "Cliente" : cliente.NomeFantasia.Trim();
            var telefoneRaw = FirstNotEmpty(cliente.Telefone1, cliente.Telefone, cliente.Telefone2, cliente.Telefone3);
            var whatsappPhone = NormalizePhoneForWhatsApp(telefoneRaw);
            var email = (cliente.Email ?? string.Empty).Trim();
            var canSendWhatsApp = IsValidWhatsAppPhone(whatsappPhone);
            var canSendEmail = IsValidEmail(email);

            var payload = BuildCobrancaMessage(clienteNome, SelectedPagamento);
            var contactInfo = BuildContatoInfo(telefoneRaw, email, canSendWhatsApp, canSendEmail);
            var model = new CobrancaWindowModel
            {
                ClienteNome = clienteNome,
                ContatoInfo = contactInfo,
                Mensagem = payload.Message,
                CanSendEmail = canSendEmail,
                CanSendWhatsApp = canSendWhatsApp
            };

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
                switch (dialog.SelectedAction)
                {
                    case CobrancaAction.Copy:
                        Clipboard.SetText(messageToSend);
                        await ShowToastAsync("Mensagem de cobrança copiada.", "Success");
                        break;

                    case CobrancaAction.Email:
                        if (!canSendEmail)
                        {
                            await ShowToastAsync("Cliente sem e-mail válido.", "Info");
                            break;
                        }

                        OpenUri(BuildMailToUri(email, payload.Subject, messageToSend));
                        await ShowToastAsync("Cliente de e-mail aberto com a cobrança.", "Info");
                        break;

                    case CobrancaAction.WhatsApp:
                        if (!canSendWhatsApp)
                        {
                            await ShowToastAsync("Cliente sem telefone válido para WhatsApp.", "Info");
                            break;
                        }

                        OpenUri($"https://wa.me/{whatsappPhone}?text={Uri.EscapeDataString(messageToSend)}");
                        await ShowToastAsync("WhatsApp aberto com a mensagem pronta.", "Info");
                        break;
                }
            }
            catch (Exception ex)
            {
                await LogAndToastErrorAsync("Falha ao abrir canal de cobrança.", "Falha ao abrir canal de cobrança", ex);
            }
        }
    }
}
