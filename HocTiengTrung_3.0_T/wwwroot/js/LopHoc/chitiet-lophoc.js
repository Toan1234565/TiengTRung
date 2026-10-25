/**
 * chitiet-lophoc.js - Nghiệp vụ chi tiết lớp học, phân công giảng viên & thành viên lớp
 */
const API_BASE_LOPHOC = '/api/quanlylophoc';
const API_RESET_PASSWORD_URL = '/api/quanlytaikhoannguoidung/admin/cap-lai-mat-khau';

let modalSuaLopInstance = null;
let modalThemHvInstance = null;

document.addEventListener('DOMContentLoaded', function () {
    const modalSuaEl = document.getElementById('modalSuaLopHoc');
    if (modalSuaEl) modalSuaLopInstance = new bootstrap.Modal(modalSuaEl);

    const modalThemEl = document.getElementById('modalThemHocVien');
    if (modalThemEl) modalThemHvInstance = new bootstrap.Modal(modalThemEl);
});

// 1. Tải trước danh sách giảng viên trực thuộc trung tâm để gán vào dropdown modal
function taiDanhSachGiangVienTrungTam(idTrungTam, callback) {
    if (!idTrungTam || idTrungTam <= 0) return;

    $.ajax({
        url: `/api/quanlytrungtam/danh-sach-nhan-su?idTrungTam=${idTrungTam}&trangThai=true`,
        type: 'GET',
        xhrFields: { withCredentials: true },
        success: function (res) {
            if (res && res.success && res.data) {
                let listGv = res.data.filter(ns => {
                    const vaiTro = (ns.tenVaiTro || '').toLowerCase();
                    return vaiTro.includes('giảng viên') || vaiTro.includes('giang vien') || vaiTro.includes('teacher');
                });
                if (listGv.length === 0) listGv = res.data;
                if (typeof callback === 'function') callback(listGv);
            }
        },
        error: function (xhr) {
            console.warn("Lỗi khi tải danh sách giảng viên cơ sở:", xhr.responseJSON?.message || xhr.statusText);
        }
    });
}

// 2. Mở Modal sửa lớp học & dựng danh sách giảng viên phân công
function moModalSuaLopHoc(id, ten, mota, khaiGiang, ketThuc, trangThai, dsGvHienTai, dsAllGv) {
    $('#editTenLopHoc').val(ten);
    $('#editMoTa').val(mota);
    $('#editNgayKhaiGiang').val(khaiGiang);
    $('#editNgayKetThuc').val(ketThuc);
    $('#editTrangThai').prop('checked', trangThai);

    $('#containerGiangVien').empty();
    if (dsGvHienTai && dsGvHienTai.length > 0) {
        dsGvHienTai.forEach(gv => {
            themDongGiangVien(gv.idTaiKhoan || gv.IdTaiKhoan, gv.vaiTroDay || gv.VaiTroDay, dsAllGv);
        });
    } else {
        themDongGiangVien(0, 'Giảng viên chính', dsAllGv);
    }

    if (modalSuaLopInstance) modalSuaLopInstance.show();
}

function themDongGiangVien(selectedId = 0, selectedRole = 'Giảng viên chính', dsAllGv = []) {
    let optionsHtml = '<option value="">-- Chọn giảng viên --</option>';

    if (dsAllGv.length > 0) {
        dsAllGv.forEach(gv => {
            const isSelected = (gv.idTaiKhoan == selectedId) ? 'selected' : '';
            optionsHtml += `<option value="${gv.idTaiKhoan}" ${isSelected}>${gv.hoTen || gv.tenDangNhap} (${gv.tenDangNhap})</option>`;
        });
    } else if (selectedId > 0) {
        optionsHtml += `<option value="${selectedId}" selected>Giảng viên #${selectedId}</option>`;
    }

    const rowHtml = `
        <div class="row g-2 align-items-center gv-row mb-2">
            <div class="col-7">
                <select class="form-select form-select-sm select-gv" required>
                    ${optionsHtml}
                </select>
            </div>
            <div class="col-4">
                <select class="form-select form-select-sm select-role">
                    <option value="Giảng viên chính" ${selectedRole === 'Giảng viên chính' ? 'selected' : ''}>Giảng viên chính</option>
                    <option value="Trợ giảng" ${selectedRole === 'Trợ giảng' ? 'selected' : ''}>Trợ giảng</option>
                </select>
            </div>
            <div class="col-1 text-end">
                <button type="button" class="btn btn-sm btn-outline-danger" onclick="$(this).closest('.gv-row').remove()" title="Xóa">
                    <i class="fa-solid fa-xmark"></i>
                </button>
            </div>
        </div>
    `;
    $('#containerGiangVien').append(rowHtml);
}

// 3. Xử lý lưu cập nhật lớp học & danh sách phân công giảng viên
function xuLyCapNhatLopHocChiTiet(e, idLopHoc) {
    e.preventDefault();

    const tenLop = $('#editTenLopHoc').val().trim();
    const moTa = $('#editMoTa').val().trim();
    const ngayKhaiGiang = $('#editNgayKhaiGiang').val() || null;
    const ngayKetThuc = $('#editNgayKetThuc').val() || null;
    const trangThai = $('#editTrangThai').is(':checked');

    if (ngayKhaiGiang && ngayKetThuc && new Date(ngayKhaiGiang) > new Date(ngayKetThuc)) {
        Swal.fire('Cảnh báo', 'Ngày bế giảng phải diễn ra sau ngày khai giảng!', 'warning');
        return;
    }

    const danhSachGiangVien = [];
    let biTrungLap = false;
    const setIds = new Set();

    $('#containerGiangVien .gv-row').each(function () {
        const idGv = parseInt($(this).find('.select-gv').val());
        const roleGv = $(this).find('.select-role').val();

        if (idGv && idGv > 0) {
            if (setIds.has(idGv)) {
                biTrungLap = true;
            }
            setIds.add(idGv);
            danhSachGiangVien.push({
                IdTaiKhoan: idGv,
                VaiTroDay: roleGv
            });
        }
    });

    if (biTrungLap) {
        Swal.fire('Cảnh báo', 'Một giảng viên không thể được chọn 2 lần trong cùng một lớp!', 'warning');
        return;
    }

    const postData = {
        IdLopHoc: idLopHoc,
        TenLopHoc: tenLop,
        MoTa: moTa,
        NgayKhaiGiang: ngayKhaiGiang,
        NgayKetThuc: ngayKetThuc,
        TrangThai: trangThai,
        DanhSachGiangVien: danhSachGiangVien
    };

    const $btn = $('#btnSubmitSuaLop');
    $btn.prop('disabled', true).html('<i class="fa-solid fa-spinner fa-spin me-1"></i> Đang lưu...');

    $.ajax({
        url: `${API_BASE_LOPHOC}/update-lophoc/${idLopHoc}`,
        type: 'PUT',
        contentType: 'application/json',
        data: JSON.stringify(postData),
        xhrFields: { withCredentials: true },
        success: function (res) {
            if (modalSuaLopInstance) modalSuaLopInstance.hide();
            Swal.fire({
                icon: 'success',
                title: 'Thành công!',
                text: res.message || 'Cập nhật lớp học và phân công giảng viên thành công!',
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

// 4. Xóa lớp học hiện tại từ màn hình chi tiết
function xoaLopHocHienTai(id, tenLop, redirectUrl) {
    Swal.fire({
        title: 'Xác nhận xóa lớp học?',
        html: `Bạn có chắc chắn muốn xóa lớp <strong>${tenLop}</strong> (#${id})?<br><small class="text-danger">Hành động này sẽ xóa cả phân công giảng viên và học viên của lớp này!</small>`,
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
                    }).then(() => {
                        window.location.href = redirectUrl;
                    });
                },
                error: function (xhr) {
                    Swal.fire('Thất bại', xhr.responseJSON?.message || 'Không thể xóa lớp học này.', 'error');
                }
            });
        }
    });
}

// 5. Thêm học viên vào lớp & Tự động cấp tài khoản
function moModalThemHocVien() {
    const form = document.getElementById('formThemHocVien');
    if (form) form.reset();
    if (modalThemHvInstance) modalThemHvInstance.show();
}

function xuLyThemHocVien(e, idLopHoc) {
    e.preventDefault();

    const postData = {
        IdLopHoc: idLopHoc,
        HoTen: $('#txtTenHocVienMoi').val().trim(),
        Email: $('#txtEmailHocVienMoi').val().trim() || null,
        SoDienThoai: $('#txtSdtHocVienMoi').val().trim() || null,
        NgaySinh: $('#txtNgaySinhHocVienMoi').val() || null
    };

    const $btn = $('#btnSubmitThemHocVien');
    $btn.prop('disabled', true).html('<i class="fa-solid fa-spinner fa-spin me-1"></i> Đang thêm...');

    $.ajax({
        url: `${API_BASE_LOPHOC}/them-hoc-vien`,
        type: 'POST',
        contentType: 'application/json',
        data: JSON.stringify(postData),
        xhrFields: { withCredentials: true },
        success: function (res) {
            if (modalThemHvInstance) modalThemHvInstance.hide();
            const tkInfo = res.data ? `<br><small class="text-muted">Tài khoản: <strong>${res.data.tenDangNhap}</strong> (Mật khẩu: ${res.data.matKhauMacDinh})</small>` : '';

            Swal.fire({
                icon: 'success',
                title: 'Thành công!',
                html: `${res.message || 'Đã thêm học viên vào lớp!'}${tkInfo}`,
                confirmButtonText: 'Đóng'
            }).then(() => location.reload());
        },
        error: function (xhr) {
            Swal.fire('Thất bại', xhr.responseJSON?.message || 'Không thể thêm học viên vào lớp.', 'error');
        },
        complete: function () {
            $btn.prop('disabled', false).html('<i class="fa-solid fa-check me-1"></i> Thêm vào lớp');
        }
    });
}

// 6. Xóa học viên khỏi lớp
function xoaHocVienKhoiLop(idLopHoc, idUser, hoTen) {
    Swal.fire({
        title: 'Xóa học viên?',
        text: `Bạn có chắc muốn xóa học viên [${hoTen}] ra khỏi lớp học này?`,
        icon: 'question',
        showCancelButton: true,
        confirmButtonColor: '#dc3545',
        cancelButtonColor: '#6c757d',
        confirmButtonText: 'Đồng ý xóa',
        cancelButtonText: 'Hủy'
    }).then((result) => {
        if (result.isConfirmed) {
            $.ajax({
                url: `${API_BASE_LOPHOC}/xoa-hoc-vien?idLopHoc=${idLopHoc}&idUser=${idUser}`,
                type: 'DELETE',
                xhrFields: { withCredentials: true },
                success: function (res) {
                    Swal.fire({
                        icon: 'success',
                        title: 'Thành công',
                        text: res.message || 'Đã xóa học viên khỏi lớp!',
                        timer: 1200,
                        showConfirmButton: false
                    });
                    $(`#row-hv-${idUser}`).fadeOut(300, function () {
                        $(this).remove();
                        const currentTotal = $('#tableHocVien tbody tr').length;
                        $('#kpiTongSoHocVien').text(currentTotal);
                        $('#lblSubTotalHocVien').text(`Tổng cộng ${currentTotal} học viên đã ghi danh`);
                    });
                },
                error: function (xhr) {
                    Swal.fire('Lỗi', xhr.responseJSON?.message || 'Không thể xóa học viên.', 'error');
                }
            });
        }
    });
}

// 7. Cấp lại mật khẩu cho học viên
function capLaiMatKhauHocVien(idTaiKhoan, hoTen) {
    if (!idTaiKhoan || idTaiKhoan <= 0) {
        Swal.fire('Cảnh báo', 'Học viên này chưa được liên kết với tài khoản hệ thống!', 'warning');
        return;
    }

    Swal.fire({
        title: 'Cấp lại mật khẩu',
        html: `Nhập mật khẩu mới cho học viên: <strong>${hoTen}</strong>`,
        input: 'password',
        inputPlaceholder: 'Tối thiểu 6 ký tự...',
        showCancelButton: true,
        confirmButtonText: 'Xác nhận',
        cancelButtonText: 'Hủy',
        confirmButtonColor: '#ffc107',
        cancelButtonColor: '#6c757d',
        showLoaderOnConfirm: true,
        inputValidator: (val) => {
            if (!val || val.trim().length < 6) return 'Mật khẩu phải từ 6 ký tự trở lên!';
        },
        preConfirm: (newPassword) => {
            return fetch(API_RESET_PASSWORD_URL, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ IdTaiKhoan: idTaiKhoan, MatKhauMoi: newPassword.trim() })
            })
                .then(async res => {
                    const data = await res.json().catch(() => null);
                    if (!res.ok) throw new Error(data?.message || `Lỗi hệ thống: ${res.status}`);
                    return data;
                })
                .catch(err => {
                    Swal.showValidationMessage(`Thất bại: ${err.message}`);
                });
        },
        allowOutsideClick: () => !Swal.isLoading()
    }).then((res) => {
        if (res.isConfirmed && res.value) {
            Swal.fire('Thành công!', res.value.message || 'Đã đổi mật khẩu thành công.', 'success');
        }
    });
}

// 8. Lọc học viên phía client
function locDanhSachHocVienClient(inputId = 'filterHocVienInput', tableId = 'tableHocVien') {
    const filter = document.getElementById(inputId).value.toLowerCase().trim();
    const tr = document.getElementById(tableId).getElementsByTagName('tr');

    for (let i = 1; i < tr.length; i++) {
        const nameEl = tr[i].querySelector('.hv-name');
        const contactEls = tr[i].querySelectorAll('.hv-contact');

        let textToSearch = "";
        if (nameEl) textToSearch += nameEl.textContent.toLowerCase() + " ";
        contactEls.forEach(el => textToSearch += el.textContent.toLowerCase() + " ");

        if (nameEl) {
            tr[i].style.display = textToSearch.indexOf(filter) > -1 ? "" : "none";
        }
    }
}