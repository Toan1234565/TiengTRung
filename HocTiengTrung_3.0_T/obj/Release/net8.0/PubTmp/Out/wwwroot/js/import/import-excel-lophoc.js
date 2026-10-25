/**
 * quanly-import-excel.js - Xử lý nhập lớp học & học viên từ file Excel
 */
const API_IMPORT_LOPHOC_EXCEL = '/api/quanlylophoc/import-excel-lophoc';
let modalImportInstance = null;

document.addEventListener('DOMContentLoaded', function () {
    const modalImportEl = document.getElementById('modalImportExcel');
    if (modalImportEl) modalImportInstance = new bootstrap.Modal(modalImportEl);
});

function moModalImportExcel() {
    const form = document.getElementById('formImportExcel');
    if (form) form.reset();
    if (modalImportInstance) modalImportInstance.show();
}

function taiFileMauExcel(maTrungTam = 'TT_001') {
    const csvContent =
        `ID_TrungTam,${maTrungTam},,,,\r\n` +
        `ID_LopHoc,LH01,,,,\r\n` +
        `Tên lớp học,Tiếng Trung Giao Tiếp HSK 1,,,,\r\n` +
        `Mô tả,Khóa học nền tảng tiếng Trung,,,,\r\n` +
        `Ngày bắt đầu (dd/MM/yyyy),12/11/2026,11/12/2026,,,\r\n` +
        `DanhSachHocVien,,,,Danh sách giảng viên,\r\n` +
        `Tên học viên,Email,Số điện thoại,Ngày sinh,Mã tài khoản,Vai trò\r\n` +
        `Nguyễn Văn A,nguyena@gmail.com,0987654321,12/12/2004,gv_demo,Giảng viên chính\r\n` +
        `Nguyễn Văn B,nguyenb@gmail.com,0912345678,13/12/2004,,\r\n`;

    const blob = new Blob(["\uFEFF" + csvContent], { type: 'text/csv;charset=utf-8;' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `Mau_Nhap_LopHoc_${maTrungTam}.csv`;
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
    URL.revokeObjectURL(url);
}

function xuLyImportExcel(event) {
    event.preventDefault();
    const fileInput = document.getElementById('fileExcelLopHoc');
    if (!fileInput.files || fileInput.files.length === 0) {
        Swal.fire('Cảnh báo', 'Vui lòng chọn một file Excel (.xlsx, .xls)!', 'warning');
        return;
    }

    const formData = new FormData();
    formData.append('fileExcel', fileInput.files[0]);

    const btnSubmit = document.getElementById('btnSubmitImport');
    btnSubmit.disabled = true;
    btnSubmit.innerHTML = '<i class="fa-solid fa-spinner fa-spin me-1"></i> Đang khởi tạo...';

    Swal.fire({
        title: 'Đang xử lý dữ liệu...',
        text: 'Hệ thống đang thêm lớp học và khởi tạo tài khoản học viên, vui lòng đợi!',
        allowOutsideClick: false,
        didOpen: () => Swal.showLoading()
    });

    fetch(API_IMPORT_LOPHOC_EXCEL, {
        method: 'POST',
        body: formData
    })
        .then(async response => {
            if (!response.ok) {
                const errorJson = await response.json().catch(() => null);
                throw new Error(errorJson?.message || `Lỗi máy chủ: ${response.status}`);
            }
            return response.blob();
        })
        .then(blob => {
            if (modalImportInstance) modalImportInstance.hide();
            const downloadUrl = window.URL.createObjectURL(blob);
            const a = document.createElement('a');
            a.href = downloadUrl;
            a.download = `DanhSach_TaiKhoan_LopHoc_${new Date().getTime()}.xlsx`;
            document.body.appendChild(a);
            a.click();
            document.body.removeChild(a);
            window.URL.revokeObjectURL(downloadUrl);

            Swal.fire({
                icon: 'success',
                title: 'Nhập thành công!',
                text: 'Lớp học và học viên đã được tạo. File danh sách tài khoản đã tải về máy.',
                confirmButtonText: 'Tải lại trang'
            }).then(() => location.reload());
        })
        .catch(error => {
            Swal.fire('Thất bại', error.message || 'Không thể nhập file Excel.', 'error');
        })
        .finally(() => {
            btnSubmit.disabled = false;
            btnSubmit.innerHTML = '<i class="fa-solid fa-upload me-1"></i> Bắt đầu xử lý';
        });
}