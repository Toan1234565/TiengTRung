using HocTiengTrung_3._0_T.Models12.PhanTichBoThu;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using System;
using System.Threading.Tasks;

namespace HocTiengTrung_3._0_T.Models12.AI
{
    public interface IGeminiHanTuService
    {
        Task<PhanTichHanTuDto> PhanTichChuHanAsync(string chuHan);
    }

    public class GeminiHanTuService : IGeminiHanTuService
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;

        public GeminiHanTuService(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _apiKey = configuration["Gemini:ApiKey"] ?? "AIzaSyBU0_hwdC9g8ZitZnaNrIpPHHJlqePHkL8";
        }

        public async Task<PhanTichHanTuDto> PhanTichChuHanAsync(string chuHan)
        {
            var endpoint = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent?key={_apiKey}";

            var systemPrompt = @"Bạn là chuyên gia Văn tự học Hán Nôm và ngôn ngữ học tiếng Hán.
Nhiệm vụ của bạn là nhận vào một từ vựng tiếng Hán (có thể là từ đơn 1 chữ hoặc từ ghép nhiều chữ như 学习) và phân tích:
1. Pinyin cả từ (chuẩn thanh điệu).
2. Âm Hán Việt cả từ.
3. Từ loại chính (Động từ, Danh từ, Tính từ...).
4. Ý nghĩa chính (Nghĩa tiếng Việt thông dụng, súc tích).
5. Bộ thủ chính đại diện của từ.
6. Danh sách các ký tự cấu tạo (cauTaoKyTu): Với mỗi ký tự Hán tự trong từ, liệt kê chi tiết các bộ thủ/thành phần cấu tạo nên nó (Ký hiệu, Tên gọi Hán Việt, Pinyin, Ý nghĩa, Vai trò biểu ý/biểu âm).
7. Giải thích triết tự và Mẹo ghi nhớ dễ hiểu.";

            var payload = new
            {
                systemInstruction = new
                {
                    parts = new[] { new { text = systemPrompt } }
                },
                contents = new[]
                {
                    new { parts = new[] { new { text = $"Phân tích từ vựng: {chuHan}" } } }
                },
                generationConfig = new
                {
                    temperature = 0.2,
                    responseMimeType = "application/json",
                    responseSchema = new
                    {
                        type = "OBJECT",
                        properties = new
                        {
                            chuHan = new { type = "STRING" },
                            pinyin = new { type = "STRING" },
                            amHanViet = new { type = "STRING" },
                            tuLoai = new { type = "STRING" },
                            nghiaTiengViet = new { type = "STRING" },
                            boThuChinh = new { type = "STRING" },
                            cauTaoKyTu = new
                            {
                                type = "ARRAY",
                                items = new
                                {
                                    type = "OBJECT",
                                    properties = new
                                    {
                                        kyTu = new { type = "STRING" },
                                        danhSachBoThu = new
                                        {
                                            type = "ARRAY",
                                            items = new
                                            {
                                                type = "OBJECT",
                                                properties = new
                                                {
                                                    kyHieu = new { type = "STRING" },
                                                    tenGoi = new { type = "STRING" },
                                                    pinyin = new { type = "STRING" },
                                                    yNghia = new { type = "STRING" },
                                                    vaiTro = new { type = "STRING" }
                                                },
                                                required = new[] { "kyHieu", "tenGoi", "yNghia", "vaiTro" }
                                            }
                                        }
                                    },
                                    required = new[] { "kyTu", "danhSachBoThu" }
                                }
                            },
                            giaiThichTrietTu = new { type = "STRING" },
                            meoGhiNho = new { type = "STRING" }
                        },
                        required = new[] { "chuHan", "pinyin", "amHanViet", "tuLoai", "nghiaTiengViet", "boThuChinh", "cauTaoKyTu", "giaiThichTrietTu", "meoGhiNho" }
                    }
                }
            };

            var jsonPayload = JsonSerializer.Serialize(payload, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
            var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            try
            {
                var response = await _httpClient.PostAsync(endpoint, content);

                if (!response.IsSuccessStatusCode)
                {
                    var errorBody = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"[Gemini API Lỗi]: {errorBody}");
                    throw new HttpRequestException($"Lỗi API Gemini ({response.StatusCode}): {errorBody}");
                }

                var responseString = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(responseString);

                if (doc.RootElement.TryGetProperty("candidates", out var candidates) && candidates.GetArrayLength() > 0)
                {
                    var jsonText = candidates[0]
                        .GetProperty("content")
                        .GetProperty("parts")[0]
                        .GetProperty("text")
                        .GetString();

                    return JsonSerializer.Deserialize<PhanTichHanTuDto>(jsonText, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                }
                throw new Exception("API trả về phản hồi không chứa dữ liệu.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Ngoại lệ khi gọi AI]: {ex.Message}");
                throw;
            }
        }
    }
}