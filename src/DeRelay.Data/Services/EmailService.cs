using DeRelay.Core.Interfaces;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using MimeKit;

namespace DeRelay.Data.Services;

public class EmailService(IConfiguration iConfiguration): IEmailService
{
    readonly string _smtpServer = iConfiguration["SmtpSettings:Server"] ?? 
                                 throw new InvalidOperationException("Smtp Server Not Configured");
    readonly int _smtpPort = int.TryParse(iConfiguration["SmtpSettings:Port"], out var port) 
        ? port : throw new InvalidOperationException("Smtp port Not Configured");
    readonly string _smtpLogin = iConfiguration["SmtpSettings:Login"] ?? 
                                 throw new InvalidOperationException("Smtp Login Not Configured");
    readonly string _smtpKey = iConfiguration["SmtpSettings:Key"] ?? 
                                 throw new InvalidOperationException("Smtp Key Not Configured");
    
    public async Task SendEmailAsync(string toEmail, string subject, string message)
    {
        var mimeMessage = new MimeMessage();
        mimeMessage.From.Add(new MailboxAddress("DancingLineClone", "noreply@sunnygameai.site"));
        mimeMessage.To.Add(new MailboxAddress("", toEmail));
        mimeMessage.Subject = subject;
        mimeMessage.Body = new TextPart("plain") { Text = message };

        using var smtp = new SmtpClient();
        await smtp.ConnectAsync(_smtpServer, _smtpPort, SecureSocketOptions.StartTls);
        await smtp.AuthenticateAsync(_smtpLogin, _smtpKey);
        await smtp.SendAsync(mimeMessage);
        await smtp.DisconnectAsync(true);
    }
}