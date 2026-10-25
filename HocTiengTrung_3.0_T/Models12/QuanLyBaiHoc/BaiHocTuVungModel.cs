using HocTiengTrung_3._0_T.Models;
using HocTiengTrung_3._0_T.Models12.TuVung;
using System.ComponentModel.DataAnnotations.Schema;

namespace HocTiengTrung_3._0_T.Models12.QuanLyBaiHoc
{
    public class BaiHocTuVungModel
    {

        public int IdBaiHoc { get; set; }

        public int IdTuVung { get; set; }

        public bool LaTuMoi { get; set; }

        public int ThuTu { get; set; }  

        public virtual TuVungBaiHocModel IdTuVungNavigation { get; set; } = null!;
    }
    public class TuVungBaiHocModel 
    {
        public int IdTuVung { get; set; }

        public string ChuHan { get; set; } = null!;

        public string? Pinyin { get; set; }

        public string? NghiaViet { get; set; }

        public string? LoaiTu { get; set; }

        public string? AmThanhPath { get; set; }
      
        public string? AmHan { get; set; }

        public virtual ICollection<CauViDuModes> CauViDus { get; set; } = new List<CauViDuModes>();


    }
}
