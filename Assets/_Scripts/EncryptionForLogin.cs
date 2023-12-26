using UnityEngine;
using System.Security.Cryptography;
using System;
using System.Text;

public class EncryptionForLogin : MonoBehaviour
{
    private const string encryptionKey = "InnocentChukwuma"; // My secret key

    public string GetEncryptionKey()
    {
        return encryptionKey;
    }

    public static string Encrypt(string data, string key)
    {
        // Convert the encryption key and the data to encrypt into byte arrays using UTF8 encoding.
        byte[] keyArray = UTF8Encoding.UTF8.GetBytes(key);
        byte[] toEncryptArray = UTF8Encoding.UTF8.GetBytes(data);

        // Create an instance of the RijndaelManaged class to perform the encryption.
        RijndaelManaged rDel = new RijndaelManaged();
        rDel.Key = keyArray; // Set the key for the encryption.
        rDel.Mode = CipherMode.ECB; // Use Electronic Codebook (ECB) cipher mode.
        rDel.Padding = PaddingMode.PKCS7; // Use PKCS7 padding mode.

        // Create an encryptor object from the RijndaelManaged instance.
        ICryptoTransform cTransform = rDel.CreateEncryptor();

        // Perform the encryption.
        byte[] resultArray = cTransform.TransformFinalBlock(toEncryptArray, 0, toEncryptArray.Length);

        // Convert the encrypted byte array to a base64 encoded string and return it.
        return Convert.ToBase64String(resultArray, 0, resultArray.Length);
    }

    public static string Decrypt(string data, string key)
    {
        // Convert the decryption key into a byte array using UTF8 encoding.
        byte[] keyArray = UTF8Encoding.UTF8.GetBytes(key);

        // Convert the base64 encoded data to encrypt into a byte array.
        byte[] toEncryptArray = Convert.FromBase64String(data);

        // Create an instance of the RijndaelManaged class to perform the decryption.
        RijndaelManaged rDel = new RijndaelManaged();
        rDel.Key = keyArray; // Set the key for the decryption.
        rDel.Mode = CipherMode.ECB; // Use Electronic Codebook (ECB) cipher mode.
        rDel.Padding = PaddingMode.PKCS7; // Use PKCS7 padding mode.

        // Create a decryptor object from the RijndaelManaged instance.
        ICryptoTransform cTransform = rDel.CreateDecryptor();

        // Perform the decryption.
        byte[] resultArray = cTransform.TransformFinalBlock(toEncryptArray, 0, toEncryptArray.Length);

        // Convert the decrypted byte array back into a string using UTF8 encoding and return it.
        return UTF8Encoding.UTF8.GetString(resultArray);
    }
}
