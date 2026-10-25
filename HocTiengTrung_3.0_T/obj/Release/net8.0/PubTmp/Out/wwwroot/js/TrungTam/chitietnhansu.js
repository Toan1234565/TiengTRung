/**
 * chitietnhansu.js - Xử lý nghiệp vụ chi tiết nhân sự / giảng viên
 */

const API_CAP_NHAT_THONG_TIN = '/api/quanlytaikhoannguoidung/cap-nhat-thong-tin';
const API_DOI_MAT_KHAU_SELF = '/api/quanlytaikhoannguoidung/doi-mat-khau';
const API_RESET_PASSWORD = '/api/quanlytaikhoannguoidung/admin/cap-lai-mat-khau';
const API_TOGGLE_KHOA_BASE = '/api/quanlytaikhoannguoidung/toggle-khoa-tai-khoan';
const API_GAN_VAI_TRO = '/api/quanlyvaitro/gan-vaitro';
const API_LAY_DANH_SACH_VAI_TRO = '/api/quanlyvaitro/getallvaitro';

let danhSachVaiTroCache = [];

// =========================================================================
// KHỞI TẠO VÀ LẮNG NGHE SỰ KIỆN DOM
// =========================================================================
$(document).ready(function () {
    // 1. Mở modal cập nhật thông tin cá nhân (Self)
    $('#btnMoModalCapNhatThongTin').on('click', function () {
        const modalEl = document.getElementById('modalCapNhatThongTin');
        if (modalEl) bootstrap.Modal.getOrCreateInstance(modalEl).show();
    });

    // 2. Mở modal đổi mật khẩu của chính mình (Self)
    $('#btnMoModalDoiMatKhauSelf').on('click', function () {
        const form = document.getElementById('formDoiMatKhauSelf');
        if (form) form.reset();
        $('#txtSelfXacNhanMatKhau').removeClass('is-invalid');

        const modalEl = document.getElementById('modalDoiMatKhauSelf');
        if (modalEl) bootstrap.Modal.getOrCreateInstance(modalEl).show();
    });

    // 3. Mở modal cấp lại mật khẩu (Admin/Quản lý)
    $('#btnMoModalCapLaiMatKhau').on('click', function () {
        const id = $(this).data('id');
        const username = $(this).data('username');

        if (!id || id <= 0) {
            Swal.fire('Lỗi', 'ID tài khoản không hợp lệ!', 'error');
            return;
        }

        $('#resetIdTaiKhoan').val(id);
        $('#txtResetTenDangNhap').text(username);

        const form = document.getElementById('formCapLaiMatKhau');
        if (form) form.reset();
        $('#txtXacNhanMatKhauMoi').removeClass('is-invalid');

        const modalEl = document.getElementById('modalCapLaiMatKhau');
        if (modalEl) bootstrap.Modal.getOrCreateInstance(modalEl).show();
    });

    // 4. Mở modal gán vai trò (Admin/Quản lý)
    $('#btnMoModalGanVaiTro').on('click', function () {
        const idTaiKhoan = $(this).data('id');
        const idVaiTroHienTai = $(this).data('idvaitro');
        const tenVaiTroHienTai = $(this).data('tenvaitro');
        const hoTen = $(this).data('hoten');

        $('#assignIdTaiKhoan').val(idTaiKhoan);
        $('#txtAssignHoTen').text(hoTen);
        $('#txtVaiTroHienTai').val(tenVaiTroHienTai || 'Chưa gán vai trò');

        loadDanhSachVaiTro(idVaiTroHienTai);

        const modalEl = document.getElementById('modalGanVaiTro');
        if (modalEl) bootstrap.Modal.getOrCreateInstance(modalEl).show();
    });
});

// Hàm xem trước ảnh đại diện khi chọn file từ máy tính
function previewImageAvatar(event) {
    const file = event.target.files[0];
    if (file) {
        if (file.size > 5 * 1024 * 1024) {
            Swal.fire('Cảnh báo', 'Kích thước ảnh đại diện không được vượt quá 5MB!', 'warning');
            event.target.value = '';
            return;
        }

        const reader = new FileReader();
        reader.onload = function (e) {
            const previewEl = document.getElementById('imgPreviewAvatar');
            if (previewEl) previewEl.src = e.target.result;
        };
        reader.readAsDataURL(file);
    }
}

// Ẩn / hiện mật khẩu
function togglePasswordVisibility(inputId, btnEl) {
    const input = document.getElementById(inputId);
    if (!input) return;
    const icon = btnEl.querySelector('i');
    if (input.type === "password") {
        input.type = "text";
        if (icon) {
            icon.classList.remove('fa-eye');
            icon.classList.add('fa-eye-slash');
        }
    } else {
        input.type = "password";
        if (icon) {
            icon.classList.remove('fa-eye-slash');
            icon.classList.add('fa-eye');
        }
    }
}

// =========================================================================
// 1. CẬP NHẬT THÔNG TIN CÁ NHÂN (SELF)
// =========================================================================
function xuLyCapNhatThongTin(e) {
    e.preventDefault();

    const hoTen = $('#txtEditHoTen').val().trim();
    const email = $('#txtEditEmail').val().trim();
    const sdt = $('#txtEditSdt').val().trim();
    const ngaySinh = $('#txtEditNgaySinh').val();

    if (!hoTen) {
        Swal.fire('Cảnh báo', 'Họ và tên không được để trống!', 'warning');
        return;
    }

    const formData = new FormData();
    formData.append('HoTen', hoTen);
    if (email) formData.append('Email', email);
    if (sdt) formData.append('SoDienThoai', sdt);
    if (ngaySinh) formData.append('NgaySinh', ngaySinh);

    const fileInput = document.getElementById('fileAvatarInput');
    if (fileInput && fileInput.files.length > 0) {
        formData.append('fileAnhDaiDien', fileInput.files[0]);
    } else {
        const anhHienTai = $('#txtEditAnhDaiDien').val().trim();
        if (anhHienTai) formData.append('AnhDaiDien', anhHienTai);
    }

    const $btn = $('#btnSubmitCapNhatThongTin');
    $btn.prop('disabled', true).html('<i class="fa-solid fa-spinner fa-spin me-1"></i> Đang lưu...');

    $.ajax({
        url: API_CAP_NHAT_THONG_TIN,
        type: 'POST',
        data: formData,
        processData: false,
        contentType: false,
        xhrFields: { withCredentials: true },
        success: function (res) {
            const modalEl = document.getElementById('modalCapNhatThongTin');
            if (modalEl) bootstrap.Modal.getOrCreateInstance(modalEl).hide();

            Swal.fire({
                icon: 'success',
                title: 'Thành công!',
                text: res.message || 'Cập nhật thông tin cá nhân thành công!',
                timer: 1500,
                showConfirmButton: false
            }).then(() => {
                location.reload();
            });
        },
        error: function (xhr) {
            const msg = xhr.responseJSON?.message || 'Có lỗi xảy ra khi cập nhật thông tin.';
            Swal.fire('Thất bại', msg, 'error');
        },
        complete: function () {
            $btn.prop('disabled', false).html('<i class="fa-solid fa-floppy-disk me-1"></i> Lưu thay đổi');
        }
    });
}

// =========================================================================
// 2. TỰ ĐỔI MẬT KHẨU (SELF)
// =========================================================================
function xuLyDoiMatKhauSelf(e) {
    e.preventDefault();

    const matKhauCu = $('#txtSelfMatKhauCu').val().trim();
    const matKhauMoi = $('#txtSelfMatKhauMoi').val().trim();
    const xacNhan = $('#txtSelfXacNhanMatKhau').val().trim();

    if (matKhauMoi !== xacNhan) {
        $('#txtSelfXacNhanMatKhau').addClass('is-invalid');
        return;
    }
    $('#txtSelfXacNhanMatKhau').removeClass('is-invalid');

    const postData = {
        MatKhauCu: matKhauCu,
        MatKhauMoi: matKhauMoi
    };

    const $btn = $('#btnSubmitDoiMatKhauSelf');
    $btn.prop('disabled', true).html('<i class="fa-solid fa-spinner fa-spin me-1"></i> Đang đổi...');

    $.ajax({
        url: API_DOI_MAT_KHAU_SELF,
        type: 'POST',
        contentType: 'application/json',
        data: JSON.stringify(postData),
        xhrFields: { withCredentials: true },
        success: function (res) {
            const modalEl = document.getElementById('modalDoiMatKhauSelf');
            if (modalEl) bootstrap.Modal.getOrCreateInstance(modalEl).hide();

            Swal.fire({
                icon: 'success',
                title: 'Thành công!',
                text: res.message || 'Đổi mật khẩu thành công!',
                confirmButtonText: 'Đóng'
            });
        },
        error: function (xhr) {
            const msg = xhr.responseJSON?.message || 'Không thể đổi mật khẩu. Vui lòng kiểm tra lại mật khẩu cũ.';
            Swal.fire('Thất bại', msg, 'error');
        },
        complete: function () {
            $btn.prop('disabled', false).html('<i class="fa-solid fa-check me-1"></i> Đổi mật khẩu');
        }
    });
}

// =========================================================================
// 3. CẤP LẠI MẬT KHẨU (ADMIN/QUẢN LÝ THỰC HIỆN QUA MODAL)
// =========================================================================
function xuLyCapLaiMatKhau(e) {
    e.preventDefault();

    const idTaiKhoan = parseInt($('#resetIdTaiKhoan').val()) || 0;
    const matKhauMoi = $('#txtMatKhauMoi').val().trim();
    const xacNhan = $('#txtXacNhanMatKhauMoi').val().trim();
    const token = $('input[name="__RequestVerificationToken"]').val();

    if (matKhauMoi !== xacNhan) {
        $('#txtXacNhanMatKhauMoi').addClass('is-invalid');
        return;
    }
    $('#txtXacNhanMatKhauMoi').removeClass('is-invalid');

    const $btn = $('#btnSubmitResetPassword');
    $btn.prop('disabled', true).html('<i class="fa-solid fa-spinner fa-spin me-1"></i> Đang xử lý...');

    const postData = {
        IdTaiKhoan: idTaiKhoan,
        MatKhauMoi: matKhauMoi
    };

    $.ajax({
        url: API_RESET_PASSWORD,
        type: 'POST',
        contentType: 'application/json',
        data: JSON.stringify(postData),
        headers: {
            'RequestVerificationToken': token
        },
        xhrFields: { withCredentials: true },
        success: function (res) {
            const modalEl = document.getElementById('modalCapLaiMatKhau');
            if (modalEl) bootstrap.Modal.getOrCreateInstance(modalEl).hide();

            Swal.fire({
                icon: 'success',
                title: 'Thành công!',
                text: res.message || 'Cấp lại mật khẩu thành công!',
                confirmButtonText: 'Đóng'
            });
        },
        error: function (xhr) {
            const msg = xhr.responseJSON?.message || 'Có lỗi xảy ra khi thực hiện cấp lại mật khẩu.';
            Swal.fire('Thất bại', msg, 'error');
        },
        complete: function () {
            $btn.prop('disabled', false).html('<i class="fa-solid fa-check me-1"></i> Xác nhận cấp lại');
        }
    });
}

// =========================================================================
// 4. KHÓA / MỞ KHÓA TÀI KHOẢN (GỌI API TRỰC TIẾP QUA SWEETALERT2)
// =========================================================================
function xuLyToggleKhoaTaiKhoan(idTaiKhoan, tenDangNhap, trangThaiHienTai) {
    const hanhDong = trangThaiHienTai ? "KHÓA" : "MỞ KHÓA";
    const confirmColor = trangThaiHienTai ? "#dc3545" : "#198754";

    Swal.fire({
        title: `Xác nhận ${hanhDong}?`,
        text: `Bạn có chắc chắn muốn ${hanhDong.toLowerCase()} tài khoản [${tenDangNhap}] không?`,
        icon: 'warning',
        showCancelButton: true,
        confirmButtonColor: confirmColor,
        cancelButtonColor: '#6c757d',
        confirmButtonText: `Đồng ý ${hanhDong.toLowerCase()}`,
        cancelButtonText: 'Hủy'
    }).then((result) => {
        if (result.isConfirmed) {
            $.ajax({
                url: `${API_TOGGLE_KHOA_BASE}/${idTaiKhoan}`,
                type: 'POST',
                xhrFields: { withCredentials: true },
                success: function (res) {
                    Swal.fire({
                        icon: 'success',
                        title: 'Thành công!',
                        text: res.message || `${hanhDong} tài khoản thành công!`,
                        timer: 1500,
                        showConfirmButton: false
                    }).then(() => {
                        location.reload();
                    });
                },
                error: function (xhr) {
                    const msg = xhr.responseJSON?.message || 'Không thể thay đổi trạng thái tài khoản.';
                    Swal.fire('Thất bại', msg, 'error');
                }
            });
        }
    });
}

// =========================================================================
// 5. GÁN VAI TRÒ
// =========================================================================
function loadDanhSachVaiTro(idVaiTroSelected) {
    const $select = $('#selectVaiTroMoi');

    if (danhSachVaiTroCache.length > 0) {
        renderDropdownVaiTro(danhSachVaiTroCache, idVaiTroSelected);
        return;
    }

    $select.html('<option value="">-- Đang nạp danh sách vai trò... --</option>').prop('disabled', true);

    $.ajax({
        url: API_LAY_DANH_SACH_VAI_TRO,
        type: 'GET',
        xhrFields: { withCredentials: true },
        success: function (res) {
            if (res.success && res.data) {
                danhSachVaiTroCache = res.data;
                renderDropdownVaiTro(res.data, idVaiTroSelected);
            } else {
                $select.html('<option value="">-- Không thể tải vai trò --</option>');
            }
        },
        error: function () {
            $select.html('<option value="">-- Lỗi kết nối danh sách vai trò --</option>');
        },
        complete: function () {
            $select.prop('disabled', false);
        }
    });
}

function renderDropdownVaiTro(list, idSelected) {
    const $select = $('#selectVaiTroMoi');
    let html = '<option value="">-- Chọn vai trò mới --</option>';

    list.forEach(v => {
        const selected = (v.idVaiTro == idSelected) ? 'selected' : '';
        html += `<option value="${v.idVaiTro}" ${selected}>${v.tenVaiTro}</option>`;
    });

    $select.html(html);
}

function xuLyGanVaiTro(e) {
    e.preventDefault();

    const idTaiKhoan = parseInt($('#assignIdTaiKhoan').val()) || 0;
    const idVaiTro = parseInt($('#selectVaiTroMoi').val()) || 0;

    if (!idVaiTro) {
        Swal.fire('Cảnh báo', 'Vui lòng chọn một vai trò mới!', 'warning');
        return;
    }

    const $btn = $('#btnSubmitGanVaiTro');
    $btn.prop('disabled', true).html('<i class="fa-solid fa-spinner fa-spin me-1"></i> Đang gán...');

    const postData = {
        IdTaiKhoan: idTaiKhoan,
        IdVaiTro: idVaiTro
    };

    $.ajax({
        url: API_GAN_VAI_TRO,
        type: 'POST',
        contentType: 'application/json',
        data: JSON.stringify(postData),
        xhrFields: { withCredentials: true },
        success: function (res) {
            const modalEl = document.getElementById('modalGanVaiTro');
            if (modalEl) bootstrap.Modal.getOrCreateInstance(modalEl).hide();

            Swal.fire({
                icon: 'success',
                title: 'Thành công!',
                text: res.message || 'Gán vai trò thành công!',
                timer: 1500,
                showConfirmButton: false
            }).then(() => {
                location.reload();
            });
        },
        error: function (xhr) {
            const msg = xhr.responseJSON?.message || 'Có lỗi xảy ra khi gán vai trò.';
            Swal.fire('Thất bại', msg, 'error');
        },
        complete: function () {
            $btn.prop('disabled', false).html('<i class="fa-solid fa-check me-1"></i> Lưu thay đổi');
        }
    });
}