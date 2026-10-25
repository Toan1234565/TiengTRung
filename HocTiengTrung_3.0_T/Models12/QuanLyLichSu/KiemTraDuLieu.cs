using System;
using System.Collections.Generic;
using System.Linq;

namespace HocTiengTrung_3._0_T.Models12.QuanLyLichSu
{
    public class KiemTraDuLieu
    {
        /// <summary>
        /// So sánh 2 object cùng hoặc khác kiểu và chỉ trả về các thuộc tính có giá trị thay đổi.
        /// Trả về 2 Dictionary: duLieuCu (chỉ chứa các trường bị đổi với giá trị cũ)
        ///                      duLieuMoi (chỉ chứa các trường bị đổi với giá trị mới)
        /// </summary>
        public static (Dictionary<string, object?> duLieuCu, Dictionary<string, object?> duLieuMoi) GetChanges(object oldObj, object newObj)
        {
            var duLieuCu = new Dictionary<string, object?>();
            var duLieuMoi = new Dictionary<string, object?>();

            if (oldObj == null || newObj == null)
                return (duLieuCu, duLieuMoi);

            var oldProps = oldObj.GetType().GetProperties();
            var newProps = newObj.GetType().GetProperties().ToDictionary(p => p.Name, p => p);

            foreach (var oldProp in oldProps)
            {
                if (!newProps.TryGetValue(oldProp.Name, out var newProp))
                    continue;

                var oldVal = oldProp.GetValue(oldObj);
                var newVal = newProp.GetValue(newObj);

                bool isDifferent = false;
                if (oldVal == null && newVal == null)
                {
                    isDifferent = false;
                }
                else if (oldVal == null || newVal == null)
                {
                    isDifferent = true;
                }
                else if (oldVal is DateTime dtOld && newVal is DateTime dtNew)
                {
                    isDifferent = Math.Abs((dtOld - dtNew).TotalSeconds) >= 1;
                }
                else
                {
                    isDifferent = !oldVal.Equals(newVal);
                }

                if (isDifferent)
                {
                    duLieuCu[oldProp.Name] = oldVal;
                    duLieuMoi[newProp.Name] = newVal;
                }
            }

            return (duLieuCu, duLieuMoi);
        }
    }
}