using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ExtintorCrm.App.Domain;
using ExtintorCrm.App.Infrastructure.Settings;

namespace ExtintorCrm.App.Presentation
{
    public partial class ClientesViewModel
    {
        private async Task LoadAlertSettingsAsync()
        {
            var config = await _configuracaoAlertaRepository.GetAsync();
            Alerta7Dias = config.Alerta7Dias;
            Alerta15Dias = config.Alerta15Dias;
            Alerta30Dias = config.Alerta30Dias;
            ApplyAlertRulesFromSettings();
        }

        private async Task SaveAlertSettingsAsync()
        {
            var config = new ConfiguracaoAlerta
            {
                Id = 1,
                Alerta7Dias = Alerta7Dias,
                Alerta15Dias = Alerta15Dias,
                Alerta30Dias = Alerta30Dias
            };

            try
            {
                await _configuracaoAlertaRepository.SaveAsync(config);
                var theme = IsDarkMode ? AppThemeManager.DarkTheme : AppThemeManager.LightTheme;
                SaveAppSettings(theme);
                AppThemeManager.ApplyTheme(theme);
                ApplyAlertRulesFromSettings();
                await ReloadListAsync();
                StartBackupScheduler();
                await ShowToastAsync("Configurações salvas com sucesso.", "Success");
            }
            catch (Exception ex)
            {
                await LogAndToastErrorAsync("Falha ao salvar configurações de alerta.", "Não foi possível salvar as configurações", ex);
            }
        }

        private void ApplyAlertRulesFromSettings()
        {
            var selectedDays = new List<int>();
            if (Alerta7Dias)
            {
                selectedDays.Add(7);
            }

            if (Alerta15Dias)
            {
                selectedDays.Add(15);
            }

            if (Alerta30Dias)
            {
                selectedDays.Add(30);
            }

            _alertRules.SetAlertDays(selectedDays.ToArray());
            OnPropertyChanged(nameof(AlertWindowText));
        }

        private void UpdateDashboardPaymentCounters()
        {
            var pagamentos = _alertService.CountPagamentos(_allPagamentos);
            Dashboard.PagamentosVencidos = pagamentos.Vencidos;
            Dashboard.PagamentosVencendo = pagamentos.Vencendo;
        }

        private void RefreshDashboardExecutiveData()
        {
            var alertItems = BuildDashboardAlertItems();

            Dashboard.ExtintoresVencidos = alertItems.Count(x => x.Tipo == "Extintor" && x.Status == "Vencido");
            Dashboard.ExtintoresVencendo7 = alertItems.Count(x => x.Tipo == "Extintor" && x.DiasParaVencer.HasValue && x.DiasParaVencer.Value >= 0 && x.DiasParaVencer.Value <= 7);
            Dashboard.ExtintoresVencendo30 = alertItems.Count(x => x.Tipo == "Extintor" && x.DiasParaVencer.HasValue && x.DiasParaVencer.Value >= 0 && x.DiasParaVencer.Value <= 30);
            Dashboard.AlvaraVencido = alertItems.Count(x => x.Tipo == "Alvará" && x.Status == "Vencido");
            Dashboard.PagamentosVencidos = alertItems.Count(x => x.Tipo == "Pagamento" && x.Status == "Vencido");
            Dashboard.PagamentosVencendo30 = alertItems.Count(x => x.Tipo == "Pagamento" && x.DiasParaVencer.HasValue && x.DiasParaVencer.Value >= 0 && x.DiasParaVencer.Value <= 30);

            var criticalOrdered = alertItems
                .Where(x => x.Status == "Vencido" || x.Status == "Vencendo")
                .OrderBy(x => x.Status == "Vencido" ? 0 : 1)
                .ThenBy(x => x.DiasParaVencer ?? int.MaxValue)
                .Take(10)
                .ToList();

            CriticalItems.Clear();
            foreach (var item in criticalOrdered)
            {
                CriticalItems.Add(item);
            }

            var next7 = alertItems
                .Where(x => x.DiasParaVencer.HasValue && x.DiasParaVencer.Value >= 0 && x.DiasParaVencer.Value <= 7)
                .OrderBy(x => x.DiasParaVencer)
                .ThenBy(x => x.DataVencimento)
                .Take(5)
                .ToList();

            Next7Days.Clear();
            foreach (var item in next7)
            {
                Next7Days.Add(item);
            }

            var next30 = alertItems
                .Where(x => x.DiasParaVencer.HasValue && x.DiasParaVencer.Value >= 0 && x.DiasParaVencer.Value <= 30)
                .OrderBy(x => x.DiasParaVencer)
                .ThenBy(x => x.DataVencimento)
                .Take(5)
                .ToList();

            Next30Days.Clear();
            foreach (var item in next30)
            {
                Next30Days.Add(item);
            }

            Dashboard.AlertasOk = alertItems.Count(x => x.Status == "OK");
            Dashboard.AlertasVencendo = alertItems.Count(x => x.Status == "Vencendo");
            Dashboard.AlertasVencidos = alertItems.Count(x => x.Status == "Vencido");
            OnPropertyChanged(nameof(PendingNotificationCount));
            OnPropertyChanged(nameof(HasPendingNotifications));
            if (!HasPendingNotifications)
            {
                IsNotificationPanelOpen = false;
            }

            var total = Dashboard.AlertasOk + Dashboard.AlertasVencendo + Dashboard.AlertasVencidos;
            if (total == 0)
            {
                Dashboard.AlertasOkPercent = 0;
                Dashboard.AlertasVencendoPercent = 0;
                Dashboard.AlertasVencidosPercent = 0;
            }
            else
            {
                Dashboard.AlertasOkPercent = (double)Dashboard.AlertasOk / total * 100;
                Dashboard.AlertasVencendoPercent = (double)Dashboard.AlertasVencendo / total * 100;
                Dashboard.AlertasVencidosPercent = (double)Dashboard.AlertasVencidos / total * 100;
            }
        }

        private List<DashboardAlertItem> BuildDashboardAlertItems()
        {
            var today = DateTime.Today;
            var items = new List<DashboardAlertItem>();

            foreach (var cliente in _allClientes)
            {
                items.Add(BuildAlertFromDueDate(cliente.Id, cliente.NomeFantasia, "Extintor", cliente.VencimentoExtintores, today));
                items.Add(BuildAlertFromDueDate(cliente.Id, cliente.NomeFantasia, "Alvará", cliente.VencimentoAlvara, today));
            }

            foreach (var pagamento in _allPagamentos.Where(p => !p.Pago))
            {
                items.Add(BuildAlertFromDueDate(
                    pagamento.ClienteId,
                    string.IsNullOrWhiteSpace(pagamento.ClienteNome) ? "Cliente não identificado" : pagamento.ClienteNome!,
                    "Pagamento",
                    pagamento.DataVencimento,
                    today));
            }

            return items;
        }

        private static DashboardAlertItem BuildAlertFromDueDate(Guid clienteId, string clienteNome, string tipo, DateTime? dueDate, DateTime today)
        {
            if (!dueDate.HasValue)
            {
                return new DashboardAlertItem
                {
                    ClienteId = clienteId,
                    ClienteNome = clienteNome,
                    Tipo = tipo,
                    DataVencimento = null,
                    DiasParaVencer = null,
                    Status = "OK"
                };
            }

            var days = (dueDate.Value.Date - today).Days;
            var status = days < 0
                ? "Vencido"
                : days <= 30
                    ? "Vencendo"
                    : "OK";

            return new DashboardAlertItem
            {
                ClienteId = clienteId,
                ClienteNome = clienteNome,
                Tipo = tipo,
                DataVencimento = dueDate,
                DiasParaVencer = days,
                Status = status
            };
        }

        private async Task OpenDashboardItemAsync(DashboardAlertItem? item)
        {
            if (item == null)
            {
                return;
            }

            IsNotificationPanelOpen = false;

            var cliente = _allClientes.FirstOrDefault(x => x.Id == item.ClienteId);
            if (cliente == null)
            {
                await ShowToastAsync("Cliente não encontrado para este alerta.", "Info");
                return;
            }

            UpdateSelectedClientes([cliente]);
            await ShowDetailsAsync();
        }

        private void RefreshCriticalAlerts()
        {
            var alerts = new List<CriticalAlertItem>();

            foreach (var cliente in _allClientes)
            {
                if (cliente.ExtintorStatus == "Vencido" || cliente.ExtintorStatus == "Vencendo")
                {
                    alerts.Add(new CriticalAlertItem
                    {
                        Cliente = cliente.NomeFantasia,
                        Tipo = "Extintor",
                        Dias = cliente.ExtintorDaysToDue ?? 0,
                        Nivel = cliente.ExtintorStatus
                    });
                }

                if (cliente.AlvaraStatus == "Vencido" || cliente.AlvaraStatus == "Vencendo")
                {
                    alerts.Add(new CriticalAlertItem
                    {
                        Cliente = cliente.NomeFantasia,
                        Tipo = "Alvará",
                        Dias = cliente.AlvaraDaysToDue ?? 0,
                        Nivel = cliente.AlvaraStatus
                    });
                }
            }

            foreach (var pagamento in _allPagamentos.Where(p => !p.Pago && (p.SituacaoNivel == "Vencido" || p.SituacaoNivel == "Vencendo")))
            {
                alerts.Add(new CriticalAlertItem
                {
                    Cliente = string.IsNullOrWhiteSpace(pagamento.ClienteNome) ? "Cliente não identificado" : pagamento.ClienteNome,
                    Tipo = "Pagamento",
                    Dias = pagamento.DaysToDue ?? 0,
                    Nivel = pagamento.SituacaoNivel
                });
            }

            var ordered = alerts
                .OrderBy(a => a.Nivel == "Vencido" ? 0 : 1)
                .ThenBy(a => a.Dias)
                .ToList();

            CriticalAlertsAll.Clear();
            foreach (var item in ordered)
            {
                CriticalAlertsAll.Add(item);
            }

            CriticalAlertsTop.Clear();
            foreach (var item in ordered.Take(5))
            {
                CriticalAlertsTop.Add(item);
            }

            if (!HasCriticalAlerts)
            {
                ShowAllCriticalAlerts = false;
            }

            OnPropertyChanged(nameof(HasCriticalAlerts));
            (ViewAllCriticalAlertsCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }
    }
}
