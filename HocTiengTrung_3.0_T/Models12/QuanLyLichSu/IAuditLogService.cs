using System.Threading.Tasks;

namespace HocTiengTrung_3._0_T.Services
{
    public interface IAuditLogService
    {
        Task GhiLichSuAsync(
            string hanhDong,
            string? moTa = null,
            object? duLieuCu = null,
            object? duLieuMoi = null,
            int? idTaiKhoanOverride = null
        );
    }
}