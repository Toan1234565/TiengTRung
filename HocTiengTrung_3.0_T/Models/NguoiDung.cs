using System;
using System.Collections.Generic;

namespace HocTiengTrung_3._0_T.Models;

public partial class NguoiDung
{
    public int IdUser { get; set; }

    public int? IdTaiKhoan { get; set; }

    public string? HoTen { get; set; }

    public string? Email { get; set; }

    public string? SoDienThoai { get; set; }

    public DateOnly? NgaySinh { get; set; }

    public string? AnhDaiDien { get; set; }

    public virtual ICollection<GhiChu> GhiChus { get; set; } = new List<GhiChu>();

    public virtual ICollection<HocVienLopHoc> HocVienLopHocs { get; set; } = new List<HocVienLopHoc>();

    public virtual TaiKhoan? IdTaiKhoanNavigation { get; set; }

    public virtual ICollection<KetQuaThi> KetQuaThis { get; set; } = new List<KetQuaThi>();

    public virtual ICollection<LichSuLamBai> LichSuLamBais { get; set; } = new List<LichSuLamBai>();

    public virtual ICollection<TuVungNguoiDung> TuVungNguoiDungs { get; set; } = new List<TuVungNguoiDung>();
}
