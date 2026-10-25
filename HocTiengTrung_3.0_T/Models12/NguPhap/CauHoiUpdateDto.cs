using static HocTiengTrung_3._0_T.ControllersAPI.QuanLyNguPhap;

namespace HocTiengTrung_3._0_T.Models12.NguPhap
{
    public class CauHoiUpdateDto
    {
        public string NoiDung { get; set; }
        public int? IdLoai { get; set; }
        public string GiaiThich { get; set; }
        public List<DapAnDto> DapAns { get; set; }
    }
    public class DapAnDto
    {
        public string NoiDungDapAn { get; set; }
        public bool LaDapAnDung { get; set; }
    }
}
