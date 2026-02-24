using System;
using System.Threading.Tasks;

namespace ExtintorCrm.App.Presentation
{
    public partial class ClientesViewModel
    {
        private const string SupportWhatsAppPhone = "12981336145";
        private const string SupportEmailAddress = "lucas_souzasjc@hotmail.com";

        private async Task ContactSupportWhatsAppAsync()
        {
            try
            {
                var phone = NormalizePhoneForWhatsApp(SupportWhatsAppPhone);
                var message = BuildSupportMessage();
                OpenUri($"https://wa.me/{phone}?text={Uri.EscapeDataString(message)}");
                await ShowToastAsync("WhatsApp de suporte aberto com mensagem pronta.", "Info");
            }
            catch (Exception ex)
            {
                await LogAndToastErrorAsync("Support.WhatsApp", "Erro ao abrir WhatsApp de suporte", ex);
            }
        }

        private async Task ContactSupportEmailAsync()
        {
            try
            {
                var subject = "Solicitação de suporte";
                var body = BuildSupportMessage();
                OpenUri(BuildMailToUri(SupportEmailAddress, subject, body));
                await ShowToastAsync("E-mail de suporte aberto com mensagem pronta.", "Info");
            }
            catch (Exception ex)
            {
                await LogAndToastErrorAsync("Support.Email", "Erro ao abrir e-mail de suporte", ex);
            }
        }

        private string BuildSupportMessage()
        {
            return
                "Olá!" + Environment.NewLine +
                "Preciso de suporte." + Environment.NewLine +
                Environment.NewLine +
                $"Versão: {AppVersionDisplay}" + Environment.NewLine +
                $"Build: {BuildDateTimeDisplay}";
        }
    }
}
