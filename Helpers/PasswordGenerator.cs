using System.Security.Cryptography;

namespace AuthDemo.Helpers;

public static class PasswordGenerator
{
    public static string Generate(int length = 16)
    {
        const string upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";
        const string lower = "abcdefghijkmnopqrstuvwxyz";
        const string numbers = "23456789";
        const string special = "!@#$%^&*";

        var all = upper + lower + numbers + special;

        var password = new char[length];

        password[0] = upper[RandomNumberGenerator.GetInt32(upper.Length)];
        password[1] = lower[RandomNumberGenerator.GetInt32(lower.Length)];
        password[2] = numbers[RandomNumberGenerator.GetInt32(numbers.Length)];
        password[3] = special[RandomNumberGenerator.GetInt32(special.Length)];

        for (int i = 4; i < length; i++)
        {
            password[i] =
                all[RandomNumberGenerator.GetInt32(all.Length)];
        }

        // Shuffle
        for (int i = password.Length - 1; i > 0; i--)
        {
            int j = RandomNumberGenerator.GetInt32(i + 1);

            (password[i], password[j]) =
                (password[j], password[i]);
        }

        return new string(password);
    }
}