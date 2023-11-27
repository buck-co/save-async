using System;
using System.IO;
using System.Security.Cryptography;

namespace Buck.DataManagement
{
    public enum EncryptionType
    {
        None,
        XOR,
        AES
    }
    
    public class Encrpytion
    {
        public static string Encrypt(string content, string password, EncryptionType encryptionType)
        {
            switch (encryptionType)
            {
                case EncryptionType.None:
                    return content;
                case EncryptionType.XOR:
                    return EncryptDecryptXOR(content, password);
                case EncryptionType.AES:
                    return EncryptStringAES(content, password);
                default:
                    throw new ArgumentOutOfRangeException(nameof(encryptionType), encryptionType, null);
            }
        }
        
        public static string Decrypt(string content, string password, EncryptionType encryptionType)
        {
            switch (encryptionType)
            {
                case EncryptionType.None:
                    return content;
                case EncryptionType.XOR:
                    return EncryptDecryptXOR(content, password);
                case EncryptionType.AES:
                    return DecryptStringAES(content, password);
                default:
                    throw new ArgumentOutOfRangeException(nameof(encryptionType), encryptionType, null);
            }
        }
        
        static string EncryptDecryptXOR(string content, string password)
        {
            string newContent = "";
            for (int i = 0; i < content.Length; i++)
                newContent += (char)(content[i] ^ password[i % password.Length]);
            return newContent;
        }
        
        const int KeySize = 256; // 256 bits for AES key
        const int Iterations = 10000; // Number of iterations for PBKDF2

        /// <summary>
        /// Encrypts a plain text string using AES encryption with a given password.
        /// </summary>
        /// <param name="plainText">The text to be encrypted.</param>
        /// <param name="password">The password used for generating the encryption key.</param>
        /// <returns>The encrypted text in Base64 format.</returns>
        /// <remarks>
        /// This method generates a random salt and IV, uses PBKDF2 to derive an AES key from the password,
        /// and then encrypts the text using AES. The salt and IV are prepended to the encrypted data.
        /// </remarks>
        static string EncryptStringAES(string plainText, string password)
        {
            var salt = GenerateRandomBytes(16); // Generate a 128-bit salt

            using var key = new Rfc2898DeriveBytes(password, salt, Iterations);
            var aesKey = key.GetBytes(KeySize / 8);
            var aesIV = GenerateRandomBytes(16); // Generate a 128-bit IV

            using Aes aesAlg = Aes.Create();
            aesAlg.Key = aesKey;
            aesAlg.IV = aesIV;

            ICryptoTransform encryptor = aesAlg.CreateEncryptor(aesAlg.Key, aesAlg.IV);

            using MemoryStream msEncrypt = new MemoryStream();
            // Prepend salt and IV to the encrypted data
            msEncrypt.Write(salt, 0, salt.Length);
            msEncrypt.Write(aesIV, 0, aesIV.Length);

            using CryptoStream csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write);
            using StreamWriter swEncrypt = new StreamWriter(csEncrypt);
            swEncrypt.Write(plainText);
            return Convert.ToBase64String(msEncrypt.ToArray());
        }

        /// <summary>
        /// Decrypts a Base64 encoded string that was encrypted using the AES algorithm.
        /// </summary>
        /// <param name="cipherText">The encrypted text in Base64 format.</param>
        /// <param name="password">The password used for generating the encryption key.</param>
        /// <returns>The decrypted plain text.</returns>
        /// <remarks>
        /// This method extracts the salt and IV that were prepended to the encrypted data,
        /// uses PBKDF2 with the extracted salt and provided password to derive the AES key,
        /// and then decrypts the text using AES.
        /// </remarks>
        static string DecryptStringAES(string cipherText, string password)
        {
            byte[] cipherBytes = Convert.FromBase64String(cipherText);
            using MemoryStream msDecrypt = new MemoryStream(cipherBytes);
            byte[] salt = new byte[16];
            byte[] iv = new byte[16];
            msDecrypt.Read(salt, 0, salt.Length);
            msDecrypt.Read(iv, 0, iv.Length);

            using var key = new Rfc2898DeriveBytes(password, salt, Iterations);
            var aesKey = key.GetBytes(KeySize / 8);

            using Aes aesAlg = Aes.Create();
            aesAlg.Key = aesKey;
            aesAlg.IV = iv;

            ICryptoTransform decryptor = aesAlg.CreateDecryptor(aesAlg.Key, aesAlg.IV);

            using CryptoStream csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read);
            using StreamReader srDecrypt = new StreamReader(csDecrypt);
            return srDecrypt.ReadToEnd();
        }

        /// <summary>
        /// Generates a random byte array of a specified length using a cryptographic random number generator.
        /// </summary>
        /// <param name="length">The desired length of the random byte array.</param>
        /// <returns>A byte array containing cryptographically strong random values.</returns>
        /// <remarks>
        /// This method is primarily used for generating random salts and IVs for cryptographic purposes.
        /// </remarks>
        static byte[] GenerateRandomBytes(int length)
        {
            using var rng = new RNGCryptoServiceProvider();
            byte[] randomNumber = new byte[length];
            rng.GetBytes(randomNumber);
            return randomNumber;
        }
    }
}
