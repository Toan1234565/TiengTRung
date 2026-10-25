
using HocTiengTrung_3._0_T.Models;
using HocTiengTrung_3._0_T.Models12.TuVung.HocTiengTrung_3._0_T.DTOs.LuyenNghe;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;

namespace HocTiengTrung_3._0_T.Controllers.LuyenNghePhanXa
{
    public class LuyenNghePhanXaController : Controller
    {
        public async Task<IActionResult> LuyenNghePhanXa()
        {
            return View();
        }

    }
}
