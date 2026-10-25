using System;
using System.Collections.Generic;
using HocTiengTrung_3._0_T.Models;
using Microsoft.EntityFrameworkCore;

namespace HocTiengTrung_3._0_T;

public partial class TmdtContext : DbContext
{
    public TmdtContext()
    {
    }

    public TmdtContext(DbContextOptions<TmdtContext> options)
        : base(options)
    {
    }

    public virtual DbSet<BaiHoc> BaiHocs { get; set; }

    public virtual DbSet<BaiHocCauHoi> BaiHocCauHois { get; set; }

    public virtual DbSet<BaiHocTuVung> BaiHocTuVungs { get; set; }

    public virtual DbSet<BangGiaTaiKhoan> BangGiaTaiKhoans { get; set; }

    public virtual DbSet<BoThu> BoThus { get; set; }

    public virtual DbSet<CapDo> CapDos { get; set; }

    public virtual DbSet<CauHoi> CauHois { get; set; }

    public virtual DbSet<CauViDu> CauViDus { get; set; }

    public virtual DbSet<ChiTietBaiLam> ChiTietBaiLams { get; set; }

    public virtual DbSet<ChiTietDeThi> ChiTietDeThis { get; set; }

    public virtual DbSet<ChiTietLichSu> ChiTietLichSus { get; set; }

    public virtual DbSet<ChiTietLuyenNgheHinhAnh> ChiTietLuyenNgheHinhAnhs { get; set; }

    public virtual DbSet<ChuDe> ChuDes { get; set; }

    public virtual DbSet<ChuongTrinhGiaoTrinh> ChuongTrinhGiaoTrinhs { get; set; }

    public virtual DbSet<DapAn> DapAns { get; set; }

    public virtual DbSet<DeThi> DeThis { get; set; }

    public virtual DbSet<DoanVan> DoanVans { get; set; }

    public virtual DbSet<GhiChu> GhiChus { get; set; }

    public virtual DbSet<GiangVienLopHoc> GiangVienLopHocs { get; set; }

    public virtual DbSet<GiaoDich> GiaoDiches { get; set; }

    public virtual DbSet<HocVienLopHoc> HocVienLopHocs { get; set; }

    public virtual DbSet<KetQuaThi> KetQuaThis { get; set; }

    public virtual DbSet<LichSuLamBai> LichSuLamBais { get; set; }

    public virtual DbSet<LichSuThayDoi> LichSuThayDois { get; set; }

    public virtual DbSet<LichSuTruyCap> LichSuTruyCaps { get; set; }

    public virtual DbSet<LoaiCauHoi> LoaiCauHois { get; set; }

    public virtual DbSet<LoaiDeThi> LoaiDeThis { get; set; }

    public virtual DbSet<LopHoc> LopHocs { get; set; }

    public virtual DbSet<LuyenNgheHinhAnh> LuyenNgheHinhAnhs { get; set; }

    public virtual DbSet<LuyenNgheNguoiDung> LuyenNgheNguoiDungs { get; set; }

    public virtual DbSet<NguPhap> NguPhaps { get; set; }

    public virtual DbSet<NguoiDung> NguoiDungs { get; set; }

    public virtual DbSet<TaiKhoan> TaiKhoans { get; set; }

    public virtual DbSet<TaiLieu> TaiLieus { get; set; }

    public virtual DbSet<TrungTam> TrungTams { get; set; }

    public virtual DbSet<TuVung> TuVungs { get; set; }

    public virtual DbSet<TuVungNguoiDung> TuVungNguoiDungs { get; set; }

    public virtual DbSet<VaiTro> VaiTros { get; set; }

    public virtual DbSet<VideoLuyenNghe> VideoLuyenNghes { get; set; }

    public virtual DbSet<VideoSubtitle> VideoSubtitles { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
#warning To protect potentially sensitive information in your connection string, you should move it out of source code. You can avoid scaffolding the connection string by using the Name= syntax to read it from configuration - see https://go.microsoft.com/fwlink/?linkid=2131148. For more guidance on storing connection strings, see https://go.microsoft.com/fwlink/?LinkId=723263.
    //   => optionsBuilder.UseSqlServer("Data Source=TOAN;Initial Catalog=db_acacd1_hoctiengtrung12;Integrated Security=True;Encrypt=False");
    => optionsBuilder.UseSqlServer("Data Source=SQL1004.site4now.net;Initial Catalog=db_acacd1_hoctiengtrung12;User Id=db_acacd1_hoctiengtrung12_admin;Password=Toan28112004@;Encrypt=True;TrustServerCertificate=True;");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.UseCollation("Latin1_General_100_CI_AS_SC_UTF8");

        modelBuilder.Entity<BaiHoc>(entity =>
        {
            entity.HasKey(e => e.IdBaiHoc);

            entity.ToTable("BaiHoc");

            entity.Property(e => e.IdTrungTam).HasColumnName("ID_TrungTam");
            entity.Property(e => e.TenBaiHoc).HasMaxLength(250);
            entity.Property(e => e.TrangThai).HasDefaultValue(true);

            entity.HasOne(d => d.IdCapDoNavigation).WithMany(p => p.BaiHocs)
                .HasForeignKey(d => d.IdCapDo)
                .HasConstraintName("FK_BaiHoc_CapDo");

            entity.HasOne(d => d.IdGiaoTrinhNavigation).WithMany(p => p.BaiHocs)
                .HasForeignKey(d => d.IdGiaoTrinh)
                .HasConstraintName("FK_BaiHoc_ChuongTrinhGiaoTrinh");

            entity.HasOne(d => d.IdTrungTamNavigation).WithMany(p => p.BaiHocs)
                .HasForeignKey(d => d.IdTrungTam)
                .HasConstraintName("FK_BaiHoc_TrungTam");
        });

        modelBuilder.Entity<BaiHocCauHoi>(entity =>
        {
            entity.HasKey(e => new { e.IdBaiHoc, e.IdCauHoi });

            entity.ToTable("BaiHoc_CauHoi");

            entity.HasOne(d => d.IdBaiHocNavigation).WithMany(p => p.BaiHocCauHois)
                .HasForeignKey(d => d.IdBaiHoc)
                .HasConstraintName("FK_BHCH_BaiHoc");

            entity.HasOne(d => d.IdCauHoiNavigation).WithMany(p => p.BaiHocCauHois)
                .HasForeignKey(d => d.IdCauHoi)
                .HasConstraintName("FK_BHCH_CauHoi");
        });

        modelBuilder.Entity<BaiHocTuVung>(entity =>
        {
            entity.HasKey(e => new { e.IdBaiHoc, e.IdTuVung });

            entity.ToTable("BaiHoc_TuVung");

            entity.Property(e => e.LaTuMoi).HasDefaultValue(true);

            entity.HasOne(d => d.IdBaiHocNavigation).WithMany(p => p.BaiHocTuVungs)
                .HasForeignKey(d => d.IdBaiHoc)
                .HasConstraintName("FK_BHTV_BaiHoc");

            entity.HasOne(d => d.IdTuVungNavigation).WithMany(p => p.BaiHocTuVungs)
                .HasForeignKey(d => d.IdTuVung)
                .HasConstraintName("FK_BHTV_TuVung");
        });

        modelBuilder.Entity<BangGiaTaiKhoan>(entity =>
        {
            entity.HasKey(e => e.IdGoi).HasName("PK__BangGiaT__0C9ED645F1A1B5C7");

            entity.ToTable("BangGiaTaiKhoan");

            entity.Property(e => e.MoTa).HasMaxLength(255);
            entity.Property(e => e.NgayTao)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.SoTien).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.TenGoi).HasMaxLength(100);
            entity.Property(e => e.TrangThai).HasDefaultValue(true);
        });

        modelBuilder.Entity<BoThu>(entity =>
        {
            entity.HasKey(e => e.IdBoThu);

            entity.ToTable("BoThu");

            entity.HasIndex(e => e.BoThu1, "IX_BoThu_BoThu");

            entity.HasIndex(e => e.SoNet, "IX_BoThu_SoNet");

            entity.Property(e => e.IdBoThu).HasColumnName("ID_BoThu");
            entity.Property(e => e.BienThe).HasMaxLength(50);
            entity.Property(e => e.BoThu1)
                .HasMaxLength(10)
                .HasColumnName("BoThu");
            entity.Property(e => e.HanViet).HasMaxLength(100);
            entity.Property(e => e.Pinyin).HasMaxLength(50);
            entity.Property(e => e.Stt).HasColumnName("STT");
            entity.Property(e => e.TrangThai).HasDefaultValue(true);
            entity.Property(e => e.Ynghia).HasColumnName("YNghia");
        });

        modelBuilder.Entity<CapDo>(entity =>
        {
            entity.HasKey(e => e.IdCapDo).HasName("PK__CapDo__728F25F5FE8316AB");

            entity.ToTable("CapDo");

            entity.Property(e => e.IdCapDo).HasColumnName("ID_CapDo");
            entity.Property(e => e.TenCapDo)
                .HasMaxLength(50)
                .UseCollation("Vietnamese_CI_AS");
            entity.Property(e => e.TrangThai).HasDefaultValue(true);
        });

        modelBuilder.Entity<CauHoi>(entity =>
        {
            entity.HasKey(e => e.IdCauHoi).HasName("PK__CauHoi__C017FCB5740F248D");

            entity.ToTable("CauHoi");

            entity.HasIndex(e => new { e.TrangThai, e.IdCapDo, e.IdLoai }, "IX_CauHoi_TrangThai_CapDo_Loai");

            entity.Property(e => e.IdCauHoi).HasColumnName("ID_CauHoi");
            entity.Property(e => e.AmThanhPath)
                .HasMaxLength(255)
                .IsUnicode(false)
                .UseCollation("Vietnamese_CI_AS");
            entity.Property(e => e.DoKho).HasDefaultValue(1);
            entity.Property(e => e.GiaiThich).UseCollation("Vietnamese_CI_AS");
            entity.Property(e => e.IdCapDo).HasColumnName("ID_CapDo");
            entity.Property(e => e.IdDoanVan).HasColumnName("ID_DoanVan");
            entity.Property(e => e.IdLoai).HasColumnName("ID_Loai");
            entity.Property(e => e.IdTrungTam).HasColumnName("ID_TrungTam");
            entity.Property(e => e.IsGeneratedOnly).HasDefaultValue(false);
            entity.Property(e => e.NoiDung).UseCollation("Vietnamese_CI_AS");

            entity.HasOne(d => d.IdCapDoNavigation).WithMany(p => p.CauHois)
                .HasForeignKey(d => d.IdCapDo)
                .HasConstraintName("FK__CauHoi__ID_CapDo__46E78A0C");

            entity.HasOne(d => d.IdDoanVanNavigation).WithMany(p => p.CauHois)
                .HasForeignKey(d => d.IdDoanVan)
                .HasConstraintName("FK__CauHoi__ID_DoanV__47DBAE45");

            entity.HasOne(d => d.IdLoaiNavigation).WithMany(p => p.CauHois)
                .HasForeignKey(d => d.IdLoai)
                .HasConstraintName("FK__CauHoi__ID_Loai__45F365D3");

            entity.HasOne(d => d.IdTrungTamNavigation).WithMany(p => p.CauHois)
                .HasForeignKey(d => d.IdTrungTam)
                .HasConstraintName("FK_CauHoi_TrungTam");

            entity.HasMany(d => d.IdNguPhaps).WithMany(p => p.IdCauHois)
                .UsingEntity<Dictionary<string, object>>(
                    "CauHoiNguPhap",
                    r => r.HasOne<NguPhap>().WithMany()
                        .HasForeignKey("IdNguPhap")
                        .HasConstraintName("FK_CHNP_NguPhap"),
                    l => l.HasOne<CauHoi>().WithMany()
                        .HasForeignKey("IdCauHoi")
                        .HasConstraintName("FK_CHNP_CauHoi"),
                    j =>
                    {
                        j.HasKey("IdCauHoi", "IdNguPhap").HasName("PK__CauHoi_N__4317E0A76CC40D1D");
                        j.ToTable("CauHoi_NguPhap");
                        j.IndexerProperty<int>("IdCauHoi").HasColumnName("ID_CauHoi");
                        j.IndexerProperty<int>("IdNguPhap").HasColumnName("ID_NguPhap");
                    });

            entity.HasMany(d => d.IdTuVungs).WithMany(p => p.IdCauHois)
                .UsingEntity<Dictionary<string, object>>(
                    "CauHoiTuVung",
                    r => r.HasOne<TuVung>().WithMany()
                        .HasForeignKey("IdTuVung")
                        .HasConstraintName("FK_CauHoiTuVung_TuVung"),
                    l => l.HasOne<CauHoi>().WithMany()
                        .HasForeignKey("IdCauHoi")
                        .HasConstraintName("FK_CauHoiTuVung_CauHoi"),
                    j =>
                    {
                        j.HasKey("IdCauHoi", "IdTuVung");
                        j.ToTable("CauHoi_TuVung");
                        j.HasIndex(new[] { "IdTuVung" }, "IX_CauHoi_TuVung_ID_TuVung");
                        j.IndexerProperty<int>("IdCauHoi").HasColumnName("ID_CauHoi");
                        j.IndexerProperty<int>("IdTuVung").HasColumnName("ID_TuVung");
                    });
        });

        modelBuilder.Entity<CauViDu>(entity =>
        {
            entity.HasKey(e => e.IdViDu).HasName("PK__CauViDu__E413313B8A198C78");

            entity.ToTable("CauViDu");

            entity.Property(e => e.IdViDu).HasColumnName("ID_ViDu");
            entity.Property(e => e.AmThanhPath)
                .HasMaxLength(255)
                .IsUnicode(false)
                .UseCollation("Vietnamese_CI_AS");
            entity.Property(e => e.IdTuVung).HasColumnName("ID_TuVung");
            entity.Property(e => e.NoiDungHan).UseCollation("Vietnamese_CI_AS");
            entity.Property(e => e.NoiDungPinyin).UseCollation("Vietnamese_CI_AS");
            entity.Property(e => e.NoiDungViet).UseCollation("Vietnamese_CI_AS");

            entity.HasOne(d => d.IdTuVungNavigation).WithMany(p => p.CauViDus)
                .HasForeignKey(d => d.IdTuVung)
                .HasConstraintName("FK__CauViDu__ID_TuVu__3C69FB99");
        });

        modelBuilder.Entity<ChiTietBaiLam>(entity =>
        {
            entity.HasKey(e => e.IdChiTietKetQua).HasName("PK__ChiTietB__345560318E870059");

            entity.ToTable("ChiTietBaiLam");

            entity.Property(e => e.DapAnChon).UseCollation("Vietnamese_CI_AS");

            entity.HasOne(d => d.IdChiTietDeThiNavigation).WithMany(p => p.ChiTietBaiLams)
                .HasForeignKey(d => d.IdChiTietDeThi)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_CTBL_ChiTietDeThi");

            entity.HasOne(d => d.IdKetQuaNavigation).WithMany(p => p.ChiTietBaiLams)
                .HasForeignKey(d => d.IdKetQua)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_CTBL_KetQua");
        });

        modelBuilder.Entity<ChiTietDeThi>(entity =>
        {
            entity.HasKey(e => e.IdChiTiet).HasName("PK__ChiTietD__4E91F45E9726318A");

            entity.ToTable("ChiTietDeThi");

            entity.Property(e => e.AmThanhPath)
                .HasMaxLength(500)
                .UseCollation("Vietnamese_CI_AS");
            entity.Property(e => e.DapAnDung).UseCollation("Vietnamese_CI_AS");
            entity.Property(e => e.GhiChu).UseCollation("Vietnamese_CI_AS");
            entity.Property(e => e.GiaiThich).UseCollation("Vietnamese_CI_AS");
            entity.Property(e => e.HinhAnhPath)
                .HasMaxLength(500)
                .UseCollation("Vietnamese_CI_AS");
            entity.Property(e => e.NoiDungJson).UseCollation("Vietnamese_CI_AS");

            entity.HasOne(d => d.IdDeThiNavigation).WithMany(p => p.ChiTietDeThis)
                .HasForeignKey(d => d.IdDeThi)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_ChiTiet_DeThi");

            entity.HasOne(d => d.IdLoaiNavigation).WithMany(p => p.ChiTietDeThis)
                .HasForeignKey(d => d.IdLoai)
                .HasConstraintName("FK_ChiTiet_LoaiDe");
        });

        modelBuilder.Entity<ChiTietLichSu>(entity =>
        {
            entity.HasKey(e => e.IdChiTiet).HasName("PK__ChiTietL__4E91F45ED8484E58");

            entity.ToTable("ChiTietLichSu");

            entity.Property(e => e.CauTraLoiNguoiDung).UseCollation("Vietnamese_CI_AS");
            entity.Property(e => e.LaDung).HasDefaultValue(false);

            entity.HasOne(d => d.IdCauHoiNavigation).WithMany(p => p.ChiTietLichSus)
                .HasForeignKey(d => d.IdCauHoi)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ChiTiet_CauHoi");

            entity.HasOne(d => d.IdLichSuNavigation).WithMany(p => p.ChiTietLichSus)
                .HasForeignKey(d => d.IdLichSu)
                .HasConstraintName("FK_ChiTiet_LichSu");
        });

        modelBuilder.Entity<ChiTietLuyenNgheHinhAnh>(entity =>
        {
            entity.HasKey(e => e.IdChiTiet).HasName("PK__ChiTietL__1EF2F7055C393F6B");

            entity.ToTable("ChiTietLuyenNgheHinhAnh");

            entity.Property(e => e.IdChiTiet).HasColumnName("ID_ChiTiet");
            entity.Property(e => e.FileAudio).HasMaxLength(500);
            entity.Property(e => e.HinhAnh).HasMaxLength(500);
            entity.Property(e => e.IdLuyenNghe).HasColumnName("ID_LuyenNghe");
            entity.Property(e => e.PhienAmPinyin).HasMaxLength(250);

            entity.HasOne(d => d.IdLuyenNgheNavigation).WithMany(p => p.ChiTietLuyenNgheHinhAnhs)
                .HasForeignKey(d => d.IdLuyenNghe)
                .HasConstraintName("FK_ChiTiet_LuyenNgheHinhAnh");
        });

        modelBuilder.Entity<ChuDe>(entity =>
        {
            entity.HasKey(e => e.IdChuDe).HasName("PK__ChuDe__967F06D5A553ED92");

            entity.ToTable("ChuDe");

            entity.Property(e => e.IdChuDe).HasColumnName("ID_ChuDe");
            entity.Property(e => e.TenChuDe)
                .HasMaxLength(100)
                .UseCollation("Vietnamese_CI_AS");
        });

        modelBuilder.Entity<ChuongTrinhGiaoTrinh>(entity =>
        {
            entity.HasKey(e => e.IdGiaoTrinh).HasName("PK__ChuongTr__47085E103AECA11B");

            entity.ToTable("ChuongTrinhGiaoTrinh");

            entity.Property(e => e.TenChuongTrinh).HasMaxLength(250);
            entity.Property(e => e.TenGiaoTrinh).HasMaxLength(250);
            entity.Property(e => e.TrangThai).HasDefaultValue(true);
        });

        modelBuilder.Entity<DapAn>(entity =>
        {
            entity.HasKey(e => e.IdDapAn).HasName("PK__DapAn__5382CD28BC03DD47");

            entity.ToTable("DapAn");

            entity.Property(e => e.IdDapAn).HasColumnName("ID_DapAn");
            entity.Property(e => e.IdCauHoi).HasColumnName("ID_CauHoi");
            entity.Property(e => e.LaDapAnDung).HasDefaultValue(false);
            entity.Property(e => e.NoiDungDapAn).UseCollation("Vietnamese_CI_AS");

            entity.HasOne(d => d.IdCauHoiNavigation).WithMany(p => p.DapAns)
                .HasForeignKey(d => d.IdCauHoi)
                .HasConstraintName("FK__DapAn__ID_CauHoi__4AB81AF0");
        });

        modelBuilder.Entity<DeThi>(entity =>
        {
            entity.HasKey(e => e.IdDeThi).HasName("PK__DeThi__67322F3956917E90");

            entity.ToTable("DeThi");

            entity.Property(e => e.IdTrungTam).HasColumnName("ID_TrungTam");
            entity.Property(e => e.NgayTao)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.TenDe)
                .HasMaxLength(250)
                .UseCollation("Vietnamese_CI_AS");
            entity.Property(e => e.ThoiGianChuyenTiep).HasDefaultValue(5);
            entity.Property(e => e.TrangThai).HasDefaultValue(true);

            entity.HasOne(d => d.IdCapDoNavigation).WithMany(p => p.DeThis)
                .HasForeignKey(d => d.IdCapDo)
                .HasConstraintName("FK_DeThi_CapDo");

            entity.HasOne(d => d.IdTrungTamNavigation).WithMany(p => p.DeThis)
                .HasForeignKey(d => d.IdTrungTam)
                .HasConstraintName("FK_DeThi_TrungTam");
        });

        modelBuilder.Entity<DoanVan>(entity =>
        {
            entity.HasKey(e => e.IdDoanVan).HasName("PK__DoanVan__6C9C62415AE1D5F3");

            entity.ToTable("DoanVan");

            entity.Property(e => e.IdDoanVan).HasColumnName("ID_DoanVan");
            entity.Property(e => e.AnhMinhHoa)
                .HasMaxLength(255)
                .IsUnicode(false)
                .UseCollation("Vietnamese_CI_AS");
            entity.Property(e => e.IdChuDe).HasColumnName("ID_ChuDe");
            entity.Property(e => e.NoiDungHan).UseCollation("Vietnamese_CI_AS");
            entity.Property(e => e.NoiDungPinyin).UseCollation("Vietnamese_CI_AS");
            entity.Property(e => e.NoiDungViet).UseCollation("Vietnamese_CI_AS");
            entity.Property(e => e.TieuDe)
                .HasMaxLength(255)
                .UseCollation("Vietnamese_CI_AS");

            entity.HasOne(d => d.IdChuDeNavigation).WithMany(p => p.DoanVans)
                .HasForeignKey(d => d.IdChuDe)
                .HasConstraintName("FK__DoanVan__ID_ChuD__412EB0B6");
        });

        modelBuilder.Entity<GhiChu>(entity =>
        {
            entity.HasKey(e => e.IdGhiChu);

            entity.ToTable("GhiChu");

            entity.HasIndex(e => e.IdUser, "IX_GhiChu_ID_User");

            entity.Property(e => e.IdGhiChu).HasColumnName("ID_GhiChu");
            entity.Property(e => e.DuongLinkFile).HasMaxLength(500);
            entity.Property(e => e.IdUser).HasColumnName("ID_User");
            entity.Property(e => e.NgayCapNhat)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.NgayTao)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.TenFile).HasMaxLength(255);
            entity.Property(e => e.TieuDe).HasMaxLength(255);

            entity.HasOne(d => d.IdUserNavigation).WithMany(p => p.GhiChus)
                .HasForeignKey(d => d.IdUser)
                .HasConstraintName("FK_GhiChu_NguoiDung");
        });

        modelBuilder.Entity<GiangVienLopHoc>(entity =>
        {
            entity.HasKey(e => new { e.IdLopHoc, e.IdTaiKhoan });

            entity.ToTable("GiangVienLopHoc");

            entity.Property(e => e.IdLopHoc).HasColumnName("ID_LopHoc");
            entity.Property(e => e.IdTaiKhoan).HasColumnName("ID_TaiKhoan");
            entity.Property(e => e.NgayPhanCong)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.VaiTroDay)
                .HasMaxLength(50)
                .HasDefaultValue("Giảng viên chính");

            entity.HasOne(d => d.IdLopHocNavigation).WithMany(p => p.GiangVienLopHocs)
                .HasForeignKey(d => d.IdLopHoc)
                .HasConstraintName("FK_GiangVienLopHoc_LopHoc");

            entity.HasOne(d => d.IdTaiKhoanNavigation).WithMany(p => p.GiangVienLopHocs)
                .HasForeignKey(d => d.IdTaiKhoan)
                .HasConstraintName("FK_GiangVienLopHoc_TaiKhoan");
        });

        modelBuilder.Entity<GiaoDich>(entity =>
        {
            entity.HasKey(e => e.IdGiaoDich).HasName("PK__GiaoDich__C48DF7DC8FD83FF2");

            entity.ToTable("GiaoDich");

            entity.Property(e => e.IdGiaoDich).HasColumnName("ID_GiaoDich");
            entity.Property(e => e.IdGoi).HasColumnName("ID_Goi");
            entity.Property(e => e.IdTaiKhoan).HasColumnName("ID_TaiKhoan");
            entity.Property(e => e.MaGiaoDich)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.NgayGiaoDich)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.NoiDung).HasMaxLength(250);
            entity.Property(e => e.TrangThai).HasMaxLength(50);

            entity.HasOne(d => d.IdGoiNavigation).WithMany(p => p.GiaoDiches)
                .HasForeignKey(d => d.IdGoi)
                .HasConstraintName("FK_GiaoDich_BangGia");

            entity.HasOne(d => d.IdTaiKhoanNavigation).WithMany(p => p.GiaoDiches)
                .HasForeignKey(d => d.IdTaiKhoan)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_GiaoDich_TaiKhoan");
        });

        modelBuilder.Entity<HocVienLopHoc>(entity =>
        {
            entity.HasKey(e => new { e.IdUser, e.IdLopHoc });

            entity.ToTable("HocVienLopHoc");

            entity.Property(e => e.IdUser).HasColumnName("ID_User");
            entity.Property(e => e.IdLopHoc).HasColumnName("ID_LopHoc");
            entity.Property(e => e.NgayThamGia)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");

            entity.HasOne(d => d.IdLopHocNavigation).WithMany(p => p.HocVienLopHocs)
                .HasForeignKey(d => d.IdLopHoc)
                .HasConstraintName("FK_HocVienLopHoc_LopHoc");

            entity.HasOne(d => d.IdUserNavigation).WithMany(p => p.HocVienLopHocs)
                .HasForeignKey(d => d.IdUser)
                .HasConstraintName("FK_HocVienLopHoc_NguoiDung");
        });

        modelBuilder.Entity<KetQuaThi>(entity =>
        {
            entity.HasKey(e => e.IdKetQua).HasName("PK__KetQuaTh__B34CED6C24924A3D");

            entity.ToTable("KetQuaThi");

            entity.Property(e => e.DiemSo).HasColumnType("decimal(5, 2)");
            entity.Property(e => e.HoTen)
                .HasMaxLength(255)
                .UseCollation("Vietnamese_CI_AS");
            entity.Property(e => e.SoCauDung).HasDefaultValue(0);
            entity.Property(e => e.ThoiGianBatDau)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.ThoiGianHoanThanh)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.TrangThai).HasDefaultValue(true);

            entity.HasOne(d => d.IdDeThiNavigation).WithMany(p => p.KetQuaThis)
                .HasForeignKey(d => d.IdDeThi)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_KetQuaThi_DeThi");

            entity.HasOne(d => d.IdUserNavigation).WithMany(p => p.KetQuaThis)
                .HasForeignKey(d => d.IdUser)
                .HasConstraintName("FK_KetQuaThi_NguoiDung");
        });

        modelBuilder.Entity<LichSuLamBai>(entity =>
        {
            entity.HasKey(e => e.IdLichSu).HasName("PK__LichSuLa__823B17723FD88A8D");

            entity.ToTable("LichSuLamBai");

            entity.Property(e => e.DanhSachIdCauHoi).UseCollation("Vietnamese_CI_AS");
            entity.Property(e => e.NgayLam)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.SoCauDung).HasDefaultValue(0);
            entity.Property(e => e.TenDe)
                .HasMaxLength(200)
                .UseCollation("Vietnamese_CI_AS");
            entity.Property(e => e.TongSoCau).HasDefaultValue(0);

            entity.HasOne(d => d.IdCapDoNavigation).WithMany(p => p.LichSuLamBais)
                .HasForeignKey(d => d.IdCapDo)
                .HasConstraintName("FK_LichSu_CapDo");

            entity.HasOne(d => d.IdLoaiNavigation).WithMany(p => p.LichSuLamBais)
                .HasForeignKey(d => d.IdLoai)
                .HasConstraintName("FK_LichSu_LoaiCauHoi");

            entity.HasOne(d => d.IdNguoiDungNavigation).WithMany(p => p.LichSuLamBais)
                .HasForeignKey(d => d.IdNguoiDung)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_LichSu_NguoiDung");
        });

        modelBuilder.Entity<LichSuThayDoi>(entity =>
        {
            entity.HasKey(e => e.IdLichSu);

            entity.ToTable("LichSuThayDoi");

            entity.HasIndex(e => e.IdTaiKhoan, "IX_LichSuThayDoi_ID_TaiKhoan");

            entity.HasIndex(e => e.ThoiGian, "IX_LichSuThayDoi_ThoiGian").IsDescending();

            entity.Property(e => e.DiaChiIp)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.HanhDong).HasMaxLength(50);
            entity.Property(e => e.IdTaiKhoan).HasColumnName("ID_TaiKhoan");
            entity.Property(e => e.MoTa).HasMaxLength(500);
            entity.Property(e => e.ThoiGian)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.UserAgent).HasMaxLength(500);

            entity.HasOne(d => d.IdTaiKhoanNavigation).WithMany(p => p.LichSuThayDois)
                .HasForeignKey(d => d.IdTaiKhoan)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("FK_LichSuThayDoi_TaiKhoan");
        });

        modelBuilder.Entity<LichSuTruyCap>(entity =>
        {
            entity.HasKey(e => e.IdTruyCap).HasName("PK__LichSuTr__CFB8EDD5B5C4471B");

            entity.ToTable("LichSuTruyCap");

            entity.Property(e => e.IdTruyCap).HasColumnName("ID_TruyCap");
            entity.Property(e => e.DiaChiIp)
                .HasMaxLength(50)
                .HasColumnName("DiaChiIP");
            entity.Property(e => e.IdTaiKhoan).HasColumnName("ID_TaiKhoan");
            entity.Property(e => e.ThietBi).HasMaxLength(255);
            entity.Property(e => e.ThoiGian)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");

            entity.HasOne(d => d.IdTaiKhoanNavigation).WithMany(p => p.LichSuTruyCaps)
                .HasForeignKey(d => d.IdTaiKhoan)
                .HasConstraintName("FK_TaiKhoan_LichSu");
        });

        modelBuilder.Entity<LoaiCauHoi>(entity =>
        {
            entity.HasKey(e => e.IdLoai).HasName("PK__LoaiCauH__914C2314A5BFB160");

            entity.ToTable("LoaiCauHoi");

            entity.Property(e => e.IdLoai).HasColumnName("ID_Loai");
            entity.Property(e => e.TenLoai)
                .HasMaxLength(100)
                .UseCollation("Vietnamese_CI_AS");
        });

        modelBuilder.Entity<LoaiDeThi>(entity =>
        {
            entity.HasKey(e => e.IdLoai).HasName("PK__LoaiDeTh__38DBC3307E5DF831");

            entity.ToTable("LoaiDeThi");

            entity.Property(e => e.MoTa)
                .HasMaxLength(255)
                .UseCollation("Vietnamese_CI_AS");
            entity.Property(e => e.TenLoai)
                .HasMaxLength(100)
                .UseCollation("Vietnamese_CI_AS");
        });

        modelBuilder.Entity<LopHoc>(entity =>
        {
            entity.HasKey(e => e.IdLopHoc).HasName("PK__LopHoc__5C25E9AE5117719B");

            entity.ToTable("LopHoc");

            entity.HasIndex(e => new { e.IdTrungTam, e.MaLopHoc }, "UQ_LopHoc_TrungTam_MaLop")
                .IsUnique()
                .HasFilter("([MaLopHoc] IS NOT NULL)");

            entity.Property(e => e.IdLopHoc).HasColumnName("ID_LopHoc");
            entity.Property(e => e.IdTrungTam).HasColumnName("ID_TrungTam");
            entity.Property(e => e.MaLopHoc).HasMaxLength(50);
            entity.Property(e => e.TenLopHoc).HasMaxLength(200);
            entity.Property(e => e.TrangThai).HasDefaultValue(true);

            entity.HasOne(d => d.IdTrungTamNavigation).WithMany(p => p.LopHocs)
                .HasForeignKey(d => d.IdTrungTam)
                .HasConstraintName("FK_LopHoc_TrungTam");
        });

        modelBuilder.Entity<LuyenNgheHinhAnh>(entity =>
        {
            entity.HasKey(e => e.IdLuyenNghe).HasName("PK__LuyenNgh__C594E58024F4F170");

            entity.ToTable("LuyenNgheHinhAnh");

            entity.Property(e => e.IdLuyenNghe).HasColumnName("ID_LuyenNghe");
            entity.Property(e => e.IdCapDo).HasColumnName("ID_CapDo");
            entity.Property(e => e.TieuDe).HasMaxLength(250);
        });

        modelBuilder.Entity<LuyenNgheNguoiDung>(entity =>
        {
            entity.HasKey(e => new { e.IdUser, e.IdChiTiet });

            entity.ToTable("LuyenNghe_NguoiDung");

            entity.Property(e => e.IdUser).HasColumnName("ID_User");
            entity.Property(e => e.IdChiTiet).HasColumnName("ID_ChiTiet");
            entity.Property(e => e.NgayDanhDau)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");

            entity.HasOne(d => d.IdChiTietNavigation).WithMany(p => p.LuyenNgheNguoiDungs)
                .HasForeignKey(d => d.IdChiTiet)
                .HasConstraintName("FK_LuyenNgheNguoiDung_ChiTiet");
        });

        modelBuilder.Entity<NguPhap>(entity =>
        {
            entity.HasKey(e => e.IdNguPhap).HasName("PK__NguPhap__3001C1290C7CEF07");

            entity.ToTable("NguPhap");

            entity.Property(e => e.IdNguPhap).HasColumnName("ID_NguPhap");
            entity.Property(e => e.CauTruc).UseCollation("Vietnamese_CI_AS");
            entity.Property(e => e.GiaiThich).UseCollation("Vietnamese_CI_AS");
            entity.Property(e => e.IdCapDo).HasColumnName("ID_CapDo");
            entity.Property(e => e.IdTrungTam).HasColumnName("ID_TrungTam");
            entity.Property(e => e.TenNguPhap)
                .HasMaxLength(255)
                .UseCollation("Vietnamese_CI_AS");
            entity.Property(e => e.ViDu).UseCollation("Vietnamese_CI_AS");

            entity.HasOne(d => d.IdCapDoNavigation).WithMany(p => p.NguPhaps)
                .HasForeignKey(d => d.IdCapDo)
                .HasConstraintName("FK_NguPhap_CapDo");

            entity.HasOne(d => d.IdTrungTamNavigation).WithMany(p => p.NguPhaps)
                .HasForeignKey(d => d.IdTrungTam)
                .HasConstraintName("FK_NguPhap_TrungTam");
        });

        modelBuilder.Entity<NguoiDung>(entity =>
        {
            entity.HasKey(e => e.IdUser).HasName("PK__NguoiDun__ED4DE4423AD3E208");

            entity.ToTable("NguoiDung");

            entity.HasIndex(e => e.IdTaiKhoan, "UQ__NguoiDun__0E3EC211F438AE47").IsUnique();

            entity.Property(e => e.IdUser).HasColumnName("ID_User");
            entity.Property(e => e.AnhDaiDien)
                .HasMaxLength(255)
                .IsUnicode(false)
                .UseCollation("Vietnamese_CI_AS");
            entity.Property(e => e.HoTen)
                .HasMaxLength(100)
                .UseCollation("Vietnamese_CI_AS");
            entity.Property(e => e.IdTaiKhoan).HasColumnName("ID_TaiKhoan");

            entity.HasOne(d => d.IdTaiKhoanNavigation).WithOne(p => p.NguoiDung)
                .HasForeignKey<NguoiDung>(d => d.IdTaiKhoan)
                .HasConstraintName("FK__NguoiDung__ID_Ta__787EE5A0");
        });

        modelBuilder.Entity<TaiKhoan>(entity =>
        {
            entity.HasKey(e => e.IdTaiKhoan).HasName("PK__TaiKhoan__0E3EC2109722F1DF");

            entity.ToTable("TaiKhoan");

            entity.HasIndex(e => e.TenDangNhap, "UQ__TaiKhoan__55F68FC05FC7B68B").IsUnique();

            entity.Property(e => e.IdTaiKhoan).HasColumnName("ID_TaiKhoan");
            entity.Property(e => e.IdTrungTam).HasColumnName("ID_TrungTam");
            entity.Property(e => e.IdVaiTro).HasColumnName("ID_VaiTro");
            entity.Property(e => e.MatKhau)
                .HasMaxLength(255)
                .IsUnicode(false)
                .UseCollation("Vietnamese_CI_AS");
            entity.Property(e => e.NgayBatDauVip).HasColumnType("datetime");
            entity.Property(e => e.NgayKetThucVip).HasColumnType("datetime");
            entity.Property(e => e.NgayTao)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.TenDangNhap)
                .HasMaxLength(50)
                .IsUnicode(false)
                .UseCollation("Vietnamese_CI_AS");
            entity.Property(e => e.TrangThai).HasDefaultValue(true);

            entity.HasOne(d => d.IdTrungTamNavigation).WithMany(p => p.TaiKhoans)
                .HasForeignKey(d => d.IdTrungTam)
                .HasConstraintName("FK_TaiKhoan_TrungTam");

            entity.HasOne(d => d.IdVaiTroNavigation).WithMany(p => p.TaiKhoans)
                .HasForeignKey(d => d.IdVaiTro)
                .HasConstraintName("FK__TaiKhoan__ID_Vai__74AE54BC");
        });

        modelBuilder.Entity<TaiLieu>(entity =>
        {
            entity.HasKey(e => e.IdTaiLieu);

            entity.ToTable("TaiLieu");

            entity.Property(e => e.IdTaiLieu).HasColumnName("ID_TaiLieu");
            entity.Property(e => e.DuongLink)
                .HasMaxLength(500)
                .IsUnicode(false);
            entity.Property(e => e.HinhAnh)
                .HasMaxLength(500)
                .IsUnicode(false);
            entity.Property(e => e.IdCapDo).HasColumnName("ID_CapDo");
            entity.Property(e => e.NgayTao)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.TenTaiLieu).HasMaxLength(250);
            entity.Property(e => e.TrangThai).HasDefaultValue(true);

            entity.HasOne(d => d.IdCapDoNavigation).WithMany(p => p.TaiLieus)
                .HasForeignKey(d => d.IdCapDo)
                .HasConstraintName("FK_TaiLieu_CapDo");
        });

        modelBuilder.Entity<TrungTam>(entity =>
        {
            entity.HasKey(e => e.IdTrungTam).HasName("PK__TrungTam__A33E4C3737CD3DF6");

            entity.ToTable("TrungTam");

            entity.HasIndex(e => e.MaTrungTam, "UQ__TrungTam__54A2B84E8363B8B8").IsUnique();

            entity.HasIndex(e => e.Subdomain, "UQ__TrungTam__AD95567DB5370DCD").IsUnique();

            entity.Property(e => e.IdTrungTam).HasColumnName("ID_TrungTam");
            entity.Property(e => e.DiaChi).HasMaxLength(500);
            entity.Property(e => e.Email).HasMaxLength(255);
            entity.Property(e => e.Logo).HasMaxLength(500);
            entity.Property(e => e.MaTrungTam)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.NgayHetHan).HasColumnType("datetime");
            entity.Property(e => e.NgayKichHoat)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.SoDienThoai).HasMaxLength(255);
            entity.Property(e => e.Subdomain)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.TenTrungTam).HasMaxLength(255);
            entity.Property(e => e.TrangThai).HasDefaultValue(true);
        });

        modelBuilder.Entity<TuVung>(entity =>
        {
            entity.HasKey(e => e.IdTuVung).HasName("PK__TuVung__C7BE150DD44F2DD0");

            entity.ToTable("TuVung");

            entity.Property(e => e.IdTuVung).HasColumnName("ID_TuVung");
            entity.Property(e => e.AmHan)
                .HasMaxLength(100)
                .UseCollation("Vietnamese_CI_AS");
            entity.Property(e => e.AmThanhPath)
                .HasMaxLength(255)
                .IsUnicode(false)
                .UseCollation("Vietnamese_CI_AS");
            entity.Property(e => e.ChuHan)
                .HasMaxLength(255)
                .UseCollation("Vietnamese_CI_AS");
            entity.Property(e => e.IdCapDo).HasColumnName("ID_CapDo");
            entity.Property(e => e.LoaiTu)
                .HasMaxLength(50)
                .UseCollation("Vietnamese_CI_AS");
            entity.Property(e => e.NghiaViet)
                .HasMaxLength(500)
                .UseCollation("Vietnamese_CI_AS");
            entity.Property(e => e.Pinyin).HasMaxLength(200);

            entity.HasOne(d => d.IdCapDoNavigation).WithMany(p => p.TuVungs)
                .HasForeignKey(d => d.IdCapDo)
                .HasConstraintName("FK__TuVung__ID_CapDo__398D8EEE");
        });

        modelBuilder.Entity<TuVungNguoiDung>(entity =>
        {
            entity.HasKey(e => new { e.IdUser, e.IdTuVung }).HasName("PK__TuVung_N__21360512764306F0");

            entity.ToTable("TuVung_NguoiDung");

            entity.Property(e => e.IdUser).HasColumnName("ID_User");
            entity.Property(e => e.IdTuVung).HasColumnName("ID_TuVung");
            entity.Property(e => e.DaThuoc).HasDefaultValue(true);
            entity.Property(e => e.NgayDanhDau)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");

            entity.HasOne(d => d.IdTuVungNavigation).WithMany(p => p.TuVungNguoiDungs)
                .HasForeignKey(d => d.IdTuVung)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__TuVung_Ng__ID_Tu__7C4F7684");

            entity.HasOne(d => d.IdUserNavigation).WithMany(p => p.TuVungNguoiDungs)
                .HasForeignKey(d => d.IdUser)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__TuVung_Ng__ID_Us__7B5B524B");
        });

        modelBuilder.Entity<VaiTro>(entity =>
        {
            entity.HasKey(e => e.IdVaiTro).HasName("PK__VaiTro__6A43AA5ACAF1D5CF");

            entity.ToTable("VaiTro");

            entity.Property(e => e.IdVaiTro).HasColumnName("ID_VaiTro");
            entity.Property(e => e.TenVaiTro)
                .HasMaxLength(50)
                .UseCollation("Vietnamese_CI_AS");
        });

        modelBuilder.Entity<VideoLuyenNghe>(entity =>
        {
            entity.HasKey(e => e.IdVideo).HasName("PK__VideoLuy__D8A30AE0AA97F599");

            entity.ToTable("VideoLuyenNghe");

            entity.Property(e => e.IdVideo).HasColumnName("ID_Video");
            entity.Property(e => e.IdCapDo).HasColumnName("ID_CapDo");
            entity.Property(e => e.MoTa).UseCollation("Vietnamese_CI_AS");
            entity.Property(e => e.NgayTao)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.TieuDe)
                .HasMaxLength(255)
                .UseCollation("Vietnamese_CI_AS");
            entity.Property(e => e.VideoPath)
                .HasMaxLength(500)
                .UseCollation("Vietnamese_CI_AS");

            entity.HasOne(d => d.IdCapDoNavigation).WithMany(p => p.VideoLuyenNghes)
                .HasForeignKey(d => d.IdCapDo)
                .HasConstraintName("FK_Video_CapDo");
        });

        modelBuilder.Entity<VideoSubtitle>(entity =>
        {
            entity.HasKey(e => e.IdSub).HasName("PK__VideoSub__27F89E8895AB9E4F");

            entity.Property(e => e.IdSub).HasColumnName("ID_Sub");
            entity.Property(e => e.DichNghia).UseCollation("Vietnamese_CI_AS");
            entity.Property(e => e.EndTime).HasPrecision(3);
            entity.Property(e => e.HanTu).UseCollation("Vietnamese_CI_AS");
            entity.Property(e => e.IdVideo).HasColumnName("ID_Video");
            entity.Property(e => e.Pinyin).UseCollation("Vietnamese_CI_AS");
            entity.Property(e => e.StartTime).HasPrecision(3);

            entity.HasOne(d => d.IdVideoNavigation).WithMany(p => p.VideoSubtitles)
                .HasForeignKey(d => d.IdVideo)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Sub_Video");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
