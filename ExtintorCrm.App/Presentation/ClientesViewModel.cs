using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Brush = System.Windows.Media.Brush;
using ExtintorCrm.App.Domain;
using ExtintorCrm.App.Infrastructure;
using ExtintorCrm.App.Infrastructure.Documents;
using ExtintorCrm.App.Infrastructure.Export;
using ExtintorCrm.App.Infrastructure.Logging;
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
        private readonly IDocumentoAnexoRepository _documentoAnexoRepository;
        private readonly DocumentoStorageService _documentoStorageService;
        private readonly AsyncRelayCommand _editCommand;
        private readonly AsyncRelayCommand _deleteCommand;
        private readonly AsyncRelayCommand _detailsCommand;
        private readonly AsyncRelayCommand _importCommand;
        private readonly AsyncRelayCommand _exportCommand;
        private readonly AsyncRelayCommand _backupCommand;
        private readonly AsyncRelayCommand _activateSelectedClientesCommand;
        private readonly AsyncRelayCommand _deactivateSelectedClientesCommand;
        private readonly AsyncRelayCommand _exportSelectedClientesCommand;
        private readonly RelayCommand _previousPageCommand;
        private readonly RelayCommand _nextPageCommand;
        private readonly AsyncRelayCommand _newPagamentoCommand;
        private readonly AsyncRelayCommand _editPagamentoCommand;
        private readonly AsyncRelayCommand _deletePagamentoCommand;
        private readonly AsyncRelayCommand _cobrancaCommand;
        private readonly AsyncRelayCommand _openPagamentoAttachmentsCommand;
        private readonly AsyncRelayCommand _saveAlertSettingsCommand;
        private readonly RelayCommand _selectBackupFolderCommand;
        private readonly AsyncRelayCommand _restoreBackupCommand;
        private readonly AsyncRelayCommand _recreateClientesCommand;
        private readonly AsyncRelayCommand _recreatePagamentosCommand;
        private readonly AsyncRelayCommand _importPagamentosCommand;
        private readonly RelayCommand _goToClientesCommand;
        private readonly RelayCommand _goToPagamentosCommand;
        private readonly RelayCommand _goToDashboardCommand;
        private readonly RelayCommand _toggleNotificationsCommand;
        private readonly AsyncRelayCommand _openDashboardItemCommand;
        private readonly AsyncRelayCommand _openDashboardAlertsCommand;
        private readonly RelayCommand _selectConfigSectionCommand;
        private readonly AsyncRelayCommand _toastActionCommand;
        private readonly RelayCommand _toggleClientesFiltersCollapsedCommand;
        private readonly RelayCommand _togglePagamentosFiltersCollapsedCommand;
        private readonly RelayCommand _goToPageCommand;
        private readonly RelayCommand _resetClienteFiltersCommand;
        private readonly RelayCommand _removeClienteFilterCommand;
        private readonly RelayCommand _searchPagamentosCommand;
        private readonly RelayCommand _resetPagamentoFiltersCommand;
        private readonly RelayCommand _setUiBorderColorPresetCommand;
        private readonly RelayCommand _setUiTitleBarColorPresetCommand;
        private readonly RelayCommand _setUiVanillaColorPresetCommand;
        private readonly RelayCommand _pickUiBorderColorCommand;
        private readonly RelayCommand _pickUiTitleBarColorCommand;
        private readonly RelayCommand _pickUiVanillaColorCommand;
        private readonly RelayCommand _resetUiBorderColorCommand;
        private readonly RelayCommand _resetUiTitleBarColorCommand;
        private readonly RelayCommand _resetUiVanillaColorCommand;
        private readonly RelayCommand _resetUiVanillaIntensityCommand;
        private readonly RelayCommand _resetUiChromeColorsCommand;
        private readonly AsyncRelayCommand _contactSupportWhatsAppCommand;
        private readonly AsyncRelayCommand _contactSupportEmailCommand;
        private readonly AlertService _alertService;
        private readonly AlertRules _alertRules;
        private readonly AppSettingsService _appSettingsService;
        private readonly IReadOnlyList<ExportColumnDefinition<Cliente>> _clienteExportColumns;
        private readonly IReadOnlyList<ExportColumnDefinition<Pagamento>> _pagamentoExportColumns;
        private readonly HashSet<string> _preferredClienteExportFields = new();
        private readonly HashSet<string> _preferredPagamentoExportFields = new();
        private const double DefaultWindowWidth = 1380;
        private const double DefaultWindowHeight = 860;
        private const int ClientesPageSize = 10;
        private const string AdvancedUnlockIdleMessage = "Area protegida. Informe a senha para liberar os comandos avancados.";
        private const string AdvancedUnlockNotConfiguredMessage = "Senha do modulo avancado nao configurada. Defina STARFIRE_ADVANCED_PASSWORD ou ajuste no appsettings.";

        private enum UiChromeColorTarget
        {
            Border,
            TitleBar,
            Vanilla
        }

        private readonly List<Pagamento> _allPagamentos = new();
        private readonly List<Cliente> _allClientes = new();
        private readonly List<Cliente> _filteredClientes = new();
        private readonly List<Cliente> _selectedClientes = new();
        private readonly List<Pagamento> _selectedPagamentos = new();
        private Cliente? _selectedCliente;
        private Pagamento? _selectedPagamento;
        private string _searchTerm = string.Empty;
        private string _pagamentoSearchTerm = string.Empty;
        private string _pagamentoFilter = "Todos";
        private string _clienteSituacaoFilter = "Todos";
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
        private bool _hasToastAction;
        private string _toastActionLabel = string.Empty;
        private Func<Task>? _toastActionHandler;
        private int _toastVersion;
        private bool _isOperationInProgress;
        private string _operationStatusMessage = string.Empty;
        private bool _isCompactLayout;
        private bool _isClientesFiltersCollapsed;
        private bool _isPagamentosFiltersCollapsed;
        private int _selectedMainTabIndex;
        private bool _isNotificationPanelOpen;
        private bool _isVersionHistoryOpen;
        private int _notificationEligibleCount;
        private bool _notificationShowExtintores = true;
        private bool _notificationShowAlvaras = true;
        private bool _notificationShowPagamentos = true;
        private bool _notificationIncludeOverdue = true;
        private int _notificationDaysWindow = 30;
        private int _notificationMaxItems = 10;
        private int _clienteStatusTabIndex;
        private bool _isDarkMode;
        private bool _isFullscreen;
        private string _selectedWindowResolutionPreset = WindowResolutionPresets.Auto;
        private bool _backupAutomatico;
        private string _backupFolder = string.Empty;
        private int _backupIntervalHours = 24;
        private int _backupRetentionCount = 10;
        private DateTime? _lastAutoBackupUtc;
        private bool _isBackupRunning;
        private bool _isSavingSettings;
        private bool _isLoadingClientes;
        private bool _isLoadingPagamentos;
        private string _configValidationMessage = string.Empty;
        private string _pagamentosLoadErrorMessage = string.Empty;
        private string _selectedConfigSection = "Aparencia";
        private string _advancedSectionPassword = string.Empty;
        private bool _isAdvancedConfigUnlocked;
        private string _advancedUnlockStatusMessage = AdvancedUnlockIdleMessage;
        private bool _isAdvancedUnlockError;
        private string _exportPreferredEntity = "Clientes";
        private bool _exportPreferExcel = true;
        private double _mainWindowWidth = DefaultWindowWidth;
        private double _mainWindowHeight = DefaultWindowHeight;
        private double _mainWindowLeft = double.NaN;
        private double _mainWindowTop = double.NaN;
        private DispatcherTimer? _backupTimer;
        private CancellationTokenSource? _searchDebounceCts;
        private CancellationTokenSource? _pagamentoSearchDebounceCts;
        private bool _hasPendingConfigChanges;
        private bool _suppressConfigDirtyTracking;
        private ConfigSnapshot _savedConfigSnapshot = ConfigSnapshot.Empty;
        private bool _hasLoadedUiSettings;
        private readonly List<DashboardKpiCardItem> _dashboardKpiCardsIndex = [];
        private string _uiBorderColorHex = string.Empty;
        private string _uiTitleBarColorHex = string.Empty;
        private string _uiVanillaColorHex = string.Empty;
        private int _uiVanillaIntensityPercent = 100;
        private readonly string _appVersion;
        private readonly string _buildDateTimeDisplay;
        private readonly IReadOnlyList<ReleaseNoteVersion> _releaseNotesHistory;
        private string _clientesSortMember = nameof(Cliente.NomeFantasia);
        private ListSortDirection _clientesSortDirection = ListSortDirection.Ascending;

        public ClientesViewModel()
            : this(
                new ClienteRepository(),
                new PagamentoRepository(),
                new ConfiguracaoAlertaRepository(),
                new DocumentoAnexoRepository(),
                new DocumentoStorageService())
        {
        }

        public ClientesViewModel(
            IClienteRepository clienteRepository,
            IPagamentoRepository pagamentoRepository,
            IConfiguracaoAlertaRepository configuracaoAlertaRepository,
            IDocumentoAnexoRepository documentoAnexoRepository,
            DocumentoStorageService documentoStorageService)
        {
            _clienteRepository = clienteRepository;
            _pagamentoRepository = pagamentoRepository;
            _configuracaoAlertaRepository = configuracaoAlertaRepository;
            _documentoAnexoRepository = documentoAnexoRepository;
            _documentoStorageService = documentoStorageService;
            _alertRules = new AlertRules();
            _alertService = new AlertService(_alertRules);
            _appSettingsService = new AppSettingsService();
            _clienteExportColumns = BuildClienteExportColumns();
            _pagamentoExportColumns = BuildPagamentoExportColumns();
            _appVersion = ResolveAppVersion();
            _buildDateTimeDisplay = ResolveBuildDateTimeDisplay();
            _releaseNotesHistory = BuildReleaseNotesHistory();

            LoadCommand = new AsyncRelayCommand(async _ => await LoadAsync(reloadUiSettings: true));
            SearchCommand = new AsyncRelayCommand(async _ => await SearchAsync());
            NewCommand = new AsyncRelayCommand(async _ => await NewAsync());
            _editCommand = new AsyncRelayCommand(async _ => await EditAsync(), _ => CanEditSelectedCliente);
            _deleteCommand = new AsyncRelayCommand(async _ => await DeleteAsync(), _ => CanDeleteSelectedClientes);
            _detailsCommand = new AsyncRelayCommand(async _ => await ShowDetailsAsync(), _ => CanEditSelectedCliente);
            _importCommand = new AsyncRelayCommand(async _ => await ImportAsync(), _ => !IsImporting);
            _exportCommand = new AsyncRelayCommand(async _ => await ExportAsync(), _ => _allClientes.Any() || _allPagamentos.Any());
            _backupCommand = new AsyncRelayCommand(async _ => await RunBackupAsync(false), _ => !IsImporting && !IsBackupRunning);
            _activateSelectedClientesCommand = new AsyncRelayCommand(async _ => await SetSelectedClientesActiveStateAsync(true), _ => CanActivateSelectedClientes);
            _deactivateSelectedClientesCommand = new AsyncRelayCommand(async _ => await SetSelectedClientesActiveStateAsync(false), _ => CanDeactivateSelectedClientes);
            _exportSelectedClientesCommand = new AsyncRelayCommand(async _ => await ExportSelectedClientesAsync(), _ => CanExportSelectedClientes);
            _previousPageCommand = new RelayCommand(_ => ChangeClientesPage(-1), _ => CanGoPrev);
            _nextPageCommand = new RelayCommand(_ => ChangeClientesPage(1), _ => CanGoNext);
            _goToPageCommand = new RelayCommand(page => GoToPage(page), page => CanGoToPage(page));
            _resetClienteFiltersCommand = new RelayCommand(_ => ResetClienteFilters(), _ => CanResetClienteFilters);
            _removeClienteFilterCommand = new RelayCommand(filter => RemoveClienteFilter(filter as string), _ => CanResetClienteFilters);
            _searchPagamentosCommand = new RelayCommand(_ => ApplyPagamentoFilter());
            _resetPagamentoFiltersCommand = new RelayCommand(_ => ResetPagamentoFilters(), _ => CanResetPagamentoFilters);
            _setUiBorderColorPresetCommand = new RelayCommand(hex => UiBorderColorHex = hex as string ?? string.Empty);
            _setUiTitleBarColorPresetCommand = new RelayCommand(hex => UiTitleBarColorHex = hex as string ?? string.Empty);
            _setUiVanillaColorPresetCommand = new RelayCommand(hex => UiVanillaColorHex = hex as string ?? string.Empty);
            _pickUiBorderColorCommand = new RelayCommand(_ => PickUiColor(UiChromeColorTarget.Border));
            _pickUiTitleBarColorCommand = new RelayCommand(_ => PickUiColor(UiChromeColorTarget.TitleBar));
            _pickUiVanillaColorCommand = new RelayCommand(_ => PickUiColor(UiChromeColorTarget.Vanilla));
            _resetUiBorderColorCommand = new RelayCommand(_ => UiBorderColorHex = string.Empty, _ => !string.IsNullOrWhiteSpace(UiBorderColorHex));
            _resetUiTitleBarColorCommand = new RelayCommand(_ => UiTitleBarColorHex = string.Empty, _ => !string.IsNullOrWhiteSpace(UiTitleBarColorHex));
            _resetUiVanillaColorCommand = new RelayCommand(_ => UiVanillaColorHex = string.Empty, _ => !string.IsNullOrWhiteSpace(UiVanillaColorHex));
            _resetUiVanillaIntensityCommand = new RelayCommand(_ => UiVanillaIntensityPercent = 100, _ => UiVanillaIntensityPercent != 100);
            _resetUiChromeColorsCommand = new RelayCommand(
                _ =>
                {
                    UiBorderColorHex = string.Empty;
                    UiTitleBarColorHex = string.Empty;
                    UiVanillaColorHex = string.Empty;
                    UiVanillaIntensityPercent = 100;
                },
                _ => !string.IsNullOrWhiteSpace(UiBorderColorHex) || !string.IsNullOrWhiteSpace(UiTitleBarColorHex) || !string.IsNullOrWhiteSpace(UiVanillaColorHex) || UiVanillaIntensityPercent != 100);
            _newPagamentoCommand = new AsyncRelayCommand(async _ => await NewPagamentoAsync());
            _editPagamentoCommand = new AsyncRelayCommand(async _ => await EditPagamentoAsync(), _ => CanEditSelectedPagamento);
            _deletePagamentoCommand = new AsyncRelayCommand(async _ => await DeletePagamentoAsync(), _ => CanDeleteSelectedPagamentos);
            _cobrancaCommand = new AsyncRelayCommand(async _ => await SendCobrancaAsync(), _ => CanEditSelectedPagamento);
            _openPagamentoAttachmentsCommand = new AsyncRelayCommand(async _ => await OpenPagamentoAttachmentsAsync(), _ => CanEditSelectedPagamento);
            _saveAlertSettingsCommand = new AsyncRelayCommand(
                async _ => await SaveAlertSettingsAsync(),
                _ => HasPendingConfigChanges && !IsImporting && !IsBackupRunning && !IsSavingSettings && IsConfigValid);
            _selectBackupFolderCommand = new RelayCommand(_ => SelectBackupFolder());
            _restoreBackupCommand = new AsyncRelayCommand(async _ => await RestoreBackupAsync(), _ => !IsImporting && !IsBackupRunning);
            _recreateClientesCommand = new AsyncRelayCommand(async _ => await RecreateClientesAsync(), _ => IsAdvancedConfigUnlocked && !IsImporting && !IsBackupRunning);
            _recreatePagamentosCommand = new AsyncRelayCommand(async _ => await RecreatePagamentosAsync(), _ => IsAdvancedConfigUnlocked && !IsImporting && !IsBackupRunning);
            _importPagamentosCommand = new AsyncRelayCommand(async _ => await ImportPagamentosAsync(), _ => !IsImporting && !IsBackupRunning);
            _goToDashboardCommand = new RelayCommand(_ =>
            {
                SelectedMainTabIndex = 0;
                IsNotificationPanelOpen = false;
            });
            _goToClientesCommand = new RelayCommand(_ => SelectedMainTabIndex = 1);
            _goToPagamentosCommand = new RelayCommand(_ => SelectedMainTabIndex = 2);
            _toggleNotificationsCommand = new RelayCommand(_ => IsNotificationPanelOpen = !IsNotificationPanelOpen);
            _openDashboardItemCommand = new AsyncRelayCommand(async item => await OpenDashboardItemAsync(item as DashboardAlertItem), item => item is DashboardAlertItem);
            _openDashboardAlertsCommand = new AsyncRelayCommand(async key => await OpenDashboardAlertsAsync(key as string));
            _selectConfigSectionCommand = new RelayCommand(section => SelectConfigSection(section as string));
            _toastActionCommand = new AsyncRelayCommand(async _ => await ExecuteToastActionAsync(), _ => HasToastAction);
            _toggleClientesFiltersCollapsedCommand = new RelayCommand(_ => IsClientesFiltersCollapsed = !IsClientesFiltersCollapsed);
            _togglePagamentosFiltersCollapsedCommand = new RelayCommand(_ => IsPagamentosFiltersCollapsed = !IsPagamentosFiltersCollapsed);
            _contactSupportWhatsAppCommand = new AsyncRelayCommand(async _ => await ContactSupportWhatsAppAsync());
            _contactSupportEmailCommand = new AsyncRelayCommand(async _ => await ContactSupportEmailAsync());
            EditCommand = _editCommand;
            DeleteCommand = _deleteCommand;
            DetailsCommand = _detailsCommand;
            ImportCommand = _importCommand;
            ExportCommand = _exportCommand;
            BackupCommand = _backupCommand;
            ActivateSelectedClientesCommand = _activateSelectedClientesCommand;
            DeactivateSelectedClientesCommand = _deactivateSelectedClientesCommand;
            ExportSelectedClientesCommand = _exportSelectedClientesCommand;
            PreviousPageCommand = _previousPageCommand;
            NextPageCommand = _nextPageCommand;
            GoToPageCommand = _goToPageCommand;
            ResetClienteFiltersCommand = _resetClienteFiltersCommand;
            RemoveClienteFilterCommand = _removeClienteFilterCommand;
            SearchPagamentosCommand = _searchPagamentosCommand;
            ResetPagamentoFiltersCommand = _resetPagamentoFiltersCommand;
            SetUiBorderColorPresetCommand = _setUiBorderColorPresetCommand;
            SetUiTitleBarColorPresetCommand = _setUiTitleBarColorPresetCommand;
            SetUiVanillaColorPresetCommand = _setUiVanillaColorPresetCommand;
            PickUiBorderColorCommand = _pickUiBorderColorCommand;
            PickUiTitleBarColorCommand = _pickUiTitleBarColorCommand;
            PickUiVanillaColorCommand = _pickUiVanillaColorCommand;
            ResetUiBorderColorCommand = _resetUiBorderColorCommand;
            ResetUiTitleBarColorCommand = _resetUiTitleBarColorCommand;
            ResetUiVanillaColorCommand = _resetUiVanillaColorCommand;
            ResetUiVanillaIntensityCommand = _resetUiVanillaIntensityCommand;
            ResetUiChromeColorsCommand = _resetUiChromeColorsCommand;
            NewPagamentoCommand = _newPagamentoCommand;
            EditPagamentoCommand = _editPagamentoCommand;
            DeletePagamentoCommand = _deletePagamentoCommand;
            CobrancaCommand = _cobrancaCommand;
            OpenPagamentoAttachmentsCommand = _openPagamentoAttachmentsCommand;
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
            OpenDashboardAlertsCommand = _openDashboardAlertsCommand;
            SelectConfigSectionCommand = _selectConfigSectionCommand;
            ToastActionCommand = _toastActionCommand;
            ToggleClientesFiltersCollapsedCommand = _toggleClientesFiltersCollapsedCommand;
            TogglePagamentosFiltersCollapsedCommand = _togglePagamentosFiltersCollapsedCommand;
            ContactSupportWhatsAppCommand = _contactSupportWhatsAppCommand;
            ContactSupportEmailCommand = _contactSupportEmailCommand;
            ViewAllCriticalAlertsCommand = new RelayCommand(_ => ShowAllCriticalAlerts = true, _ => HasCriticalAlerts);
            Dashboard = new DashboardViewModel();
            InitializeDashboardKpiCards();
            ApplyWindowMode();
            RecomputeConfigValidation();
        }

        public ObservableCollection<Cliente> Clientes { get; } = new();
        public ObservableCollection<Pagamento> Pagamentos { get; } = new();
        public ObservableCollection<CriticalAlertItem> CriticalAlertsTop { get; } = new();
        public ObservableCollection<CriticalAlertItem> CriticalAlertsAll { get; } = new();
        public ObservableCollection<DashboardAlertItem> CriticalItems { get; } = new();
        public ObservableCollection<DashboardAlertItem> BellNotificationItems { get; } = new();
        public ObservableCollection<DashboardAlertItem> Next7Days { get; } = new();
        public ObservableCollection<DashboardAlertItem> Next30Days { get; } = new();
        public ObservableCollection<DashboardKpiCardItem> DashboardKpiCards { get; } = new();
        public ObservableCollection<string> ClienteSituacaoFilterOptions { get; } = new() { "Todos", "Vencido", "Vencendo", "OK" };
        public ObservableCollection<string> ClienteSortFieldOptions { get; } = new() { "Nome", "CPF/CNPJ", "Telefone", "Cidade", "Vencimento Extintores", "Situação" };
        public ObservableCollection<string> ClienteSortDirectionOptions { get; } = new() { "Crescente", "Decrescente" };
        public ObservableCollection<int> BackupIntervalOptions { get; } = new() { 1, 6, 12, 24 };
        public ObservableCollection<int> BackupRetentionOptions { get; } = new() { 5, 10, 20, 30 };
        public ObservableCollection<int> NotificationDaysWindowOptions { get; } = new() { 7, 15, 30, 45, 60, 90 };
        public ObservableCollection<int> NotificationMaxItemsOptions { get; } = new() { 5, 10, 15, 20, 30 };
        public ObservableCollection<string> WindowResolutionOptions { get; } = new(WindowResolutionPresets.All);
        public ObservableCollection<int> PageIndexes { get; } = new();

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
                OnPropertyChanged(nameof(CanActivateSelectedClientes));
                OnPropertyChanged(nameof(CanDeactivateSelectedClientes));
                OnPropertyChanged(nameof(CanExportSelectedClientes));
                OnPropertyChanged(nameof(SelectedClientesSummary));
                _editCommand.RaiseCanExecuteChanged();
                _deleteCommand.RaiseCanExecuteChanged();
                _detailsCommand.RaiseCanExecuteChanged();
                _activateSelectedClientesCommand.RaiseCanExecuteChanged();
                _deactivateSelectedClientesCommand.RaiseCanExecuteChanged();
                _exportSelectedClientesCommand.RaiseCanExecuteChanged();
            }
        }

        public int SelectedClientesCount => _selectedClientes.Count;
        public bool HasMultiSelection => SelectedClientesCount > 1;
        public bool CanEditSelectedCliente => SelectedClientesCount == 1 && SelectedCliente != null;
        public bool CanDeleteSelectedClientes => SelectedClientesCount >= 1;
        public bool CanActivateSelectedClientes => _selectedClientes.Any(c => !c.IsAtivo);
        public bool CanDeactivateSelectedClientes => _selectedClientes.Any(c => c.IsAtivo);
        public bool CanExportSelectedClientes => _selectedClientes.Count > 0;
        public string ClientesSortMember => _clientesSortMember;
        public ListSortDirection ClientesSortDirection => _clientesSortDirection;
        public string ClienteSortField
        {
            get => MapSortMemberToDisplay(_clientesSortMember);
            set
            {
                var member = MapSortDisplayToMember(value);
                if (string.Equals(_clientesSortMember, member, StringComparison.Ordinal))
                {
                    return;
                }

                _clientesSortMember = member;
                _clientesSortDirection = ListSortDirection.Ascending;
                ApplyClientesSortingAndRefreshPage();
                OnPropertyChanged();
                OnPropertyChanged(nameof(ClientesSortMember));
                OnPropertyChanged(nameof(ClientesSortDirection));
                OnPropertyChanged(nameof(ClienteSortDirection));
                OnPropertyChanged(nameof(CanResetClienteFilters));
                _resetClienteFiltersCommand.RaiseCanExecuteChanged();
                _removeClienteFilterCommand.RaiseCanExecuteChanged();
            }
        }

        public string ClienteSortDirection
        {
            get => _clientesSortDirection == ListSortDirection.Ascending ? "Crescente" : "Decrescente";
            set
            {
                var direction = ParseSortDirection(value);
                if (_clientesSortDirection == direction)
                {
                    return;
                }

                _clientesSortDirection = direction;
                ApplyClientesSortingAndRefreshPage();
                OnPropertyChanged();
                OnPropertyChanged(nameof(ClientesSortDirection));
                OnPropertyChanged(nameof(CanResetClienteFilters));
                _resetClienteFiltersCommand.RaiseCanExecuteChanged();
                _removeClienteFilterCommand.RaiseCanExecuteChanged();
            }
        }

        public string ClienteSituacaoFilter
        {
            get => _clienteSituacaoFilter;
            set
            {
                var normalized = NormalizeClienteSituacaoFilter(value);
                if (string.Equals(_clienteSituacaoFilter, normalized, StringComparison.Ordinal))
                {
                    return;
                }

                _clienteSituacaoFilter = normalized;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanResetClienteFilters));
                _resetClienteFiltersCommand.RaiseCanExecuteChanged();
                _removeClienteFilterCommand.RaiseCanExecuteChanged();
                ApplyClienteStatusFilter();
            }
        }

        public bool CanResetClienteFilters =>
            ClienteStatusTabIndex != 0 ||
            !string.Equals(ClienteSituacaoFilter, "Todos", StringComparison.Ordinal) ||
            !string.Equals(_clientesSortMember, nameof(Cliente.NomeFantasia), StringComparison.Ordinal) ||
            _clientesSortDirection != ListSortDirection.Ascending;
        public string SelectedClientesSummary => SelectedClientesCount == 0
            ? "Nenhum cliente selecionado"
            : SelectedClientesCount == 1
                ? "1 cliente selecionado"
                : $"{SelectedClientesCount} clientes selecionados";
        public string SelectedPagamentosSummary => SelectedPagamentosCount == 0
            ? "Nenhum pagamento selecionado"
            : SelectedPagamentosCount == 1
                ? "1 pagamento selecionado"
                : $"{SelectedPagamentosCount} pagamentos selecionados";

        public Pagamento? SelectedPagamento
        {
            get => _selectedPagamento;
            set
            {
                _selectedPagamento = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SelectedPagamentosCount));
                OnPropertyChanged(nameof(CanEditSelectedPagamento));
                OnPropertyChanged(nameof(CanDeleteSelectedPagamentos));
                OnPropertyChanged(nameof(SelectedPagamentosSummary));
                _editPagamentoCommand.RaiseCanExecuteChanged();
                _deletePagamentoCommand.RaiseCanExecuteChanged();
                _cobrancaCommand.RaiseCanExecuteChanged();
                _openPagamentoAttachmentsCommand.RaiseCanExecuteChanged();
            }
        }
        public int SelectedPagamentosCount => _selectedPagamentos.Count;
        public bool CanEditSelectedPagamento => SelectedPagamentosCount == 1 && SelectedPagamento != null;
        public bool CanDeleteSelectedPagamentos => SelectedPagamentosCount >= 1;

        public string SearchTerm
        {
            get => _searchTerm;
            set
            {
                _searchTerm = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ClientesStateDescription));
                QueueSearch();
            }
        }

        public string PagamentoSearchTerm
        {
            get => _pagamentoSearchTerm;
            set
            {
                var normalized = value ?? string.Empty;
                if (string.Equals(_pagamentoSearchTerm, normalized, StringComparison.Ordinal))
                {
                    return;
                }

                _pagamentoSearchTerm = normalized;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanResetPagamentoFilters));
                OnPropertyChanged(nameof(PagamentosStateDescription));
                _resetPagamentoFiltersCommand.RaiseCanExecuteChanged();
                QueuePagamentoSearch();
            }
        }

        public string PagamentoFilter
        {
            get => _pagamentoFilter;
            set
            {
                var normalized = string.IsNullOrWhiteSpace(value) ? "Todos" : value;
                if (string.Equals(_pagamentoFilter, normalized, StringComparison.Ordinal))
                {
                    return;
                }

                _pagamentoFilter = normalized;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanResetPagamentoFilters));
                OnPropertyChanged(nameof(PagamentosStateDescription));
                _resetPagamentoFiltersCommand.RaiseCanExecuteChanged();
                ApplyPagamentoFilter();
            }
        }

        public bool CanResetPagamentoFilters =>
            !string.Equals(PagamentoFilter, "Todos", StringComparison.Ordinal) ||
            !string.IsNullOrWhiteSpace(PagamentoSearchTerm);

        public ICommand LoadCommand { get; }
        public ICommand SearchCommand { get; }
        public ICommand SearchPagamentosCommand { get; }
        public ICommand NewCommand { get; }
        public ICommand EditCommand { get; }
        public ICommand DeleteCommand { get; }
        public ICommand DetailsCommand { get; }
        public ICommand ImportCommand { get; }
        public ICommand ExportCommand { get; }
        public ICommand BackupCommand { get; }
        public ICommand ActivateSelectedClientesCommand { get; }
        public ICommand DeactivateSelectedClientesCommand { get; }
        public ICommand ExportSelectedClientesCommand { get; }
        public ICommand PreviousPageCommand { get; }
        public ICommand NextPageCommand { get; }
        public ICommand GoToPageCommand { get; }
        public ICommand ResetClienteFiltersCommand { get; }
        public ICommand ResetPagamentoFiltersCommand { get; }
        public ICommand RemoveClienteFilterCommand { get; }
        public ICommand SetUiBorderColorPresetCommand { get; }
        public ICommand SetUiTitleBarColorPresetCommand { get; }
        public ICommand SetUiVanillaColorPresetCommand { get; }
        public ICommand PickUiBorderColorCommand { get; }
        public ICommand PickUiTitleBarColorCommand { get; }
        public ICommand PickUiVanillaColorCommand { get; }
        public ICommand ResetUiBorderColorCommand { get; }
        public ICommand ResetUiTitleBarColorCommand { get; }
        public ICommand ResetUiVanillaColorCommand { get; }
        public ICommand ResetUiVanillaIntensityCommand { get; }
        public ICommand ResetUiChromeColorsCommand { get; }
        public ICommand NewPagamentoCommand { get; }
        public ICommand EditPagamentoCommand { get; }
        public ICommand DeletePagamentoCommand { get; }
        public ICommand CobrancaCommand { get; }
        public ICommand OpenPagamentoAttachmentsCommand { get; }
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
        public ICommand OpenDashboardAlertsCommand { get; }
        public ICommand SelectConfigSectionCommand { get; }
        public ICommand ToastActionCommand { get; }
        public ICommand ToggleClientesFiltersCollapsedCommand { get; }
        public ICommand TogglePagamentosFiltersCollapsedCommand { get; }
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
                OnPropertyChanged(nameof(IsConfigNotificacoesSection));
                OnPropertyChanged(nameof(IsConfigAtualizacoesSection));
                OnPropertyChanged(nameof(IsConfigAvancadoSection));
                OnPropertyChanged(nameof(IsConfigSobreSection));

                if (!IsConfigAvancadoSection)
                {
                    ResetAdvancedUnlockFeedback();
                }
            }
        }

        public bool IsConfigAparenciaSection => SelectedConfigSection == "Aparencia";
        public bool IsConfigBackupSection => SelectedConfigSection == "Backup";
        public bool IsConfigAlertasSection => SelectedConfigSection == "Alertas";
        public bool IsConfigNotificacoesSection => SelectedConfigSection == "Notificacoes";
        public bool IsConfigAtualizacoesSection => SelectedConfigSection == "Atualizacoes";
        public bool IsConfigAvancadoSection => SelectedConfigSection == "Avancado";
        public bool IsConfigSobreSection => SelectedConfigSection == "Sobre";
        public bool IsAdvancedConfigUnlocked
        {
            get => _isAdvancedConfigUnlocked;
            private set
            {
                if (_isAdvancedConfigUnlocked == value)
                {
                    return;
                }

                _isAdvancedConfigUnlocked = value;
                OnPropertyChanged();
                _recreateClientesCommand.RaiseCanExecuteChanged();
                _recreatePagamentosCommand.RaiseCanExecuteChanged();
            }
        }

        public string AdvancedUnlockStatusMessage
        {
            get => _advancedUnlockStatusMessage;
            private set
            {
                if (string.Equals(_advancedUnlockStatusMessage, value, StringComparison.Ordinal))
                {
                    return;
                }

                _advancedUnlockStatusMessage = value;
                OnPropertyChanged();
            }
        }

        public bool IsAdvancedUnlockError
        {
            get => _isAdvancedUnlockError;
            private set
            {
                if (_isAdvancedUnlockError == value)
                {
                    return;
                }

                _isAdvancedUnlockError = value;
                OnPropertyChanged();
            }
        }

        private void SelectConfigSection(string? section)
        {
            SelectedConfigSection = section switch
            {
                "Backup" => "Backup",
                "Alertas" => "Alertas",
                "Notificacoes" => "Notificacoes",
                "Atualizacoes" => "Atualizacoes",
                "Avancado" => "Avancado",
                "Sobre" => "Sobre",
                _ => "Aparencia"
            };
        }

        public bool TryUnlockAdvancedConfig(string? password)
        {
            if (IsAdvancedConfigUnlocked)
            {
                return true;
            }

            if (string.IsNullOrWhiteSpace(_advancedSectionPassword))
            {
                IsAdvancedUnlockError = true;
                AdvancedUnlockStatusMessage = AdvancedUnlockNotConfiguredMessage;
                return false;
            }

            var typed = password?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(typed))
            {
                IsAdvancedUnlockError = true;
                AdvancedUnlockStatusMessage = "Informe a senha para liberar o modulo avancado.";
                return false;
            }

            if (!string.Equals(typed, _advancedSectionPassword, StringComparison.Ordinal))
            {
                IsAdvancedUnlockError = true;
                AdvancedUnlockStatusMessage = "Senha invalida. Tente novamente.";
                return false;
            }

            IsAdvancedConfigUnlocked = true;
            IsAdvancedUnlockError = false;
            AdvancedUnlockStatusMessage = "Acesso avancado liberado para esta sessao.";
            return true;
        }

        public void LockAdvancedConfig()
        {
            IsAdvancedConfigUnlocked = false;
            ResetAdvancedUnlockFeedback();
        }

        private void ResetAdvancedUnlockFeedback()
        {
            IsAdvancedUnlockError = false;
            AdvancedUnlockStatusMessage = string.IsNullOrWhiteSpace(_advancedSectionPassword)
                ? AdvancedUnlockNotConfiguredMessage
                : AdvancedUnlockIdleMessage;
        }

        private static string ResolveAdvancedSectionPassword(string? configuredPassword)
        {
            var custom = Environment.GetEnvironmentVariable("STARFIRE_ADVANCED_PASSWORD");
            if (!string.IsNullOrWhiteSpace(custom))
            {
                return custom.Trim();
            }

            return string.IsNullOrWhiteSpace(configuredPassword)
                ? string.Empty
                : configuredPassword.Trim();
        }

        private void InitializeDashboardKpiCards()
        {
            DashboardKpiCards.Clear();
            _dashboardKpiCardsIndex.Clear();

            AddDashboardKpiCard("Extintores vencidos", "Crítico", "\uE814", "ext-vencidos", "Danger");
            AddDashboardKpiCard("Extintores vencendo (7 dias)", "Atenção", "\uE823", "ext-vencendo7", "Warning");
            AddDashboardKpiCard("Extintores vencendo (30 dias)", "30 dias", "\uE823", "ext-vencendo30", "Warning");
            AddDashboardKpiCard("Alvarás vencidos", "Crítico", "\uE8B4", "alvara-vencidos", "Danger");
            AddDashboardKpiCard("Alvarás vencendo (30 dias)", "30 dias", "\uE8B4", "alvara-vencendo30", "Warning");
            AddDashboardKpiCard("Pagamentos vencidos", "Crítico", "\uE8C7", "pag-vencidos", "Danger");
            AddDashboardKpiCard("Pagamentos vencendo (30 dias)", "30 dias", "\uE8C7", "pag-vencendo30", "Warning");

            SyncDashboardKpiCardValues();
        }

        private void AddDashboardKpiCard(
            string title,
            string badgeText,
            string iconGlyph,
            string commandParameter,
            string severity)
        {
            var item = new DashboardKpiCardItem(
                title,
                badgeText,
                iconGlyph,
                commandParameter,
                "Clique para listar os clientes deste aviso",
                severity);

            DashboardKpiCards.Add(item);
            _dashboardKpiCardsIndex.Add(item);
        }

        private void SyncDashboardKpiCardValues()
        {
            if (_dashboardKpiCardsIndex.Count < 7)
            {
                return;
            }

            _dashboardKpiCardsIndex[0].Value = Dashboard.ExtintoresVencidos;
            _dashboardKpiCardsIndex[1].Value = Dashboard.ExtintoresVencendo7;
            _dashboardKpiCardsIndex[2].Value = Dashboard.ExtintoresVencendo30;
            _dashboardKpiCardsIndex[3].Value = Dashboard.AlvaraVencido;
            _dashboardKpiCardsIndex[4].Value = Dashboard.AlvaraVencendo;
            _dashboardKpiCardsIndex[5].Value = Dashboard.PagamentosVencidos;
            _dashboardKpiCardsIndex[6].Value = Dashboard.PagamentosVencendo30;
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
            OnPropertyChanged(nameof(CanActivateSelectedClientes));
            OnPropertyChanged(nameof(CanDeactivateSelectedClientes));
            OnPropertyChanged(nameof(CanExportSelectedClientes));
            OnPropertyChanged(nameof(SelectedClientesSummary));
            _editCommand.RaiseCanExecuteChanged();
            _deleteCommand.RaiseCanExecuteChanged();
            _detailsCommand.RaiseCanExecuteChanged();
            _activateSelectedClientesCommand.RaiseCanExecuteChanged();
            _deactivateSelectedClientesCommand.RaiseCanExecuteChanged();
            _exportSelectedClientesCommand.RaiseCanExecuteChanged();
        }

        public void UpdateSelectedPagamentos(IReadOnlyCollection<Pagamento> selectedPagamentos)
        {
            _selectedPagamentos.Clear();
            _selectedPagamentos.AddRange(selectedPagamentos.Where(p => p != null));
            _selectedPagamento = _selectedPagamentos.FirstOrDefault();
            OnPropertyChanged(nameof(SelectedPagamento));
            OnPropertyChanged(nameof(SelectedPagamentosCount));
            OnPropertyChanged(nameof(CanEditSelectedPagamento));
            OnPropertyChanged(nameof(CanDeleteSelectedPagamentos));
            OnPropertyChanged(nameof(SelectedPagamentosSummary));
            _editPagamentoCommand.RaiseCanExecuteChanged();
            _deletePagamentoCommand.RaiseCanExecuteChanged();
            _cobrancaCommand.RaiseCanExecuteChanged();
            _openPagamentoAttachmentsCommand.RaiseCanExecuteChanged();
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
                OnPropertyChanged(nameof(ShowClientesEmptyState));
            }
        }

        public bool CanGoPrev => PageNumber > 1;
        public bool CanGoNext => PageNumber < PageCount;
        public int DisplayFrom => TotalClientes == 0 ? 0 : ((PageNumber - 1) * ClientesPageSize) + 1;
        public int DisplayTo => TotalClientes == 0 ? 0 : Math.Min(PageNumber * ClientesPageSize, TotalClientes);
        public string FooterSummary => $"Exibindo {DisplayFrom} a {DisplayTo} de {TotalClientes} clientes";
        public bool ShowPagination => TotalClientes > ClientesPageSize;

        public int SelectedMainTabIndex
        {
            get => _selectedMainTabIndex;
            set
            {
                _selectedMainTabIndex = value;
                OnPropertyChanged();
                IsNotificationPanelOpen = false;
                IsVersionHistoryOpen = false;
            }
        }

        public bool IsNotificationPanelOpen
        {
            get => _isNotificationPanelOpen;
            set
            {
                if (_isNotificationPanelOpen == value)
                {
                    return;
                }

                _isNotificationPanelOpen = value;
                if (value)
                {
                    IsVersionHistoryOpen = false;
                }

                OnPropertyChanged();
            }
        }

        public bool IsVersionHistoryOpen
        {
            get => _isVersionHistoryOpen;
            set
            {
                if (_isVersionHistoryOpen == value)
                {
                    return;
                }

                _isVersionHistoryOpen = value;
                if (value)
                {
                    IsNotificationPanelOpen = false;
                }

                OnPropertyChanged();
            }
        }

        public bool NotificationShowExtintores
        {
            get => _notificationShowExtintores;
            set
            {
                if (_notificationShowExtintores == value)
                {
                    return;
                }

                _notificationShowExtintores = value;
                OnPropertyChanged();
                RefreshDashboardExecutiveData();
                RecomputePendingConfigChanges();
            }
        }

        public bool NotificationShowAlvaras
        {
            get => _notificationShowAlvaras;
            set
            {
                if (_notificationShowAlvaras == value)
                {
                    return;
                }

                _notificationShowAlvaras = value;
                OnPropertyChanged();
                RefreshDashboardExecutiveData();
                RecomputePendingConfigChanges();
            }
        }

        public bool NotificationShowPagamentos
        {
            get => _notificationShowPagamentos;
            set
            {
                if (_notificationShowPagamentos == value)
                {
                    return;
                }

                _notificationShowPagamentos = value;
                OnPropertyChanged();
                RefreshDashboardExecutiveData();
                RecomputePendingConfigChanges();
            }
        }

        public bool NotificationIncludeOverdue
        {
            get => _notificationIncludeOverdue;
            set
            {
                if (_notificationIncludeOverdue == value)
                {
                    return;
                }

                _notificationIncludeOverdue = value;
                OnPropertyChanged();
                RefreshDashboardExecutiveData();
                RecomputePendingConfigChanges();
            }
        }

        public int NotificationDaysWindow
        {
            get => _notificationDaysWindow;
            set
            {
                var normalized = value <= 0 ? 30 : value;
                if (_notificationDaysWindow == normalized)
                {
                    return;
                }

                _notificationDaysWindow = normalized;
                OnPropertyChanged();
                RefreshDashboardExecutiveData();
                RecomputePendingConfigChanges();
            }
        }

        public int NotificationMaxItems
        {
            get => _notificationMaxItems;
            set
            {
                var normalized = value <= 0 ? 10 : value;
                if (_notificationMaxItems == normalized)
                {
                    return;
                }

                _notificationMaxItems = normalized;
                OnPropertyChanged();
                RefreshDashboardExecutiveData();
                RecomputePendingConfigChanges();
            }
        }

        public int VisibleNotificationCount => BellNotificationItems.Count;
        public int PendingNotificationCount => _notificationEligibleCount;
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
                _saveAlertSettingsCommand.RaiseCanExecuteChanged();
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
                _saveAlertSettingsCommand.RaiseCanExecuteChanged();
            }
        }

        public bool IsLoadingClientes
        {
            get => _isLoadingClientes;
            private set
            {
                if (_isLoadingClientes == value)
                {
                    return;
                }

                _isLoadingClientes = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ShowClientesLoadingState));
                OnPropertyChanged(nameof(ShowClientesEmptyState));
                OnPropertyChanged(nameof(ClientesStateTitle));
                OnPropertyChanged(nameof(ClientesStateDescription));
            }
        }

        public bool IsLoadingPagamentos
        {
            get => _isLoadingPagamentos;
            private set
            {
                if (_isLoadingPagamentos == value)
                {
                    return;
                }

                _isLoadingPagamentos = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ShowPagamentosLoadingState));
                OnPropertyChanged(nameof(ShowPagamentosErrorState));
                OnPropertyChanged(nameof(ShowPagamentosEmptyState));
                OnPropertyChanged(nameof(PagamentosStateTitle));
                OnPropertyChanged(nameof(PagamentosStateDescription));
            }
        }

        public bool IsSavingSettings
        {
            get => _isSavingSettings;
            private set
            {
                if (_isSavingSettings == value)
                {
                    return;
                }

                _isSavingSettings = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ConfigSaveButtonText));
                _saveAlertSettingsCommand.RaiseCanExecuteChanged();
            }
        }

        public string ConfigValidationMessage
        {
            get => _configValidationMessage;
            private set
            {
                if (string.Equals(_configValidationMessage, value, StringComparison.Ordinal))
                {
                    return;
                }

                _configValidationMessage = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasConfigValidationError));
                OnPropertyChanged(nameof(IsConfigValid));
                _saveAlertSettingsCommand.RaiseCanExecuteChanged();
            }
        }

        public string PagamentosLoadErrorMessage
        {
            get => _pagamentosLoadErrorMessage;
            private set
            {
                if (string.Equals(_pagamentosLoadErrorMessage, value, StringComparison.Ordinal))
                {
                    return;
                }

                _pagamentosLoadErrorMessage = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasPagamentosLoadError));
                OnPropertyChanged(nameof(ShowPagamentosErrorState));
                OnPropertyChanged(nameof(ShowPagamentosEmptyState));
                OnPropertyChanged(nameof(PagamentosStateTitle));
                OnPropertyChanged(nameof(PagamentosStateDescription));
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
                OnPropertyChanged(nameof(IsClienteAtivosMode));
                OnPropertyChanged(nameof(CanResetClienteFilters));
                _resetClienteFiltersCommand.RaiseCanExecuteChanged();
                _removeClienteFilterCommand.RaiseCanExecuteChanged();
                ApplyClienteStatusFilter();
            }
        }

        public bool IsClienteAtivosMode
        {
            get => ClienteStatusTabIndex == 0;
            set
            {
                var targetIndex = value ? 0 : 1;
                if (ClienteStatusTabIndex == targetIndex)
                {
                    return;
                }

                ClienteStatusTabIndex = targetIndex;
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
                if (_alerta7Dias == value)
                {
                    return;
                }

                _alerta7Dias = value;
                OnPropertyChanged();
                RecomputePendingConfigChanges();
            }
        }

        public bool Alerta15Dias
        {
            get => _alerta15Dias;
            set
            {
                if (_alerta15Dias == value)
                {
                    return;
                }

                _alerta15Dias = value;
                OnPropertyChanged();
                RecomputePendingConfigChanges();
            }
        }

        public bool Alerta30Dias
        {
            get => _alerta30Dias;
            set
            {
                if (_alerta30Dias == value)
                {
                    return;
                }

                _alerta30Dias = value;
                OnPropertyChanged();
                RecomputePendingConfigChanges();
            }
        }

        public string AlertWindowText => _alertRules.MaxAlertDays > 0 ? $"até {_alertRules.MaxAlertDays} dias" : "desativados";

        public bool HasPendingConfigChanges
        {
            get => _hasPendingConfigChanges;
            private set
            {
                if (_hasPendingConfigChanges == value)
                {
                    return;
                }

                _hasPendingConfigChanges = value;
                OnPropertyChanged();
                _saveAlertSettingsCommand.RaiseCanExecuteChanged();
            }
        }

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

        public bool HasToastAction
        {
            get => _hasToastAction;
            private set
            {
                if (_hasToastAction == value)
                {
                    return;
                }

                _hasToastAction = value;
                OnPropertyChanged();
                _toastActionCommand.RaiseCanExecuteChanged();
            }
        }

        public string ToastActionLabel
        {
            get => _toastActionLabel;
            private set
            {
                if (string.Equals(_toastActionLabel, value, StringComparison.Ordinal))
                {
                    return;
                }

                _toastActionLabel = value;
                OnPropertyChanged();
            }
        }

        public bool IsOperationInProgress
        {
            get => _isOperationInProgress;
            private set
            {
                if (_isOperationInProgress == value)
                {
                    return;
                }

                _isOperationInProgress = value;
                OnPropertyChanged();
            }
        }

        public string OperationStatusMessage
        {
            get => _operationStatusMessage;
            private set
            {
                if (string.Equals(_operationStatusMessage, value, StringComparison.Ordinal))
                {
                    return;
                }

                _operationStatusMessage = value;
                OnPropertyChanged();
            }
        }

        public bool IsCompactLayout
        {
            get => _isCompactLayout;
            private set
            {
                if (_isCompactLayout == value)
                {
                    return;
                }

                _isCompactLayout = value;
                OnPropertyChanged();
            }
        }

        public bool IsClientesFiltersCollapsed
        {
            get => _isClientesFiltersCollapsed;
            set
            {
                if (_isClientesFiltersCollapsed == value)
                {
                    return;
                }

                _isClientesFiltersCollapsed = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ClientesFiltersToggleText));
            }
        }

        public bool IsPagamentosFiltersCollapsed
        {
            get => _isPagamentosFiltersCollapsed;
            set
            {
                if (_isPagamentosFiltersCollapsed == value)
                {
                    return;
                }

                _isPagamentosFiltersCollapsed = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(PagamentosFiltersToggleText));
            }
        }

        public string ClientesFiltersToggleText => IsClientesFiltersCollapsed ? "Mostrar filtros" : "Ocultar filtros";
        public string PagamentosFiltersToggleText => IsPagamentosFiltersCollapsed ? "Mostrar filtros" : "Ocultar filtros";

        public string UiBorderColorHex
        {
            get => _uiBorderColorHex;
            set
            {
                var normalized = NormalizeUiHexInput(value, out var isValidInput);
                if (!isValidInput)
                {
                    OnPropertyChanged();
                    return;
                }

                if (string.Equals(_uiBorderColorHex, normalized, StringComparison.Ordinal))
                {
                    if (!string.Equals(value ?? string.Empty, normalized, StringComparison.Ordinal))
                    {
                        OnPropertyChanged();
                    }

                    return;
                }

                _uiBorderColorHex = normalized;
                OnPropertyChanged();
                OnPropertyChanged(nameof(UiBorderColorPreviewBrush));
                OnPropertyChanged(nameof(UiBorderColorDisplay));
                OnPropertyChanged(nameof(HasCustomChromeColors));
                ApplyUiChromeCustomization();
                RecomputePendingConfigChanges();
                _resetUiBorderColorCommand.RaiseCanExecuteChanged();
                _resetUiChromeColorsCommand.RaiseCanExecuteChanged();
            }
        }

        public string UiTitleBarColorHex
        {
            get => _uiTitleBarColorHex;
            set
            {
                var normalized = NormalizeUiHexInput(value, out var isValidInput);
                if (!isValidInput)
                {
                    OnPropertyChanged();
                    return;
                }

                if (string.Equals(_uiTitleBarColorHex, normalized, StringComparison.Ordinal))
                {
                    if (!string.Equals(value ?? string.Empty, normalized, StringComparison.Ordinal))
                    {
                        OnPropertyChanged();
                    }

                    return;
                }

                _uiTitleBarColorHex = normalized;
                OnPropertyChanged();
                OnPropertyChanged(nameof(UiTitleBarColorPreviewBrush));
                OnPropertyChanged(nameof(UiTitleBarColorDisplay));
                OnPropertyChanged(nameof(HasCustomChromeColors));
                ApplyUiChromeCustomization();
                RecomputePendingConfigChanges();
                _resetUiTitleBarColorCommand.RaiseCanExecuteChanged();
                _resetUiChromeColorsCommand.RaiseCanExecuteChanged();
            }
        }

        public string UiVanillaColorHex
        {
            get => _uiVanillaColorHex;
            set
            {
                var normalized = NormalizeUiHexInput(value, out var isValidInput);
                if (!isValidInput)
                {
                    OnPropertyChanged();
                    return;
                }

                if (string.Equals(_uiVanillaColorHex, normalized, StringComparison.Ordinal))
                {
                    if (!string.Equals(value ?? string.Empty, normalized, StringComparison.Ordinal))
                    {
                        OnPropertyChanged();
                    }

                    return;
                }

                _uiVanillaColorHex = normalized;
                OnPropertyChanged();
                OnPropertyChanged(nameof(UiVanillaColorPreviewBrush));
                OnPropertyChanged(nameof(UiVanillaColorDisplay));
                OnPropertyChanged(nameof(HasCustomChromeColors));
                ApplyUiChromeCustomization();
                RecomputePendingConfigChanges();
                _resetUiVanillaColorCommand.RaiseCanExecuteChanged();
                _resetUiVanillaIntensityCommand.RaiseCanExecuteChanged();
                _resetUiChromeColorsCommand.RaiseCanExecuteChanged();
            }
        }

        public int UiVanillaIntensityPercent
        {
            get => _uiVanillaIntensityPercent;
            set
            {
                var normalized = AppThemeManager.NormalizeVanillaIntensityPercent(value);
                if (_uiVanillaIntensityPercent == normalized)
                {
                    return;
                }

                _uiVanillaIntensityPercent = normalized;
                OnPropertyChanged();
                OnPropertyChanged(nameof(UiVanillaIntensityLabel));
                OnPropertyChanged(nameof(HasCustomChromeColors));
                ApplyUiChromeCustomization();
                RecomputePendingConfigChanges();
                _resetUiVanillaIntensityCommand.RaiseCanExecuteChanged();
                _resetUiChromeColorsCommand.RaiseCanExecuteChanged();
            }
        }

        public bool HasCustomChromeColors =>
            !string.IsNullOrWhiteSpace(UiBorderColorHex) ||
            !string.IsNullOrWhiteSpace(UiTitleBarColorHex) ||
            !string.IsNullOrWhiteSpace(UiVanillaColorHex) ||
            UiVanillaIntensityPercent != 100;

        public Brush UiBorderColorPreviewBrush => ResolvePreviewBrush(UiBorderColorHex, "BorderColor");
        public Brush UiTitleBarColorPreviewBrush => ResolvePreviewBrush(UiTitleBarColorHex, "TitleBarBorder");
        public Brush UiVanillaColorPreviewBrush => ResolvePreviewBrush(UiVanillaColorHex, "ActionAccentBg");
        public string UiBorderColorDisplay => string.IsNullOrWhiteSpace(UiBorderColorHex) ? "Padrão do tema" : UiBorderColorHex;
        public string UiTitleBarColorDisplay => string.IsNullOrWhiteSpace(UiTitleBarColorHex) ? "Padrão do tema" : UiTitleBarColorHex;
        public string UiVanillaColorDisplay => string.IsNullOrWhiteSpace(UiVanillaColorHex) ? "Padrão do tema" : UiVanillaColorHex;
        public string UiVanillaIntensityLabel => $"{UiVanillaIntensityPercent}%";

        public bool IsDarkMode
        {
            get => _isDarkMode;
            set
            {
                if (_isDarkMode == value)
                {
                    return;
                }

                _isDarkMode = value;
                AppThemeManager.ApplyTheme(_isDarkMode ? AppThemeManager.DarkTheme : AppThemeManager.LightTheme);
                ApplyUiChromeCustomization();
                OnPropertyChanged();
                OnPropertyChanged(nameof(CurrentThemeLabel));
                OnPropertyChanged(nameof(UiBorderColorPreviewBrush));
                OnPropertyChanged(nameof(UiTitleBarColorPreviewBrush));
                OnPropertyChanged(nameof(UiVanillaColorPreviewBrush));
                OnPropertyChanged(nameof(UiBorderColorDisplay));
                OnPropertyChanged(nameof(UiTitleBarColorDisplay));
                OnPropertyChanged(nameof(UiVanillaColorDisplay));
                RecomputePendingConfigChanges();
            }
        }

        public bool IsFullscreen
        {
            get => _isFullscreen;
            set
            {
                if (_isFullscreen == value)
                {
                    return;
                }

                _isFullscreen = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CurrentWindowModeLabel));
                OnPropertyChanged(nameof(CanChangeWindowResolution));
                ApplyWindowMode();
                RecomputePendingConfigChanges();
            }
        }

        public string SelectedWindowResolutionPreset
        {
            get => _selectedWindowResolutionPreset;
            set
            {
                var normalized = WindowResolutionPresets.Normalize(value);
                if (string.Equals(_selectedWindowResolutionPreset, normalized, StringComparison.Ordinal))
                {
                    return;
                }

                _selectedWindowResolutionPreset = normalized;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CurrentWindowResolutionLabel));
                if (!IsFullscreen)
                {
                    ApplyWindowMode();
                }

                RecomputePendingConfigChanges();
            }
        }

        public bool BackupAutomatico
        {
            get => _backupAutomatico;
            set
            {
                if (_backupAutomatico == value)
                {
                    return;
                }

                _backupAutomatico = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(BackupModeLabel));
                RecomputePendingConfigChanges();
                RecomputeConfigValidation();
            }
        }

        public string BackupFolder
        {
            get => _backupFolder;
            set
            {
                var normalized = value?.Trim() ?? string.Empty;
                if (string.Equals(_backupFolder, normalized, StringComparison.Ordinal))
                {
                    return;
                }

                _backupFolder = normalized;
                OnPropertyChanged();
                RecomputePendingConfigChanges();
                RecomputeConfigValidation();
            }
        }

        public int BackupIntervalHours
        {
            get => _backupIntervalHours;
            set
            {
                var normalized = value <= 0 ? 24 : value;
                if (_backupIntervalHours == normalized)
                {
                    return;
                }

                _backupIntervalHours = normalized;
                OnPropertyChanged();
                OnPropertyChanged(nameof(BackupIntervalLabel));
                RecomputePendingConfigChanges();
            }
        }

        public int BackupRetentionCount
        {
            get => _backupRetentionCount;
            set
            {
                var normalized = value <= 0 ? 10 : value;
                if (_backupRetentionCount == normalized)
                {
                    return;
                }

                _backupRetentionCount = normalized;
                OnPropertyChanged();
                OnPropertyChanged(nameof(BackupRetentionLabel));
                RecomputePendingConfigChanges();
            }
        }

        public string CurrentThemeLabel => IsDarkMode ? "Dark" : "Light";
        public string CurrentWindowModeLabel => IsFullscreen ? "Fullscreen" : "Normal";
        public string CurrentWindowResolutionLabel => SelectedWindowResolutionPreset == WindowResolutionPresets.Auto
            ? "Auto"
            : SelectedWindowResolutionPreset;
        public bool CanChangeWindowResolution => !IsFullscreen;
        public IReadOnlyList<ReleaseNoteVersion> ReleaseNotesHistory => _releaseNotesHistory;
        public string AppVersion => _appVersion;
        public string AppVersionDisplay => $"v.{_appVersion}";
        public string LatestReleaseNotesTitle => _releaseNotesHistory.Count > 0
            ? $"Novidades da versao {_releaseNotesHistory[0].Version}"
            : "Novidades da versao atual";
        public IReadOnlyList<string> LatestReleaseNotesItems => _releaseNotesHistory.Count > 0
            ? _releaseNotesHistory[0].Highlights
            : Array.Empty<string>();
        public string BuildDateTimeDisplay => _buildDateTimeDisplay;
        public string BackupModeLabel => BackupAutomatico ? "Ativado" : "Desativado";
        public string BackupIntervalLabel => $"A cada {BackupIntervalHours}h";
        public string BackupRetentionLabel => $"Manter últimos {BackupRetentionCount} arquivos";
        public string ConfigSaveButtonText => IsSavingSettings ? "Salvando configurações..." : "Salvar configurações";
        public bool HasConfigValidationError => !string.IsNullOrWhiteSpace(ConfigValidationMessage);
        public bool IsConfigValid => !HasConfigValidationError;
        public bool ShowClientesLoadingState => IsLoadingClientes;
        public bool ShowClientesEmptyState => !IsLoadingClientes && TotalClientes == 0;
        public string ClientesStateTitle => IsLoadingClientes ? "Carregando clientes..." : "Nenhum cliente encontrado";
        public string ClientesStateDescription => IsLoadingClientes
            ? "Aguarde enquanto carregamos os dados."
            : string.IsNullOrWhiteSpace(SearchTerm)
                ? "Cadastre um cliente novo ou importe uma planilha para começar."
                : $"Sem resultados para \"{SearchTerm.Trim()}\". Ajuste os filtros e tente novamente.";
        public bool HasPagamentosLoadError => !string.IsNullOrWhiteSpace(PagamentosLoadErrorMessage);
        public bool ShowPagamentosLoadingState => IsLoadingPagamentos;
        public bool ShowPagamentosErrorState => !IsLoadingPagamentos && HasPagamentosLoadError;
        public bool ShowPagamentosEmptyState => !IsLoadingPagamentos && !HasPagamentosLoadError && Pagamentos.Count == 0;
        public string PagamentosStateTitle => IsLoadingPagamentos
            ? "Carregando pagamentos..."
            : HasPagamentosLoadError
                ? "Falha ao carregar pagamentos"
                : "Nenhum pagamento encontrado";
        public string PagamentosStateDescription => IsLoadingPagamentos
            ? "Aguarde enquanto carregamos os dados."
            : HasPagamentosLoadError
                ? PagamentosLoadErrorMessage
                : string.IsNullOrWhiteSpace(PagamentoSearchTerm)
                    ? "Cadastre um pagamento novo ou importe uma planilha para começar."
                    : $"Sem resultados para \"{PagamentoSearchTerm.Trim()}\" com os filtros atuais.";
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

        private async Task LoadAsync(bool reloadUiSettings = false)
        {
            if (reloadUiSettings || !_hasLoadedUiSettings)
            {
                _suppressConfigDirtyTracking = true;
                try
                {
                    LoadThemeSettings();
                    await LoadAlertSettingsAsync();
                }
                finally
                {
                    _suppressConfigDirtyTracking = false;
                }

                CaptureSavedConfigSnapshot();
                _hasLoadedUiSettings = true;
            }

            IsLoadingClientes = true;
            try
            {
                var clientes = await _clienteRepository.GetAllAsync();
                ReplaceClientes(clientes);
                await LoadPagamentosAsync();
            }
            finally
            {
                IsLoadingClientes = false;
            }
        }

        private void LoadThemeSettings()
        {
            var settings = _appSettingsService.Load();
            IsDarkMode = AppThemeManager.NormalizeTheme(settings.Theme) == AppThemeManager.DarkTheme;
            SelectedWindowResolutionPreset = settings.WindowResolutionPreset;
            IsFullscreen = settings.Fullscreen;
            BackupAutomatico = settings.BackupEnabled;
            BackupFolder = settings.BackupFolder;
            BackupIntervalHours = settings.BackupIntervalHours;
            BackupRetentionCount = settings.BackupRetentionCount;
            _lastAutoBackupUtc = settings.LastAutoBackupUtc;
            NotificationShowExtintores = settings.NotificationShowExtintores;
            NotificationShowAlvaras = settings.NotificationShowAlvaras;
            NotificationShowPagamentos = settings.NotificationShowPagamentos;
            NotificationIncludeOverdue = settings.NotificationIncludeOverdue;
            NotificationDaysWindow = settings.NotificationDaysWindow;
            NotificationMaxItems = settings.NotificationMaxItems;
            UiBorderColorHex = settings.UiBorderColorHex;
            UiTitleBarColorHex = settings.UiTitleBarColorHex;
            UiVanillaColorHex = settings.UiVanillaColorHex;
            UiVanillaIntensityPercent = settings.UiVanillaIntensityPercent;
            _exportPreferredEntity = settings.ExportPreferredEntity == "Pagamentos" ? "Pagamentos" : "Clientes";
            _exportPreferExcel = settings.ExportPreferExcel;
            _advancedSectionPassword = ResolveAdvancedSectionPassword(settings.AdvancedSectionPassword);
            ResetAdvancedUnlockFeedback();
            ApplyPreferredExportFields(settings.ExportClienteSelectedFields, _preferredClienteExportFields);
            ApplyPreferredExportFields(settings.ExportPagamentoSelectedFields, _preferredPagamentoExportFields);
            AppThemeManager.ApplyTheme(settings.Theme);
            ApplyUiChromeCustomization();
            ApplyWindowMode();
            RecomputeConfigValidation();
            OnPropertyChanged(nameof(LastBackupLabel));
            StartBackupScheduler();
        }

        private void ApplyUiChromeCustomization()
        {
            AppThemeManager.ApplyChromeCustomization(UiBorderColorHex, UiTitleBarColorHex, UiVanillaColorHex, UiVanillaIntensityPercent);
        }

        private void PickUiColor(UiChromeColorTarget target)
        {
            var currentHex = target switch
            {
                UiChromeColorTarget.Border => UiBorderColorHex,
                UiChromeColorTarget.TitleBar => UiTitleBarColorHex,
                _ => UiVanillaColorHex
            };

            var dialogTitle = target switch
            {
                UiChromeColorTarget.Border => "Selecionar cor da borda",
                UiChromeColorTarget.TitleBar => "Selecionar cor da barra superior",
                _ => "Selecionar cor vanilla do app"
            };

            var owner = Application.Current?.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
                        ?? Application.Current?.MainWindow;
            var dialog = new ColorPickerWindow(dialogTitle, currentHex, owner);

            if (dialog.ShowDialog() != true || string.IsNullOrWhiteSpace(dialog.SelectedHexColor))
            {
                return;
            }

            switch (target)
            {
                case UiChromeColorTarget.Border:
                    UiBorderColorHex = dialog.SelectedHexColor;
                    return;
                case UiChromeColorTarget.TitleBar:
                    UiTitleBarColorHex = dialog.SelectedHexColor;
                    return;
                default:
                    UiVanillaColorHex = dialog.SelectedHexColor;
                    return;
            }
        }

        private static string NormalizeUiHexInput(string? value, out bool isValidInput)
        {
            var raw = value?.Trim() ?? string.Empty;
            if (raw.Length == 0)
            {
                isValidInput = true;
                return string.Empty;
            }

            var normalized = AppThemeManager.NormalizeOptionalHexColor(raw);
            isValidInput = normalized.Length > 0;
            return normalized;
        }

        private static Brush ResolvePreviewBrush(string hex, string fallbackResourceKey)
        {
            if (AppThemeManager.TryParseOptionalHexColor(hex, out var color))
            {
                return new SolidColorBrush(color);
            }

            if (Application.Current?.TryFindResource(fallbackResourceKey) is SolidColorBrush brush)
            {
                return brush;
            }

            return Brushes.Transparent;
        }

        private void RecomputePendingConfigChanges()
        {
            if (_suppressConfigDirtyTracking)
            {
                return;
            }

            HasPendingConfigChanges = BuildCurrentConfigSnapshot() != _savedConfigSnapshot;
        }

        private void RecomputeConfigValidation()
        {
            if (!BackupAutomatico)
            {
                ConfigValidationMessage = string.Empty;
                return;
            }

            if (string.IsNullOrWhiteSpace(BackupFolder))
            {
                ConfigValidationMessage = "Defina a pasta de destino para o backup automático.";
                return;
            }

            if (!Directory.Exists(BackupFolder))
            {
                ConfigValidationMessage = "A pasta de backup informada não existe. Selecione uma pasta válida.";
                return;
            }

            ConfigValidationMessage = string.Empty;
        }

        private void CaptureSavedConfigSnapshot()
        {
            _savedConfigSnapshot = BuildCurrentConfigSnapshot();
            HasPendingConfigChanges = false;
        }

        private ConfigSnapshot BuildCurrentConfigSnapshot()
        {
            return new ConfigSnapshot(
                IsDarkMode,
                IsFullscreen,
                SelectedWindowResolutionPreset,
                BackupAutomatico,
                BackupFolder.Trim(),
                BackupIntervalHours,
                BackupRetentionCount,
                Alerta7Dias,
                Alerta15Dias,
                Alerta30Dias,
                NotificationShowExtintores,
                NotificationShowAlvaras,
                NotificationShowPagamentos,
                NotificationIncludeOverdue,
                NotificationDaysWindow,
                NotificationMaxItems,
                UiBorderColorHex,
                UiTitleBarColorHex,
                UiVanillaColorHex,
                UiVanillaIntensityPercent);
        }

        private readonly record struct ConfigSnapshot(
            bool IsDarkMode,
            bool IsFullscreen,
            string WindowResolutionPreset,
            bool BackupAutomatico,
            string BackupFolder,
            int BackupIntervalHours,
            int BackupRetentionCount,
            bool Alerta7Dias,
            bool Alerta15Dias,
            bool Alerta30Dias,
            bool NotificationShowExtintores,
            bool NotificationShowAlvaras,
            bool NotificationShowPagamentos,
            bool NotificationIncludeOverdue,
            int NotificationDaysWindow,
            int NotificationMaxItems,
            string UiBorderColorHex,
            string UiTitleBarColorHex,
            string UiVanillaColorHex,
            int UiVanillaIntensityPercent)
        {
            public static ConfigSnapshot Empty { get; } = new(
                false,
                false,
                WindowResolutionPresets.Auto,
                false,
                string.Empty,
                24,
                10,
                true,
                true,
                true,
                true,
                true,
                true,
                true,
                30,
                10,
                string.Empty,
                string.Empty,
                string.Empty,
                100);
        }



        public void UpdateAdaptiveLayout(double windowWidth)
        {
            var compact = windowWidth > 0 && windowWidth < 1220;
            if (IsCompactLayout == compact)
            {
                return;
            }

            IsCompactLayout = compact;
            if (compact)
            {
                IsClientesFiltersCollapsed = true;
                IsPagamentosFiltersCollapsed = true;
            }
        }

        private async Task ExecuteToastActionAsync()
        {
            if (_toastActionHandler == null)
            {
                return;
            }

            var action = _toastActionHandler;
            _toastActionHandler = null;
            HasToastAction = false;
            ToastActionLabel = string.Empty;
            IsToastVisible = false;
            await action();
        }

        private void ResetToastAction()
        {
            _toastActionHandler = null;
            HasToastAction = false;
            ToastActionLabel = string.Empty;
        }

        private async Task ShowToastAsync(
            string message,
            string kind = "Success",
            string? actionLabel = null,
            Func<Task>? action = null,
            int durationMs = 2400)
        {
            _toastVersion++;
            var currentVersion = _toastVersion;
            ToastMessage = message;
            ToastKind = kind;
            _toastActionHandler = action;
            ToastActionLabel = actionLabel?.Trim() ?? string.Empty;
            HasToastAction = !string.IsNullOrWhiteSpace(ToastActionLabel) && action != null;
            IsToastVisible = true;
            await Task.Delay(Math.Max(1200, durationMs));

            if (currentVersion == _toastVersion)
            {
                IsToastVisible = false;
                ResetToastAction();
            }
        }

    }
}

