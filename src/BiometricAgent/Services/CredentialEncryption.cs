using System;
using System.Security.Cryptography;
using System.Text;

namespace BiometricAgent.Services;

/// <summary>
/// Provides Windows DPAPI-based encryption for credentials stored in configuration files.
/// Uses CurrentUser scope to tie credentials to the Windows account running the agent.
/// </summary>
public static class CredentialEncryption
{
    /// <summary>
    /// Encrypts a plaintext credential using Windows DPAPI (Data Protection API).
    /// The encrypted value is tied to the current Windows user account.
    /// </summary>
    /// <param name="plaintext">The plaintext password or credential to encrypt.</param>
    /// <returns>Base64-encoded encrypted credential suitable for storage in config.json.</returns>
    /// <exception cref="ArgumentNullException">Thrown when plaintext is null.</exception>
    /// <exception cref="CryptographicException">Thrown when encryption fails.</exception>
    public static string Encrypt(string plaintext)
    {
        if (plaintext == null)
        {
            throw new ArgumentNullException(nameof(plaintext), "Cannot encrypt null value");
        }

        try
        {
            var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
            var encryptedBytes = ProtectedData.Protect(
                plaintextBytes,
                optionalEntropy: null,
                scope: DataProtectionScope.CurrentUser
            );
            return Convert.ToBase64String(encryptedBytes);
        }
        catch (CryptographicException ex)
        {
            throw new CryptographicException(
                "Failed to encrypt credential. Ensure Windows user profile is accessible.", ex);
        }
    }

    /// <summary>
    /// Decrypts a DPAPI-encrypted credential back to plaintext.
    /// Must be called from the same Windows user account that performed encryption.
    /// </summary>
    /// <param name="ciphertext">Base64-encoded encrypted credential from config.json.</param>
    /// <returns>Decrypted plaintext password or credential.</returns>
    /// <exception cref="ArgumentNullException">Thrown when ciphertext is null.</exception>
    /// <exception cref="ArgumentException">Thrown when ciphertext is not valid Base64.</exception>
    /// <exception cref="CryptographicException">Thrown when decryption fails (wrong user or corrupted data).</exception>
    public static string Decrypt(string ciphertext)
    {
        if (ciphertext == null)
        {
            throw new ArgumentNullException(nameof(ciphertext), "Cannot decrypt null value");
        }

        try
        {
            var encryptedBytes = Convert.FromBase64String(ciphertext);
            var plaintextBytes = ProtectedData.Unprotect(
                encryptedBytes,
                optionalEntropy: null,
                scope: DataProtectionScope.CurrentUser
            );
            return Encoding.UTF8.GetString(plaintextBytes);
        }
        catch (FormatException ex)
        {
            throw new ArgumentException(
                "Invalid encrypted credential format. Expected Base64-encoded string.", ex);
        }
        catch (CryptographicException ex)
        {
            throw new CryptographicException(
                "Failed to decrypt credential. Ensure this is the same Windows user that encrypted the value.", ex);
        }
    }

    /// <summary>
    /// Validates whether a string appears to be a valid encrypted credential.
    /// Checks Base64 format and minimum length (DPAPI output is at least 20 bytes).
    /// </summary>
    /// <param name="value">The value to validate.</param>
    /// <returns>True if the value appears to be encrypted; false if plaintext or invalid.</returns>
    public static bool IsEncrypted(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        try
        {
            var bytes = Convert.FromBase64String(value);
            // DPAPI-encrypted values are typically at least 20 bytes
            return bytes.Length >= 20;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
