namespace HocTiengTrung_3._0_T.Models12.TuVung
{
    public class EditTuVung
    {
        public int IdTuVung { get; set; }

        public string ChuHan { get; set; } = null!;

        public string? Pinyin { get; set; }

        public string? NghiaViet { get; set; }

        public string? LoaiTu { get; set; }

        public string? AmThanhPath { get; set; }

        public int? IdCapDo { get; set; }

        public string? AmHan { get; set; }

        public virtual ICollection<EditCauViDu> CauViDus { get; set; } = new List<EditCauViDu>();
    }

    public class EditCauViDu
    {
        public int IdViDu { get; set; }

        public int? IdTuVung { get; set; }

        public string? NoiDungHan { get; set; }

        public string? NoiDungPinyin { get; set; }

        public string? NoiDungViet { get; set; }

        public string? AmThanhPath { get; set; }
    }
}
