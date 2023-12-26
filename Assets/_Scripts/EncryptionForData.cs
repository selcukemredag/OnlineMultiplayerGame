using System;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

public class EncryptionForData : MonoBehaviour
{
    private static readonly string EncryptionKey = "SelcukEmreDag270"; // My secret key

    public static string EncryptString(string plainText)
    {
        // Convert the plain text into a byte array using UTF-8 encoding.
        byte[] b = Encoding.UTF8.GetBytes(plainText);

        // Create a new instance of the Aes cryptographic service provider.
        using (Aes aes = Aes.Create())
        {
            // Set the encryption key. Convert the predefined EncryptionKey string into a byte array.
            aes.Key = Encoding.UTF8.GetBytes(EncryptionKey);

            // Set the Initialization Vector (IV) to a new byte array of length 16.
            // This is used to increase the security of the encryption and ensure unique encryption results.
            aes.IV = new byte[16];

            // Create an encryptor object from the Aes instance.
            using (var encryptor = aes.CreateEncryptor(aes.Key, aes.IV))
            {
                // Perform the encryption on the byte array of the plain text.
                byte[] encrypted = encryptor.TransformFinalBlock(b, 0, b.Length);

                // Convert the encrypted byte array back into a base64 string.
                // Base64 is used to ensure the byte array can be represented in a string format.
                return Convert.ToBase64String(encrypted);
            }
        }
    }

    public static string DecryptString(string cipherText)
    {
        // Convert the base64 encoded cipher text back into a byte array.
        byte[] b = Convert.FromBase64String(cipherText);

        // Create a new instance of the Aes cryptographic service provider.
        using (Aes aes = Aes.Create())
        {
            // Set the decryption key. Convert the predefined EncryptionKey string into a byte array.
            aes.Key = Encoding.UTF8.GetBytes(EncryptionKey);

            // Set the Initialization Vector (IV) to a new byte array of length 16.
            aes.IV = new byte[16];

            // Create a decryptor object from the Aes instance.
            using (var decryptor = aes.CreateDecryptor(aes.Key, aes.IV))
            {
                // Perform the decryption on the byte array of the cipher text.
                byte[] decrypted = decryptor.TransformFinalBlock(b, 0, b.Length);

                // Convert the decrypted byte array back into a plain text string using UTF-8 encoding.
                return Encoding.UTF8.GetString(decrypted);
            }
        }
    }
}
