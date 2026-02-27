using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using ExtintorCrm.App.Domain;

namespace ExtintorCrm.App.Presentation
{
    public class ClienteDetalhesViewModel : ViewModelBase
    {
        private readonly Cliente _source;
        private readonly List<Pagamento> _pagamentosOriginais = new();
        private string _nomeFantasia = string.Empty;
        private string? _cpf;
        private string? _telefone1;
        private string? _email;
        private string? _contato;
        private string? _razaoSocial;
        private string? _endereco;
        private string? _cidade;
        private string? _uf;
        private string? _tipo;
        private string? _numeroAlvara;
        private string? _statusAlvara;
        private string? _statusRecarga;
        private DateTime? _vencimentoExtintores;
        private DateTime? _vencimentoAlvara;
        private string? _observacoes;
        private bool _isAtivo;
        private bool _isEditMode;

        public ClienteDetalhesViewModel(Cliente cliente, IEnumerable<Pagamento>? pagamentos = null)
        {
            _source = cliente;
            _nomeFantasia = cliente.NomeFantasia;
            _cpf = cliente.CPF ?? cliente.Documento;
            _telefone1 = cliente.Telefone1 ?? cliente.Telefone;
            _email = cliente.Email;
            _contato = cliente.Contato;
            _razaoSocial = cliente.RazaoSocial;
            _endereco = cliente.Endereco;
            _cidade = cliente.Cidade;
            _uf = cliente.UF;
            _tipo = cliente.TipoServico;
            _numeroAlvara = cliente.NumeroAlvara;
            _statusAlvara = cliente.Status;
            _statusRecarga = cliente.StatusRecarga;
            _vencimentoExtintores = cliente.VencimentoExtintores ?? cliente.VencimentoServico;
            _vencimentoAlvara = cliente.VencimentoAlvara;
            _observacoes = cliente.Observacoes;
            _isAtivo = cliente.IsAtivo;

            RG = cliente.RG;
            Nascimento = cliente.Nascimento;
            Sexo = cliente.Sexo;
            Telefone2 = cliente.Telefone2;
            Telefone3 = cliente.Telefone3;
            Representante = cliente.Representante;
            Endereco = cliente.Endereco;
            Numero = cliente.Numero;
            Complemento = cliente.Complemento;
            Bairro = cliente.Bairro;
            CEP = cliente.CEP;
            CriadoEm = cliente.CriadoEm;

            var today = DateTime.Today;
            var pagamentosCliente = (pagamentos ?? Array.Empty<Pagamento>())
                .Select(ClonePagamento)
                .OrderBy(p => p.DataVencimento)
                .Select(p => new PagamentoPerfilItem
                {
                    Id = p.Id,
                    ClienteId = p.ClienteId,
                    CpfCnpjCliente = p.CpfCnpjCliente,
                    Descricao = p.Descricao,
                    DataVencimento = p.DataVencimento,
                    Valor = p.Valor,
                    Pago = p.Pago,
                    DataPagamento = p.DataPagamento
                })
                .ToList();

            foreach (var item in pagamentosCliente)
            {
                Pagamentos.Add(item);
                _pagamentosOriginais.Add(item.ToPagamento());
            }
        }

        public string NomeFantasia
        {
            get => _nomeFantasia;
            set
            {
                _nomeFantasia = value;
                OnPropertyChanged();
            }
        }

        public Guid ClienteId => _source.Id;

        public string? RG { get; }

        public string? CPF
        {
            get => _cpf;
            set
            {
                _cpf = value;
                OnPropertyChanged();
            }
        }

        public DateTime? Nascimento { get; }
        public string? Sexo { get; }

        public string? Telefone1
        {
            get => _telefone1;
            set
            {
                _telefone1 = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(TelefoneResumo));
            }
        }

        public string? Telefone2 { get; }
        public string? Telefone3 { get; }

        public string? Email
        {
            get => _email;
            set
            {
                _email = value;
                OnPropertyChanged();
            }
        }

        public string? Contato
        {
            get => _contato;
            set
            {
                _contato = value;
                OnPropertyChanged();
            }
        }

        public string? RazaoSocial
        {
            get => _razaoSocial;
            set
            {
                _razaoSocial = value;
                OnPropertyChanged();
            }
        }

        public string? Endereco
        {
            get => _endereco;
            set
            {
                _endereco = value;
                OnPropertyChanged();
            }
        }

        public string? Representante { get; }
        public string? Numero { get; }
        public string? Complemento { get; }
        public string? Bairro { get; }

        public string? Cidade
        {
            get => _cidade;
            set
            {
                _cidade = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CidadeResumo));
            }
        }

        public string? UF
        {
            get => _uf;
            set
            {
                _uf = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CidadeResumo));
            }
        }

        public string? CEP { get; }

        public string? Tipo
        {
            get => _tipo;
            set
            {
                _tipo = value;
                OnPropertyChanged();
            }
        }

        public string? NumeroAlvara
        {
            get => _numeroAlvara;
            set
            {
                _numeroAlvara = value;
                OnPropertyChanged();
            }
        }

        public string? StatusAlvara
        {
            get => _statusAlvara;
            set
            {
                _statusAlvara = value;
                OnPropertyChanged();
            }
        }

        public string? StatusRecarga
        {
            get => _statusRecarga;
            set
            {
                _statusRecarga = value;
                OnPropertyChanged();
            }
        }

        public DateTime? VencimentoExtintores
        {
            get => _vencimentoExtintores;
            set
            {
                _vencimentoExtintores = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ExtintorBadgeTexto));
                OnPropertyChanged(nameof(ExtintorBadgeNivel));
                OnPropertyChanged(nameof(ProximoVencimento));
            }
        }

        public DateTime? VencimentoAlvara
        {
            get => _vencimentoAlvara;
            set
            {
                _vencimentoAlvara = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(AlvaraBadgeTexto));
                OnPropertyChanged(nameof(AlvaraBadgeNivel));
                OnPropertyChanged(nameof(ProximoVencimento));
            }
        }

        public string? Observacoes
        {
            get => _observacoes;
            set
            {
                _observacoes = value;
                OnPropertyChanged();
            }
        }

        public bool IsAtivo
        {
            get => _isAtivo;
            set
            {
                _isAtivo = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(Status));
            }
        }

        public string Status => IsAtivo ? "Ativo" : "Inativo";

        public bool IsEditMode
        {
            get => _isEditMode;
            set
            {
                _isEditMode = value;
                OnPropertyChanged();
            }
        }

        public DateTime CriadoEm { get; }

        public string TelefoneResumo => string.IsNullOrWhiteSpace(Telefone1) ? "Não informado" : Telefone1;

        public string CidadeResumo => string.IsNullOrWhiteSpace(Cidade)
            ? "Não informada"
            : string.IsNullOrWhiteSpace(UF) ? Cidade : $"{Cidade}/{UF}";

        public string ProximoVencimento
        {
            get
            {
                var nextDueCandidates = new List<DateTime>();

                if (VencimentoExtintores.HasValue)
                {
                    nextDueCandidates.Add(VencimentoExtintores.Value.Date);
                }

                if (VencimentoAlvara.HasValue)
                {
                    nextDueCandidates.Add(VencimentoAlvara.Value.Date);
                }

                nextDueCandidates.AddRange(Pagamentos
                    .Where(p => !p.Pago)
                    .Select(p => p.DataVencimento.Date));

                return nextDueCandidates.Any()
                    ? nextDueCandidates.Min().ToString("dd/MM/yyyy")
                    : "Sem vencimentos";
            }
        }

        public string ExtintorBadgeTexto => BuildDueStatus(VencimentoExtintores, DateTime.Today).Texto;
        public string ExtintorBadgeNivel => BuildDueStatus(VencimentoExtintores, DateTime.Today).Nivel;
        public string AlvaraBadgeTexto => BuildDueStatus(VencimentoAlvara, DateTime.Today).Texto;
        public string AlvaraBadgeNivel => BuildDueStatus(VencimentoAlvara, DateTime.Today).Nivel;

        public ObservableCollection<PagamentoPerfilItem> Pagamentos { get; } = new();

        public void CancelEdit()
        {
            NomeFantasia = _source.NomeFantasia;
            CPF = _source.CPF ?? _source.Documento;
            Telefone1 = _source.Telefone1 ?? _source.Telefone;
            Email = _source.Email;
            Contato = _source.Contato;
            RazaoSocial = _source.RazaoSocial;
            Endereco = _source.Endereco;
            Cidade = _source.Cidade;
            UF = _source.UF;
            Tipo = _source.TipoServico;
            NumeroAlvara = _source.NumeroAlvara;
            StatusAlvara = _source.Status;
            StatusRecarga = _source.StatusRecarga;
            VencimentoExtintores = _source.VencimentoExtintores ?? _source.VencimentoServico;
            VencimentoAlvara = _source.VencimentoAlvara;
            Observacoes = _source.Observacoes;
            IsAtivo = _source.IsAtivo;
            Pagamentos.Clear();
            foreach (var pagamento in _pagamentosOriginais
                         .Select(p => new PagamentoPerfilItem
                         {
                             Id = p.Id,
                             ClienteId = p.ClienteId,
                             CpfCnpjCliente = p.CpfCnpjCliente,
                             Descricao = p.Descricao,
                             DataVencimento = p.DataVencimento,
                             Valor = p.Valor,
                             Pago = p.Pago,
                             DataPagamento = p.DataPagamento
                         }))
            {
                Pagamentos.Add(pagamento);
            }
            IsEditMode = false;
        }

        public Cliente BuildUpdatedCliente()
        {
            var cliente = CloneFromSource(_source);
            var normalizedCpf = DigitsOnly(CPF);
            cliente.NomeFantasia = NomeFantasia.Trim();
            cliente.CPF = normalizedCpf;
            cliente.Documento = normalizedCpf;
            cliente.Telefone1 = Telefone1;
            cliente.Telefone = Telefone1;
            cliente.Email = Email;
            cliente.Contato = Contato;
            cliente.RazaoSocial = RazaoSocial;
            cliente.Endereco = Endereco;
            cliente.Cidade = Cidade;
            cliente.UF = UF;
            cliente.TipoServico = Tipo;
            cliente.NumeroAlvara = NumeroAlvara;
            cliente.Status = StatusAlvara;
            cliente.StatusRecarga = StatusRecarga;
            cliente.VencimentoExtintores = VencimentoExtintores;
            cliente.VencimentoServico = VencimentoExtintores;
            cliente.VencimentoAlvara = VencimentoAlvara;
            cliente.Observacoes = Observacoes;
            cliente.IsAtivo = IsAtivo;
            return cliente;
        }

        private static string? DigitsOnly(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            var digits = new string(value.Where(char.IsDigit).ToArray());
            return string.IsNullOrWhiteSpace(digits) ? null : digits;
        }

        public List<Pagamento> BuildUpdatedPagamentos()
        {
            return Pagamentos
                .Where(x => x.Id != Guid.Empty && x.ClienteId == _source.Id)
                .Select(x => x.ToPagamento())
                .ToList();
        }

        public void AcceptCurrentStateAsSaved()
        {
            var savedCliente = BuildUpdatedCliente();
            _source.NomeFantasia = savedCliente.NomeFantasia;
            _source.Documento = savedCliente.Documento;
            _source.CPF = savedCliente.CPF;
            _source.Telefone = savedCliente.Telefone;
            _source.Telefone1 = savedCliente.Telefone1;
            _source.Email = savedCliente.Email;
            _source.Contato = savedCliente.Contato;
            _source.RazaoSocial = savedCliente.RazaoSocial;
            _source.Endereco = savedCliente.Endereco;
            _source.Cidade = savedCliente.Cidade;
            _source.UF = savedCliente.UF;
            _source.TipoServico = savedCliente.TipoServico;
            _source.NumeroAlvara = savedCliente.NumeroAlvara;
            _source.Status = savedCliente.Status;
            _source.StatusRecarga = savedCliente.StatusRecarga;
            _source.VencimentoServico = savedCliente.VencimentoServico;
            _source.VencimentoExtintores = savedCliente.VencimentoExtintores;
            _source.VencimentoAlvara = savedCliente.VencimentoAlvara;
            _source.Observacoes = savedCliente.Observacoes;
            _source.IsAtivo = savedCliente.IsAtivo;

            _pagamentosOriginais.Clear();
            foreach (var pagamento in BuildUpdatedPagamentos().Select(ClonePagamento))
            {
                _pagamentosOriginais.Add(pagamento);
            }
        }

        private static (string Texto, string Nivel) BuildDueStatus(DateTime? dueDate, DateTime today)
        {
            if (!dueDate.HasValue)
            {
                return ("Sem data", "Info");
            }

            var days = (dueDate.Value.Date - today).Days;
            if (days < 0)
            {
                return ($"Vencido há {Math.Abs(days)} dia(s)", "Critico");
            }

            if (days <= 30)
            {
                return ($"Vence em {days} dia(s)", "Vencendo");
            }

            return ("OK", "OK");
        }

        private static string ResolvePagamentoStatus(Pagamento pagamento, DateTime today)
        {
            if (pagamento.Pago)
            {
                return "Pago";
            }

            return pagamento.DataVencimento.Date < today ? "Vencido" : "Em aberto";
        }

        private static string ResolvePagamentoStatusNivel(Pagamento pagamento, DateTime today)
        {
            if (pagamento.Pago)
            {
                return "OK";
            }

            return pagamento.DataVencimento.Date < today ? "Critico" : "Vencendo";
        }

        private static Cliente CloneFromSource(Cliente source)
        {
            return new Cliente
            {
                Id = source.Id,
                NomeFantasia = source.NomeFantasia,
                RazaoSocial = source.RazaoSocial,
                Documento = source.Documento,
                RG = source.RG,
                CPF = source.CPF,
                Nascimento = source.Nascimento,
                Sexo = source.Sexo,
                Categoria = source.Categoria,
                Contato = source.Contato,
                TipoContato = source.TipoContato,
                Telefone = source.Telefone,
                Telefone1 = source.Telefone1,
                Telefone2 = source.Telefone2,
                Telefone3 = source.Telefone3,
                Email = source.Email,
                Endereco = source.Endereco,
                Numero = source.Numero,
                Complemento = source.Complemento,
                Bairro = source.Bairro,
                Cidade = source.Cidade,
                UF = source.UF,
                CEP = source.CEP,
                Observacoes = source.Observacoes,
                TipoServico = source.TipoServico,
                StatusRecarga = source.StatusRecarga,
                VencimentoServico = source.VencimentoServico,
                VencimentoExtintores = source.VencimentoExtintores,
                NumeroAlvara = source.NumeroAlvara,
                VencimentoAlvara = source.VencimentoAlvara,
                Representante = source.Representante,
                IsAtivo = source.IsAtivo,
                Status = source.Status,
                AvisoAtivo = source.AvisoAtivo,
                CriadoEm = source.CriadoEm,
                AtualizadoEm = source.AtualizadoEm
            };
        }

        private static Pagamento ClonePagamento(Pagamento source)
        {
            return new Pagamento
            {
                Id = source.Id,
                ClienteId = source.ClienteId,
                CpfCnpjCliente = source.CpfCnpjCliente,
                Tipo = source.Tipo,
                Status = source.Status,
                DataPrevista = source.DataPrevista,
                DataEfetiva = source.DataEfetiva,
                VencimentoFatura = source.VencimentoFatura,
                ValorPrevisto = source.ValorPrevisto,
                ValorEfetivo = source.ValorEfetivo,
                Categoria = source.Categoria,
                Subcategoria = source.Subcategoria,
                Conta = source.Conta,
                ContaTransferencia = source.ContaTransferencia,
                Centro = source.Centro,
                Contato = source.Contato,
                RazaoSocial = source.RazaoSocial,
                Forma = source.Forma,
                Projeto = source.Projeto,
                NumeroDocumento = source.NumeroDocumento,
                Observacoes = source.Observacoes,
                Descricao = source.Descricao,
                Valor = source.Valor,
                DataVencimento = source.DataVencimento,
                Pago = source.Pago,
                DataPagamento = source.DataPagamento,
                CriadoEm = source.CriadoEm,
                AtualizadoEm = source.AtualizadoEm,
                ClienteNome = source.ClienteNome
            };
        }
    }

    public class PagamentoPerfilItem : ViewModelBase
    {
        private DateTime _dataVencimento;
        private decimal _valor;
        private bool _pago;
        private DateTime? _dataPagamento;

        public Guid Id { get; set; }
        public Guid ClienteId { get; set; }
        public string? CpfCnpjCliente { get; set; }
        public string Descricao { get; set; } = string.Empty;

        public DateTime DataVencimento
        {
            get => _dataVencimento;
            set
            {
                _dataVencimento = value;
                RefreshStatus();
                OnPropertyChanged();
            }
        }

        public decimal Valor
        {
            get => _valor;
            set
            {
                _valor = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ValorFormatado));
            }
        }

        public bool Pago
        {
            get => _pago;
            set
            {
                _pago = value;
                if (_pago && !DataPagamento.HasValue)
                {
                    _dataPagamento = DateTime.Today;
                    OnPropertyChanged(nameof(DataPagamento));
                }
                if (!_pago)
                {
                    _dataPagamento = null;
                    OnPropertyChanged(nameof(DataPagamento));
                }
                RefreshStatus();
                OnPropertyChanged();
            }
        }

        public DateTime? DataPagamento
        {
            get => _dataPagamento;
            set
            {
                _dataPagamento = value;
                OnPropertyChanged();
            }
        }

        public string StatusTexto { get; private set; } = "Em aberto";
        public string StatusNivel { get; private set; } = "OK";

        public bool IsVencido => string.Equals(StatusNivel, "Critico", StringComparison.OrdinalIgnoreCase);

        public string ValorFormatado => Valor.ToString("C2", new CultureInfo("pt-BR"));

        public Pagamento ToPagamento()
        {
            return new Pagamento
            {
                Id = Id,
                ClienteId = ClienteId,
                CpfCnpjCliente = CpfCnpjCliente,
                Descricao = Descricao,
                Valor = Valor,
                DataVencimento = DataVencimento,
                Pago = Pago,
                DataPagamento = Pago ? DataPagamento : null
            };
        }

        private void RefreshStatus()
        {
            if (Pago)
            {
                StatusTexto = "Pago";
                StatusNivel = "OK";
            }
            else if (DataVencimento.Date < DateTime.Today)
            {
                StatusTexto = "Vencido";
                StatusNivel = "Critico";
            }
            else
            {
                StatusTexto = "Em aberto";
                StatusNivel = "Vencendo";
            }

            OnPropertyChanged(nameof(StatusTexto));
            OnPropertyChanged(nameof(StatusNivel));
            OnPropertyChanged(nameof(IsVencido));
        }
    }
}
