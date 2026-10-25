using System.Security.Cryptography;
using System.Text;

namespace HocTiengTrung_3._0_T.Models12.QuanLyTaiKhoan
{
    public static class SecurityHelper
    {
        // LƯU Ý: Khóa này nên để trong appsettings.json (Environment Variables)
        // Phải đúng 32 ký tự cho AES-256
        private static readonly string Key = "12345678901234567890123456789012";

        public static string Encrypt(string plainText)
        {
            if (string.IsNullOrEmpty(plainText)) return plainText;
            using Aes aes = Aes.Create();
            aes.Key = Encoding.UTF8.GetBytes(Key);
            aes.GenerateIV();

            using var encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
            byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
            byte[] encrypted = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);

            // Kết hợp IV và dữ liệu đã mã hóa để có thể giải mã sau này
            return Convert.ToBase64String(aes.IV.Concat(encrypted).ToArray());
        }

        public static string Decrypt(string cipherText)
        {
            if (string.IsNullOrEmpty(cipherText)) return cipherText;
            byte[] buffer = Convert.FromBase64String(cipherText);
            using Aes aes = Aes.Create();
            aes.Key = Encoding.UTF8.GetBytes(Key);

            byte[] iv = new byte[16];
            byte[] cipherData = new byte[buffer.Length - 16];
            Array.Copy(buffer, 0, iv, 0, 16);
            Array.Copy(buffer, 16, cipherData, 0, cipherData.Length);

            aes.IV = iv;
            using var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
            return Encoding.UTF8.GetString(decryptor.TransformFinalBlock(cipherData, 0, cipherData.Length));
        }
    }
}
