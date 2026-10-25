namespace HocTiengTrung_3._0_T.Models12.TuVung
{
    public class KiemTraDapAnRequest
    {
        public int IdTuVung { get; set; }
        public string CauTraLoi { get; set; }
        public int KieuCauHoi { get; set; } // 1: Hán -> Việt, 2: Việt -> Hán
    }
}
