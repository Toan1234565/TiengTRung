using HocTiengTrung_3._0_T.Models12.DauTruongSieuToc;
using System.Threading.Tasks;

namespace HocTiengTrung_3._0_T.Services
{
    public interface IDauTruongTH2Service
    {
        Task<PhongDauTH2ResponseDTO> TaoPhongAsync(TaoPhongTH2RequestDTO request);
        Task<PhongDauTH2ResponseDTO> VaoPhongAsync(VaoPhongTH2RequestDTO request);
        Task<PhongDauTH2ResponseDTO> GetThongTinPhongAsync(string maPhong);
        Task<PhongDauTH2ResponseDTO> SanSangAsync(SanSangTH2RequestDTO request);
        Task<PhongDauTH2ResponseDTO> KickNguoiChoiAsync(KickNguoiChoiTH2RequestDTO request);
        Task<PhongDauTH2ResponseDTO> RoiPhongAsync(RoiPhongTH2RequestDTO request);
        Task<ThongTinTranDauTH2ResponseDTO> BatDauAsync(BatDauTH2RequestDTO request);
        Task<NopBaiTH2ResponseDTO> NopBaiAsync(NopBaiTH2RequestDTO request);
        Task<BangXepHangTH2ResponseDTO> GetBangXepHangAsync(string maPhong);
        Task<bool> XemLaiBaiAsync(XemLaiBaiTH2RequestDTO request);
        Task<PhongDauTH2ResponseDTO> QuayLaiPhongAsync(QuayLaiPhongTH2RequestDTO request);
        Task<TraLoiTH2ResponseDTO> XuLyTraLoiAsync(TraLoiTH2RequestDTO request);
    }
}