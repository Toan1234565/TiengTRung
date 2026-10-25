using Microsoft.AspNetCore.Mvc;

namespace HocTiengTrung_3._0_T.Controllers.GhiChu
{
    public class GhiChu : Controller
    {
        public IActionResult DanhSachGhiChu()
        {
            return View();
        }
    }
}
