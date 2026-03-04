using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Media;
using ExtintorCrm.App.Domain;
using ExtintorCrm.App.Infrastructure.Documents;
using ExtintorCrm.App.UseCases;
using Microsoft.Win32;

namespace ExtintorCrm.App.Presentation
{
    public sealed class DocumentoAnexosViewModel : ViewModelBase
    {
        private readonly IDocumentoAnexoRepository _documentoAnexoRepository;
        private readonly DocumentoStorageService _documentoStorageService;
        private readonly DocumentoAnexosContext _context;
        private readonly AsyncRelayCommand _addCommand;
        private readonly RelayCommand _openCommand;
        private readonly AsyncRelayCommand _removeCommand;
        private readonly AsyncRelayCommand _refreshCommand;
        private DocumentoAnexoItem? _selectedAnexo;
        private string _selectedTipoDocumento = "Outro";
        private bool _isBusy;

        public DocumentoAnexosViewModel(
            IDocumentoAnexoRepository documentoAnexoRepository,
            DocumentoStorageService documentoStorageService,
            DocumentoAnexosContext context)
        {
            _documentoAnexoRepository = documentoAnexoRepository;
            _documentoStorageService = documentoStorageService;
            _context = context;

            if (_context.Contexto == "Alvara")
            {
                TipoDocumentoOptions.Add("Alvará");
                TipoDocumentoOptions.Add("Laudo");
                TipoDocumentoOptions.Add("Licença");
                TipoDocumentoOptions.Add("Outro");
                _selectedTipoDocumento = "Alvará";
            }
            else
            {
                TipoDocumentoOptions.Add("Nota fiscal");
                TipoDocumentoOptions.Add("Boleto");
                TipoDocumentoOptions.Add("Comprovante");
                TipoDocumentoOptions.Add("Recibo");
                TipoDocumentoOptions.Add("Contrato");
                TipoDocumentoOptions.Add("Outro");
                _selectedTipoDocumento = "Nota fiscal";
            }

            _addCommand = new AsyncRelayCommand(async _ => await AddFilesAsync(), _ => !IsBusy);
            _openCommand = new RelayCommand(_ => OpenSelected(), _ => !IsBusy && SelectedAnexo != null);
            _removeCommand = new AsyncRelayCommand(async _ => await RemoveSelectedAsync(), _ => !IsBusy && SelectedAnexo != null);
            _refreshCommand = new AsyncRelayCommand(async _ => await LoadAsync(), _ => !IsBusy);
            AddCommand = _addCommand;
            OpenCommand = _openCommand;
            RemoveCommand = _removeCommand;
            RefreshCommand = _refreshCommand;
        }

        public ObservableCollection<string> TipoDocumentoOptions { get; } = new();
        public ObservableCollection<DocumentoAnexoItem> Anexos { get; } = new();

        public string Title => _context.Contexto == "Alvara" ? "Anexos do Alvará" : "Anexos do Pagamento";

        public string Subtitle => _context.Referencia;

        public string SelectedTipoDocumento
        {
            get => _selectedTipoDocumento;
            set
            {
                if (string.Equals(_selectedTipoDocumento, value, StringComparison.Ordinal))
                {
                    return;
                }

                _selectedTipoDocumento = value;
                OnPropertyChanged();
            }
        }

        public DocumentoAnexoItem? SelectedAnexo
        {
            get => _selectedAnexo;
            set
            {
                _selectedAnexo = value;
                OnPropertyChanged();
                _openCommand.RaiseCanExecuteChanged();
                _removeCommand.RaiseCanExecuteChanged();
            }
        }

        public bool IsBusy
        {
            get => _isBusy;
            private set
            {
                _isBusy = value;
                OnPropertyChanged();
                _addCommand.RaiseCanExecuteChanged();
                _openCommand.RaiseCanExecuteChanged();
                _removeCommand.RaiseCanExecuteChanged();
                _refreshCommand.RaiseCanExecuteChanged();
            }
        }

        public int TotalAnexos => Anexos.Count;

        public AsyncRelayCommand AddCommand { get; }
        public RelayCommand OpenCommand { get; }
        public AsyncRelayCommand RemoveCommand { get; }
        public AsyncRelayCommand RefreshCommand { get; }

        public async Task LoadAsync()
        {
            IsBusy = true;
            try
            {
                var anexos = _context.Contexto == "Alvara"
                    ? await _documentoAnexoRepository.ListByClienteAlvaraAsync(_context.ClienteId ?? Guid.Empty)
                    : await _documentoAnexoRepository.ListByPagamentoAsync(_context.PagamentoId ?? Guid.Empty);

                Anexos.Clear();
                foreach (var anexo in anexos)
                {
                    Anexos.Add(new DocumentoAnexoItem(anexo));
                }

                OnPropertyChanged(nameof(TotalAnexos));
                SelectedAnexo = Anexos.FirstOrDefault();
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task AddFilesAsync()
        {
            var dialog = new OpenFileDialog
            {
                Title = "Selecione os documentos para anexar",
                Filter = "Documentos (*.*)|*.*",
                CheckFileExists = true,
                Multiselect = true
            };

            if (dialog.ShowDialog() != true || dialog.FileNames.Length == 0)
            {
                return;
            }

            IsBusy = true;
            try
            {
                foreach (var fileName in dialog.FileNames)
                {
                    StoredDocumentoArquivo? stored = null;
                    try
                    {
                        stored = _context.Contexto == "Alvara"
                            ? _documentoStorageService.StoreForAlvara(_context.ClienteId ?? Guid.Empty, fileName)
                            : _documentoStorageService.StoreForPagamento(_context.PagamentoId ?? Guid.Empty, fileName);

                        var entity = new DocumentoAnexo
                        {
                            Id = stored.DocumentoId,
                            Contexto = _context.Contexto,
                            ClienteId = _context.ClienteId,
                            PagamentoId = _context.PagamentoId,
                            TipoDocumento = string.IsNullOrWhiteSpace(SelectedTipoDocumento) ? "Outro" : SelectedTipoDocumento,
                            NomeOriginal = stored.NomeOriginal,
                            CaminhoRelativo = stored.CaminhoRelativo,
                            TamanhoBytes = stored.TamanhoBytes,
                            CriadoEm = DateTime.UtcNow,
                            AtualizadoEm = DateTime.UtcNow
                        };

                        await _documentoAnexoRepository.AddAsync(entity);
                    }
                    catch (Exception ex)
                    {
                        if (stored != null)
                        {
                            _documentoStorageService.DeleteByRelativePath(stored.CaminhoRelativo);
                        }

                        DialogService.Error(
                            "Anexos",
                            $"Falha ao anexar '{Path.GetFileName(fileName)}': {ex.Message}",
                            null);
                    }
                }

                await LoadAsync();
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void OpenSelected()
        {
            if (SelectedAnexo == null)
            {
                return;
            }

            try
            {
                var absolutePath = _documentoStorageService.ResolveAbsolutePath(SelectedAnexo.CaminhoRelativo);
                if (!File.Exists(absolutePath))
                {
                    DialogService.Info("Anexos", "Arquivo não encontrado no armazenamento local.", null);
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
                DialogService.Error("Anexos", $"Falha ao abrir arquivo: {ex.Message}", null);
            }
        }

        private async Task RemoveSelectedAsync()
        {
            if (SelectedAnexo == null)
            {
                return;
            }

            var confirmed = DialogService.Confirm(
                "Excluir anexo",
                $"Deseja realmente excluir o anexo '{SelectedAnexo.NomeOriginal}'?",
                null);
            if (!confirmed)
            {
                return;
            }

            IsBusy = true;
            try
            {
                await _documentoAnexoRepository.DeleteAsync(SelectedAnexo.Id);
                _documentoStorageService.DeleteByRelativePath(SelectedAnexo.CaminhoRelativo);
                await LoadAsync();
            }
            finally
            {
                IsBusy = false;
            }
        }
    }

    public sealed class DocumentoAnexoItem
    {
        public DocumentoAnexoItem(DocumentoAnexo source)
        {
            Id = source.Id;
            TipoDocumento = source.TipoDocumento;
            NomeOriginal = source.NomeOriginal;
            CaminhoRelativo = source.CaminhoRelativo;
            TamanhoBytes = source.TamanhoBytes;
            CriadoEm = source.CriadoEm;
        }

        public Guid Id { get; }
        public string TipoDocumento { get; }
        public string NomeOriginal { get; }
        public string CaminhoRelativo { get; }
        public long TamanhoBytes { get; }
        public DateTime CriadoEm { get; }
        public string CriadoEmTexto => CriadoEm.ToLocalTime().ToString("dd/MM/yyyy HH:mm");
        public string TamanhoTexto => FormatFileSize(TamanhoBytes);
        public string ExtensaoRotulo => ResolveExtensaoRotulo(NomeOriginal);
        public string ExtensaoCurta => ResolveExtensaoCurta(ExtensaoRotulo);
        public string CategoriaArquivo => ResolveCategoriaArquivo(NomeOriginal);
        public ImageSource? IconSource => FileIconHelper.GetSmallIcon(NomeOriginal);

        private static string FormatFileSize(long sizeInBytes)
        {
            if (sizeInBytes <= 0)
            {
                return "0 B";
            }

            string[] units = ["B", "KB", "MB", "GB"];
            var value = (double)sizeInBytes;
            var unit = 0;
            while (value >= 1024 && unit < units.Length - 1)
            {
                value /= 1024;
                unit++;
            }

            return string.Format(CultureInfo.InvariantCulture, "{0:0.##} {1}", value, units[unit]);
        }

        private static string ResolveExtensaoRotulo(string nomeOriginal)
        {
            var extension = Path.GetExtension(nomeOriginal);
            return string.IsNullOrWhiteSpace(extension)
                ? "ARQ"
                : extension.Trim('.').ToUpperInvariant();
        }

        private static string ResolveExtensaoCurta(string extensao)
        {
            if (string.IsNullOrWhiteSpace(extensao))
            {
                return "ARQ";
            }

            return extensao.Length <= 3 ? extensao : extensao[..3];
        }

        private static string ResolveCategoriaArquivo(string nomeOriginal)
        {
            var extension = Path.GetExtension(nomeOriginal)?.ToLowerInvariant() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(extension))
            {
                return "Documento";
            }

            return extension switch
            {
                ".pdf" => "Pdf",
                ".jpg" or ".jpeg" or ".png" or ".bmp" or ".gif" or ".webp" or ".svg" => "Imagem",
                ".xls" or ".xlsx" or ".csv" or ".ods" => "Planilha",
                ".zip" or ".rar" or ".7z" => "Comprimido",
                _ => "Documento"
            };
        }
    }

    public sealed class DocumentoAnexosContext
    {
        public string Contexto { get; init; } = "Pagamento";
        public Guid? ClienteId { get; init; }
        public Guid? PagamentoId { get; init; }
        public string Referencia { get; init; } = string.Empty;

        public static DocumentoAnexosContext ForPagamento(Guid pagamentoId, string referencia)
        {
            return new DocumentoAnexosContext
            {
                Contexto = "Pagamento",
                PagamentoId = pagamentoId,
                Referencia = string.IsNullOrWhiteSpace(referencia) ? "Pagamento" : referencia
            };
        }

        public static DocumentoAnexosContext ForAlvara(Guid clienteId, string referencia)
        {
            return new DocumentoAnexosContext
            {
                Contexto = "Alvara",
                ClienteId = clienteId,
                Referencia = string.IsNullOrWhiteSpace(referencia) ? "Alvará" : referencia
            };
        }
    }
}
