using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using AuthDemo.Models;

namespace AuthDemo.Services;

public sealed class PmsRazorpaySupport
{
    private readonly IConfiguration _config;
    private readonly IHttpClientFactory _factory;
    public PmsRazorpaySupport(IConfiguration config, IHttpClientFactory factory)
    { _config = config; _factory = factory; }

    private byte[] EncryptionKey()
    {
        var key = _config["PaymentGateway:CredentialEncryptionKey"];
        if (string.IsNullOrWhiteSpace(key)) throw new InvalidOperationException("PaymentGateway credential encryption key is not configured.");
        byte[] bytes;
        try { bytes = Convert.FromBase64String(key); }
        catch (FormatException) { throw new InvalidOperationException("Credential encryption key must be base64-encoded."); }
        if (bytes.Length != 32) throw new InvalidOperationException("Credential encryption key must decode to 32 bytes.");
        return bytes;
    }
    public string Encrypt(string secret)
    {
        var nonce = RandomNumberGenerator.GetBytes(12);
        var plaintext = Encoding.UTF8.GetBytes(secret);
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[16];
        using var aes = new AesGcm(EncryptionKey(), 16);
        aes.Encrypt(nonce, plaintext, ciphertext, tag);
        return Convert.ToBase64String(nonce.Concat(tag).Concat(ciphertext).ToArray());
    }
    public string Decrypt(string encrypted)
    {
        var bytes = Convert.FromBase64String(encrypted);
        if (bytes.Length < 28) throw new InvalidOperationException("Invalid encrypted gateway credentials.");
        var plaintext = new byte[bytes.Length - 28];
        using var aes = new AesGcm(EncryptionKey(), 16);
        aes.Decrypt(bytes.AsSpan(0,12), bytes.AsSpan(28), bytes.AsSpan(12,16), plaintext);
        return Encoding.UTF8.GetString(plaintext);
    }
    public static bool VerifyHmac(string payload, string suppliedHex, string secret)
    {
        if (string.IsNullOrWhiteSpace(suppliedHex) || suppliedHex.Length != 64) return false;
        byte[] actual;
        try { actual = Convert.FromHexString(suppliedHex); }
        catch (FormatException) { return false; }
        var expected = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(payload));
        return CryptographicOperations.FixedTimeEquals(expected, actual);
    }
    public HttpClient Client(BranchGatewaySettings merchant)
    {
        var client = _factory.CreateClient("PmsRazorpay");
        var password = Decrypt(merchant.EncryptedKeySecret);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{merchant.KeyId}:{password}")));
        return client;
    }
}
