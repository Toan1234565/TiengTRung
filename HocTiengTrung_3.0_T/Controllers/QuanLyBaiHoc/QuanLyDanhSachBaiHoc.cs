using HocTiengTrung_3._0_T.Controllers.QuanLyBaiTap;
using HocTiengTrung_3._0_T.Models12;
using HocTiengTrung_3._0_T.Models12.BaiTap;
using HocTiengTrung_3._0_T.Models12.QuanLyBaiHoc;
using HocTiengTrung_3._0_T.Models12.TuVung;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;

namespace HocTiengTrung_3._0_T.Controllers.QuanLyBaiHoc
{
    public class QuanLyDanhSachBaiHoc : Controller
    {
        private readonly TmdtContext _context;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<QuanLyDanhSachBaiHoc> _logger;
        // Chú ý: Đảm bảo port 7151 khớp với dự án API của bạn
        private readonly string apiBaseUrl;
        private readonly string apiBaseUrl1;
        private readonly IUserService _userService;

        // SỬA: Thêm IConfiguration vào tham số của Constructor
        public QuanLyDanhSachBaiHoc(IHttpClientFactory httpClientFactory, ILogger<QuanLyDanhSachBaiHoc> logger, IConfiguration configuration, TmdtContext context, IUserService userService)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
            _context = context;

            // 1. Lấy link gốc từ appsettings.json (http://Hoctiengtrung-001-site1.jtempurl.com)
            // Nếu không tìm thấy trong file cấu hình, mặc định sẽ lấy localhost để chạy ở máy
            //string baseUrl = configuration["ApiSettings:BaseUrl"] ?? "https://localhost:7151";
            string baseUrl = configuration["ApiSettings:BaseUrl"] ?? "https://localhost:7151";
            //string baseUrl = "https://localhost:7151";
            // 2. Đảm bảo không bị thừa dấu "/" ở cuối link gốc
            baseUrl = baseUrl.TrimEnd('/');


            // 3. Gán giá trị cho các đường dẫn API cụ thể
            // Kết quả sẽ là: http://Hoctiengtrung-001-site1.jtempurl.com/api/quanlytuvung
            apiBaseUrl = $"{baseUrl}/api/quanlybaihoc";
            _userService = userService;
        }
        public async Task<IActionResult> DanhSachBaiHoc(int capdo, int idGiaoTrinh, int page = 1)
        {
            // 1. Đảm bảo số trang tối thiểu luôn lớn hơn hoặc bằng 1
            if (page < 1) page = 1;

            var client = _httpClientFactory.CreateClient("BypassSSL");

            // =========================================================================
            // 2. CHUYỂN TIẾP XÁC THỰC (COOKIE / TOKEN) TỪ MVC SANG API
            // =========================================================================
            // Forward Cookie đăng nhập hiện tại sang API để API đọc được Claims & Session
            if (Request.Headers.ContainsKey("Cookie"))
            {
                client.DefaultRequestHeaders.Add("Cookie", Request.Headers["Cookie"].ToString());
            }

            // Forward Bearer Token nếu bạn có dùng JWT Authorization header
            if (Request.Headers.ContainsKey("Authorization"))
            {
                client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", Request.Headers["Authorization"].ToString());
            }

            // =========================================================================
            // 3. LẤY MÃ/ID TRUNG TÂM TỪ USER ĐĂNG NHẬP Ở PHÍA MVC
            // =========================================================================
            string? idTrungTam = User.FindFirst("IdTrungTam")?.Value
                                 ?? HttpContext.Session.GetString("IdTrungTam");

            string? maTrungTam = User.FindFirst("MaTrungTam")?.Value
                                 ?? HttpContext.Session.GetString("MaTrungTam");

            // Lưu vào ViewBag để View hiển thị nếu cần
            ViewBag.IdTrungTam = idTrungTam;
            ViewBag.MaTrungTam = maTrungTam;

            // =========================================================================
            // 4. KHỞI TẠO URL GỌI SANG API (KÈM ID TRUNG TÂM / MÃ TRUNG TÂM)
            // =========================================================================
            var queryParams = new List<string> { $"page={page}" };

            if (!string.IsNullOrWhiteSpace(idTrungTam) && idTrungTam != "0")
            {
                queryParams.Add($"idTrungTam={Uri.EscapeDataString(idTrungTam)}");
            }

            if (!string.IsNullOrWhiteSpace(maTrungTam))
            {
                queryParams.Add($"maTrungTam={Uri.EscapeDataString(maTrungTam)}");
            }

            string queryString = string.Join("&", queryParams);
            string apiUrl = $"{apiBaseUrl}/danhsachbaihoc/{capdo}/{idGiaoTrinh}?{queryString}";

            try
            {
                // 5. Kiểm tra trạng thái VIP của User hiện tại
                bool isUserVip = await _userService.IsUserPremiumAsync(User, HttpContext);
                ViewBag.IsUserVip = isUserVip;

                var response = await client.GetAsync(apiUrl);
                if (response.IsSuccessStatusCode)
                {
                    var jsonString = await response.Content.ReadAsStringAsync();

                    // 6. Giải mã JSON sang kiểu dữ liệu động
                    dynamic pagedResult = JsonConvert.DeserializeObject<dynamic>(jsonString);

                    // 7. Lưu trữ trạng thái vào ViewBag để View sử dụng
                    ViewBag.IdCapDo = capdo;
                    ViewBag.IdGiaoTrinh = idGiaoTrinh;
                    ViewBag.CurrentPage = page;

                    return View(pagedResult);
                }

                _logger.LogWarning($"API Lấy danh sách bài học thất bại với Status Code: {response.StatusCode} | URL: {apiUrl}");
                return View(null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Lỗi kết nối khi lấy danh sách bài học cho Cấp độ: {capdo}, Giáo trình: {idGiaoTrinh}, Trang: {page}");
                return View(null);
            }
        }

        public async Task<IActionResult> DanhSachBaiHocOnLuyen(int capdo, int idGiaoTrinh, int page = 1)
        {
            // 1. Đảm bảo số trang tối thiểu luôn lớn hơn hoặc bằng 1
            if (page < 1) page = 1;

            var client = _httpClientFactory.CreateClient("BypassSSL");
            dynamic pagedResult = null;

            // =========================================================================
            // 2. CHUYỂN TIẾP XÁC THỰC (COOKIE / TOKEN) TỪ MVC SANG API BACKEND
            // =========================================================================
            if (Request.Headers.ContainsKey("Cookie"))
            {
                client.DefaultRequestHeaders.Add("Cookie", Request.Headers["Cookie"].ToString());
            }

            if (Request.Headers.ContainsKey("Authorization"))
            {
                client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", Request.Headers["Authorization"].ToString());
            }

            // =========================================================================
            // 3. LẤY MÃ / ID TRUNG TÂM CỦA TÀI KHOẢN ĐANG ĐĂNG NHẬP
            // =========================================================================
            string? idTrungTam = User.FindFirst("IdTrungTam")?.Value
                                 ?? HttpContext.Session.GetString("IdTrungTam");

            string? maTrungTam = User.FindFirst("MaTrungTam")?.Value
                                 ?? HttpContext.Session.GetString("MaTrungTam");

            // =========================================================================
            // 4. KHỞI TẠO URL GỌI SANG API KÈM THÔNG TIN BỘ LỌC TRUNG TÂM
            // =========================================================================
            var queryParams = new List<string> { $"page={page}" };

            if (!string.IsNullOrWhiteSpace(idTrungTam) && idTrungTam != "0")
            {
                queryParams.Add($"idTrungTam={Uri.EscapeDataString(idTrungTam)}");
            }

            if (!string.IsNullOrWhiteSpace(maTrungTam))
            {
                queryParams.Add($"maTrungTam={Uri.EscapeDataString(maTrungTam.Trim())}");
            }

            string queryString = string.Join("&", queryParams);
            string apiUrl = $"{apiBaseUrl}/danhsachbaihoc/{capdo}/{idGiaoTrinh}?{queryString}";

            try
            {
                // 5. Kiểm tra trạng thái VIP
                bool isUserVip = await _userService.IsUserPremiumAsync(User, HttpContext);
                ViewBag.IsUserVip = isUserVip;

                var response = await client.GetAsync(apiUrl);
                if (response.IsSuccessStatusCode)
                {
                    var jsonString = await response.Content.ReadAsStringAsync();

                    // 6. Giải mã JSON
                    pagedResult = JsonConvert.DeserializeObject<dynamic>(jsonString);

                    // 7. Lưu trữ thông tin vào ViewBag để View sử dụng
                    ViewBag.IdCapDo = capdo;
                    ViewBag.IdGiaoTrinh = idGiaoTrinh;
                    ViewBag.CurrentPage = page;
                    ViewBag.IdTrungTam = idTrungTam;
                    ViewBag.MaTrungTam = maTrungTam;

                    return View(pagedResult);
                }

                _logger.LogWarning($"API Lấy danh sách bài học ôn luyện thất bại với Status Code: {response.StatusCode} | URL: {apiUrl}");

                // Vẫn lưu các giá trị điều hướng để View không bị lỗi null khi render paging
                ViewBag.IdCapDo = capdo;
                ViewBag.IdGiaoTrinh = idGiaoTrinh;
                ViewBag.CurrentPage = page;
                ViewBag.IdTrungTam = idTrungTam;
                ViewBag.MaTrungTam = maTrungTam;

                return View(null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Lỗi kết nối khi lấy danh sách bài học ôn luyện cho Cấp độ: {capdo}, Giáo trình: {idGiaoTrinh}, Trang: {page}, Trung tâm: {idTrungTam ?? maTrungTam}");

                ViewBag.IdCapDo = capdo;
                ViewBag.IdGiaoTrinh = idGiaoTrinh;
                ViewBag.CurrentPage = page;
                ViewBag.IdTrungTam = idTrungTam;
                ViewBag.MaTrungTam = maTrungTam;

                return View(null);
            }
        }
        public async Task<IActionResult> ChiTietBaiHoc(int id, int loaiId = 1, int deSo = 1)
        {
            var client = _httpClientFactory.CreateClient("BypassSSL");

            

            string apiUrl = $"{apiBaseUrl}/chitietbaihoc/{id}";

            var listTuVung = new List<dynamic>();
            var listAllCauHoi = new List<dynamic>();
            var danhSachCauHoiTheoDe = new List<dynamic>();
            string tenBaiHoc = "Không rõ tên bài học";
            int tongSoDe = 1;
            int idGiaoTrinh = 1; // Mặc định ID giáo trình = 1

            try
            {
                var response = await client.GetAsync(apiUrl);

                if (response.IsSuccessStatusCode)
                {
                    var jsonString = await response.Content.ReadAsStringAsync();
                    var baiHoc = JsonConvert.DeserializeObject<dynamic>(jsonString);

                    if (baiHoc != null)
                    {
                        // Lấy thông tin cơ bản
                        int idCapDo = baiHoc.idCapDo ?? baiHoc.IdCapDo ?? 1;
                        tenBaiHoc = baiHoc.tenBaiHoc ?? baiHoc.TenBaiHoc ?? tenBaiHoc;
                        idGiaoTrinh = baiHoc.idGiaoTrinh ?? baiHoc.IdGiaoTrinh ?? 1;

                        ViewBag.IdCapDo = idCapDo;
                        ViewBag.TenBaiHoc = tenBaiHoc;
                        ViewBag.IdGiaoTrinh = idGiaoTrinh;

                        // Lấy danh sách từ vựng
                        if (baiHoc.baiHocTuVungs != null)
                        {
                            foreach (var item in baiHoc.baiHocTuVungs)
                            {
                                listTuVung.Add(item);
                            }
                        }

                        // Lấy danh sách câu hỏi qua navigation property
                        if (baiHoc.baiHocCauHois != null)
                        {
                            foreach (var item in baiHoc.baiHocCauHois)
                            {
                                var cauHoi = item.idCauHoiNavigation ?? item.IdCauHoiNavigation;
                                if (cauHoi != null)
                                {
                                    listAllCauHoi.Add(cauHoi);
                                }
                            }
                        }
                    }
                }
                else
                {
                    _logger.LogWarning($"API Chi tiết bài học {id} trả về mã lỗi: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Lỗi hệ thống khi bốc bộ đề luyện tập cho bài học ID {id}");
            }

            // =========================================================================
            // Phân loại câu hỏi & tạo danh sách menu theo idGiaoTrinh (Xử lý an toàn dynamic)
            // =========================================================================
            var cauHoiTheoLoai = listAllCauHoi
                .GroupBy(c => {
                    try
                    {
                        return (int)(c.idLoai ?? c.IdLoai ?? 0);
                    }
                    catch
                    {
                        return 0;
                    }
                })
                .ToDictionary(g => g.Key, g => g.ToList());

            List<MenuLoaiModel> menuLoaiBaiTap;

           if (idGiaoTrinh == 1)
            {
                // Menu rút gọn dành riêng cho ID Giáo trình = 1
                menuLoaiBaiTap = new List<MenuLoaiModel>
                {
                    new MenuLoaiModel { IdLoai = 1, TenLoai = "Trắc nghiệm", Icon = "fa-list-check", TotalCount = cauHoiTheoLoai.ContainsKey(1) ? cauHoiTheoLoai[1].Count : 0 },
                    new MenuLoaiModel { IdLoai = 2, TenLoai = "Điền vào chỗ trống", Icon = "fa-pen-to-square", TotalCount = cauHoiTheoLoai.ContainsKey(2) ? cauHoiTheoLoai[2].Count : 0 },
                    new MenuLoaiModel { IdLoai = 3, TenLoai = "Sắp xếp câu", Icon = "fa-arrow-down-a-z", TotalCount = cauHoiTheoLoai.ContainsKey(3) ? cauHoiTheoLoai[3].Count : 0 },
                    new MenuLoaiModel { IdLoai = 4, TenLoai = "Dịch thuật Trung - Việt", Icon = "fa-language", TotalCount = cauHoiTheoLoai.ContainsKey(4) ? cauHoiTheoLoai[4].Count : 0 },
                    new MenuLoaiModel { IdLoai = 5, TenLoai = "Dịch thuật Việt - Trung", Icon = "fa-rotate", TotalCount = cauHoiTheoLoai.ContainsKey(5) ? cauHoiTheoLoai[5].Count : 0 }
                };
            }
            else if(idGiaoTrinh == 2|| idGiaoTrinh == 3)
            {
                // Menu rút gọn dành riêng cho các ID Giáo trình = 2 hoặc 3
                menuLoaiBaiTap = new List<MenuLoaiModel>
                {
                    // 1. Nhập môn & Phát âm
                    new MenuLoaiModel { IdLoai = 1020, TenLoai = "Phiên âm tiếng trung", Icon = "fa-rotate", TotalCount = cauHoiTheoLoai.ContainsKey(1020) ? cauHoiTheoLoai[1020].Count : 0 },
                    new MenuLoaiModel { IdLoai = 1, TenLoai = "Trắc nghiệm", Icon = "fa-list-check", TotalCount = cauHoiTheoLoai.ContainsKey(1) ? cauHoiTheoLoai[1].Count : 0 },
                    new MenuLoaiModel { IdLoai = 2, TenLoai = "Điền vào chỗ trống", Icon = "fa-pen-to-square", TotalCount = cauHoiTheoLoai.ContainsKey(2) ? cauHoiTheoLoai[2].Count : 0 },
                    new MenuLoaiModel { IdLoai = 1023, TenLoai = "Nối câu", Icon = "fa-rotate", TotalCount = cauHoiTheoLoai.ContainsKey(1023) ? cauHoiTheoLoai[1023].Count : 0 },
                    new MenuLoaiModel { IdLoai = 3, TenLoai = "Sắp xếp câu", Icon = "fa-arrow-down-a-z", TotalCount = cauHoiTheoLoai.ContainsKey(3) ? cauHoiTheoLoai[3].Count : 0 },
                    new MenuLoaiModel { IdLoai = 4, TenLoai = "Dịch thuật Trung - Việt", Icon = "fa-language", TotalCount = cauHoiTheoLoai.ContainsKey(4) ? cauHoiTheoLoai[4].Count : 0 },
                    new MenuLoaiModel { IdLoai = 5, TenLoai = "Dịch thuật Việt - Trung", Icon = "fa-rotate", TotalCount = cauHoiTheoLoai.ContainsKey(5) ? cauHoiTheoLoai[5].Count : 0 },
                    new MenuLoaiModel { IdLoai = 1021, TenLoai = "Sửa lỗi sai tự luận", Icon = "fa-rotate", TotalCount = cauHoiTheoLoai.ContainsKey(1021) ? cauHoiTheoLoai[1021].Count : 0 },
                    new MenuLoaiModel { IdLoai = 1022, TenLoai = "Đọc hiểu", Icon = "fa-rotate", TotalCount = cauHoiTheoLoai.ContainsKey(1022) ? cauHoiTheoLoai[1022].Count : 0 }
                };
            }
            else
            {
                // Menu đầy đủ cho các ID Giáo trình khác
                menuLoaiBaiTap = new List<MenuLoaiModel>
                {
                    new MenuLoaiModel { IdLoai = 1002, TenLoai = "Luyện nghe ghi chép", Icon = "fa-rotate", TotalCount = cauHoiTheoLoai.ContainsKey(1002) ? cauHoiTheoLoai[1002].Count : 0 },
                    new MenuLoaiModel { IdLoai = 1, TenLoai = "Trắc nghiệm", Icon = "fa-list-check", TotalCount = cauHoiTheoLoai.ContainsKey(1) ? cauHoiTheoLoai[1].Count : 0 },
                    new MenuLoaiModel { IdLoai = 2, TenLoai = "Điền vào chỗ trống", Icon = "fa-pen-to-square", TotalCount = cauHoiTheoLoai.ContainsKey(2) ? cauHoiTheoLoai[2].Count : 0 },
                    new MenuLoaiModel { IdLoai = 1023, TenLoai = "Nối câu", Icon = "fa-rotate", TotalCount = cauHoiTheoLoai.ContainsKey(1023) ? cauHoiTheoLoai[1023].Count : 0 },
                    new MenuLoaiModel { IdLoai = 3, TenLoai = "Sắp xếp câu", Icon = "fa-arrow-down-a-z", TotalCount = cauHoiTheoLoai.ContainsKey(3) ? cauHoiTheoLoai[3].Count : 0 },
                    new MenuLoaiModel { IdLoai = 4, TenLoai = "Dịch thuật Trung - Việt", Icon = "fa-language", TotalCount = cauHoiTheoLoai.ContainsKey(4) ? cauHoiTheoLoai[4].Count : 0 },
                    new MenuLoaiModel { IdLoai = 5, TenLoai = "Dịch thuật Việt - Trung", Icon = "fa-rotate", TotalCount = cauHoiTheoLoai.ContainsKey(5) ? cauHoiTheoLoai[5].Count : 0 },
                    new MenuLoaiModel { IdLoai = 1021, TenLoai = "Sửa lỗi sai tự luận", Icon = "fa-rotate", TotalCount = cauHoiTheoLoai.ContainsKey(1021) ? cauHoiTheoLoai[1021].Count : 0 },
                    new MenuLoaiModel { IdLoai = 1022, TenLoai = "Đọc hiểu", Icon = "fa-rotate", TotalCount = cauHoiTheoLoai.ContainsKey(1022) ? cauHoiTheoLoai[1022].Count : 0 },
                    //new MenuLoaiModel { IdLoai = 1004, TenLoai = "Đọc hiểu đoạn văn", Icon = "fa-rotate", TotalCount = cauHoiTheoLoai.ContainsKey(1004) ? cauHoiTheoLoai[1004].Count : 0 }
                };
            }

            ViewBag.DanhSachTuVung = listTuVung;
            ViewBag.MenuLoai = menuLoaiBaiTap;
            ViewBag.CurrentLoaiId = loaiId;
            ViewBag.IdBaiHoc = id;

            List<dynamic> listCauHoiTheoLoaiHienTai = cauHoiTheoLoai.ContainsKey(loaiId) ? cauHoiTheoLoai[loaiId] : new List<dynamic>();

            // 1. Kiểm tra loại bài học hiện tại
            bool isDocHieu = (loaiId == 1004 || loaiId == 1011);

            if (isDocHieu)
            {
                // ==========================================
                // LOGIC CHO BÀI TẬP ĐỌC HIỂU (1 Đoạn văn = 1 Đề)
                // ==========================================

                // Gom nhóm các câu hỏi theo IdDoanVan (bỏ qua những câu không có đoạn văn nếu có)
                var danhSachCacDeDocHieu = listCauHoiTheoLoaiHienTai
                    .Where(q => q.IdDoanVan != null && q.IdDoanVan != 0)
                    .GroupBy(q => q.IdDoanVan)
                    .Select((group, index) => new
                    {
                        DeSo = index + 1,
                        CauHois = group.ToList()
                    })
                    .ToList();

                // Tính tổng số đề = Tổng số đoạn văn
                tongSoDe = danhSachCacDeDocHieu.Count;
                if (tongSoDe == 0) tongSoDe = 1;

                // Giới hạn đề hiện tại hợp lệ
                if (deSo < 1) deSo = 1;
                if (deSo > tongSoDe) deSo = tongSoDe;

                ViewBag.TongSoDe = tongSoDe;
                ViewBag.CurrentDeSo = deSo;

                // Lấy ra tất cả câu hỏi thuộc Đoạn văn tương ứng với deSo
                var deHienTai = danhSachCacDeDocHieu.FirstOrDefault(d => d.DeSo == deSo);
                danhSachCauHoiTheoDe = listCauHoiTheoLoaiHienTai
                    .Cast<dynamic>()
                    .ToList();
            }
            else
            {
                // ==========================================
                // LOGIC CHO CÁC DẠNG BÀI KHÁC (Phân trang theo số lượng câu)
                // ==========================================

                // Đặt số câu/đề: Dạng Nối câu (1023) hiện 5 câu, các dạng khác giữ 10 câu
                int soCauMoiDe = (loaiId == 1023) ? 5 : 10;

                tongSoDe = (int)Math.Ceiling((double)listCauHoiTheoLoaiHienTai.Count / soCauMoiDe);
                if (tongSoDe == 0) tongSoDe = 1;

                if (deSo < 1) deSo = 1;
                if (deSo > tongSoDe) deSo = tongSoDe;

                ViewBag.TongSoDe = tongSoDe;
                ViewBag.CurrentDeSo = deSo;

                danhSachCauHoiTheoDe = listCauHoiTheoLoaiHienTai
                    .Skip((deSo - 1) * soCauMoiDe)
                    .Take(soCauMoiDe)
                    .ToList();
            }

            return View(danhSachCauHoiTheoDe);
        }

        public async Task<IActionResult> DanhSachBaiHocAdmin(int capdo, int idGiaoTrinh, int page = 1)
        {
            // 1. Đảm bảo số trang tối thiểu luôn lớn hơn hoặc bằng 1
            if (page < 1) page = 1;

            var client = _httpClientFactory.CreateClient("BypassSSL");
            dynamic pagedResult = null;

            // 2. Khởi tạo URL gọi sang API gốc (khớp đúng route: /danhsachbaihoc/{idCapDo}/{idGiaoTrinh}?page={page})
            string apiUrl = $"{apiBaseUrl}/danhsachbaihoc/{capdo}/{idGiaoTrinh}?page={page}";

            try
            {
                var response = await client.GetAsync(apiUrl);
                if (response.IsSuccessStatusCode)
                {
                    var jsonString = await response.Content.ReadAsStringAsync();

                    // 3. Giải mã JSON sang kiểu dữ liệu động
                    pagedResult = JsonConvert.DeserializeObject<dynamic>(jsonString);

                    // 4. Lưu trữ trạng thái vào ViewBag để View sử dụng build link phân trang & chuyển cấp độ/giáo trình
                    ViewBag.IdCapDo = capdo;
                    ViewBag.IdGiaoTrinh = idGiaoTrinh;
                    ViewBag.CurrentPage = page;

                    return View(pagedResult);
                }

                _logger.LogWarning($"API Lấy danh sách bài học thất bại với Status Code: {response.StatusCode} | URL: {apiUrl}");
                return View(null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Lỗi kết nối khi lấy danh sách bài học cho Cấp độ: {capdo}, Giáo trình: {idGiaoTrinh}, Trang: {page}");
                return View(null);
            }
        }

        public async Task<IActionResult> ChiTietBaiHocAdmin(int id, int loaiId = 1, int deSo = 1)
        {
            var client = _httpClientFactory.CreateClient("BypassSSL");
            string apiUrl = $"{apiBaseUrl}/chitietbaihoc/{id}";

            var idTaiKhoan = User.FindFirst("IdTaiKhoan")?.Value;
            if (!string.IsNullOrEmpty(idTaiKhoan))
            {
                client.DefaultRequestHeaders.Add("X-User-Id", idTaiKhoan);
            }

            var listTuVung = new List<dynamic>();
            var listAllCauHoi = new List<dynamic>();
            var danhSachCauHoiTheoDe = new List<dynamic>();
            string tenBaiHoc = "Không rõ tên bài học";
            int tongSoDe = 1;
            int idGiaoTrinh = 1; // Mặc định ID giáo trình = 1

            try
            {
                var response = await client.GetAsync(apiUrl);

                // TRƯỜNG HỢP 1: API chặn và trả về lỗi 403 Forbidden
                if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                {
                    var jsonString = await response.Content.ReadAsStringAsync();
                    var errorData = JsonConvert.DeserializeObject<dynamic>(jsonString);

                    ViewBag.RequiresPremium = true;
                    ViewBag.IdCapDo = errorData?.idCapDo ?? 1;
                    ViewBag.TenBaiHoc = "Tính năng giới hạn VIP";
                    ViewBag.CurrentLoaiId = loaiId;
                    ViewBag.IdBaiHoc = id;

                    // Truyền idGiaoTrinh vào SetupMockMenu
                    SetupMockMenu(0, idGiaoTrinh);

                    return View(new List<dynamic>());
                }

                // TRƯỜNG HỢP 2: API trả về dữ liệu thành công
                if (response.IsSuccessStatusCode)
                {
                    var jsonString = await response.Content.ReadAsStringAsync();

                    if (jsonString.Contains("CHUA_NAP_TIEN") || jsonString.Contains("REQUIRES_PREMIUM"))
                    {
                        ViewBag.RequiresPremium = true;
                        ViewBag.IdCapDo = 1;
                        ViewBag.TenBaiHoc = "Tính năng giới hạn VIP";
                        ViewBag.CurrentLoaiId = loaiId;
                        ViewBag.IdBaiHoc = id;

                        SetupMockMenu(0, idGiaoTrinh);
                        return View(new List<dynamic>());
                    }

                    var baiHoc = JsonConvert.DeserializeObject<dynamic>(jsonString);

                    if (baiHoc != null)
                    {
                        // Lấy thông tin cơ bản
                        int idCapDo = baiHoc.idCapDo ?? baiHoc.IdCapDo ?? 1;
                        int thuTuBaiHoc = baiHoc.soThuTuBai ?? baiHoc.SoThuTuBai ?? baiHoc.thuTu ?? 1;
                        tenBaiHoc = baiHoc.tenBaiHoc ?? baiHoc.TenBaiHoc ?? tenBaiHoc;

                        // Cập nhật idGiaoTrinh thực tế từ dữ liệu bài học (nếu có)
                        idGiaoTrinh = baiHoc.idGiaoTrinh ?? baiHoc.IdGiaoTrinh ?? 1;

                        ViewBag.IdCapDo = idCapDo;
                        ViewBag.TenBaiHoc = tenBaiHoc;

                        bool isUserVip = await _userService.IsUserPremiumAsync(User, HttpContext);

                        if (thuTuBaiHoc > 3 && !isUserVip)
                        {
                            ViewBag.RequiresPremium = true;
                            ViewBag.CurrentLoaiId = loaiId;
                            ViewBag.IdBaiHoc = id;

                            SetupMockMenu(0, idGiaoTrinh);
                            return View(new List<dynamic>());
                        }

                        if (baiHoc.baiHocTuVungs != null)
                        {
                            foreach (var item in baiHoc.baiHocTuVungs) listTuVung.Add(item);
                        }

                        if (baiHoc.baiHocCauHois != null)
                        {
                            foreach (var item in baiHoc.baiHocCauHois)
                            {
                                if (item.idCauHoiNavigation != null) listAllCauHoi.Add(item.idCauHoiNavigation);
                            }
                        }
                    }
                }
                else
                {
                    _logger.LogWarning($"API Chi tiết bài học {id} trả về mã lỗi không xác định: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Lỗi hệ thống khi bốc bộ đề luyện tập cho bài học ID {id}");
            }

            // =========================================================================
            // Phân loại câu hỏi & tạo danh sách menu theo idGiaoTrinh
            // =========================================================================
            var cauHoiTheoLoai = listAllCauHoi
                .GroupBy(c => c.idLoai != null ? (int)c.idLoai : 0)
                .ToDictionary(g => g.Key, g => g.ToList());

            List<MenuLoaiModel> menuLoaiBaiTap;

            if (idGiaoTrinh == 1)
            {
                // Menu rút gọn dành riêng cho ID Giáo trình = 1
                menuLoaiBaiTap = new List<MenuLoaiModel>
                {
                    new MenuLoaiModel { IdLoai = 1, TenLoai = "Trắc nghiệm", Icon = "fa-list-check", TotalCount = cauHoiTheoLoai.ContainsKey(1) ? cauHoiTheoLoai[1].Count : 0 },
                    new MenuLoaiModel { IdLoai = 2, TenLoai = "Điền vào chỗ trống", Icon = "fa-pen-to-square", TotalCount = cauHoiTheoLoai.ContainsKey(2) ? cauHoiTheoLoai[2].Count : 0 },
                    new MenuLoaiModel { IdLoai = 3, TenLoai = "Sắp xếp câu", Icon = "fa-arrow-down-a-z", TotalCount = cauHoiTheoLoai.ContainsKey(3) ? cauHoiTheoLoai[3].Count : 0 },
                    new MenuLoaiModel { IdLoai = 4, TenLoai = "Dịch thuật Trung - Việt", Icon = "fa-language", TotalCount = cauHoiTheoLoai.ContainsKey(4) ? cauHoiTheoLoai[4].Count : 0 },
                    new MenuLoaiModel { IdLoai = 5, TenLoai = "Dịch thuật Việt - Trung", Icon = "fa-rotate", TotalCount = cauHoiTheoLoai.ContainsKey(5) ? cauHoiTheoLoai[5].Count : 0 }
                };
            }
            else if(idGiaoTrinh == 2|| idGiaoTrinh == 3)
            {
                // Menu rút gọn dành riêng cho các ID Giáo trình = 2 hoặc 3
                menuLoaiBaiTap = new List<MenuLoaiModel>
                {
                    // 1. Nhập môn & Phát âm
                    new MenuLoaiModel { IdLoai = 1020, TenLoai = "Phiên âm tiếng trung", Icon = "fa-rotate", TotalCount = cauHoiTheoLoai.ContainsKey(1020) ? cauHoiTheoLoai[1020].Count : 0 },
                    new MenuLoaiModel { IdLoai = 1, TenLoai = "Trắc nghiệm", Icon = "fa-list-check", TotalCount = cauHoiTheoLoai.ContainsKey(1) ? cauHoiTheoLoai[1].Count : 0 },
                    new MenuLoaiModel { IdLoai = 2, TenLoai = "Điền vào chỗ trống", Icon = "fa-pen-to-square", TotalCount = cauHoiTheoLoai.ContainsKey(2) ? cauHoiTheoLoai[2].Count : 0 },
                    new MenuLoaiModel { IdLoai = 1023, TenLoai = "Nối câu", Icon = "fa-rotate", TotalCount = cauHoiTheoLoai.ContainsKey(1023) ? cauHoiTheoLoai[1023].Count : 0 },
                    new MenuLoaiModel { IdLoai = 3, TenLoai = "Sắp xếp câu", Icon = "fa-arrow-down-a-z", TotalCount = cauHoiTheoLoai.ContainsKey(3) ? cauHoiTheoLoai[3].Count : 0 },
                    new MenuLoaiModel { IdLoai = 4, TenLoai = "Dịch thuật Trung - Việt", Icon = "fa-language", TotalCount = cauHoiTheoLoai.ContainsKey(4) ? cauHoiTheoLoai[4].Count : 0 },
                    new MenuLoaiModel { IdLoai = 5, TenLoai = "Dịch thuật Việt - Trung", Icon = "fa-rotate", TotalCount = cauHoiTheoLoai.ContainsKey(5) ? cauHoiTheoLoai[5].Count : 0 },
                    new MenuLoaiModel { IdLoai = 1021, TenLoai = "Sửa lỗi sai tự luận", Icon = "fa-rotate", TotalCount = cauHoiTheoLoai.ContainsKey(1021) ? cauHoiTheoLoai[1021].Count : 0 },
                    new MenuLoaiModel { IdLoai = 1022, TenLoai = "Đọc hiểu", Icon = "fa-rotate", TotalCount = cauHoiTheoLoai.ContainsKey(1022) ? cauHoiTheoLoai[1022].Count : 0 }
                };
            }
            else
            {
                // Menu đầy đủ cho các ID Giáo trình khác
                menuLoaiBaiTap = new List<MenuLoaiModel>
                {
                    new MenuLoaiModel { IdLoai = 1002, TenLoai = "Luyện nghe ghi chép", Icon = "fa-rotate", TotalCount = cauHoiTheoLoai.ContainsKey(1002) ? cauHoiTheoLoai[1002].Count : 0 },
                    new MenuLoaiModel { IdLoai = 1, TenLoai = "Trắc nghiệm", Icon = "fa-list-check", TotalCount = cauHoiTheoLoai.ContainsKey(1) ? cauHoiTheoLoai[1].Count : 0 },
                    new MenuLoaiModel { IdLoai = 2, TenLoai = "Điền vào chỗ trống", Icon = "fa-pen-to-square", TotalCount = cauHoiTheoLoai.ContainsKey(2) ? cauHoiTheoLoai[2].Count : 0 },
                    new MenuLoaiModel { IdLoai = 1023, TenLoai = "Nối câu", Icon = "fa-rotate", TotalCount = cauHoiTheoLoai.ContainsKey(1023) ? cauHoiTheoLoai[1023].Count : 0 },
                    new MenuLoaiModel { IdLoai = 3, TenLoai = "Sắp xếp câu", Icon = "fa-arrow-down-a-z", TotalCount = cauHoiTheoLoai.ContainsKey(3) ? cauHoiTheoLoai[3].Count : 0 },
                    new MenuLoaiModel { IdLoai = 4, TenLoai = "Dịch thuật Trung - Việt", Icon = "fa-language", TotalCount = cauHoiTheoLoai.ContainsKey(4) ? cauHoiTheoLoai[4].Count : 0 },
                    new MenuLoaiModel { IdLoai = 5, TenLoai = "Dịch thuật Việt - Trung", Icon = "fa-rotate", TotalCount = cauHoiTheoLoai.ContainsKey(5) ? cauHoiTheoLoai[5].Count : 0 },
                    new MenuLoaiModel { IdLoai = 1021, TenLoai = "Sửa lỗi sai tự luận", Icon = "fa-rotate", TotalCount = cauHoiTheoLoai.ContainsKey(1021) ? cauHoiTheoLoai[1021].Count : 0 },
                    new MenuLoaiModel { IdLoai = 1022, TenLoai = "Đọc hiểu", Icon = "fa-rotate", TotalCount = cauHoiTheoLoai.ContainsKey(1022) ? cauHoiTheoLoai[1022].Count : 0 },
                    new MenuLoaiModel { IdLoai = 1004, TenLoai = "Đọc hiểu đoạn văn", Icon = "fa-rotate", TotalCount = cauHoiTheoLoai.ContainsKey(1004) ? cauHoiTheoLoai[1004].Count : 0 }
                };
            }

            ViewBag.DanhSachTuVung = listTuVung;
            ViewBag.MenuLoai = menuLoaiBaiTap;
            ViewBag.CurrentLoaiId = loaiId;
            ViewBag.IdBaiHoc = id;

            List<dynamic> listCauHoiTheoLoaiHienTai = cauHoiTheoLoai.ContainsKey(loaiId) ? cauHoiTheoLoai[loaiId] : new List<dynamic>();

            int soCauMoiDe = 10;
            tongSoDe = (int)Math.Ceiling((double)listCauHoiTheoLoaiHienTai.Count / soCauMoiDe);
            if (tongSoDe == 0) tongSoDe = 1;

            if (deSo < 1) deSo = 1;
            if (deSo > tongSoDe) deSo = tongSoDe;

            ViewBag.TongSoDe = tongSoDe;
            ViewBag.CurrentDeSo = deSo;

            danhSachCauHoiTheoDe = listCauHoiTheoLoaiHienTai
                .Skip((deSo - 1) * soCauMoiDe)
                .Take(soCauMoiDe)
                .ToList();

            return View(danhSachCauHoiTheoDe);
        }
        

        public async Task<IActionResult> DanhSachChuongTrinh(int page = 1, int pageSize = 10)
        {
            // 1. Đảm bảo số trang tối thiểu luôn lớn hơn hoặc bằng 1
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 10;

            var client = _httpClientFactory.CreateClient("BypassSSL");
            dynamic pagedResult = null;

            // 2. Khởi tạo URL gọi sang API danh sách chương trình giáo trình
            string apiUrl = $"{apiBaseUrl}/danhsach-chuongtrinh?page={page}&pageSize={pageSize}";

            try
            {
                var response = await client.GetAsync(apiUrl);
                if (response.IsSuccessStatusCode)
                {
                    var jsonString = await response.Content.ReadAsStringAsync();

                    // 3. Giải mã JSON sang kiểu dữ liệu động (chứa list Data + thông tin phân trang)
                    pagedResult = JsonConvert.DeserializeObject<dynamic>(jsonString);

                    // 4. Lưu trữ trạng thái vào ViewBag để phục vụ phân trang ở View
                    ViewBag.CurrentPage = page;
                    ViewBag.PageSize = pageSize;

                    return View(pagedResult);
                }

                _logger.LogWarning($"API Lấy danh sách chương trình thất bại: {response.StatusCode}");
                return View(null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi kết nối khi gọi API lấy danh sách chương trình giáo trình.");
                return View(null);
            }
        }
        public async Task<IActionResult> DanhSachChuongTrinhTinh()
        {
            return View();
        }
        /// <summary>
        /// Hàm bổ trợ gán Menu giả lập với số lượng câu = 0 khi bị chặn VIP, tránh lỗi View biên dịch Foreach null.
        /// </summary>
        private void SetupMockMenu(int count, int idGiaoTrinh)
        {
            ViewBag.DanhSachTuVung = new List<dynamic>();

            if (idGiaoTrinh == 1)
            {
                // Menu dành cho ID giáo trình = 1 (các mục comment)
                ViewBag.MenuLoai = new List<MenuLoaiModel>
                {
                    new MenuLoaiModel { IdLoai = 1, TenLoai = "Trắc nghiệm", Icon = "fa-list-check", TotalCount = count },
                    new MenuLoaiModel { IdLoai = 2, TenLoai = "Điền vào chỗ trống", Icon = "fa-pen-to-square", TotalCount = count },
                    new MenuLoaiModel { IdLoai = 3, TenLoai = "Sắp xếp câu", Icon = "fa-arrow-down-a-z", TotalCount = count },
                    new MenuLoaiModel { IdLoai = 4, TenLoai = "Dịch thuật Trung - Việt", Icon = "fa-language", TotalCount = count },
                    new MenuLoaiModel { IdLoai = 5, TenLoai = "Dịch thuật Việt - Trung", Icon = "fa-rotate", TotalCount = count }
                };
            }
            else if(idGiaoTrinh == 2 || idGiaoTrinh == 3)
            {
                // Menu dành cho các ID giáo trình khác
                ViewBag.MenuLoai = new List<MenuLoaiModel>
{
                    // 1. Nhập môn & Phiên âm
                    new MenuLoaiModel { IdLoai = 1020, TenLoai = "Phiên âm tiếng Trung", Icon = "fa-font", TotalCount = count },
                   
                    // 4. Ngữ pháp, Cấu trúc & Bài tập nhỏ
                    new MenuLoaiModel { IdLoai = 1, TenLoai = "Trắc nghiệm", Icon = "fa-list-check", TotalCount = count },
                    new MenuLoaiModel { IdLoai = 2, TenLoai = "Điền vào chỗ trống", Icon = "fa-pen-to-square", TotalCount = count },
                    new MenuLoaiModel { IdLoai = 1023, TenLoai = "Nối câu", Icon = "fa-link", TotalCount = count },
                    new MenuLoaiModel { IdLoai = 3, TenLoai = "Sắp xếp câu", Icon = "fa-arrow-down-a-z", TotalCount = count },
                    new MenuLoaiModel { IdLoai = 1022, TenLoai = "Sửa lỗi sai tự luận", Icon = "fa-triangle-exclamation", TotalCount = count }, // Đã đổi IdLoai thành 1022 để tránh trùng

                    // 5. Dịch thuật (Cao cấp)
                    new MenuLoaiModel { IdLoai = 4, TenLoai = "Dịch thuật Trung - Việt", Icon = "fa-language", TotalCount = count },
                    new MenuLoaiModel { IdLoai = 5, TenLoai = "Dịch thuật Việt - Trung", Icon = "fa-right-left", TotalCount = count },

                     // 3. Kỹ năng Đọc hiểu
                    new MenuLoaiModel { IdLoai = 1021, TenLoai = "Đọc hiểu", Icon = "fa-book-open", TotalCount = count },
                    

                };
            }
            else
            {
                // Menu dành cho các ID giáo trình khác
                ViewBag.MenuLoai = new List<MenuLoaiModel>
{
                  

                    // 2. Kỹ năng Nghe
                    new MenuLoaiModel { IdLoai = 1002, TenLoai = "Luyện nghe ghi chép", Icon = "fa-headphones", TotalCount = count },

                   
                    // 4. Ngữ pháp, Cấu trúc & Bài tập nhỏ
                    new MenuLoaiModel { IdLoai = 1, TenLoai = "Trắc nghiệm", Icon = "fa-list-check", TotalCount = count },
                    new MenuLoaiModel { IdLoai = 2, TenLoai = "Điền vào chỗ trống", Icon = "fa-pen-to-square", TotalCount = count },
                    new MenuLoaiModel { IdLoai = 1023, TenLoai = "Nối câu", Icon = "fa-link", TotalCount = count },
                    new MenuLoaiModel { IdLoai = 3, TenLoai = "Sắp xếp câu", Icon = "fa-arrow-down-a-z", TotalCount = count },
                    new MenuLoaiModel { IdLoai = 1022, TenLoai = "Sửa lỗi sai tự luận", Icon = "fa-triangle-exclamation", TotalCount = count }, // Đã đổi IdLoai thành 1022 để tránh trùng

                    // 5. Dịch thuật (Cao cấp)
                    new MenuLoaiModel { IdLoai = 4, TenLoai = "Dịch thuật Trung - Việt", Icon = "fa-language", TotalCount = count },
                    new MenuLoaiModel { IdLoai = 5, TenLoai = "Dịch thuật Việt - Trung", Icon = "fa-right-left", TotalCount = count },

                     // 3. Kỹ năng Đọc hiểu
                    new MenuLoaiModel { IdLoai = 1021, TenLoai = "Đọc hiểu", Icon = "fa-book-open", TotalCount = count },
                    //new MenuLoaiModel { IdLoai = 1004, TenLoai = "Đọc hiểu đoạn văn", Icon = "fa-file-lines", TotalCount = count },

                };
            }

            ViewBag.TongSoDe = 1;
            ViewBag.CurrentDeSo = 1;
        }
        // Lớp phụ trợ định nghĩa cấu trúc Menu Dọc
    }
    public class MenuLoaiModel
    {
        public int idGiaoTrinh { get; set; }
        public int IdLoai { get; set; }
        public string TenLoai { get; set; }
        public string Icon { get; set; }
        public int TotalCount { get; set; }
    }
}

