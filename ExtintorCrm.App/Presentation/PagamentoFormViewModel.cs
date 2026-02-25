using System;
using System.Collections.Generic;
using System.Linq;
using ExtintorCrm.App.Domain;

namespace ExtintorCrm.App.Presentation
{
    public class PagamentoFormViewModel : ViewModelBase
    {
        private readonly Pagamento? _source;
        private Guid _clienteId;
        private string _descricao = string.Empty;
        private string _tipo = "Receita";
        private string _statusFinanceiro = "Agendado";
        private string _categoria = string.Empty;
        private string _subcategoria = string.Empty;
        private string? _observacoes;
        private decimal _valor;
        private DateTime _dataVencimento = DateTime.Today;
        private bool _pago;
        private DateTime? _dataPagamento;

        public PagamentoFormViewModel(IEnumerable<Cliente> clientes, Pagamento? source = null)
        {
            Clientes = clientes
                .OrderBy(c => c.NomeFantasia)
                .Select(c => new ClienteOption
                {
                    Id = c.Id,
                    Nome = c.NomeFantasia,
                    CpfCnpj = c.CPF ?? c.Documento
                })
                .ToList();

            TipoOptions = new List<string> { "Receita", "Despesa" };
            StatusOptions = new List<string> { "Agendado", "Efetivado", "Cancelado" };

            _source = source;
            if (source != null)
            {
                PagamentoId = source.Id;
                _clienteId = source.ClienteId;
                _descricao = source.Descricao;
                _tipo = string.IsNullOrWhiteSpace(source.Tipo) ? "Receita" : source.Tipo;
                _statusFinanceiro = string.IsNullOrWhiteSpace(source.Status) ? "Agendado" : source.Status;
                _categoria = source.Categoria ?? string.Empty;
                _subcategoria = source.Subcategoria ?? string.Empty;
                _observacoes = source.Observacoes;
                _valor = source.Valor;
                _dataVencimento = source.DataVencimento;
                _pago = source.Pago;
                _dataPagamento = source.DataPagamento;
            }
            else
            {
                _clienteId = Clientes.FirstOrDefault()?.Id ?? Guid.Empty;
            }
        }

        public Guid PagamentoId { get; } = Guid.Empty;
        public List<ClienteOption> Clientes { get; }
        public List<string> TipoOptions { get; }
        public List<string> StatusOptions { get; }
        public bool IsClienteSelectable => _source == null;
        public bool IsEditMode => _source != null;
        public bool IsClienteReadOnly => _source != null;
        public string WindowTitle => IsEditMode ? "Editar Pagamento" : "Novo Pagamento";
        public string WindowSubtitle => IsEditMode
            ? "Atualize os dados da cobranca selecionada."
            : "Cadastre uma nova cobranca para o cliente.";

        public Guid ClienteId
        {
            get => _clienteId;
            set
            {
                _clienteId = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ClienteNomeSelecionado));
                OnPropertyChanged(nameof(CpfCnpjClienteSelecionado));
            }
        }

        public string ClienteNomeSelecionado => Clientes.FirstOrDefault(x => x.Id == ClienteId)?.Nome ?? string.Empty;

        public string CpfCnpjClienteSelecionado => Clientes.FirstOrDefault(x => x.Id == ClienteId)?.CpfCnpj ?? string.Empty;

        public string Descricao
        {
            get => _descricao;
            set { _descricao = value; OnPropertyChanged(); }
        }

        public string Tipo
        {
            get => _tipo;
            set { _tipo = value; OnPropertyChanged(); }
        }

        public string StatusFinanceiro
        {
            get => _statusFinanceiro;
            set { _statusFinanceiro = value; OnPropertyChanged(); }
        }

        public string Categoria
        {
            get => _categoria;
            set { _categoria = value; OnPropertyChanged(); }
        }

        public string Subcategoria
        {
            get => _subcategoria;
            set { _subcategoria = value; OnPropertyChanged(); }
        }

        public string? Observacoes
        {
            get => _observacoes;
            set { _observacoes = value; OnPropertyChanged(); }
        }

        public decimal Valor
        {
            get => _valor;
            set { _valor = value; OnPropertyChanged(); }
        }

        public DateTime DataVencimento
        {
            get => _dataVencimento;
            set { _dataVencimento = value; OnPropertyChanged(); }
        }

        public bool Pago
        {
            get => _pago;
            set
            {
                _pago = value;
                if (!value)
                {
                    DataPagamento = null;
                }
                else if (DataPagamento == null)
                {
                    DataPagamento = DateTime.Today;
                }
                OnPropertyChanged();
            }
        }

        public DateTime? DataPagamento
        {
            get => _dataPagamento;
            set { _dataPagamento = value; OnPropertyChanged(); }
        }

        public Pagamento ToPagamento()
        {
            var pagamento = _source != null
                ? new Pagamento
                {
                    Id = _source.Id,
                    CriadoEm = _source.CriadoEm
                }
                : new Pagamento();

            pagamento.ClienteId = ClienteId;
            pagamento.CpfCnpjCliente = Clientes.FirstOrDefault(x => x.Id == ClienteId)?.CpfCnpj;
            pagamento.Tipo = Tipo;
            pagamento.Status = StatusFinanceiro;
            pagamento.Categoria = string.IsNullOrWhiteSpace(Categoria) ? null : Categoria.Trim();
            pagamento.Subcategoria = string.IsNullOrWhiteSpace(Subcategoria) ? null : Subcategoria.Trim();
            pagamento.Observacoes = string.IsNullOrWhiteSpace(Observacoes) ? null : Observacoes.Trim();
            pagamento.Descricao = string.IsNullOrWhiteSpace(Descricao)
                ? (pagamento.Categoria ?? "Pagamento")
                : Descricao.Trim();
            pagamento.Valor = Valor;
            pagamento.ValorPrevisto = Valor;
            pagamento.DataVencimento = DataVencimento;
            pagamento.DataPrevista = DataVencimento;
            pagamento.Pago = Pago;
            pagamento.DataPagamento = Pago ? DataPagamento : null;
            pagamento.DataEfetiva = Pago ? DataPagamento : null;

            return pagamento;
        }
    }

    public class ClienteOption
    {
        public Guid Id { get; set; }
        public string Nome { get; set; } = string.Empty;
        public string? CpfCnpj { get; set; }

        public override string ToString()
        {
            return Nome;
        }
    }
}
