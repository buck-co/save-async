// MIT License - Copyright (c) 2024 BUCK Design LLC - https://github.com/buck-co

using System;

namespace Buck.SaveAsync
{
    public enum EncryptionType
    {
        None,
        XOR
    }
    
    public class Encryption
    {
        public static string Encrypt(string content, string password, EncryptionType encryptionType)
        {
            switch (encryptionType)
            {
                case EncryptionType.None:
                    return content;
                case EncryptionType.XOR:
                    return EncryptDecryptXOR(content, password);
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
    }
}
