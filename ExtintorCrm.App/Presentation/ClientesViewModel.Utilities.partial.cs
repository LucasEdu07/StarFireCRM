using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using ExtintorCrm.App.Domain;
using ExtintorCrm.App.Infrastructure.Logging;
using ExtintorCrm.App.UseCases.Common;

namespace ExtintorCrm.App.Presentation
{
    public partial class ClientesViewModel
    {
        private async Task RunTrackedOperationAsync(string statusMessage, Func<Task> operation)
        {
            if (operation == null)
            {
                return;
            }

            IsOperationInProgress = true;
            OperationStatusMessage = statusMessage;
            try
            {
                await operation();
            }
            finally
            {
                IsOperationInProgress = false;
                OperationStatusMessage = string.Empty;
            }
        }

        private async Task LogAndToastErrorAsync(string context, string userPrefix, Exception ex)
        {
            AppLogger.Error(context, ex);
            await ShowToastAsync($"{userPrefix}: {ex.Message}", "Error");
        }

        private async Task ShowOperationResultAsync(OperationResult result, bool showDialogOnFailure = true)
        {
            var toastKind = ResolveToastKind(result);
            var toastMessage = string.IsNullOrWhiteSpace(result.NextStep)
                ? result.Message
                : $"{result.Message} Proximo passo: {result.NextStep}";

            await ShowToastAsync(toastMessage, toastKind);

            if (!result.IsSuccess && showDialogOnFailure)
            {
                var body = result.Message;
                if (result.Details.Count > 0)
                {
                    body = $"{body}{Environment.NewLine}{Environment.NewLine}{string.Join(Environment.NewLine, result.Details)}";
                }

                if (!string.IsNullOrWhiteSpace(result.NextStep))
                {
                    body = $"{body}{Environment.NewLine}{Environment.NewLine}Proximo passo: {result.NextStep}";
                }

                DialogService.Error(result.Title, body, Application.Current?.MainWindow);
            }
        }

        private static string ResolveToastKind(OperationResult result)
        {
            if (!result.IsSuccess)
            {
                return "Error";
            }

            return result.Code.Contains("WARN", StringComparison.OrdinalIgnoreCase)
                ? "Info"
                : "Success";
        }

        private static string FirstNotEmpty(params string?[] values)
        {
            foreach (var value in values)
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value.Trim();
                }
            }

            return string.Empty;
        }

        private static string? NormalizeDigits(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            var digits = new string(value.Where(char.IsDigit).ToArray());
            return string.IsNullOrWhiteSpace(digits) ? null : digits;
        }

        private static string NormalizePhoneForWhatsApp(string? rawPhone)
        {
            if (string.IsNullOrWhiteSpace(rawPhone))
            {
                return string.Empty;
            }

            var digits = new string(rawPhone.Where(char.IsDigit).ToArray());
            if (string.IsNullOrWhiteSpace(digits))
            {
                return string.Empty;
            }

            if (digits.StartsWith("55"))
            {
                return digits;
            }

            return digits.Length is 10 or 11
                ? $"55{digits}"
                : digits;
        }

        private static bool IsValidWhatsAppPhone(string? phone)
        {
            if (string.IsNullOrWhiteSpace(phone))
            {
                return false;
            }

            var digits = new string(phone.Where(char.IsDigit).ToArray());
            return digits.StartsWith("55") && digits.Length is 12 or 13;
        }

        private static bool IsValidEmail(string? email)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                return false;
            }

            try
            {
                _ = new System.Net.Mail.MailAddress(email.Trim());
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static string BuildContatoInfo(string telefoneRaw, string email, bool hasWhatsApp, bool hasEmail)
        {
            var phoneStatus = hasWhatsApp
                ? $"WhatsApp: {telefoneRaw}"
                : "WhatsApp nao disponivel";

            var emailStatus = hasEmail
                ? $"E-mail: {email}"
                : "E-mail nao disponivel";

            return $"{phoneStatus}  •  {emailStatus}";
        }

        private static string ResolveCobrancaEtapa(Pagamento pagamento, DateTime? referenceDate = null)
        {
            var today = (referenceDate ?? DateTime.Today).Date;
            var dias = (pagamento.DataVencimento.Date - today).Days;

            if (dias > 0)
            {
                return "Lembrete preventivo";
            }

            if (dias == 0)
            {
                return "Vencimento hoje";
            }

            if (dias >= -7)
            {
                return "Atraso leve";
            }

            return "Atraso critico";
        }

        private static string ResolvePrazoResumo(Pagamento pagamento, DateTime? referenceDate = null)
        {
            var today = (referenceDate ?? DateTime.Today).Date;
            var dias = (pagamento.DataVencimento.Date - today).Days;

            return dias switch
            {
                > 0 => $"Vence em {dias} dia(s)",
                0 => "Vence hoje",
                _ => $"Vencido ha {Math.Abs(dias)} dia(s)"
            };
        }

        private static IReadOnlyList<string> BuildCobrancaEtapaOptions(Pagamento pagamento)
        {
            return new[]
            {
                ResolveCobrancaEtapa(pagamento),
                "Lembrete preventivo",
                "Vencimento hoje",
                "Atraso leve",
                "Atraso critico",
                "Negociacao"
            }
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        }

        private static IReadOnlyList<string> BuildCobrancaToneOptions()
        {
            return new[] { "Cordial", "Profissional", "Urgente" };
        }

        private static IReadOnlyList<string> ExtractCobrancaHistory(string? observacoes, int maxItems = 8)
        {
            if (string.IsNullOrWhiteSpace(observacoes))
            {
                return Array.Empty<string>();
            }

            var entries = observacoes
                .Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)
                .Select(line => line.Trim())
                .Where(line => line.StartsWith("[COBRANCA ", StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (entries.Count == 0)
            {
                return Array.Empty<string>();
            }

            entries.Reverse();
            return entries.Take(Math.Max(1, maxItems)).ToList();
        }

        private static string AppendCobrancaHistory(string? observacoes, string historicoEntry)
        {
            var existing = string.IsNullOrWhiteSpace(observacoes)
                ? string.Empty
                : observacoes.Trim();

            return string.IsNullOrWhiteSpace(existing)
                ? historicoEntry.Trim()
                : $"{existing}{Environment.NewLine}{historicoEntry.Trim()}";
        }

        private static string SummarizeMessageForHistory(string? mensagem, int maxLength = 120)
        {
            if (string.IsNullOrWhiteSpace(mensagem))
            {
                return string.Empty;
            }

            var compact = mensagem
                .Replace("\r", " ", StringComparison.Ordinal)
                .Replace("\n", " ", StringComparison.Ordinal)
                .Trim();

            if (compact.Length <= maxLength)
            {
                return compact;
            }

            return $"{compact[..maxLength]}...";
        }

        private static string BuildCobrancaHistoryEntry(string canal, string etapa, string tom, string mensagem)
        {
            var timestamp = DateTime.Now.ToString("dd/MM/yyyy HH:mm");
            var resumo = SummarizeMessageForHistory(mensagem, 100);
            return $"[COBRANCA {timestamp}] Canal={canal} | Etapa={etapa} | Tom={tom} | Msg=\"{resumo}\"";
        }

        private static Cliente CloneClienteForUndo(Cliente source)
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

        private static Pagamento ClonePagamentoForUndo(Pagamento source)
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

        private static (string Subject, string Message) BuildCobrancaMessage(
            string clienteNome,
            Pagamento pagamento,
            string? etapa = null,
            string? tom = null)
        {
            var culture = new CultureInfo("pt-BR");
            var vencimento = pagamento.DataVencimento.ToString("dd/MM/yyyy");
            var valor = pagamento.Valor.ToString("C2", culture);
            var descricao = string.IsNullOrWhiteSpace(pagamento.Descricao) ? "pagamento" : pagamento.Descricao.Trim();
            var etapaResolvida = string.IsNullOrWhiteSpace(etapa) ? ResolveCobrancaEtapa(pagamento) : etapa.Trim();
            var tomResolvido = string.IsNullOrWhiteSpace(tom) ? "Profissional" : tom.Trim();
            var prazoResumo = ResolvePrazoResumo(pagamento);

            var abertura = tomResolvido switch
            {
                "Cordial" => $"Ola, {clienteNome}! Tudo bem?",
                "Urgente" => $"Ola, {clienteNome}. Precisamos tratar este pagamento com prioridade.",
                _ => $"Ola, {clienteNome}."
            };

            var orientacao = etapaResolvida switch
            {
                "Lembrete preventivo" =>
                    "Este e um lembrete preventivo para manter o financeiro em dia.",
                "Vencimento hoje" =>
                    "O vencimento e hoje e recomendamos a regularizacao ainda neste periodo.",
                "Atraso leve" =>
                    "Identificamos atraso recente e queremos apoiar a regularizacao sem juros adicionais.",
                "Atraso critico" =>
                    "Identificamos atraso relevante e precisamos alinhar um plano imediato de regularizacao.",
                "Negociacao" =>
                    "Podemos construir uma proposta de negociacao para facilitar a regularizacao.",
                _ =>
                    "Seguimos disponiveis para apoiar a regularizacao."
            };

            var fechamento = etapaResolvida == "Negociacao"
                ? "Se preferir, responda com a melhor data para fecharmos a negociacao."
                : "Pode nos confirmar o melhor canal e horario para concluir esta regularizacao?";

            var subject = $"Cobranca - {clienteNome} - {vencimento}";
            var message =
                $"{abertura}{Environment.NewLine}{Environment.NewLine}" +
                $"Pagamento: \"{descricao}\"{Environment.NewLine}" +
                $"Valor: {valor}{Environment.NewLine}" +
                $"Vencimento: {vencimento} ({prazoResumo}){Environment.NewLine}" +
                $"Etapa: {etapaResolvida}{Environment.NewLine}" +
                $"{orientacao}{Environment.NewLine}{Environment.NewLine}" +
                $"{fechamento}{Environment.NewLine}{Environment.NewLine}" +
                "_Equipe Star Fire_";

            return (subject, message);
        }

        private static string BuildMailToUri(string email, string subject, string body)
        {
            return $"mailto:{email}?subject={Uri.EscapeDataString(subject)}&body={Uri.EscapeDataString(body)}";
        }

        private static void OpenUri(string uri)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = uri,
                UseShellExecute = true
            });
        }

        private static string ResolveAppVersion()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var infoVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            var cleanInfo = string.IsNullOrWhiteSpace(infoVersion)
                ? null
                : infoVersion.Split('+')[0];

            if (!string.IsNullOrWhiteSpace(cleanInfo))
            {
                return cleanInfo;
            }

            var version = assembly.GetName().Version;
            return version is null
                ? "1.0.0"
                : $"{version.Major}.{version.Minor}.{version.Build}";
        }

        private static string ResolveBuildDateTimeDisplay()
        {
            try
            {
                var executablePath = Process.GetCurrentProcess().MainModule?.FileName;
                if (!string.IsNullOrWhiteSpace(executablePath) && File.Exists(executablePath))
                {
                    return File.GetLastWriteTime(executablePath).ToString("dd/MM/yyyy HH:mm");
                }
            }
            catch
            {
                // fallback below
            }

            return DateTime.Now.ToString("dd/MM/yyyy HH:mm");
        }

        private static IReadOnlyList<ReleaseNoteVersion> BuildReleaseNotesHistory()
        {
            return new List<ReleaseNoteVersion>
            {
                new(
                    "1.0.9",
                    "03/03/2026",
                    new[]
                    {
                        "Estados vazios de Clientes e Pagamentos agora possuem ação direta para limpar busca e filtros.",
                        "Barras de ação ficaram mais adaptativas em resoluções menores, com menos ruído visual no modo compacto.",
                        "Ajustes de UX para manter navegação fluida e consistente no fluxo MVP."
                    }),
                new(
                    "1.0.8",
                    "02/03/2026",
                    new[]
                    {
                        "Toasts com ações rápidas para abrir cadastro, anexos e exportações concluídas.",
                        "Indicador global de progresso para operações longas (importação, exportação e backups).",
                        "Modo compacto inicial com filtros colapsáveis e base de design tokens para evolução visual."
                    }),
                new(
                    "1.0.7",
                    "02/03/2026",
                    new[]
                    {
                        "Janelas com cantos verdadeiramente arredondados de forma consistente.",
                        "Estados de carregamento, vazio e erro refinados nas grades principais.",
                        "Validacao inline aprimorada e ajustes visuais para clientes inativos no light/dark mode."
                    }),
                new(
                    "1.0.6",
                    "27/02/2026",
                    new[]
                    {
                        "Painel de historico de releases implementado no selo de versao ao lado do sino.",
                        "Secao Sobre agora consome automaticamente as notas da versao atual.",
                        "Padronizacao das comunicacoes de release para facilitar suporte e operacao."
                    }),
                new(
                    "1.0.5",
                    "26/02/2026",
                    new[]
                    {
                        "Editor de pagamento com layout premium alinhado ao Perfil do Cliente.",
                        "Cobranca inteligente com estrategia por etapa e tom, com registro de interacoes.",
                        "Historico recente de cobrancas refinado para leitura e acompanhamento.",
                        "Melhorias gerais de UX, anexos e fluxo operacional de pagamentos."
                    }),
                new(
                    "1.0.4",
                    "25/02/2026",
                    new[]
                    {
                        "Dropdowns modernizados globalmente, com consistencia entre light e dark mode.",
                        "Rocker switches aplicados em todo o sistema com comportamento consolidado.",
                        "Reducao de micro-piscada na primeira renderizacao dos controles."
                    })
            };
        }
    }
}
