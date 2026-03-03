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

            IsLoadingClientes = true;
            try
            {
                var clientes = await _clienteRepository.SearchAsync(SearchTerm);
                ReplaceClientes(clientes);
                UpdateDashboardPaymentCounters();
            }
            finally
            {
                IsLoadingClientes = false;
            }
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
            var createdCliente = _allClientes.FirstOrDefault(c => c.Id == novoCliente.Id);
            if (createdCliente != null)
            {
                UpdateSelectedClientes([createdCliente]);
            }

            await ShowToastAsync(
                "Cliente criado com sucesso.",
                "Success",
                "Abrir cadastro",
                async () => await OpenClienteDetalhesAsync(startInEditMode: false));
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

            var deletedSnapshots = selected
                .Select(CloneClienteForUndo)
                .ToArray();

            foreach (var cliente in selected)
            {
                await _clienteRepository.DeleteAsync(cliente.Id);
            }

            UpdateSelectedClientes([]);
            await ReloadListAsync();
            await ShowToastAsync(
                selected.Count == 1 ? "Cliente excluído com sucesso." : "Clientes excluídos com sucesso.",
                "Success",
                "Desfazer",
                async () => await RestoreDeletedClientesAsync(deletedSnapshots),
                6000);
        }

        private async Task SetSelectedClientesActiveStateAsync(bool isAtivo)
        {
            var selected = _selectedClientes.Any()
                ? _selectedClientes.ToList()
                : (SelectedCliente != null ? new List<Cliente> { SelectedCliente } : []);

            if (selected.Count == 0)
            {
                await ShowToastAsync("Selecione ao menos um cliente.", "Info");
                return;
            }

            var changes = selected
                .Where(c => c.IsAtivo != isAtivo)
                .Select(c => (Snapshot: CloneClienteForUndo(c), PreviousIsAtivo: c.IsAtivo))
                .ToList();

            if (changes.Count == 0)
            {
                await ShowToastAsync(
                    isAtivo ? "Os clientes selecionados já estão ativos." : "Os clientes selecionados já estão inativos.",
                    "Info");
                return;
            }

            foreach (var change in changes)
            {
                var updated = CloneClienteForUndo(change.Snapshot);
                updated.IsAtivo = isAtivo;
                await _clienteRepository.UpdateAsync(updated);
            }

            await ReloadListAsync();
            await ShowToastAsync(
                isAtivo
                    ? $"{changes.Count} cliente(s) ativado(s) com sucesso."
                    : $"{changes.Count} cliente(s) inativado(s) com sucesso.",
                "Success",
                "Desfazer",
                async () => await RestoreClientesActiveStateAsync(changes),
                6000);
        }

        private async Task RestoreClientesActiveStateAsync(IReadOnlyList<(Cliente Snapshot, bool PreviousIsAtivo)> changes)
        {
            if (changes.Count == 0)
            {
                return;
            }

            var restored = 0;
            foreach (var change in changes)
            {
                var current = await _clienteRepository.GetByIdAsync(change.Snapshot.Id);
                if (current == null)
                {
                    continue;
                }

                var updated = CloneClienteForUndo(current);
                updated.IsAtivo = change.PreviousIsAtivo;
                await _clienteRepository.UpdateAsync(updated);
                restored++;
            }

            await ReloadListAsync();
            await ShowToastAsync(
                restored > 0
                    ? $"Desfazer concluído: {restored} cliente(s) restaurado(s)."
                    : "Nenhum cliente pôde ser restaurado.",
                restored > 0 ? "Success" : "Info");
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
            detalhesWindow.SaveChangesAsync = () => SaveClienteDetalhesAsync(detalhesViewModel);

            detalhesWindow.ShowDialog();

            if (!detalhesWindow.SaveRequested)
            {
                return;
            }
            await SaveClienteDetalhesAsync(detalhesViewModel);
        }

        private async Task<bool> SaveClienteDetalhesAsync(ClienteDetalhesViewModel detalhesViewModel)
        {
            if (SelectedCliente == null)
            {
                return false;
            }

            var clienteAtualizado = detalhesViewModel.BuildUpdatedCliente();
            if (!string.IsNullOrWhiteSpace(clienteAtualizado.CPF) &&
                await _clienteRepository.ExistsByCpfAsync(NormalizeDigits(clienteAtualizado.CPF)!, SelectedCliente.Id))
            {
                await ShowToastAsync("Já existe outro cliente com este CPF/CNPJ.", "Error");
                return false;
            }

            var pagamentosAtualizados = detalhesViewModel.BuildUpdatedPagamentos();
            await _clienteRepository.UpdateAsync(clienteAtualizado);
            foreach (var pagamento in pagamentosAtualizados)
            {
                await _pagamentoRepository.UpdateAsync(pagamento);
            }

            detalhesViewModel.AcceptCurrentStateAsSaved();
            await ReloadListAsync();
            var reselected = _allClientes.FirstOrDefault(c => c.Id == clienteAtualizado.Id);
            UpdateSelectedClientes(reselected is null ? [] : [reselected]);
            await ShowToastAsync("Cliente atualizado com sucesso.", "Success");
            return true;
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

        private async Task RestoreDeletedClientesAsync(Cliente[] snapshots)
        {
            if (snapshots.Length == 0)
            {
                return;
            }

            var restoredIds = new List<Guid>();
            var skippedCount = 0;

            foreach (var snapshot in snapshots)
            {
                if (await _clienteRepository.GetByIdAsync(snapshot.Id) != null)
                {
                    skippedCount++;
                    continue;
                }

                var normalizedCpf = NormalizeDigits(snapshot.CPF ?? snapshot.Documento);
                if (!string.IsNullOrWhiteSpace(normalizedCpf) && await _clienteRepository.ExistsByCpfAsync(normalizedCpf!))
                {
                    skippedCount++;
                    continue;
                }

                await _clienteRepository.AddAsync(CloneClienteForUndo(snapshot));
                restoredIds.Add(snapshot.Id);
            }

            await ReloadListAsync();

            var reselected = _allClientes.Where(c => restoredIds.Contains(c.Id)).ToList();
            if (reselected.Count > 0)
            {
                UpdateSelectedClientes(reselected);
            }

            var restoredCount = restoredIds.Count;
            var summary = skippedCount > 0
                ? $"Desfazer concluído: {restoredCount} restaurado(s), {skippedCount} ignorado(s)."
                : $"Desfazer concluído: {restoredCount} restaurado(s).";
            await ShowToastAsync(summary, restoredCount > 0 ? "Success" : "Info");
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

            if (!string.Equals(ClienteSituacaoFilter, "Todos", System.StringComparison.Ordinal))
            {
                query = query.Where(c => string.Equals(c.SituacaoNivel, ClienteSituacaoFilter, System.StringComparison.OrdinalIgnoreCase));
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
            OnPropertyChanged(nameof(CanResetClienteFilters));
            OnPropertyChanged(nameof(CanClearClientesSearchAndFilters));
            _resetClienteFiltersCommand.RaiseCanExecuteChanged();
            _removeClienteFilterCommand.RaiseCanExecuteChanged();
            _clearClientesSearchAndFiltersCommand.RaiseCanExecuteChanged();
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

            ApplyClientesSortingAndRefreshPage();
            OnPropertyChanged(nameof(ClientesSortMember));
            OnPropertyChanged(nameof(ClientesSortDirection));
            OnPropertyChanged(nameof(ClienteSortField));
            OnPropertyChanged(nameof(ClienteSortDirection));
            OnPropertyChanged(nameof(CanResetClienteFilters));
            OnPropertyChanged(nameof(CanClearClientesSearchAndFilters));
            _resetClienteFiltersCommand.RaiseCanExecuteChanged();
            _removeClienteFilterCommand.RaiseCanExecuteChanged();
            _clearClientesSearchAndFiltersCommand.RaiseCanExecuteChanged();
        }

        private void ResetClienteFilters()
        {
            _clienteStatusTabIndex = 0;
            _clienteSituacaoFilter = "Todos";
            _clientesSortMember = nameof(Cliente.NomeFantasia);
            _clientesSortDirection = System.ComponentModel.ListSortDirection.Ascending;

            ApplyClienteStatusFilter();

            OnPropertyChanged(nameof(ClienteStatusTabIndex));
            OnPropertyChanged(nameof(IsClienteAtivosMode));
            OnPropertyChanged(nameof(ClienteSituacaoFilter));
            OnPropertyChanged(nameof(ClienteSortField));
            OnPropertyChanged(nameof(ClienteSortDirection));
            OnPropertyChanged(nameof(ClientesSortMember));
            OnPropertyChanged(nameof(ClientesSortDirection));
            OnPropertyChanged(nameof(CanResetClienteFilters));
            OnPropertyChanged(nameof(CanClearClientesSearchAndFilters));
            _resetClienteFiltersCommand.RaiseCanExecuteChanged();
            _removeClienteFilterCommand.RaiseCanExecuteChanged();
            _clearClientesSearchAndFiltersCommand.RaiseCanExecuteChanged();
        }

        private void ClearClientesSearchAndFilters()
        {
            var hasSearch = !string.IsNullOrWhiteSpace(SearchTerm);
            var hasAdvancedFilters = CanResetClienteFilters;
            if (!hasSearch && !hasAdvancedFilters)
            {
                return;
            }

            if (hasSearch)
            {
                SearchTerm = string.Empty;
            }

            if (hasAdvancedFilters)
            {
                ResetClienteFilters();
            }
        }

        private void RemoveClienteFilter(string? filterKey)
        {
            switch (filterKey?.Trim().ToLowerInvariant())
            {
                case "status":
                    if (ClienteStatusTabIndex != 0)
                    {
                        ClienteStatusTabIndex = 0;
                    }

                    break;

                case "situacao":
                    if (!string.Equals(ClienteSituacaoFilter, "Todos", System.StringComparison.Ordinal))
                    {
                        ClienteSituacaoFilter = "Todos";
                    }

                    break;

                case "sortfield":
                    if (!string.Equals(ClienteSortField, "Nome", System.StringComparison.Ordinal))
                    {
                        ClienteSortField = "Nome";
                    }

                    break;

                case "sortdirection":
                    if (!string.Equals(ClienteSortDirection, "Crescente", System.StringComparison.Ordinal))
                    {
                        ClienteSortDirection = "Crescente";
                    }

                    break;

                default:
                    ResetClienteFilters();
                    break;
            }
        }

        private static string NormalizeClienteSituacaoFilter(string? value)
        {
            var normalized = value?.Trim();
            return normalized switch
            {
                "Vencido" => "Vencido",
                "Vencendo" => "Vencendo",
                "OK" => "OK",
                _ => "Todos"
            };
        }

        private static string MapSortDisplayToMember(string? display)
        {
            return display?.Trim() switch
            {
                "CPF/CNPJ" => nameof(Cliente.CPF),
                "Telefone" => nameof(Cliente.Telefone1),
                "Cidade" => nameof(Cliente.Cidade),
                "Vencimento Extintores" => nameof(Cliente.VencimentoExtintores),
                "Situação" => nameof(Cliente.SituacaoTexto),
                _ => nameof(Cliente.NomeFantasia)
            };
        }

        private static string MapSortMemberToDisplay(string? member)
        {
            return member switch
            {
                nameof(Cliente.CPF) => "CPF/CNPJ",
                nameof(Cliente.Telefone1) => "Telefone",
                nameof(Cliente.Cidade) => "Cidade",
                nameof(Cliente.VencimentoExtintores) => "Vencimento Extintores",
                nameof(Cliente.SituacaoTexto) => "Situação",
                _ => "Nome"
            };
        }

        private static System.ComponentModel.ListSortDirection ParseSortDirection(string? direction)
        {
            return string.Equals(direction?.Trim(), "Decrescente", System.StringComparison.OrdinalIgnoreCase)
                ? System.ComponentModel.ListSortDirection.Descending
                : System.ComponentModel.ListSortDirection.Ascending;
        }

        private void ApplyClientesSortingAndRefreshPage()
        {
            ApplyClientesSorting();
            ApplyClientesPage();
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

