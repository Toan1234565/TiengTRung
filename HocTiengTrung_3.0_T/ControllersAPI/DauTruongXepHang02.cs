using HocTiengTrung_3._0_T.Models12.DauTruongSieuToc;
using HocTiengTrung_3._0_T.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace HocTiengTrung_3._0_T.Controllers
{
    [ApiController]
    [Route("api/DauTruongTH2")]
    public class DauTruongXepHang02 : ControllerBase
    {
        private readonly IDauTruongTH2Service _service;
        private readonly IHubContext<DauTruongTH2Hub> _hubContext;

        public DauTruongXepHang02(IDauTruongTH2Service service, IHubContext<DauTruongTH2Hub> hubContext)
        {
            _service = service;
            _hubContext = hubContext;
        }

        [HttpPost("tao-phong")]
        public async Task<IActionResult> TaoPhong([FromBody] TaoPhongTH2RequestDTO request)
        {
            try
            {
                var result = await _service.TaoPhongAsync(request);

                // Lưu ý: Khi chủ phòng vừa tạo phòng thành công, 
                // client nên Join vào SignalR Group của phòng ngay ở phía Frontend sau khi nhận response này.
                return Ok(new { success = true, data = result });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        [HttpPost("vao-phong")]
        public async Task<IActionResult> VaoPhong([FromBody] VaoPhongTH2RequestDTO request)
        {
            try
            {
                var result = await _service.VaoPhongAsync(request);
                await _hubContext.Clients.Group(request.MaPhong).SendAsync("RoomUpdated", result);
                return Ok(new { success = true, data = result });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { success = false, message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "Lỗi hệ thống: " + ex.Message });
            }
        }

        [HttpGet("thong-tin-phong/{maPhong}")]
        public async Task<IActionResult> GetThongTinPhong(string maPhong)
        {
            try
            {
                var result = await _service.GetThongTinPhongAsync(maPhong);
                return Ok(new { success = true, data = result });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { success = false, message = ex.Message });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        [HttpPost("san-sang")]
        public async Task<IActionResult> SanSang([FromBody] SanSangTH2RequestDTO request)
        {
            try
            {
                var result = await _service.SanSangAsync(request);
                await _hubContext.Clients.Group(request.MaPhong).SendAsync("RoomUpdated", result);
                return Ok(new { success = true, data = result });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { success = false, message = ex.Message });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        [HttpPost("kick")]
        public async Task<IActionResult> KickNguoiChoi([FromBody] KickNguoiChoiTH2RequestDTO request)
        {
            try
            {
                var result = await _service.KickNguoiChoiAsync(request);
                await _hubContext.Clients.Group(request.MaPhong).SendAsync("PlayerKicked", request.IdNguoiChoiBiKick);
                await _hubContext.Clients.Group(request.MaPhong).SendAsync("RoomUpdated", result);
                return Ok(new { success = true, data = result });
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(403, new { success = false, message = ex.Message });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { success = false, message = ex.Message });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        [HttpPost("roi-phong")]
        public async Task<IActionResult> RoiPhong([FromBody] RoiPhongTH2RequestDTO request)
        {
            try
            {
                var result = await _service.RoiPhongAsync(request);
                if (result != null)
                {
                    await _hubContext.Clients.Group(request.MaPhong).SendAsync("RoomUpdated", result);
                }
                return Ok(new { success = true, data = result });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { success = false, message = ex.Message });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        [HttpPost("bat-dau")]
        public async Task<IActionResult> BatDau([FromBody] BatDauTH2RequestDTO request)
        {
            try
            {
                var result = await _service.BatDauAsync(request);
                await _hubContext.Clients.Group(request.MaPhong).SendAsync("GameStarted", result);
                return Ok(new { success = true, data = result });
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(403, new { success = false, message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { success = false, message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "Lỗi hệ thống: " + ex.Message });
            }
        }

        [HttpPost("tra-loi")]
        public async Task<IActionResult> TraLoi([FromBody] TraLoiTH2RequestDTO request)
        {
            try
            {
                var result = await _service.XuLyTraLoiAsync(request);

                if (result.Success)
                {
                    var bangXepHang = await _service.GetBangXepHangAsync(request.MaPhong);
                    await _hubContext.Clients.Group(request.MaPhong).SendAsync("LeaderboardUpdated", bangXepHang);
                }

                return Ok(new { success = result.Success, message = result.Message, data = result });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        [HttpPost("nop-bai")]
        public async Task<IActionResult> NopBai([FromBody] NopBaiTH2RequestDTO request)
        {
            try
            {
                var result = await _service.NopBaiAsync(request);

                await _hubContext.Clients.Group(request.MaPhong).SendAsync("PlayerSubmitted", new
                {
                    UserId = request.IdNguoiChoi,
                    TrangThai = request.TrangThai.ToString()
                });

                if (result.IsAllFinished)
                {
                    var bangXepHang = await _service.GetBangXepHangAsync(request.MaPhong);
                    await _hubContext.Clients.Group(request.MaPhong).SendAsync("AllPlayersFinished", bangXepHang);
                }

                return Ok(new { success = true, data = result });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { success = false, message = ex.Message });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        [HttpGet("bang-xep-hang/{maPhong}")]
        public async Task<IActionResult> BangXepHang(string maPhong)
        {
            try
            {
                var result = await _service.GetBangXepHangAsync(maPhong);
                return Ok(new { success = true, data = result });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { success = false, message = ex.Message });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        [HttpPost("xem-lai-bai")]
        public async Task<IActionResult> XemLaiBai([FromBody] XemLaiBaiTH2RequestDTO request)
        {
            try
            {
                var success = await _service.XemLaiBaiAsync(request);
                if (success)
                {
                    var phongInfo = await _service.GetThongTinPhongAsync(request.MaPhong);
                    await _hubContext.Clients.Group(request.MaPhong).SendAsync("RoomUpdated", phongInfo);
                }
                return Ok(new { success });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { success = false, message = ex.Message });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        [HttpPost("quay-lai-phong")]
        public async Task<IActionResult> QuayLaiPhong([FromBody] QuayLaiPhongTH2RequestDTO request)
        {
            try
            {
                var result = await _service.QuayLaiPhongAsync(request);
                await _hubContext.Clients.Group(request.MaPhong).SendAsync("RoomUpdated", result);
                return Ok(new { success = true, data = result });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { success = false, message = ex.Message });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }
    }
}