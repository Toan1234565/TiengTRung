using HocTiengTrung_3._0_T.Models12.QuanLyTaiKhoan;

namespace HocTiengTrung_3._0_T.Models12
{
    public static class GiaiMaHelper
    {
        public static string? GiaiMaAnToan(string? cipherText)
        {
            if (string.IsNullOrWhiteSpace(cipherText))
                return null;

            try
            {
                return SecurityHelper.Decrypt(cipherText.Trim());
            }
            catch
            {
                // Nếu chuỗi là dữ liệu cũ chưa mã hóa (plaintext) hoặc lỗi giải mã, trả về nguyên bản
                return cipherText;
            }
        }
    }
}