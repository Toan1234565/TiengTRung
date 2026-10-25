/**
 * nhansu-trungtam.js - Quản lý danh sách & tạo tài khoản nhân sự / giảng viên
 */
const API_TAO_TK_NOI_BO = '/api/quanlytrungtam/tao-tai-khoan-noi-bo';
const API_VAI_TRO_NOI_BO = '/api/quanlyvaitro/danh-sach-vaitro-noibo';
const API_DANH_SACH_NHAN_SU = '/api/quanlytrungtam/danh-sach-nhan-su';

// Dùng window để không bị trùng lặp khai báo 'let' với các file khác
window.modalNhanSuInstance = window.modalNhanSuInstance || null;
window.danhSachNhanSuCache = window.danhSachNhanSuCache || [];

document.addEventListener('DOMContentLoaded', function () {
    const modalNhanSuEl = document.getElementById('modalThemNhanSu');
    if (modalNhanSuEl) {
        window.modalNhanSuInstance = bootstrap.Modal.getOrCreateInstance(modalNhanSuEl);
    }
});

// ==========================================
// 1. TẢI DANH SÁCH NHÂN SỰ CỦA CƠ SỞ (API GET)
// ==========================================
function loadDanhSachNhanSu(idTrungTamParam = 0) {
    const $body = $('#bodyNhanSu');
    if ($body.length === 0) return;

    const idTT = idTrungTamParam > 0 ? idTrungTamParam : (typeof ID_TRUNG_TAM !== 'undefined' ? ID_TRUNG_TAM : 0);
    if (idTT <= 0) {
        $body.html(`
            <tr>
                <td colspan="7" class="text-center py-4 text-muted">
                    Chưa xác định được cơ sở đào tạo.
                </td>
            </tr>
        `);
        return;
    }

    $body.html(`
        <tr>
            <td colspan="7" class="text-center py-5 text-muted">
                <div class="spinner-border spinner-border-sm text-primary me-2"></div> Đang nạp danh sách nhân sự...
            </td>
        </tr>
    `);

    $.ajax({
        url: `${API_DANH_SACH_NHAN_SU}?idTrungTam=${idTT}`,
        type: 'GET',
        xhrFields: { withCredentials: true },
        success: function (res) {
            const dataList = res.data || res.Data || (Array.isArray(res) ? res : []);
            window.danhSachNhanSuCache = dataList;

            const total = res.tongSoLuong || res.total || dataList.length;
            $('#countTongNhanSu').text(total);

            if (dataList.length === 0) {
                $body.html(`
                    <tr>
                        <td colspan="7" class="text-center py-5 text-muted">
                            <i class="fa-regular fa-folder-open fa-2x mb-2 d-block"></i>
                            Chưa có nhân sự (Quản lý / Giảng viên) nào được tạo cho cơ sở này.
                        </td>
                    </tr>
                `);
                return;
            }

            renderTableNhanSu(dataList);
        },
        error: function (xhr) {
            $body.html(`
                <tr>
                    <td colspan="7" class="text-center py-4 text-danger">
                        <i class="fa-solid fa-triangle-exclamation me-1"></i> Không thể nạp danh sách nhân sự: ${xhr.responseJSON?.message || xhr.statusText || 'Lỗi kết nối máy chủ'}
                    </td>
                </tr>
            `);
        }
    });
}

// ==========================================
// 2. RENDER BẢNG DANH SÁCH NHÂN SỰ
// ==========================================
function renderTableNhanSu(data) {
    const $body = $('#bodyNhanSu');
    if ($body.length === 0) return;

    let html = '';
    const urlChiTietBase = '/QuanLyTrungtam/ChiTietNhanSu';

    data.forEach(item => {
        const idTaiKhoan = item.idTaiKhoan ?? item.IdTaiKhoan ?? 0;
        const hoTen = item.hoTen ?? item.HoTen ?? 'Chưa đặt tên';
        const tenDangNhap = item.tenDangNhap ?? item.TenDangNhap ?? '';
        const tenVaiTro = item.tenVaiTro ?? item.TenVaiTro ?? 'Giảng viên';
        const email = item.email ?? item.Email ?? '--';
        const soDienThoai = item.soDienThoai ?? item.SoDienThoai ?? '--';
        const trangThai = item.trangThai ?? item.TrangThai ?? true;
        const ngayTao = item.ngayTao ?? item.NgayTao;
        const anhDaiDien = item.anhDaiDien ?? item.AnhDaiDien;

        const isQuanLy = tenVaiTro.toLowerCase().includes('quản lý') || tenVaiTro.toLowerCase().includes('quan ly');
        const badgeClass = isQuanLy ? 'bg-danger-subtle text-danger border-danger-subtle' : 'bg-primary-subtle text-primary border-primary-subtle';
        const avatar = (anhDaiDien && anhDaiDien !== 'default_avatar.png')
            ? anhDaiDien
            : 'https://ui-avatars.com/api/?name=' + encodeURIComponent(hoTen) + '&background=random';

        html += `
            <tr class="row-nhansu">
                <td class="fw-bold text-muted">#${idTaiKhoan}</td>
                <td>
                    <div class="d-flex align-items-center gap-2">
                        <img src="${avatar}" class="rounded-circle border object-fit-cover shadow-sm" width="36" height="36" alt="${hoTen}">
                        <div>
                            <span class="fw-semibold text-dark d-block nhansu-name">${hoTen}</span>
                            <small class="text-muted">${ngayTao ? new Date(ngayTao).toLocaleDateString('vi-VN') : '--'}</small>
                        </div>
                    </div>
                </td>
                <td>
                    <span class="badge bg-light text-dark border font-monospace nhansu-username">${tenDangNhap}</span>
                </td>
                <td>
                    <span class="badge border ${badgeClass}">
                        <i class="${isQuanLy ? 'fa-solid fa-user-shield' : 'fa-solid fa-chalkboard-user'} me-1"></i>${tenVaiTro}
                    </span>
                </td>
                <td>
                    <div class="small text-muted"><i class="fa-regular fa-envelope me-1"></i>${email}</div>
                    <div class="small text-muted"><i class="fa-solid fa-phone me-1"></i>${soDienThoai}</div>
                </td>
                <td class="text-center">
                    <span class="badge ${trangThai ? 'bg-success-subtle text-success' : 'bg-secondary-subtle text-secondary'}">
                        ${trangThai ? 'Hoạt động' : 'Đã khóa'}
                    </span>
                </td>
                <td class="text-end">
                    <a href="${urlChiTietBase}/${idTaiKhoan}"
                       class="btn btn-sm btn-outline-primary d-inline-flex align-items-center gap-1 shadow-sm"
                       title="Xem hồ sơ chi tiết">
                        <i class="fa-regular fa-eye"></i> Chi tiết
                    </a>
                </td>
            </tr>
        `;
    });
    $body.html(html);
}

// ==========================================
// 3. TÌM KIẾM / LỌC NHÂN SỰ CLIENT
// ==========================================
function locDanhSachNhanSu(inputId = 'filterNhanSuInput') {
    const inputEl = document.getElementById(inputId);
    if (!inputEl) return;

    const filter = inputEl.value.toLowerCase().trim();
    const rows = document.querySelectorAll('.row-nhansu');
    rows.forEach(r => {
        const name = r.querySelector('.nhansu-name')?.textContent.toLowerCase() || '';
        const user = r.querySelector('.nhansu-username')?.textContent.toLowerCase() || '';
        r.style.display = (name.includes(filter) || user.includes(filter)) ? '' : 'none';
    });
}

// ==========================================
// 4. MỞ MODAL THÊM NHÂN SỰ
// ==========================================
function moModalThemNhanSu() {
    const form = document.getElementById('formThemNhanSu');
    if (form) form.reset();

    const $select = $('#selectVaiTroNhanSu');
    $select.html('<option value="">-- Đang nạp danh sách vai trò... --</option>').prop('disabled', true);

    $.ajax({
        url: API_VAI_TRO_NOI_BO,
        type: 'GET',
        xhrFields: { withCredentials: true },
        success: function (res) {
            const dataRoles = res.data || res.Data || [];
            if (dataRoles.length > 0) {
                let options = '<option value="">-- Chọn vai trò nhân sự --</option>';
                dataRoles.forEach(v => {
                    options += `<option value="${v.idVaiTro || v.IdVaiTro}">${v.tenVaiTro || v.TenVaiTro}</option>`;
                });
                $select.html(options);
            } else {
                setFallbackVaiTro($select);
            }
        },
        error: function () {
            setFallbackVaiTro($select);
        },
        complete: function () {
            $select.prop('disabled', false);
        }
    });

    const modalEl = document.getElementById('modalThemNhanSu');
    if (modalEl) bootstrap.Modal.getOrCreateInstance(modalEl).show();
}

function setFallbackVaiTro($select) {
    $select.html(`
        <option value="">-- Chọn vai trò --</option>
        <option value="1003">Quản lý trung tâm</option>
        <option value="1002">Giảng viên</option>
    `);
}

// ==========================================
// 5. GỬI API TẠO TÀI KHOẢN NỘI BỘ
// ==========================================
function xuLyThemNhanSu(e, idTrungTamContext = 0, onSuccessCallback = null) {
    e.preventDefault();

    const idTT = idTrungTamContext > 0 ? idTrungTamContext : (typeof ID_TRUNG_TAM !== 'undefined' ? ID_TRUNG_TAM : null);

    const postData = {
        TenDangNhap: document.getElementById('txtTenDangNhapNhanSu').value.trim(),
        MatKhau: document.getElementById('txtMatKhauNhanSu').value.trim(),
        HoTen: document.getElementById('txtHoTenNhanSu').value.trim(),
        IdVaiTro: parseInt(document.getElementById('selectVaiTroNhanSu').value),
        IdTrungTam: idTT > 0 ? idTT : null,
        Email: document.getElementById('txtEmailNhanSu')?.value.trim() || null,
        SoDienThoai: document.getElementById('txtSdtNhanSu')?.value.trim() || null
    };

    const btn = document.getElementById('btnSubmitNhanSu');
    if (btn) {
        btn.disabled = true;
        btn.innerHTML = '<i class="fa-solid fa-spinner fa-spin me-1"></i> Đang tạo...';
    }

    $.ajax({
        url: API_TAO_TK_NOI_BO,
        type: 'POST',
        contentType: 'application/json',
        data: JSON.stringify(postData),
        xhrFields: { withCredentials: true },
        success: function (res) {
            const modalEl = document.getElementById('modalThemNhanSu');
            if (modalEl) bootstrap.Modal.getOrCreateInstance(modalEl).hide();

            Swal.fire({
                icon: 'success',
                title: 'Thành công!',
                text: res.message || 'Đã tạo tài khoản nhân sự thành công!',
                timer: 1500,
                showConfirmButton: false
            }).then(() => {
                if (typeof onSuccessCallback === 'function') {
                    onSuccessCallback();
                } else {
                    loadDanhSachNhanSu(idTT);
                }
            });
        },
        error: function (xhr) {
            Swal.fire('Thất bại', xhr.responseJSON?.message || 'Không thể tạo tài khoản nhân sự.', 'error');
        },
        complete: function () {
            if (btn) {
                btn.disabled = false;
                btn.innerHTML = '<i class="fa-solid fa-check me-1"></i> Tạo tài khoản';
            }
        }
    });
}