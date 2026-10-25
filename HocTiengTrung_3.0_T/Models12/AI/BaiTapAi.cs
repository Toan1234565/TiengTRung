using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace HocTiengTrung_3._0_T.Models.AI
{
    public class EvaluationResult
    {
        public double Score { get; set; }
        public bool IsLevelAppropriate { get; set; }
        public string Comment { get; set; } = string.Empty;
        public string BestAlternative { get; set; } = string.Empty;
        public List<string> ErrorDetails { get; set; } = new List<string>();
    }

    public class BaiTapAi
    {
        public int IdCauHoi { get; set; }
        public string CauHoiGoc { get; set; } = string.Empty;
        public string UserAnswer { get; set; } = string.Empty;
        public string TenCapDo { get; set; } = string.Empty;
        public int IdLoai { get; set; }

        public EvaluationResult? Evaluation { get; set; }

        // Hàm kiểm tra ký tự Hán tự
        private static bool ChuaKyTuHan(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            foreach (char c in text)
            {
                if (c >= 0x4E00 && c <= 0x9FA5) return true;
            }
            return false;
        }

        public async Task<(bool IsSuccess, string ErrorMessage)> EvaluateTranslationAsync(string apiKey)
        {
            if (string.IsNullOrWhiteSpace(CauHoiGoc))
            {
                return (false, "Câu hỏi gốc bị trống.");
            }

            string endpoint = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent?key={apiKey}";

            using var httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromSeconds(60);

            // Tự động nhận diện chính xác: Đề có chữ Hán -> Dịch sang Tiếng Việt
            bool deBaiLaTiengTrung = ChuaKyTuHan(CauHoiGoc);
            string ngonNguDich = deBaiLaTiengTrung ? "TIẾNG VIỆT" : "TIẾNG TRUNG QUỐC (CHỮ HÁN)";

            bool isBoTrong = string.IsNullOrWhiteSpace(UserAnswer);
            string baiLamText = isBoTrong ? "[Học sinh không điền đáp án, nộp bài trống]" : UserAnswer.Trim();

            string systemPrompt = $@"BẠN LÀ CHUYÊN GIA KHẢO THÍ VÀ BIÊN DỊCH TIẾNG TRUNG.
NGÔN NGỮ ĐỀ BÀI: {(deBaiLaTiengTrung ? "Tiếng Trung Quốc" : "Tiếng Việt")}.
NGÔN NGỮ CỦA PHƯƠNG ÁN TỐI ƯU (BestAlternative): BẮT BUỘC PHẢI LÀ {ngonNguDich}. TUYỆT ĐỐI KHÔNG TRẢ VỀ CÙNG NGÔN NGỮ VỚI CÂU GỐC.

NHIỆM VỤ:
1. Đánh giá bài làm của học sinh theo thang điểm 0 - 10.
2. BestAlternative: Dịch câu gốc ""{CauHoiGoc.Trim()}"" sang {ngonNguDich} thật chuẩn xác, tự nhiên theo cấp độ {TenCapDo}.

KHI HỌC SINH BỎ TRỐNG / KHÔNG ĐIỀN ĐÁP ÁN:
- Score: 0
- IsLevelAppropriate: false
- Comment: ""Học sinh chưa hoàn thành bài dịch, không có nội dung để chấm điểm.""
- ErrorDetails: [""Chưa điền đáp án bài tập.""]
- BestAlternative: Dịch câu gốc sang {ngonNguDich}.

KHI HỌC SINH CÓ LÀM BÀI:
- Score: 0 - 10 tùy theo độ chính xác.
- IsLevelAppropriate: true nếu câu dịch phù hợp {TenCapDo}, false nếu sai lệch nhiều.
- Comment: Nhận xét tối đa 3 ý tách dòng (\n).
- ErrorDetails: Tối đa 2 lỗi sai chính.
- BestAlternative: Câu dịch mẫu sang {ngonNguDich}.";

            var requestBody = new
            {
                systemInstruction = new
                {
                    parts = new[] { new { text = systemPrompt } }
                },
                contents = new[]
                {
                    new
                    {
                        parts = new[]
                        {
                            new { text = $"Câu đề bài gốc: \"{CauHoiGoc.Trim()}\"\nBài làm của học sinh: \"{baiLamText}\"\nCấp độ: {TenCapDo}" }
                        }
                    }
                },
                generationConfig = new
                {
                    temperature = 0.1,
                    responseMimeType = "application/json",
                    responseSchema = new
                    {
                        type = "OBJECT",
                        properties = new
                        {
                            score = new { type = "NUMBER" },
                            isLevelAppropriate = new { type = "BOOLEAN" },
                            comment = new { type = "STRING" },
                            bestAlternative = new { type = "STRING" },
                            errorDetails = new
                            {
                                type = "ARRAY",
                                items = new { type = "STRING" }
                            }
                        },
                        required = new[] { "score", "isLevelAppropriate", "comment", "bestAlternative", "errorDetails" }
                    }
                }
            };

            string jsonPayload = JsonSerializer.Serialize(requestBody, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
            var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            try
            {
                HttpResponseMessage response = await httpClient.PostAsync(endpoint, content);
                string responseString = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    return (false, $"Lỗi từ Google ({response.StatusCode}): {responseString}");
                }

                using JsonDocument doc = JsonDocument.Parse(responseString);

                if (doc.RootElement.TryGetProperty("candidates", out var candidates) && candidates.GetArrayLength() > 0)
                {
                    string? aiResponseText = candidates[0]
                        .GetProperty("content")
                        .GetProperty("parts")[0]
                        .GetProperty("text")
                        .GetString();

                    if (!string.IsNullOrEmpty(aiResponseText))
                    {
                        this.Evaluation = JsonSerializer.Deserialize<EvaluationResult>(aiResponseText, new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        });

                        if (isBoTrong && this.Evaluation != null)
                        {
                            this.Evaluation.Score = 0;
                            this.Evaluation.IsLevelAppropriate = false;
                        }

                        return (true, "Thành công");
                    }
                }

                return (false, "Google API không trả về nội dung Text hợp lệ.");
            }
            catch (TaskCanceledException)
            {
                return (false, "Lỗi Timeout: Đã chờ quá 60 giây nhưng Gemini không phản hồi.");
            }
            catch (JsonException jsonEx)
            {
                return (false, $"Lỗi Parse JSON: {jsonEx.Message}");
            }
            catch (Exception ex)
            {
                return (false, $"Lỗi Hệ Thống: {ex.Message}");
            }
        }
    }
}