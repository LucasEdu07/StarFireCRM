using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using ExtintorCrm.App.Domain;
using ExtintorCrm.App.Infrastructure.Logging;

namespace ExtintorCrm.App.Presentation
{
    public partial class ClientesViewModel
    {
        private async Task LogAndToastErrorAsync(string context, string userPrefix, Exception ex)
        {
            AppLogger.Error(context, ex);
            await ShowToastAsync($"{userPrefix}: {ex.Message}", "Error");
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
                : "WhatsApp não disponível";

            var emailStatus = hasEmail
                ? $"E-mail: {email}"
                : "E-mail não disponível";

            return $"{phoneStatus}  •  {emailStatus}";
        }

        private static (string Subject, string Message) BuildCobrancaMessage(string clienteNome, Pagamento pagamento)
        {
            var culture = new CultureInfo("pt-BR");
            var vencimento = pagamento.DataVencimento.ToString("dd/MM/yyyy");
            var valor = pagamento.Valor.ToString("C2", culture);
            var descricao = string.IsNullOrWhiteSpace(pagamento.Descricao) ? "pagamento" : pagamento.Descricao.Trim();
            var dias = (pagamento.DataVencimento.Date - DateTime.Today).Days;

            var status = dias switch
            {
                < 0 => $"está vencido há {Math.Abs(dias)} dia(s)",
                0 => "vence hoje",
                _ => $"vence em {dias} dia(s)"
            };

            var subject = $"Cobrança - {clienteNome} - {vencimento}";
            var message =
                $"Olá, {clienteNome}! Tudo bem?{Environment.NewLine}{Environment.NewLine}" +
                $"Passando para lembrar que o pagamento \"{descricao}\" no valor de {valor} {status} (vencimento: {vencimento}).{Environment.NewLine}" +
                $"Podemos seguir com a regularização?{Environment.NewLine}{Environment.NewLine}" +
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
    }
}
