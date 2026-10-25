using System;
using System.Collections.Generic;

namespace HocTiengTrung_3._0_T.Models;

public partial class DoanVan
{
    public int IdDoanVan { get; set; }

    public int? IdChuDe { get; set; }

    public string? TieuDe { get; set; }

    public string? NoiDungHan { get; set; }

    public string? NoiDungPinyin { get; set; }

    public string? NoiDungViet { get; set; }

    public string? AnhMinhHoa { get; set; }

    public virtual ICollection<CauHoi> CauHois { get; set; } = new List<CauHoi>();

    public virtual ChuDe? IdChuDeNavigation { get; set; }
}
