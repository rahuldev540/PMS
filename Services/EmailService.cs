using System.Net;
using AuthDemo.Services.Interfaces;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace AuthDemo.Services;

public sealed class EmailService : IEmailService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IConfiguration configuration, ILogger<EmailService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task SendPasswordResetOtpAsync(string recipientEmail, string recipientName, string otp)
    {
        if (string.IsNullOrWhiteSpace(otp))
            throw new ArgumentException("OTP is required.", nameof(otp));

        var message = new MimeMessage
        {
            Subject = "Pharmacy password reset OTP",
            Body = new TextPart("plain")
            {
                Text = $"Your password reset OTP is {otp}. It expires in 10 minutes. Do not share it with anyone."
            }
        };

        await SendEmailAsync(recipientEmail, recipientName, message);
    }

    public async Task SendPharmacyAdminWelcomeEmailAsync(
        string recipientEmail, string recipientName, string temporaryPassword, string loginUrl)
    {
        ValidateWelcomeEmailArguments(recipientEmail, recipientName, temporaryPassword, loginUrl);

        var safeName = WebUtility.HtmlEncode(recipientName);
        var safeEmail = WebUtility.HtmlEncode(recipientEmail);
        var safePassword = WebUtility.HtmlEncode(temporaryPassword);
        var safeLoginUrl = WebUtility.HtmlEncode(loginUrl);

        var message = new MimeMessage
        {
            Subject = "Your Pharmacy Admin Account",
            Body = new TextPart("html")
            {
                Text = $$"""
                    <!DOCTYPE html>
                    <html lang="en">
                    <head>
                        <meta charset="utf-8" />
                        <meta name="viewport" content="width=device-width, initial-scale=1" />
                    </head>
                    <body style="margin:0;padding:24px;background-color:#f3f4f6;font-family:Arial,sans-serif;color:#111827;">
                        <div style="max-width:600px;margin:0 auto;padding:32px;background-color:#ffffff;border-radius:10px;border:1px solid #e5e7eb;">
                            <h2 style="margin-top:0;color:#1d4ed8;">Welcome to Pharmacy Management</h2>
                            <p>Hello <strong>{{safeName}}</strong>,</p>
                            <p>Your Pharmacy Admin account has been created by the Pharmacy Super Admin.</p>
                            <p>Use the button below to open the Pharmacy login page.</p>
                            <p style="margin:24px 0;">
                                <a href="{{safeLoginUrl}}" style="display:inline-block;padding:12px 20px;background-color:#2563eb;color:#ffffff;text-decoration:none;border-radius:6px;">Open Pharmacy Login</a>
                            </p>
                            <h3>Login details</h3>
                            <p><strong>Email:</strong> {{safeEmail}}</p>
                            <p><strong>Temporary password:</strong> {{safePassword}}</p>
                            <p style="color:#b45309;"><strong>Important:</strong> You must change this temporary password after your first login.</p>
                            <p>Do not share this password with anyone.</p>
                            <p style="margin-top:32px;">Regards,<br />Pharmacy Management System</p>
                        </div>
                    </body>
                    </html>
                    """
            }
        };

        await SendEmailAsync(recipientEmail, recipientName, message);
    }

    public async Task SendPharmacistWelcomeEmailAsync(
        string recipientEmail, string recipientName, string temporaryPassword, string loginUrl)
    {
        ValidateWelcomeEmailArguments(recipientEmail, recipientName, temporaryPassword, loginUrl);

        var safeName = WebUtility.HtmlEncode(recipientName);
        var safeEmail = WebUtility.HtmlEncode(recipientEmail);
        var safePassword = WebUtility.HtmlEncode(temporaryPassword);
        var safeLoginUrl = WebUtility.HtmlEncode(loginUrl);

        var message = new MimeMessage
        {
            Subject = "Your Pharmacist Account",
            Body = new TextPart("html")
            {
                Text = $$"""
                    <!DOCTYPE html>
                    <html lang="en">
                    <head>
                        <meta charset="utf-8" />
                        <meta name="viewport" content="width=device-width, initial-scale=1" />
                    </head>
                    <body style="margin:0;padding:24px;background-color:#f3f4f6;font-family:Arial,sans-serif;color:#111827;">
                        <div style="max-width:600px;margin:0 auto;padding:32px;background-color:#ffffff;border-radius:10px;border:1px solid #e5e7eb;">
                            <h2 style="margin-top:0;color:#047857;">Welcome to Pharmacy Management</h2>
                            <p>Hello <strong>{{safeName}}</strong>,</p>
                            <p>Your Pharmacist account has been created by the Pharmacy Admin.</p>
                            <p>Use the button below to open the Pharmacist login page.</p>
                            <p style="margin:24px 0;">
                                <a href="{{safeLoginUrl}}" style="display:inline-block;padding:12px 20px;background-color:#059669;color:#ffffff;text-decoration:none;border-radius:6px;">Open Pharmacist Login</a>
                            </p>
                            <h3>Login details</h3>
                            <p><strong>Email:</strong> {{safeEmail}}</p>
                            <p><strong>Temporary password:</strong> {{safePassword}}</p>
                            <p style="color:#b45309;"><strong>Important:</strong> You must change this temporary password after your first login.</p>
                            <p>Do not share this password with anyone.</p>
                            <p style="margin-top:32px;">Regards,<br />Pharmacy Management System</p>
                        </div>
                    </body>
                    </html>
                    """
            }
        };

        await SendEmailAsync(recipientEmail, recipientName, message);
    }

    private async Task SendEmailAsync(string recipientEmail, string recipientName, MimeMessage message)
    {
        if (string.IsNullOrWhiteSpace(recipientEmail))
            throw new ArgumentException("Recipient email is required.", nameof(recipientEmail));

        if (string.IsNullOrWhiteSpace(recipientName))
            recipientName = recipientEmail;

        var settings = GetEmailSettings();
        Console.WriteLine("SMTP SENDER: " + settings.Sender);
        Console.WriteLine("SMTP HOST: " + settings.Host);
        Console.WriteLine("SMTP PORT: " + settings.Port);
        Console.WriteLine("APP PASSWORD CONFIGURED: " +
            !string.IsNullOrWhiteSpace(settings.Password));

        message.From.Add(new MailboxAddress("Pharmacy Management System", settings.Sender));
        message.To.Add(new MailboxAddress(recipientName.Trim(), recipientEmail.Trim()));

        using var client = new SmtpClient();
        var stage = "connect";
        try
        {
            _logger.LogInformation("SMTP STEP 1: Connecting to {Host}:{Port} using STARTTLS", settings.Host, settings.Port);
            await client.ConnectAsync(settings.Host, settings.Port, SecureSocketOptions.StartTls);

            stage = "authenticate";
            _logger.LogInformation("SMTP STEP 2: Connected. Authenticating sender {Sender}", settings.Sender);
            await client.AuthenticateAsync(settings.Sender, settings.Password);

            stage = "send";
            _logger.LogInformation("SMTP STEP 3: Authentication successful. Sending message.");
            await client.SendAsync(message);

            stage = "disconnect";
            _logger.LogInformation("SMTP STEP 4: Message accepted by SMTP server. Subject: {Subject}", message.Subject);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SMTP failed at stage {Stage}. Exception type: {ExceptionType}. Subject: {Subject}", stage, ex.GetType().Name, message.Subject);
            throw;
        }
        finally
        {
            if (client.IsConnected)
            {
                try
                {
                    await client.DisconnectAsync(true);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "SMTP disconnect failed after stage {Stage}", stage);
                }
            }
        }
    }

    private EmailConfiguration GetEmailSettings()
    {
        var section = _configuration.GetSection("EmailSettings");
        var sender = section["Email"]?.Trim();
        var password = section["Password"]?.Replace(" ", "").Trim();
        var host = section["Host"]?.Trim();
        var port = section.GetValue<int?>("Port") ?? 587;

        if (string.IsNullOrWhiteSpace(sender))
            throw new InvalidOperationException("EmailSettings:Email is missing.");
        if (string.IsNullOrWhiteSpace(password))
            throw new InvalidOperationException("EmailSettings:Password is missing.");
        if (string.IsNullOrWhiteSpace(host))
            throw new InvalidOperationException("EmailSettings:Host is missing.");
        if (port is < 1 or > 65535)
            throw new InvalidOperationException("EmailSettings:Port is invalid.");

        return new EmailConfiguration(sender, password, host, port);
    }

    private static void ValidateWelcomeEmailArguments(
        string recipientEmail, string recipientName, string temporaryPassword, string loginUrl)
    {
        if (string.IsNullOrWhiteSpace(recipientEmail))
            throw new ArgumentException("Recipient email is required.", nameof(recipientEmail));
        if (string.IsNullOrWhiteSpace(recipientName))
            throw new ArgumentException("Recipient name is required.", nameof(recipientName));
        if (string.IsNullOrWhiteSpace(temporaryPassword))
            throw new ArgumentException("Temporary password is required.", nameof(temporaryPassword));
        if (string.IsNullOrWhiteSpace(loginUrl))
            throw new ArgumentException("Login URL is required.", nameof(loginUrl));
    }

    private sealed record EmailConfiguration(string Sender, string Password, string Host, int Port);
}
