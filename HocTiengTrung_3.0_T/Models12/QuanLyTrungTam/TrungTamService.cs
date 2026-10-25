using HocTiengTrung_3._0_T.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System;
using System.Threading.Tasks;

namespace HocTiengTrung_3._0_T.Services
{
    public interface ITrungTamService
    {
        Task<(string? TenTrungTam, string? Logo, string? MaTrungTam)> GetThongTinTrungTamAsync(int idTrungTam);
    }

    public class TrungTamService : ITrungTamService
    {
        private readonly TmdtContext _context;
        private readonly IMemoryCache _cache;

        public TrungTamService(TmdtContext context, IMemoryCache cache)
        {
            _context = context;
            _cache = cache;
        }

        public async Task<(string? TenTrungTam, string? Logo, string? MaTrungTam)> GetThongTinTrungTamAsync(int idTrungTam)
        {
            if (idTrungTam <= 0) return (null, null, null);

            string cacheKey = $"TrungTam_Info_{idTrungTam}";

            return await _cache.GetOrCreateAsync(cacheKey, async entry =>
            {
                // Cache trong 2 giờ
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(2);

                var tt = await _context.TrungTams
                    .AsNoTracking()
                    .Where(t => t.IdTrungTam == idTrungTam)
                    .Select(t => new { t.TenTrungTam, t.Logo, t.MaTrungTam })
                    .FirstOrDefaultAsync();

                if (tt == null) return (null, null, null);

                return (tt.TenTrungTam, tt.Logo, tt.MaTrungTam);
            });
        }
    }
}