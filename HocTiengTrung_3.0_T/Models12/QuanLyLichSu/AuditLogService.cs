using HocTiengTrung_3._0_T.Models;
using Microsoft.AspNetCore.Http;
using System;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;

namespace HocTiengTrung_3._0_T.Services
{
    public class AuditLogService : IAuditLogService
    {
        private readonly TmdtContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public AuditLogService(TmdtContext context, IHttpContextAccessor httpContextAccessor)
        {
            _context = context;
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task GhiLichSuAsync(
            string hanhDong,
            string? moTa = null,
            object? duLieuCu = null,
            object? duLieuMoi = null,
            int? idTaiKhoanOverride = null)
        {
            var httpContext = _httpContextAccessor.HttpContext;

            // 1. Alaen ti IdTaiKhoan manipud Claims no saan a nated iti parametro
            int? idTaiKhoan = idTaiKhoanOverride;
            if (!idTaiKhoan.HasValue && httpContext?.User?.Identity?.IsAuthenticated == true)
            {
                var idClaim = httpContext.User.FindFirst("IdTaiKhoan")?.Value
                              ?? httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

                if (int.TryParse(idClaim, out int parsedId))
                {
                    idTaiKhoan = parsedId;
                }
            }

            // 2. Alaen ti IP Address ken User-Agent
            string? diaChiIp = httpContext?.Connection?.RemoteIpAddress?.ToString();
            if (httpContext?.Request?.Headers?.ContainsKey("X-Forwarded-For") == true)
            {
                diaChiIp = httpContext.Request.Headers["X-Forwarded-For"].ToString();
            }

            string? userAgent = httpContext?.Request?.Headers["User-Agent"].ToString();

            // 3. Serialize dagiti datos agbalin a JSON string
            string? jsonCu = duLieuCu != null ? (duLieuCu is string s1 ? s1 : JsonSerializer.Serialize(duLieuCu)) : null;
            string? jsonMoi = duLieuMoi != null ? (duLieuMoi is string s2 ? s2 : JsonSerializer.Serialize(duLieuMoi)) : null;

            // 4. Isagana ti entidad
            var lichSu = new LichSuThayDoi
            {
                IdTaiKhoan = idTaiKhoan,
                HanhDong = hanhDong,
                DuLieuCu = jsonCu,
                DuLieuMoi = jsonMoi,
                ThoiGian = DateTime.Now,
                DiaChiIp = diaChiIp,
                UserAgent = userAgent,
                MoTa = moTa
            };

            _context.LichSuThayDois.Add(lichSu);
            await _context.SaveChangesAsync();
        }
        /// <summary>
        /// So sánh 2 object cùng hoặc khác kiểu và chỉ trả về các thuộc tính có giá trị thay đổi.
        /// Trả về 2 Dictionary: duLieuCu (chỉ chứa các trường bị đổi với giá trị cũ)
        ///                      duLieuMoi (chỉ chứa các trường bị đổi với giá trị mới)
        /// </summary>
        private (Dictionary<string, object?> duLieuCu, Dictionary<string, object?> duLieuMoi) GetChanges(object oldObj, object newObj)
        {
            var duLieuCu = new Dictionary<string, object?>();
            var duLieuMoi = new Dictionary<string, object?>();

            if (oldObj == null || newObj == null)
                return (duLieuCu, duLieuMoi);

            var oldProps = oldObj.GetType().GetProperties();
            var newProps = newObj.GetType().GetProperties().ToDictionary(p => p.Name, p => p);

            foreach (var oldProp in oldProps)
            {
                // Kiểm tra xem newObj có cùng tên property không
                if (!newProps.TryGetValue(oldProp.Name, out var newProp))
                    continue;

                var oldVal = oldProp.GetValue(oldObj);
                var newVal = newProp.GetValue(newObj);

                // So sánh giá trị giữa 2 object
                bool isDifferent = false;
                if (oldVal == null && newVal == null)
                {
                    isDifferent = false;
                }
                else if (oldVal == null || newVal == null)
                {
                    isDifferent = true;
                }
                else if (oldVal is DateTime dtOld && newVal is DateTime dtNew)
                {
                    // Với DateTime, so sánh đến giây để tránh lệch sai số tick nhỏ
                    isDifferent = Math.Abs((dtOld - dtNew).TotalSeconds) >= 1;
                }
                else
                {
                    isDifferent = !oldVal.Equals(newVal);
                }

                // Nếu có thay đổi -> gom vào kết quả
                if (isDifferent)
                {
                    duLieuCu[oldProp.Name] = oldVal;
                    duLieuMoi[newProp.Name] = newVal;
                }
            }

            return (duLieuCu, duLieuMoi);
        }
    }
}