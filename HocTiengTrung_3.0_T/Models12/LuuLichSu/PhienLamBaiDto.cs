namespace HocTiengTrung_3._0_T.Models12.LuuLichSu
{
    public class PhienLamBaiDto
    {
        public int IdCapDo { get; set; }
        public int IdLoai { get; set; }
        
        public int SoCauDung { get; set; }
        public int TongSoCau { get; set; }
        public string? TenDe { get; set; }
        public List<ChiTietKetQuaDto> ChiTiets { get; set; }
    }
}
