/**
 * danhsach-lophoc.js - Nghiệp vụ danh sách lớp học & Thêm mới thủ công
 */
const API_BASE_LOPHOC = '/api/quanlylophoc';
const API_THEM_LOP_THU_CONG = '/api/quanlylophoc/them-lophoc-thucong';

let modalSuaLopInstance = null;
let modalThemLopInstance = null;

// Biến lưu trữ phục vụ modal thêm lớp thủ công
let danhSachGiangVienPhanCong = [];
let danhSachNhanSuCache = [];

document.addEventListener('DOMContentLoaded', function () {
    const modalSuaLopEl = document.getElementById('modalSuaLopHoc');
    if (modalSuaLopEl) modalSuaLopInstance = new bootstrap.Modal(modalSuaLopEl);

    const modalThemLopEl = document.getElementById('modalThemLopThuCong');
    if (modalThemLopEl) modalThemLopInstance = new bootstrap.Modal(modalThemLopEl);
});

// ==========================================
// 1. TÌM KIẾM VÀ LỌC DANH SÁCH LỚP REALTIME
// ==========================================
function locDanhSachLopClient(inputId = 'filterLopHocInput', tableId = 'tableLopHoc') {
    const filter = document.getElementById(inputId).value.toLowerCase().trim();
    const tr = document.getElementById(tableId).getElementsByTagName('tr');
    for (let i = 1; i < tr.length; i++) {
        const nameEl = tr[i].querySelector('.lop-name');
        if (nameEl) {
            const rowText = tr[i].textContent.toLowerCase();
            tr[i].style.display = rowText.indexOf(filter) > -1 ? "" : "none";
        }
    }
}

// ==========================================
// 2. MỞ MODAL SỬA THÔNG TIN LỚP HỌC
// ==========================================
function moModalSuaLopHoc(id, ten, mota, khaiGiang, ketThuc, lichHoc, trangThai) {
    $('#editIdLopHoc').val(id);
    $('#editTenLopHoc').val(ten);
    $('#editMoTa').val(mota);
    $('#editNgayKhaiGiang').val(khaiGiang);
    $('#editNgayKetThuc').val(ketThuc);
    $('#editLichHocTrongTuan').val(lichHoc);
    $('#editTrangThai').prop('checked', trangThai);

    $('#modalSuaLopHocTitle').html(`<i class="fa-regular fa-pen-to-square me-2"></i>Chỉnh sửa lớp #${id}`);
    if (modalSuaLopInstance) modalSuaLopInstance.show();
}

// ==========================================
// 3. GỬI API PUT CẬP NHẬT LỚP HỌC
// ==========================================
function xuLyCapNhatLopHoc(e) {
    e.preventDefault();

    const id = parseInt($('#editIdLopHoc').val()) || 0;
    const tenLop = $('#editTenLopHoc').val().trim();
    const moTa = $('#editMoTa').val().trim();
    const ngayKhaiGiang = $('#editNgayKhaiGiang').val() || null;
    const ngayKetThuc = $('#editNgayKetThuc').val() || null;
    const lichHoc = $('#editLichHocTrongTuan')?.val()?.trim() || '';
    const trangThai = $('#editTrangThai').is(':checked');

    if (!tenLop) {
        Swal.fire('Cảnh báo', 'Tên lớp học không được để trống!', 'warning');
        return;
    }

    const postData = {
        IdLopHoc: id,
        TenLopHoc: tenLop,
        MoTa: moTa,
        NgayKhaiGiang: ngayKhaiGiang,
        NgayKetThuc: ngayKetThuc,
        LichHocTrongTuan: lichHoc,
        TrangThai: trangThai
    };

    const $btn = $('#btnSubmitSuaLop');
    $btn.prop('disabled', true).html('<i class="fa-solid fa-spinner fa-spin me-1"></i> Đang lưu...');

    $.ajax({
        url: `${API_BASE_LOPHOC}/update-lophoc/${id}`,
        type: 'PUT',
        contentType: 'application/json',
        data: JSON.stringify(postData),
        xhrFields: { withCredentials: true },
        success: function (res) {
            if (modalSuaLopInstance) modalSuaLopInstance.hide();
            Swal.fire({
                icon: 'success',
                title: 'Thành công!',
                text: res.message || 'Cập nhật thông tin lớp học thành công!',
                timer: 1500,
                showConfirmButton: false
            }).then(() => location.reload());
        },
        error: function (xhr) {
            const msg = xhr.responseJSON?.message || 'Có lỗi xảy ra khi cập nhật lớp học.';
            Swal.fire('Thất bại', msg, 'error');
        },
        complete: function () {
            $btn.prop('disabled', false).html('<i class="fa-solid fa-floppy-disk me-1"></i> Lưu thay đổi');
        }
    });
}

// ==========================================
// 4. XÓA LỚP HỌC (DELETE)
// ==========================================
function xoaLopHocApi(id, tenLop) {
    Swal.fire({
        title: 'Xác nhận xóa lớp học?',
        html: `Bạn có chắc chắn muốn xóa lớp <strong>${tenLop}</strong> (#${id})?<br><small class="text-danger">Lưu ý: Hành động này sẽ xóa cả liên kết học viên và giảng viên của lớp!</small>`,
        icon: 'warning',
        showCancelButton: true,
        confirmButtonColor: '#dc3545',
        cancelButtonColor: '#6c757d',
        confirmButtonText: 'Đồng ý xóa',
        cancelButtonText: 'Hủy'
    }).then((result) => {
        if (result.isConfirmed) {
            $.ajax({
                url: `${API_BASE_LOPHOC}/delete-lophoc/${id}`,
                type: 'DELETE',
                xhrFields: { withCredentials: true },
                success: function (res) {
                    Swal.fire({
                        icon: 'success',
                        title: 'Đã xóa!',
                        text: res.message || 'Xóa lớp học thành công!',
                        timer: 1500,
                        showConfirmButton: false
                    });
                    $(`#row-lophoc-${id}`).fadeOut(400, function () {
                        $(this).remove();
                    });
                },
                error: function (xhr) {
                    const msg = xhr.responseJSON?.message || 'Không thể xóa lớp học này.';
                    Swal.fire('Thất bại', msg, 'error');
                }
            });
        }
    });
}

// ==========================================
// 5. NGHIỆP VỤ MODAL THÊM LỚP HỌC THỦ CÔNG
// ==========================================
function moModalThemLopThuCong() {
    const form = document.getElementById('formThemLopThuCong');
    if (form) form.reset();

    danhSachGiangVienPhanCong = [];
    renderBangGiangVienPhanCong();

    $('#tbodyHocVienThuCong').html(`
        <tr id="rowHvEmpty">
            <td colspan="5" class="text-center text-muted small py-3">
                Chưa có học viên nào. Bấm <strong>"Thêm dòng"</strong> nếu muốn tạo tài khoản học viên ngay.
            </td>
        </tr>
    `);

    const ngayKhaiGiangInput = document.getElementById('txtThemNgayKhaiGiang');
    if (ngayKhaiGiangInput) {
        ngayKhaiGiangInput.value = new Date().toISOString().split('T')[0];
    }

    napDropdownGiangVien();

    if (modalThemLopInstance) modalThemLopInstance.show();
}

function napDropdownGiangVien() {
    const $select = $('#selectChonGiangVien');
    if ($select.length === 0) return;

    $select.empty();

    if (!danhSachNhanSuCache || danhSachNhanSuCache.length === 0) {
        // Nếu chưa có cache, kiểm tra nếu có ID_TRUNG_TAM thì gọi API lấy danh sách giảng viên
        const idTT = typeof ID_TRUNG_TAM !== 'undefined' ? ID_TRUNG_TAM : 0;
        if (idTT > 0) {
            $.ajax({
                url: `/api/quanlytrungtam/danh-sach-nhan-su?idTrungTam=${idTT}&trangThai=true`,
                type: 'GET',
                xhrFields: { withCredentials: true },
                success: function (res) {
                    if (res && res.success && res.data) {
                        danhSachNhanSuCache = res.data;
                        napDropdownGiangVien();
                    }
                }
            });
            return;
        }

        $select.append('<option value="">-- Cơ sở chưa có tài khoản giáo viên nào --</option>');
        return;
    }

    $select.append('<option value="">-- Chọn giảng viên phụ trách --</option>');
    danhSachNhanSuCache.forEach(ns => {
        $select.append(`<option value="${ns.idTaiKhoan}" data-name="${ns.hoTen}" data-user="${ns.tenDangNhap}">${ns.hoTen} (${ns.tenDangNhap}) - ${ns.tenVaiTro}</option>`);
    });
}

function themGiangVienVaoBang() {
    const selectEl = document.getElementById('selectChonGiangVien');
    if (!selectEl) return;

    const idTaiKhoan = parseInt(selectEl.value);
    if (!idTaiKhoan) {
        Swal.fire('Thông báo', 'Vui lòng chọn một giảng viên từ danh sách!', 'warning');
        return;
    }

    if (danhSachGiangVienPhanCong.some(g => g.idTaiKhoan === idTaiKhoan)) {
        Swal.fire('Thông báo', 'Giảng viên này đã được phân công vào lớp rồi!', 'warning');
        return;
    }

    const selectedOption = selectEl.options[selectEl.selectedIndex];
    const hoTen = selectedOption.getAttribute('data-name');
    const tenDangNhap = selectedOption.getAttribute('data-user');
    const vaiTroDay = document.getElementById('selectVaiTroDayLop').value;

    danhSachGiangVienPhanCong.push({
        idTaiKhoan: idTaiKhoan,
        hoTen: hoTen,
        tenDangNhap: tenDangNhap,
        vaiTroDay: vaiTroDay
    });

    renderBangGiangVienPhanCong();
    selectEl.value = "";
}

function xoaGiangVienKhoiBang(idTaiKhoan) {
    danhSachGiangVienPhanCong = danhSachGiangVienPhanCong.filter(g => g.idTaiKhoan !== idTaiKhoan);
    renderBangGiangVienPhanCong();
}

function renderBangGiangVienPhanCong() {
    const $tbody = $('#tbodyGvPhanCong');
    if ($tbody.length === 0) return;

    if (danhSachGiangVienPhanCong.length === 0) {
        $tbody.html(`
            <tr id="rowGvEmpty">
                <td colspan="4" class="text-center text-muted small py-2">Chưa phân công giảng viên nào</td>
            </tr>
        `);
        return;
    }

    let html = '';
    danhSachGiangVienPhanCong.forEach(g => {
        html += `
            <tr>
                <td class="fw-semibold text-dark">${g.hoTen}</td>
                <td class="font-monospace small">@${g.tenDangNhap}</td>
                <td><span class="badge bg-primary-subtle text-primary border border-primary-subtle">${g.vaiTroDay}</span></td>
                <td class="text-center">
                    <button type="button" class="btn btn-sm btn-link text-danger p-0" onclick="xoaGiangVienKhoiBang(${g.idTaiKhoan})">
                        <i class="fa-solid fa-trash-can"></i>
                    </button>
                </td>
            </tr>
        `;
    });
    $tbody.html(html);
}

function themDongHocVien() {
    $('#rowHvEmpty').remove();
    const rowId = 'hv_row_' + new Date().getTime();
    const trHtml = `
        <tr id="${rowId}" class="row-hoc-vien-item">
            <td><input type="text" class="form-control form-control-sm hv-hoten" placeholder="Họ và tên *" required></td>
            <td><input type="email" class="form-control form-control-sm hv-email" placeholder="Email"></td>
            <td><input type="tel" class="form-control form-control-sm hv-sdt" placeholder="Số điện thoại"></td>
            <td><input type="date" class="form-control form-control-sm hv-ngaysinh"></td>
            <td class="text-center">
                <button type="button" class="btn btn-sm btn-link text-danger p-0" onclick="$('#${rowId}').remove(); kiemTraBangHocVienRong();">
                    <i class="fa-solid fa-trash-can"></i>
                </button>
            </td>
        </tr>
    `;
    $('#tbodyHocVienThuCong').append(trHtml);
}

function kiemTraBangHocVienRong() {
    if ($('#tbodyHocVienThuCong tr').length === 0) {
        $('#tbodyHocVienThuCong').html(`
            <tr id="rowHvEmpty">
                <td colspan="5" class="text-center text-muted small py-3">
                    Chưa có học viên nào. Bấm <strong>"Thêm dòng"</strong> nếu muốn tạo tài khoản học viên ngay.
                </td>
            </tr>
        `);
    }
}

// ==========================================
// 6. GỬI API POST THÊM LỚP HỌC THỦ CÔNG
// ==========================================
function xuLyThemLopThuCong(e) {
    e.preventDefault();

    const tenLopHoc = document.getElementById('txtThemTenLop').value.trim();
    if (!tenLopHoc) {
        Swal.fire('Cảnh báo', 'Vui lòng nhập tên lớp học!', 'warning');
        return;
    }

    const maLopHoc = document.getElementById('txtThemMaLop')?.value.trim() || null;
    const ngayKhaiGiang = document.getElementById('txtThemNgayKhaiGiang')?.value || null;
    const ngayKetThuc = document.getElementById('txtThemNgayKetThuc')?.value || null;
    const moTa = document.getElementById('txtThemMoTaLop')?.value.trim() || null;

    if (ngayKhaiGiang && ngayKetThuc && ngayKetThuc < ngayKhaiGiang) {
        Swal.fire('Lỗi thời gian', 'Ngày kết thúc không thể trước ngày khai giảng!', 'warning');
        return;
    }

    const dsGiangVien = danhSachGiangVienPhanCong.map(g => ({
        idTaiKhoan: g.idTaiKhoan,
        vaiTroDay: g.vaiTroDay
    }));

    const dsHocVien = [];
    let coLoiNhapLieu = false;
    $('.row-hoc-vien-item').each(function () {
        const hoTen = $(this).find('.hv-hoten').val().trim();
        const email = $(this).find('.hv-email').val().trim() || null;
        const sdt = $(this).find('.hv-sdt').val().trim() || null;
        const ngaysinh = $(this).find('.hv-ngaysinh').val() || null;

        if (!hoTen) {
            coLoiNhapLieu = true;
            return false;
        }

        dsHocVien.push({
            hoTen: hoTen,
            email: email,
            soDienThoai: sdt,
            ngaySinh: ngaysinh
        });
    });

    if (coLoiNhapLieu) {
        Swal.fire('Cảnh báo', 'Vui lòng điền Họ và tên cho tất cả các dòng học viên đã tạo!', 'warning');
        return;
    }

    const idTT = typeof ID_TRUNG_TAM !== 'undefined' ? ID_TRUNG_TAM : null;

    const payload = {
        idTrungTam: idTT && idTT > 0 ? idTT : null,
        maLopHoc: maLopHoc,
        tenLopHoc: tenLopHoc,
        moTa: moTa,
        ngayKhaiGiang: ngayKhaiGiang,
        ngayKetThuc: ngayKetThuc,
        trangThai: true,
        danhSachGiangVien: dsGiangVien.length > 0 ? dsGiangVien : null,
        danhSachHocVien: dsHocVien.length > 0 ? dsHocVien : null
    };

    const $btn = $('#btnSubmitThemLopThuCong');
    $btn.prop('disabled', true).html('<i class="fa-solid fa-spinner fa-spin me-1"></i> Đang khởi tạo...');

    $.ajax({
        url: API_THEM_LOP_THU_CONG,
        type: 'POST',
        contentType: 'application/json',
        data: JSON.stringify(payload),
        xhrFields: { withCredentials: true },
        success: function (res) {
            if (modalThemLopInstance) modalThemLopInstance.hide();
            Swal.fire({
                icon: 'success',
                title: 'Tạo lớp thành công!',
                text: res.message || 'Lớp học đã được tạo thành công!',
                confirmButtonText: 'Đồng ý'
            }).then(() => location.reload());
        },
        error: function (xhr) {
            const msg = xhr.responseJSON?.message || 'Có lỗi xảy ra khi tạo lớp học.';
            Swal.fire('Thất bại', msg, 'error');
        },
        complete: function () {
            $btn.prop('disabled', false).html('<i class="fa-solid fa-check me-1"></i> Hoàn tất tạo lớp');
        }
    });
}