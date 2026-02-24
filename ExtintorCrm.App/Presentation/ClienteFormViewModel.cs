using System;
using System.Linq;
using ExtintorCrm.App.Domain;

namespace ExtintorCrm.App.Presentation
{
    public class ClienteFormViewModel : ViewModelBase
    {
        private readonly Cliente? _source;
        private string _nomeFantasia = string.Empty;
        private string? _cpfCnpj;
        private string? _contato;
        private string? _telefone1;
        private string? _email;
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
        private bool _isAtivo = true;

        public ClienteFormViewModel()
        {
        }

        public ClienteFormViewModel(Cliente cliente)
        {
            _source = cliente;
            _nomeFantasia = cliente.NomeFantasia;
            _cpfCnpj = cliente.CPF ?? cliente.Documento;
            _contato = cliente.Contato;
            _telefone1 = cliente.Telefone1 ?? cliente.Telefone;
            _email = cliente.Email;
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
        }

        public string NomeFantasia
        {
            get => _nomeFantasia;
            set { _nomeFantasia = value; OnPropertyChanged(); }
        }

        public string? CpfCnpj
        {
            get => _cpfCnpj;
            set { _cpfCnpj = value; OnPropertyChanged(); }
        }

        public string? Contato
        {
            get => _contato;
            set { _contato = value; OnPropertyChanged(); }
        }

        public string? Telefone1
        {
            get => _telefone1;
            set { _telefone1 = value; OnPropertyChanged(); }
        }

        public string? Email
        {
            get => _email;
            set { _email = value; OnPropertyChanged(); }
        }

        public string? RazaoSocial
        {
            get => _razaoSocial;
            set { _razaoSocial = value; OnPropertyChanged(); }
        }

        public string? Endereco
        {
            get => _endereco;
            set { _endereco = value; OnPropertyChanged(); }
        }

        public string? Cidade
        {
            get => _cidade;
            set { _cidade = value; OnPropertyChanged(); }
        }

        public string? UF
        {
            get => _uf;
            set { _uf = value; OnPropertyChanged(); }
        }

        public string? Tipo
        {
            get => _tipo;
            set { _tipo = value; OnPropertyChanged(); }
        }

        public string? NumeroAlvara
        {
            get => _numeroAlvara;
            set { _numeroAlvara = value; OnPropertyChanged(); }
        }

        public string? StatusAlvara
        {
            get => _statusAlvara;
            set { _statusAlvara = value; OnPropertyChanged(); }
        }

        public string? StatusRecarga
        {
            get => _statusRecarga;
            set { _statusRecarga = value; OnPropertyChanged(); }
        }

        public DateTime? VencimentoExtintores
        {
            get => _vencimentoExtintores;
            set { _vencimentoExtintores = value; OnPropertyChanged(); }
        }

        public DateTime? VencimentoAlvara
        {
            get => _vencimentoAlvara;
            set { _vencimentoAlvara = value; OnPropertyChanged(); }
        }

        public string? Observacoes
        {
            get => _observacoes;
            set { _observacoes = value; OnPropertyChanged(); }
        }

        public bool IsAtivo
        {
            get => _isAtivo;
            set
            {
                _isAtivo = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(StatusLabel));
            }
        }

        public string StatusLabel => IsAtivo ? "Ativo" : "Inativo";

        public Cliente ToCliente()
        {
            var cliente = _source != null
                ? CloneFromSource(_source)
                : new Cliente();
            var normalizedCpf = DigitsOnly(CpfCnpj);

            cliente.NomeFantasia = NomeFantasia.Trim();
            cliente.CPF = normalizedCpf;
            cliente.Documento = normalizedCpf;
            cliente.Contato = Contato;
            cliente.Telefone1 = Telefone1;
            cliente.Telefone = Telefone1;
            cliente.Email = Email;
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
    }
}
