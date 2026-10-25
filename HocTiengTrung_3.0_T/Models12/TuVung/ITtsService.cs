using System;
using System.Net;

namespace HocTiengTrung_3._0_T.Services
{
    public interface ITtsService
    {
        string GetVietnameseAudioUrl(string text);
        string GetChineseAudioUrl(string text);
    }

    public class TtsService : ITtsService
    {
        public string GetVietnameseAudioUrl(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;

            string encodedText = WebUtility.UrlEncode(text.Trim());

            // Đổi sang endpoint API TTS Tiếng Việt hoạt động ổn định trên browser
            return $"https://dict.youdao.com/dictvoice?audio={encodedText}&le=zh";
            // Hoặc sử dụng Endpoint Google Speech mới:
            // return $"https://translate.google.com/translate_tts?ie=UTF-8&q={encodedText}&tl=vi&total=1&idx=0&textlen={text.Length}&client=tw-ob";
        }

        public string GetChineseAudioUrl(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;

            string encodedText = WebUtility.UrlEncode(text.Trim());

            // Link Google TTS đọc Tiếng Trung chuẩn (zh-CN)
            return $"https://translate.google.com/translate_tts?ie=UTF-8&tl=zh-CN&client=tw-ob&q={encodedText}";
        }
    }
}