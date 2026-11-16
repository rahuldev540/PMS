namespace AuthDemo.Services.Interfaces;

public interface IEmailService
{
    Task SendPasswordResetOtpAsync(
        string recipientEmail,
        string recipientName,
        string otp);

    Task SendPharmacyAdminWelcomeEmailAsync(
        string recipientEmail,
        string recipientName,
        string temporaryPassword,
        string loginUrl);
    Task SendPharmacistWelcomeEmailAsync(
        string recipientEmail,
        string recipientName,
        string temporaryPassword,
        string loginUrl);
}