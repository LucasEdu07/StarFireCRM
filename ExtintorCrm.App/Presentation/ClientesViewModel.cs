using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using ExtintorCrm.App.Domain;
using ExtintorCrm.App.Infrastructure;
using ExtintorCrm.App.Infrastructure.Export;
using ExtintorCrm.App.Infrastructure.Settings;
using ExtintorCrm.App.UseCases;
using ExtintorCrm.App.UseCases.Alerts;
namespace ExtintorCrm.App.Presentation
{
    public partial class ClientesViewModel : ViewModelBase
    {
        private readonly IClienteRepository _clienteRepository;
        private readonly IPagamentoRepository _pagamentoRepository;
        private readonly IConfiguracaoAlertaRepository _configuracaoAlertaRepository;
        private readonly RelayCommand _editCommand;
        private readonly RelayCommand _deleteCommand;
        private readonly RelayCommand _detailsCommand;
        private readonly RelayCommand _importCommand;
        private readonly RelayCommand _exportCommand;
        private readonly RelayCommand _backupCommand;
        private readonly RelayCommand _previousPageCommand;
        private readonly RelayCommand _nextPageCommand;
        private readonly RelayCommand _newPagamentoCommand;
        private readonly RelayCommand _editPagamentoCommand;
        private readonly RelayCommand _deletePagamentoCommand;
        private readonly RelayCommand _cobrancaCommand;
        private readonly RelayCommand _saveAlertSettingsCommand;
        private readonly RelayCommand _selectBackupFolderCommand;
        private readonly RelayCommand _restoreBackupCommand;
        private readonly RelayCommand _recreateClientesCommand;
        private readonly RelayCommand _recreatePagamentosCommand;
        private readonly RelayCommand _importPagamentosCommand;
        private readonly RelayCommand _goToClientesCommand;
        private readonly RelayCommand _goToPagamentosCommand;
        private readonly RelayCommand _goToDashboardCommand;
        private readonly RelayCommand _toggleNotificationsCommand;
        private readonly RelayCommand _openDashboardItemCommand;
        private readonly RelayCommand _selectConfigSectionCommand;
        private readonly RelayCommand _contactSupportWhatsAppCommand;
        private readonly RelayCommand _contactSupportEmailCommand;
        private readonly AlertService _alertService;
        private readonly AlertRules _alertRules;
        private readonly AppSettingsService _appSettingsService;
        private readonly IReadOnlyList<ExportColumnDefinition<Cliente>> _clienteExportColumns;
        private readonly IReadOnlyList<ExportColumnDefinition<Pagamento>> _pagamentoExportColumns;
        private readonly HashSet<string> _preferredClienteExportFields = new();
        private readonly HashSet<string> _preferredPagamentoExportFields = new();
        private const double DefaultWindowWidth = 1380;
        private const double DefaultWindowHeight = 860;
        private readonly List<Pagamento> _allPagamentos = new();
        private readonly List<Cliente> _allClientes = new();
        private readonly List<Cliente> _selectedClientes = new();
        private Cliente? _selectedCliente;
        private Pagamento? _selectedPagamento;
        private string _searchTerm = string.Empty;
        private string _pagamentoFilter = "Todos";
        private bool _isImporting;
        private int _pageNumber = 1;
        private int _pageCount = 1;
        private int _totalClientes;
        private bool _showAllCriticalAlerts;
        private bool _alerta7Dias = true;
        private bool _alerta15Dias = true;
        private bool _alerta30Dias = true;
        private bool _isToastVisible;
        private string _toastKind = "Success";
        private string _toastMessage = string.Empty;
        private int _toastVersion;
        private int _selectedMainTabIndex;
        private bool _isNotificationPanelOpen;
        private int _clienteStatusTabIndex;
        private bool _isDarkMode;
        private bool _isFullscreen;
        private bool _backupAutomatico;
        private string _backupFolder = string.Empty;
        private int _backupIntervalHours = 24;
        private int _backupRetentionCount = 10;
        private DateTime? _lastAutoBackupUtc;
        private bool _isBackupRunning;
        private string _selectedConfigSection = "Aparencia";
        private string _exportPreferredEntity = "Clientes";
        private bool _exportPreferExcel = true;
        private double _mainWindowWidth = DefaultWindowWidth;
        private double _mainWindowHeight = DefaultWindowHeight;
        private double _mainWindowLeft = double.NaN;
        private double _mainWindowTop = double.NaN;
        private DispatcherTimer? _backupTimer;
        private readonly string _appVersion;
        private readonly string _buildDateTimeDisplay;

        public ClientesViewModel()
            : this(new ClienteRepository(), new PagamentoRepository(), new ConfiguracaoAlertaRepository())
        {
        }

        public ClientesViewModel(
            IClienteRepository clienteRepository,
            IPagamentoRepository pagamentoRepository,
            IConfiguracaoAlertaRepository configuracaoAlertaRepository)
        {
            _clienteRepository = clienteRepository;
            _pagamentoRepository = pagamentoRepository;
            _configuracaoAlertaRepository = configuracaoAlertaRepository;
            _alertRules = new AlertRules();
            _alertService = new AlertService(_alertRules);
            _appSettingsService = new AppSettingsService();
            _clienteExportColumns = BuildClienteExportColumns();
            _pagamentoExportColumns = BuildPagamentoExportColumns();
            _appVersion = ResolveAppVersion();
            _buildDateTimeDisplay = ResolveBuildDateTimeDisplay();

            LoadCommand = new RelayCommand(async _ => await LoadAsync());
            SearchCommand = new RelayCommand(async _ => await SearchAsync());
            NewCommand = new RelayCommand(async _ => await NewAsync());
            _editCommand = new RelayCommand(async _ => await EditAsync(), _ => CanEditSelectedCliente);
            _deleteCommand = new RelayCommand(async _ => await DeleteAsync(), _ => CanDeleteSelectedClientes);
            _detailsCommand = new RelayCommand(async _ => await ShowDetailsAsync(), _ => CanEditSelectedCliente);
            _importCommand = new RelayCommand(async _ => await ImportAsync(), _ => !IsImporting);
            _exportCommand = new RelayCommand(async _ => await ExportAsync(), _ => _allClientes.Any() || _allPagamentos.Any());
            _backupCommand = new RelayCommand(async _ => await RunBackupAsync(false), _ => !IsImporting && !IsBackupRunning);
            _previousPageCommand = new RelayCommand(_ => { }, _ => CanGoPrev);
            _nextPageCommand = new RelayCommand(_ => { }, _ => CanGoNext);
            _newPagamentoCommand = new RelayCommand(async _ => await NewPagamentoAsync());
            _editPagamentoCommand = new RelayCommand(async _ => await EditPagamentoAsync(), _ => SelectedPagamento != null);
            _deletePagamentoCommand = new RelayCommand(async _ => await DeletePagamentoAsync(), _ => SelectedPagamento != null);
            _cobrancaCommand = new RelayCommand(async _ => await SendCobrancaAsync(), _ => SelectedPagamento != null);
            _saveAlertSettingsCommand = new RelayCommand(async _ => await SaveAlertSettingsAsync());
            _selectBackupFolderCommand = new RelayCommand(_ => SelectBackupFolder());
            _restoreBackupCommand = new RelayCommand(async _ => await RestoreBackupAsync(), _ => !IsImporting && !IsBackupRunning);
            _recreateClientesCommand = new RelayCommand(async _ => await RecreateClientesAsync(), _ => !IsImporting && !IsBackupRunning);
            _recreatePagamentosCommand = new RelayCommand(async _ => await RecreatePagamentosAsync(), _ => !IsImporting && !IsBackupRunning);
            _importPagamentosCommand = new RelayCommand(async _ => await ImportPagamentosAsync(), _ => !IsImporting && !IsBackupRunning);
            _goToDashboardCommand = new RelayCommand(_ =>
            {
                SelectedMainTabIndex = 0;
                IsNotificationPanelOpen = false;
            });
            _goToClientesCommand = new RelayCommand(_ => SelectedMainTabIndex = 1);
            _goToPagamentosCommand = new RelayCommand(_ => SelectedMainTabIndex = 2);
            _toggleNotificationsCommand = new RelayCommand(_ => IsNotificationPanelOpen = !IsNotificationPanelOpen);
            _openDashboardItemCommand = new RelayCommand(async item => await OpenDashboardItemAsync(item as DashboardAlertItem), item => item is DashboardAlertItem);
            _selectConfigSectionCommand = new RelayCommand(section => SelectConfigSection(section as string));
            _contactSupportWhatsAppCommand = new RelayCommand(async _ => await ContactSupportWhatsAppAsync());
            _contactSupportEmailCommand = new RelayCommand(async _ => await ContactSupportEmailAsync());
            EditCommand = _editCommand;
            DeleteCommand = _deleteCommand;
            DetailsCommand = _detailsCommand;
            ImportCommand = _importCommand;
            ExportCommand = _exportCommand;
            BackupCommand = _backupCommand;
            PreviousPageCommand = _previousPageCommand;
            NextPageCommand = _nextPageCommand;
            NewPagamentoCommand = _newPagamentoCommand;
            EditPagamentoCommand = _editPagamentoCommand;
            DeletePagamentoCommand = _deletePagamentoCommand;
            CobrancaCommand = _cobrancaCommand;
            SaveAlertSettingsCommand = _saveAlertSettingsCommand;
            SelectBackupFolderCommand = _selectBackupFolderCommand;
            RestoreBackupCommand = _restoreBackupCommand;
            RecreateClientesCommand = _recreateClientesCommand;
            RecreatePagamentosCommand = _recreatePagamentosCommand;
            ImportPagamentosCommand = _importPagamentosCommand;
            GoToDashboardCommand = _goToDashboardCommand;
            GoToClientesCommand = _goToClientesCommand;
            GoToPagamentosCommand = _goToPagamentosCommand;
            ToggleNotificationsCommand = _toggleNotificationsCommand;
            OpenDashboardItemCommand = _openDashboardItemCommand;
            SelectConfigSectionCommand = _selectConfigSectionCommand;
            ContactSupportWhatsAppCommand = _contactSupportWhatsAppCommand;
            ContactSupportEmailCommand = _contactSupportEmailCommand;
            ViewAllCriticalAlertsCommand = new RelayCommand(_ => ShowAllCriticalAlerts = true, _ => HasCriticalAlerts);
            Dashboard = new DashboardViewModel();
            ApplyWindowMode();
        }

        public ObservableCollection<Cliente> Clientes { get; } = new();
        public ObservableCollection<Pagamento> Pagamentos { get; } = new();
        public ObservableCollection<CriticalAlertItem> CriticalAlertsTop { get; } = new();
        public ObservableCollection<CriticalAlertItem> CriticalAlertsAll { get; } = new();
        public ObservableCollection<DashboardAlertItem> CriticalItems { get; } = new();
        public ObservableCollection<DashboardAlertItem> Next7Days { get; } = new();
        public ObservableCollection<DashboardAlertItem> Next30Days { get; } = new();
        public ObservableCollection<int> BackupIntervalOptions { get; } = new() { 1, 6, 12, 24 };
        public ObservableCollection<int> BackupRetentionOptions { get; } = new() { 5, 10, 20, 30 };

        public Cliente? SelectedCliente
        {
            get => _selectedCliente;
            set
            {
                _selectedCliente = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SelectedClientesCount));
                OnPropertyChanged(nameof(HasMultiSelection));
                OnPropertyChanged(nameof(CanEditSelectedCliente));
                OnPropertyChanged(nameof(CanDeleteSelectedClientes));
                OnPropertyChanged(nameof(SelectedClientesSummary));
                _editCommand.RaiseCanExecuteChanged();
                _deleteCommand.RaiseCanExecuteChanged();
                _detailsCommand.RaiseCanExecuteChanged();
            }
        }

        public int SelectedClientesCount => _selectedClientes.Count;
        public bool HasMultiSelection => SelectedClientesCount > 1;
        public bool CanEditSelectedCliente => SelectedClientesCount == 1 && SelectedCliente != null;
        public bool CanDeleteSelectedClientes => SelectedClientesCount >= 1;
        public string SelectedClientesSummary => SelectedClientesCount == 0
            ? "Nenhum cliente selecionado"
            : SelectedClientesCount == 1
                ? "1 cliente selecionado"
                : $"{SelectedClientesCount} clientes selecionados";

        public Pagamento? SelectedPagamento
        {
            get => _selectedPagamento;
            set
            {
                _selectedPagamento = value;
                OnPropertyChanged();
                _editPagamentoCommand.RaiseCanExecuteChanged();
                _deletePagamentoCommand.RaiseCanExecuteChanged();
                _cobrancaCommand.RaiseCanExecuteChanged();
            }
        }

        public string SearchTerm
        {
            get => _searchTerm;
            set
            {
                _searchTerm = value;
                OnPropertyChanged();
            }
        }

        public string PagamentoFilter
        {
            get => _pagamentoFilter;
            set
            {
                _pagamentoFilter = value;
                OnPropertyChanged();
                ApplyPagamentoFilter();
            }
        }

        public ICommand LoadCommand { get; }
        public ICommand SearchCommand { get; }
        public ICommand NewCommand { get; }
        public ICommand EditCommand { get; }
        public ICommand DeleteCommand { get; }
        public ICommand DetailsCommand { get; }
        public ICommand ImportCommand { get; }
        public ICommand ExportCommand { get; }
        public ICommand BackupCommand { get; }
        public ICommand PreviousPageCommand { get; }
        public ICommand NextPageCommand { get; }
        public ICommand NewPagamentoCommand { get; }
        public ICommand EditPagamentoCommand { get; }
        public ICommand DeletePagamentoCommand { get; }
        public ICommand CobrancaCommand { get; }
        public ICommand SaveAlertSettingsCommand { get; }
        public ICommand SelectBackupFolderCommand { get; }
        public ICommand RestoreBackupCommand { get; }
        public ICommand RecreateClientesCommand { get; }
        public ICommand RecreatePagamentosCommand { get; }
        public ICommand ImportPagamentosCommand { get; }
        public ICommand GoToDashboardCommand { get; }
        public ICommand GoToClientesCommand { get; }
        public ICommand GoToPagamentosCommand { get; }
        public ICommand ToggleNotificationsCommand { get; }
        public ICommand OpenDashboardItemCommand { get; }
        public ICommand SelectConfigSectionCommand { get; }
        public ICommand ContactSupportWhatsAppCommand { get; }
        public ICommand ContactSupportEmailCommand { get; }
        public ICommand ViewAllCriticalAlertsCommand { get; }
        public DashboardViewModel Dashboard { get; }

        public string SelectedConfigSection
        {
            get => _selectedConfigSection;
            set
            {
                _selectedConfigSection = string.IsNullOrWhiteSpace(value) ? "Aparencia" : value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsConfigAparenciaSection));
                OnPropertyChanged(nameof(IsConfigBackupSection));
                OnPropertyChanged(nameof(IsConfigAlertasSection));
                OnPropertyChanged(nameof(IsConfigAtualizacoesSection));
                OnPropertyChanged(nameof(IsConfigAvancadoSection));
                OnPropertyChanged(nameof(IsConfigSobreSection));
            }
        }

        public bool IsConfigAparenciaSection => SelectedConfigSection == "Aparencia";
        public bool IsConfigBackupSection => SelectedConfigSection == "Backup";
        public bool IsConfigAlertasSection => SelectedConfigSection == "Alertas";
        public bool IsConfigAtualizacoesSection => SelectedConfigSection == "Atualizacoes";
        public bool IsConfigAvancadoSection => SelectedConfigSection == "Avancado";
        public bool IsConfigSobreSection => SelectedConfigSection == "Sobre";

        private void SelectConfigSection(string? section)
        {
            SelectedConfigSection = section switch
            {
                "Backup" => "Backup",
                "Alertas" => "Alertas",
                "Atualizacoes" => "Atualizacoes",
                "Avancado" => "Avancado",
                "Sobre" => "Sobre",
                _ => "Aparencia"
            };
        }

        public void UpdateSelectedClientes(IReadOnlyCollection<Cliente> selectedClientes)
        {
            _selectedClientes.Clear();
            _selectedClientes.AddRange(selectedClientes.Where(c => c != null));
            _selectedCliente = _selectedClientes.FirstOrDefault();
            OnPropertyChanged(nameof(SelectedCliente));
            OnPropertyChanged(nameof(SelectedClientesCount));
            OnPropertyChanged(nameof(HasMultiSelection));
            OnPropertyChanged(nameof(CanEditSelectedCliente));
            OnPropertyChanged(nameof(CanDeleteSelectedClientes));
            OnPropertyChanged(nameof(SelectedClientesSummary));
            _editCommand.RaiseCanExecuteChanged();
            _deleteCommand.RaiseCanExecuteChanged();
            _detailsCommand.RaiseCanExecuteChanged();
        }

        public int PageNumber
        {
            get => _pageNumber;
            private set
            {
                _pageNumber = value;
                OnPropertyChanged();
            }
        }

        public int PageCount
        {
            get => _pageCount;
            private set
            {
                _pageCount = value;
                OnPropertyChanged();
            }
        }

        public int TotalClientes
        {
            get => _totalClientes;
            private set
            {
                _totalClientes = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DisplayFrom));
                OnPropertyChanged(nameof(DisplayTo));
                OnPropertyChanged(nameof(FooterSummary));
                OnPropertyChanged(nameof(ShowPagination));
            }
        }

        public bool CanGoPrev => PageNumber > 1;
        public bool CanGoNext => PageNumber < PageCount;
        public int DisplayFrom => TotalClientes == 0 ? 0 : 1;
        public int DisplayTo => TotalClientes;
        public string FooterSummary => $"Exibindo {DisplayFrom} a {DisplayTo} de {TotalClientes} clientes";
        public bool ShowPagination => TotalClientes > 10;

        public int SelectedMainTabIndex
        {
            get => _selectedMainTabIndex;
            set
            {
                _selectedMainTabIndex = value;
                OnPropertyChanged();
                IsNotificationPanelOpen = false;
            }
        }

        public bool IsNotificationPanelOpen
        {
            get => _isNotificationPanelOpen;
            set
            {
                _isNotificationPanelOpen = value;
                OnPropertyChanged();
            }
        }

        public int PendingNotificationCount => Dashboard.AlertasVencendo + Dashboard.AlertasVencidos;
        public bool HasPendingNotifications => PendingNotificationCount > 0;

        public bool IsImporting
        {
            get => _isImporting;
            private set
            {
                _isImporting = value;
                OnPropertyChanged();
                _importCommand.RaiseCanExecuteChanged();
                _backupCommand.RaiseCanExecuteChanged();
                _restoreBackupCommand.RaiseCanExecuteChanged();
                _recreateClientesCommand.RaiseCanExecuteChanged();
                _recreatePagamentosCommand.RaiseCanExecuteChanged();
                _importPagamentosCommand.RaiseCanExecuteChanged();
            }
        }

        public bool IsBackupRunning
        {
            get => _isBackupRunning;
            private set
            {
                _isBackupRunning = value;
                OnPropertyChanged();
                _backupCommand.RaiseCanExecuteChanged();
                _restoreBackupCommand.RaiseCanExecuteChanged();
                _recreateClientesCommand.RaiseCanExecuteChanged();
                _recreatePagamentosCommand.RaiseCanExecuteChanged();
            }
        }

        public bool HasCriticalAlerts => CriticalAlertsTop.Count > 0;

        public int ClienteStatusTabIndex
        {
            get => _clienteStatusTabIndex;
            set
            {
                if (_clienteStatusTabIndex == value)
                {
                    return;
                }

                _clienteStatusTabIndex = value;
                OnPropertyChanged();
                ApplyClienteStatusFilter();
            }
        }

        public bool ShowAllCriticalAlerts
        {
            get => _showAllCriticalAlerts;
            private set
            {
                _showAllCriticalAlerts = value;
                OnPropertyChanged();
            }
        }

        public bool Alerta7Dias
        {
            get => _alerta7Dias;
            set
            {
                _alerta7Dias = value;
                OnPropertyChanged();
            }
        }

        public bool Alerta15Dias
        {
            get => _alerta15Dias;
            set
            {
                _alerta15Dias = value;
                OnPropertyChanged();
            }
        }

        public bool Alerta30Dias
        {
            get => _alerta30Dias;
            set
            {
                _alerta30Dias = value;
                OnPropertyChanged();
            }
        }

        public string AlertWindowText => _alertRules.MaxAlertDays > 0 ? $"até {_alertRules.MaxAlertDays} dias" : "desativados";

        public bool IsToastVisible
        {
            get => _isToastVisible;
            private set
            {
                _isToastVisible = value;
                OnPropertyChanged();
            }
        }

        public string ToastKind
        {
            get => _toastKind;
            private set
            {
                _toastKind = value;
                OnPropertyChanged();
            }
        }

        public string ToastMessage
        {
            get => _toastMessage;
            private set
            {
                _toastMessage = value;
                OnPropertyChanged();
            }
        }

        public bool IsDarkMode
        {
            get => _isDarkMode;
            set
            {
                _isDarkMode = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CurrentThemeLabel));
            }
        }

        public bool IsFullscreen
        {
            get => _isFullscreen;
            set
            {
                _isFullscreen = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CurrentWindowModeLabel));
                ApplyWindowMode();
            }
        }

        public bool BackupAutomatico
        {
            get => _backupAutomatico;
            set
            {
                _backupAutomatico = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(BackupModeLabel));
            }
        }

        public string BackupFolder
        {
            get => _backupFolder;
            set
            {
                _backupFolder = value;
                OnPropertyChanged();
            }
        }

        public int BackupIntervalHours
        {
            get => _backupIntervalHours;
            set
            {
                _backupIntervalHours = value <= 0 ? 24 : value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(BackupIntervalLabel));
            }
        }

        public int BackupRetentionCount
        {
            get => _backupRetentionCount;
            set
            {
                _backupRetentionCount = value <= 0 ? 10 : value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(BackupRetentionLabel));
            }
        }

        public string CurrentThemeLabel => IsDarkMode ? "Dark" : "Light";
        public string CurrentWindowModeLabel => IsFullscreen ? "Fullscreen" : "Normal";
        public string AppVersion => _appVersion;
        public string AppVersionDisplay => $"v.{_appVersion}";
        public string BuildDateTimeDisplay => _buildDateTimeDisplay;
        public string BackupModeLabel => BackupAutomatico ? "Ativado" : "Desativado";
        public string BackupIntervalLabel => $"A cada {BackupIntervalHours}h";
        public string BackupRetentionLabel => $"Manter últimos {BackupRetentionCount} arquivos";
        public string LastBackupLabel => _lastAutoBackupUtc.HasValue
            ? _lastAutoBackupUtc.Value.ToLocalTime().ToString("dd/MM/yyyy HH:mm")
            : "Nunca executado";

        public WindowState MainWindowState => IsFullscreen ? WindowState.Maximized : WindowState.Normal;
        public WindowStyle MainWindowStyle => WindowStyle.SingleBorderWindow;
        public ResizeMode MainResizeMode => ResizeMode.CanMinimize;
        public bool MainTopmost => false;

        public double MainWindowWidth
        {
            get => _mainWindowWidth;
            private set
            {
                _mainWindowWidth = value;
                OnPropertyChanged();
            }
        }

        public double MainWindowHeight
        {
            get => _mainWindowHeight;
            private set
            {
                _mainWindowHeight = value;
                OnPropertyChanged();
            }
        }

        public double MainWindowLeft
        {
            get => _mainWindowLeft;
            private set
            {
                _mainWindowLeft = value;
                OnPropertyChanged();
            }
        }

        public double MainWindowTop
        {
            get => _mainWindowTop;
            private set
            {
                _mainWindowTop = value;
                OnPropertyChanged();
            }
        }

        private async Task LoadAsync()
        {
            LoadThemeSettings();
            await LoadAlertSettingsAsync();
            var clientes = await _clienteRepository.GetAllAsync();
            ReplaceClientes(clientes);
            await LoadPagamentosAsync();
        }

        private void LoadThemeSettings()
        {
            var settings = _appSettingsService.Load();
            IsDarkMode = AppThemeManager.NormalizeTheme(settings.Theme) == AppThemeManager.DarkTheme;
            IsFullscreen = settings.Fullscreen;
            BackupAutomatico = settings.BackupEnabled;
            BackupFolder = settings.BackupFolder;
            BackupIntervalHours = settings.BackupIntervalHours;
            BackupRetentionCount = settings.BackupRetentionCount;
            _lastAutoBackupUtc = settings.LastAutoBackupUtc;
            _exportPreferredEntity = settings.ExportPreferredEntity == "Pagamentos" ? "Pagamentos" : "Clientes";
            _exportPreferExcel = settings.ExportPreferExcel;
            ApplyPreferredExportFields(settings.ExportClienteSelectedFields, _preferredClienteExportFields);
            ApplyPreferredExportFields(settings.ExportPagamentoSelectedFields, _preferredPagamentoExportFields);
            AppThemeManager.ApplyTheme(settings.Theme);
            ApplyWindowMode();
            OnPropertyChanged(nameof(LastBackupLabel));
            StartBackupScheduler();
        }



        private async Task ShowToastAsync(string message, string kind = "Success")
        {
            _toastVersion++;
            var currentVersion = _toastVersion;
            ToastMessage = message;
            ToastKind = kind;
            IsToastVisible = true;
            await Task.Delay(2400);

            if (currentVersion == _toastVersion)
            {
                IsToastVisible = false;
            }
        }




    }
}

