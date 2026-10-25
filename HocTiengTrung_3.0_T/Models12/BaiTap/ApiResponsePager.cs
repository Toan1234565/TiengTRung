using Newtonsoft.Json;

namespace HocTiengTrung_3._0_T.Models12.BaiTap
{
    public class ApiResponsePager<T>
    {
        [JsonProperty("success")]
        public bool Success { get; set; }

        [JsonProperty("data")]
        public List<T> Data { get; set; }

        [JsonProperty("totalItems")]
        public int TotalItems { get; set; }

        [JsonProperty("totalPages")]
        public int TotalPages { get; set; }

        [JsonProperty("currentPage")]
        public int CurrentPage { get; set; }

       
    }
}
