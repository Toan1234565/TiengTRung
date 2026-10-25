using System;
using System.Collections.Generic;

namespace HocTiengTrung_3._0_T.Models;

public partial class TaiKhoan
{
    public int IdTaiKhoan { get; set; }

    public string TenDangNhap { get; set; } = null!;

    public string MatKhau { get; set; } = null!;

    public bool? TrangThai { get; set; }

    public DateTime? NgayTao { get; set; }

    public int? IdVaiTro { get; set; }

    public bool IsVip { get; set; }

    public DateTime? NgayBatDauVip { get; set; }

    public DateTime? NgayKetThucVip { get; set; }

    public int? IdTrungTam { get; set; }

    public virtual ICollection<GiangVienLopHoc> GiangVienLopHocs { get; set; } = new List<GiangVienLopHoc>();

    public virtual ICollection<GiaoDich> GiaoDiches { get; set; } = new List<GiaoDich>();

    public virtual TrungTam? IdTrungTamNavigation { get; set; }

    public virtual VaiTro? IdVaiTroNavigation { get; set; }

    public virtual ICollection<LichSuThayDoi> LichSuThayDois { get; set; } = new List<LichSuThayDoi>();

    public virtual ICollection<LichSuTruyCap> LichSuTruyCaps { get; set; } = new List<LichSuTruyCap>();

    public virtual NguoiDung? NguoiDung { get; set; }
}
