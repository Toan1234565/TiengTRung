namespace HocTiengTrung_3._0_T.Models12.QuanLyDeThi
{    
    public class ApiResponseKetQuaToanBo
    {
        public bool Success { get; set; }
        public KetQuaThiViewModel Info { get; set; }  // Thuộc tính này phải là "Info" hoặc "info"
        public List<ChiTietKetQuaModel> Details { get; set; } // Thuộc tính này phải là "Details" hoặc "details"
        public string Message { get; set; }
    }
}
