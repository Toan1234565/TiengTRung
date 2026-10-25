namespace HocTiengTrung_3._0_T.Models12.PhanTichBoThu
{
    public class BoThuDto
    {
        public int IdBoThu { get; set; }
        public int Stt { get; set; }
        public string BoThu1 { get; set; } = null!;
        public string? BienThe { get; set; }
        public string HanViet { get; set; } = null!;
        public string Pinyin { get; set; } = null!;
        public string? Ynghia { get; set; }
        public int SoNet { get; set; }
        public bool TrangThai { get; set; }
    }

    public class BoThuCreateUpdateDto
    {
        public int Stt { get; set; }
        public string BoThu1 { get; set; } = null!;
        public string? BienThe { get; set; }
        public string HanViet { get; set; } = null!;
        public string Pinyin { get; set; } = null!;
        public string? Ynghia { get; set; }
        public int SoNet { get; set; }
        public bool TrangThai { get; set; } = true;
    }
}
