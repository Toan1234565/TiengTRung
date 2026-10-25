namespace HocTiengTrung_3._0_T.Models12.TuVung
{
    public class CauViDuModes
    {
        public int IdViDu { get; set; }

        public int? IdTuVung { get; set; }

        public string? NoiDungHan { get; set; }

        public string? NoiDungPinyin { get; set; }

        public string? NoiDungViet { get; set; }

        public string? AmThanh { get; set; }

        public virtual TuVungModes? IdTuVungNavigation { get; set; }
    }
}
