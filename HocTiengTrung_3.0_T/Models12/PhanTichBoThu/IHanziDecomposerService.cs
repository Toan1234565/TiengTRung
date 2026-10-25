using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace HocTiengTrung_3._0_T.Services
{
    public struct RadicalSourceInfo
    {
        public string Radical { get; set; }
        public string ParentComponent { get; set; }
        public bool IsDirect { get; set; }
    }

    public class HanziDetailInfo
    {
        public List<string> Components { get; set; } = new();
        public string Decomposition { get; set; }
        public string EtymologyType { get; set; }
        public int StrokeCount { get; set; }
    }

    public interface IHanziDecomposerService
    {
        Task EnsureDataLoadedAsync();
        List<string> GetComponents(char c);
        List<RadicalSourceInfo> GetSmartRadicals(char c, HashSet<string> validRadicals);
        HanziDetailInfo GetCharacterDetails(char c);
    }

    public class HanziDecomposerService : IHanziDecomposerService
    {
        private static readonly ConcurrentDictionary<char, HanziDetailInfo> CharacterMap = new();
        private static readonly List<string> EmptyList = new();
        private static bool _isLoaded = false;
        private static readonly SemaphoreSlim _semaphore = new(1, 1);
        private static readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(30) };

        public List<string> GetComponents(char c)
        {
            return CharacterMap.TryGetValue(c, out var info) ? info.Components : EmptyList;
        }

        public HanziDetailInfo GetCharacterDetails(char c)
        {
            return CharacterMap.TryGetValue(c, out var info) ? info : null;
        }

        public List<RadicalSourceInfo> GetSmartRadicals(char c, HashSet<string> validRadicals)
        {
            var results = new List<RadicalSourceInfo>();

            if (validRadicals.Contains(c.ToString()))
            {
                results.Add(new RadicalSourceInfo { Radical = c.ToString(), ParentComponent = c.ToString(), IsDirect = true });
                return results;
            }

            var level1Comps = GetComponents(c);
            var visited = new HashSet<char> { c };

            foreach (var compStr in level1Comps)
            {
                if (string.IsNullOrEmpty(compStr)) continue;

                if (validRadicals.Contains(compStr))
                {
                    results.Add(new RadicalSourceInfo
                    {
                        Radical = compStr,
                        ParentComponent = c.ToString(),
                        IsDirect = true
                    });
                }
                else
                {
                    results.Add(new RadicalSourceInfo
                    {
                        Radical = compStr,
                        ParentComponent = c.ToString(),
                        IsDirect = true
                    });

                    DecomposeSubComponent(compStr[0], compStr, validRadicals, results, visited, 1, 3);
                }
            }

            return results.GroupBy(r => r.Radical).Select(g => g.First()).ToList();
        }

        private void DecomposeSubComponent(
            char targetChar,
            string parentCompStr,
            HashSet<string> validRadicals,
            List<RadicalSourceInfo> results,
            HashSet<char> visited,
            int currentDepth,
            int maxDepth)
        {
            if (currentDepth > maxDepth || !visited.Add(targetChar)) return;

            var subComps = GetComponents(targetChar);
            foreach (var subStr in subComps)
            {
                if (string.IsNullOrEmpty(subStr)) continue;
                if (validRadicals.Contains(subStr))
                {
                    results.Add(new RadicalSourceInfo { Radical = subStr, ParentComponent = parentCompStr, IsDirect = false });
                }
                else
                {
                    DecomposeSubComponent(subStr[0], parentCompStr, validRadicals, results, visited, currentDepth + 1, maxDepth);
                }
            }
        }

        public async Task EnsureDataLoadedAsync()
        {
            if (_isLoaded) return;

            await _semaphore.WaitAsync();
            try
            {
                if (_isLoaded) return;

                string localFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "hanzi_dictionary.txt");

                if (!File.Exists(localFilePath))
                {
                    string url = "https://raw.githubusercontent.com/skishore/makemeahanzi/master/dictionary.txt";
                    var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
                    response.EnsureSuccessStatusCode();

                    using var fileStream = new FileStream(localFilePath, FileMode.Create, FileAccess.Write, FileShare.None);
                    await response.Content.CopyToAsync(fileStream);
                }

                // Tối ưu bộ nhớ: Đọc từng dòng Stream thay vì File.ReadAllLinesAsync (tránh chiếm RAM lớn)
                using (var reader = new StreamReader(localFilePath))
                {
                    string line;
                    while ((line = await reader.ReadLineAsync()) != null)
                    {
                        if (string.IsNullOrWhiteSpace(line)) continue;
                        try
                        {
                            using var doc = JsonDocument.Parse(line);
                            var root = doc.RootElement;

                            if (root.TryGetProperty("character", out var charProp) && charProp.GetString()?.Length == 1)
                            {
                                char character = charProp.GetString()[0];
                                var info = new HanziDetailInfo();

                                if (root.TryGetProperty("decomposition", out var decompProp))
                                {
                                    info.Decomposition = decompProp.GetString() ?? "";
                                    foreach (char ch in info.Decomposition)
                                    {
                                        if (ch != character && IsChineseCharacter(ch))
                                        {
                                            info.Components.Add(ch.ToString());
                                        }
                                    }
                                    info.Components = info.Components.Distinct().ToList();
                                }

                                if (root.TryGetProperty("etymology", out var etymProp) && etymProp.TryGetProperty("type", out var typeProp))
                                {
                                    info.EtymologyType = typeProp.GetString();
                                }

                                if (root.TryGetProperty("strokes", out var strokesProp) && strokesProp.ValueKind == JsonValueKind.Array)
                                {
                                    info.StrokeCount = strokesProp.GetArrayLength();
                                }

                                CharacterMap[character] = info;
                            }
                        }
                        catch { continue; }
                    }
                }

                _isLoaded = true;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        private static bool IsChineseCharacter(char c)
        {
            return (c >= 0x4E00 && c <= 0x9FFF) ||
                   (c >= 0x3400 && c <= 0x4DBF) ||
                   (c >= 0x2E80 && c <= 0x2EFF) ||
                   (c >= 0x2F00 && c <= 0x2FDF);
        }
    }
}