using HocTiengTrung_3._0_T.Models;
using System.ComponentModel.DataAnnotations.Schema;

namespace HocTiengTrung_3._0_T.Models12.TuVung
{
    public class TuVungModes
    {
        public int IdTuVung { get; set; }

        public string ChuHan { get; set; } = null!;

        public string? Pinyin { get; set; }

        public string? NghiaViet { get; set; }

        public string? LoaiTu { get; set; }

        public string? AmThanhPath { get; set; }

        public int? IdCapDo { get; set; }

        public string? AmHan { get; set; }

        [NotMapped] // Dòng này cực kỳ quan trọng: Nó báo cho C# biết "Đừng tìm cột này trong bảng TuVung ở SQL"
        public bool IsLearned { get; set; } = false;

        public virtual ICollection<CauViDuModes> CauViDus { get; set; } = new List<CauViDuModes>();

        public virtual CapDoModels? IdCapDoNavigation { get; set; }
       
        public int KieuCauHoi { get; set; }
    }
}
