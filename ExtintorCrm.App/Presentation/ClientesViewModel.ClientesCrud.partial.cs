using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using ExtintorCrm.App.Domain;

namespace ExtintorCrm.App.Presentation
{
    public partial class ClientesViewModel
    {
        private void QueueSearch()
        {
            _searchDebounceCts?.Cancel();
            _searchDebounceCts?.Dispose();
            _searchDebounceCts = new CancellationTokenSource();
            var token = _searchDebounceCts.Token;

            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(220, token);
                    await Application.Current.Dispatcher.InvokeAsync(async () =>
                    {
                        if (token.IsCancellationRequested)
                        {
                            return;
                        }

                        await SearchAsync();
                    });
                }
                catch (TaskCanceledException)
                {
                    // debounce cancelado
                }
            }, token);
        }

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
            await OpenClienteDetalhesAsync(startInEditMode: true);
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
            await OpenClienteDetalhesAsync(startInEditMode: false);
        }

        private async Task OpenClienteDetalhesAsync(bool startInEditMode)
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
            detalhesViewModel.IsEditMode = startInEditMode;
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
            _filteredClientes.Clear();
            _filteredClientes.AddRange(filtered);
            ApplyClientesSorting();

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

            TotalClientes = _filteredClientes.Count;
            PageCount = TotalClientes == 0 ? 1 : (int)System.Math.Ceiling(TotalClientes / (double)ClientesPageSize);
            PageNumber = 1;
            ApplyClientesPage();
            RefreshPageIndexes();
            _exportCommand.RaiseCanExecuteChanged();
            OnPropertyChanged(nameof(CanGoPrev));
            OnPropertyChanged(nameof(CanGoNext));
            _previousPageCommand.RaiseCanExecuteChanged();
            _nextPageCommand.RaiseCanExecuteChanged();
            _goToPageCommand.RaiseCanExecuteChanged();
        }

        public void SortClientesBy(string? sortMemberPath)
        {
            if (string.IsNullOrWhiteSpace(sortMemberPath))
            {
                return;
            }

            if (string.Equals(_clientesSortMember, sortMemberPath, System.StringComparison.Ordinal))
            {
                _clientesSortDirection = _clientesSortDirection == System.ComponentModel.ListSortDirection.Ascending
                    ? System.ComponentModel.ListSortDirection.Descending
                    : System.ComponentModel.ListSortDirection.Ascending;
            }
            else
            {
                _clientesSortMember = sortMemberPath;
                _clientesSortDirection = System.ComponentModel.ListSortDirection.Ascending;
            }

            ApplyClientesSorting();
            ApplyClientesPage();
            OnPropertyChanged(nameof(ClientesSortMember));
            OnPropertyChanged(nameof(ClientesSortDirection));
        }

        private void ApplyClientesSorting()
        {
            var source = _filteredClientes.ToList();
            IOrderedEnumerable<Cliente> ordered = _clientesSortMember switch
            {
                nameof(Cliente.CPF) => OrderByDirection(source, c => NormalizeSortString(c.CPF ?? c.Documento)),
                nameof(Cliente.Telefone1) => OrderByDirection(source, c => NormalizeSortString(c.Telefone1 ?? c.Telefone)),
                nameof(Cliente.Cidade) => OrderByDirection(source, c => NormalizeSortString(c.Cidade)),
                nameof(Cliente.VencimentoExtintores) => OrderByDirection(source, c => c.VencimentoExtintores ?? c.VencimentoServico ?? System.DateTime.MaxValue),
                nameof(Cliente.SituacaoTexto) => OrderByDirection(source, c => NormalizeSortString(c.SituacaoTexto)),
                _ => OrderByDirection(source, c => NormalizeSortString(c.NomeFantasia))
            };

            _filteredClientes.Clear();
            _filteredClientes.AddRange(ordered.ToList());
        }

        private IOrderedEnumerable<Cliente> OrderByDirection<TKey>(IEnumerable<Cliente> source, System.Func<Cliente, TKey> keySelector)
        {
            return _clientesSortDirection == System.ComponentModel.ListSortDirection.Ascending
                ? source.OrderBy(keySelector)
                : source.OrderByDescending(keySelector);
        }

        private static string NormalizeSortString(string? value)
        {
            return value?.Trim().ToUpperInvariant() ?? string.Empty;
        }

        private void ChangeClientesPage(int delta)
        {
            if (TotalClientes == 0)
            {
                return;
            }

            var next = PageNumber + delta;
            if (next < 1)
            {
                next = 1;
            }
            else if (next > PageCount)
            {
                next = PageCount;
            }

            if (next == PageNumber)
            {
                return;
            }

            PageNumber = next;
            ApplyClientesPage();
            RefreshPageIndexes();
            OnPropertyChanged(nameof(CanGoPrev));
            OnPropertyChanged(nameof(CanGoNext));
            _previousPageCommand.RaiseCanExecuteChanged();
            _nextPageCommand.RaiseCanExecuteChanged();
            _goToPageCommand.RaiseCanExecuteChanged();
        }

        private bool CanGoToPage(object? page)
        {
            if (page is null || PageCount <= 0)
            {
                return false;
            }

            var value = page switch
            {
                int i => i,
                string s when int.TryParse(s, out var parsed) => parsed,
                _ => 0
            };

            return value >= 1 && value <= PageCount && value != PageNumber;
        }

        private void GoToPage(object? page)
        {
            if (!CanGoToPage(page))
            {
                return;
            }

            var target = page is int i ? i : int.Parse(page!.ToString()!);
            PageNumber = target;
            ApplyClientesPage();
            RefreshPageIndexes();
            OnPropertyChanged(nameof(CanGoPrev));
            OnPropertyChanged(nameof(CanGoNext));
            _previousPageCommand.RaiseCanExecuteChanged();
            _nextPageCommand.RaiseCanExecuteChanged();
            _goToPageCommand.RaiseCanExecuteChanged();
        }

        private void RefreshPageIndexes()
        {
            PageIndexes.Clear();
            if (PageCount <= 0)
            {
                return;
            }

            const int maxVisible = 7;
            var halfWindow = maxVisible / 2;
            var start = System.Math.Max(1, PageNumber - halfWindow);
            var end = System.Math.Min(PageCount, start + maxVisible - 1);

            if (end - start + 1 < maxVisible)
            {
                start = System.Math.Max(1, end - maxVisible + 1);
            }

            for (var i = start; i <= end; i++)
            {
                PageIndexes.Add(i);
            }
        }

        private void ApplyClientesPage()
        {
            Clientes.Clear();

            if (_filteredClientes.Count == 0)
            {
                return;
            }

            var skip = (PageNumber - 1) * ClientesPageSize;
            var pageItems = _filteredClientes.Skip(skip).Take(ClientesPageSize);
            foreach (var cliente in pageItems)
            {
                Clientes.Add(cliente);
            }

            if (SelectedCliente != null && Clientes.All(c => c.Id != SelectedCliente.Id))
            {
                UpdateSelectedClientes([]);
            }
        }
    }
}

