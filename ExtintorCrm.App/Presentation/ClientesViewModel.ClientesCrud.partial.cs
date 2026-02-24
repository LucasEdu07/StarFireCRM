using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using ExtintorCrm.App.Domain;

namespace ExtintorCrm.App.Presentation
{
    public partial class ClientesViewModel
    {
        private async Task SearchAsync()
        {
            if (string.IsNullOrWhiteSpace(SearchTerm))
            {
                await LoadAsync();
                return;
            }

            var clientes = await _clienteRepository.SearchAsync(SearchTerm);
            ReplaceClientes(clientes);
            UpdateDashboardPaymentCounters();
        }

        private async Task NewAsync()
        {
            var formViewModel = new ClienteFormViewModel();
            var form = new ClienteFormWindow(formViewModel)
            {
                Owner = Application.Current?.MainWindow
            };

            if (form.ShowDialog() != true)
            {
                return;
            }

            var novoCliente = formViewModel.ToCliente();
            if (!string.IsNullOrWhiteSpace(novoCliente.CPF) &&
                await _clienteRepository.ExistsByCpfAsync(NormalizeDigits(novoCliente.CPF)!))
            {
                await ShowToastAsync("Já existe um cliente com este CPF/CNPJ.", "Error");
                return;
            }

            await _clienteRepository.AddAsync(novoCliente);
            await ReloadListAsync();
            await ShowToastAsync("Cliente criado com sucesso.", "Success");
        }

        private async Task EditAsync()
        {
            if (!CanEditSelectedCliente || SelectedCliente == null)
            {
                return;
            }

            var formViewModel = new ClienteFormViewModel(SelectedCliente);
            var form = new ClienteFormWindow(formViewModel)
            {
                Owner = Application.Current?.MainWindow
            };

            if (form.ShowDialog() != true)
            {
                return;
            }

            var clienteAtualizado = formViewModel.ToCliente();
            if (!string.IsNullOrWhiteSpace(clienteAtualizado.CPF) &&
                await _clienteRepository.ExistsByCpfAsync(NormalizeDigits(clienteAtualizado.CPF)!, SelectedCliente.Id))
            {
                await ShowToastAsync("Já existe outro cliente com este CPF/CNPJ.", "Error");
                return;
            }
            clienteAtualizado.Id = SelectedCliente.Id;
            clienteAtualizado.CriadoEm = SelectedCliente.CriadoEm;
            await _clienteRepository.UpdateAsync(clienteAtualizado);
            await ReloadListAsync();
            await ShowToastAsync("Cliente atualizado com sucesso.", "Success");
        }

        private async Task DeleteAsync()
        {
            if (!CanDeleteSelectedClientes)
            {
                return;
            }

            var selected = _selectedClientes.Any() ? _selectedClientes.ToList() : (SelectedCliente != null ? new List<Cliente> { SelectedCliente } : []);
            if (selected.Count == 0)
            {
                return;
            }

            var nome = selected.Count == 1
                ? (string.IsNullOrWhiteSpace(selected[0].NomeFantasia) ? "este cliente" : selected[0].NomeFantasia)
                : $"{selected.Count} clientes selecionados";
            var confirmed = DialogService.Confirm(
                "Excluir cliente",
                $"Deseja realmente excluir {nome}?",
                Application.Current?.MainWindow);

            if (!confirmed)
            {
                return;
            }

            foreach (var cliente in selected)
            {
                await _clienteRepository.DeleteAsync(cliente.Id);
            }

            UpdateSelectedClientes([]);
            await ReloadListAsync();
            await ShowToastAsync(selected.Count == 1 ? "Cliente excluído com sucesso." : "Clientes excluídos com sucesso.", "Success");
        }

        private async Task ShowDetailsAsync()
        {
            if (SelectedCliente == null)
            {
                return;
            }

            var selectedCpf = NormalizeDigits(SelectedCliente.CPF ?? SelectedCliente.Documento);
            var pagamentosCliente = _allPagamentos
                .Where(p =>
                    p.ClienteId == SelectedCliente.Id ||
                    (!string.IsNullOrWhiteSpace(selectedCpf) && NormalizeDigits(p.CpfCnpjCliente) == selectedCpf))
                .ToList();
            var detalhesViewModel = new ClienteDetalhesViewModel(SelectedCliente, pagamentosCliente);
            var detalhesWindow = new ClienteDetalhesWindow(detalhesViewModel)
            {
                Owner = Application.Current?.MainWindow
            };

            detalhesWindow.ShowDialog();

            if (!detalhesWindow.SaveRequested)
            {
                return;
            }

            var clienteAtualizado = detalhesViewModel.BuildUpdatedCliente();
            if (!string.IsNullOrWhiteSpace(clienteAtualizado.CPF) &&
                await _clienteRepository.ExistsByCpfAsync(NormalizeDigits(clienteAtualizado.CPF)!, SelectedCliente.Id))
            {
                await ShowToastAsync("Já existe outro cliente com este CPF/CNPJ.", "Error");
                return;
            }
            var pagamentosAtualizados = detalhesViewModel.BuildUpdatedPagamentos();
            await _clienteRepository.UpdateAsync(clienteAtualizado);
            foreach (var pagamento in pagamentosAtualizados)
            {
                await _pagamentoRepository.UpdateAsync(pagamento);
            }
            await ReloadListAsync();
            var reselected = _allClientes.FirstOrDefault(c => c.Id == clienteAtualizado.Id);
            UpdateSelectedClientes(reselected is null ? [] : [reselected]);
            await ShowToastAsync("Cliente atualizado com sucesso.", "Success");
        }

        private async Task ReloadListAsync()
        {
            if (string.IsNullOrWhiteSpace(SearchTerm))
            {
                await LoadAsync();
                return;
            }

            await SearchAsync();
        }

        private void ReplaceClientes(IEnumerable<Cliente> clientes)
        {
            var list = clientes.ToList();
            _allClientes.Clear();
            _allClientes.AddRange(list);
            _alertService.ApplyAlerts(list);
            ApplyClienteStatusFilter();

            var ext = _alertService.CountExtintores(list);
            var alv = _alertService.CountAlvaras(list);
            Dashboard.ExtintoresVencidos = ext.Vencidos;
            Dashboard.ExtintoresVencendo = ext.Vencendo;
            Dashboard.AlvaraVencido = alv.Vencidos;
            Dashboard.AlvaraVencendo = alv.Vencendo;
            UpdateDashboardPaymentCounters();
            RefreshCriticalAlerts();
            RefreshDashboardExecutiveData();

        }

        private void ApplyClienteStatusFilter()
        {
            IEnumerable<Cliente> query = _allClientes;

            if (ClienteStatusTabIndex == 0)
            {
                query = query.Where(c => c.IsAtivo);
            }
            else
            {
                query = query.Where(c => !c.IsAtivo);
            }

            var filtered = query.ToList();

            Clientes.Clear();
            foreach (var cliente in filtered)
            {
                Clientes.Add(cliente);
            }

            if (SelectedCliente != null && filtered.All(c => c.Id != SelectedCliente.Id))
            {
                UpdateSelectedClientes([]);
            }
            else if (_selectedClientes.Count > 0)
            {
                var validSelection = _selectedClientes
                    .Where(s => filtered.Any(c => c.Id == s.Id))
                    .ToList();

                if (validSelection.Count != _selectedClientes.Count)
                {
                    UpdateSelectedClientes(validSelection);
                }
            }

            TotalClientes = Clientes.Count;
            PageNumber = 1;
            PageCount = 1;
            _exportCommand.RaiseCanExecuteChanged();
            OnPropertyChanged(nameof(CanGoPrev));
            OnPropertyChanged(nameof(CanGoNext));
            _previousPageCommand.RaiseCanExecuteChanged();
            _nextPageCommand.RaiseCanExecuteChanged();
        }
    }
}
