/**
 * quanly-trungtam.js - Quản lý nghiệp vụ Cơ sở / Trung tâm đào tạo
 */
const API_BASE_TRUNGTAM = '/api/quanlytrungtam';
let modalTrungTamInstance = null;

document.addEventListener('DOMContentLoaded', function () {
    const modalEl = document.getElementById('modalTrungTam');
    if (modalEl) modalTrungTamInstance = new bootstrap.Modal(modalEl);
});

// 1. Xem trước ảnh Logo khi người dùng chọn file
function previewImageTrungTam(input, previewImgId = 'imgPreview', placeholderId = 'imgPlaceholder') {
    if (input.files && input.files[0]) {
        const file = input.files[0];
        const validExtensions = ['image/jpeg', 'image/jpg', 'image/png', 'image/webp'];
        if (!validExtensions.includes(file.type)) {
            Swal.fire('Lỗi', 'Chỉ chấp nhận các định dạng ảnh: .jpg, .jpeg, .png, .webp', 'error');
            input.value = '';
            return;
        }

        const reader = new FileReader();
        reader.onload = function (e) {
            const preview = document.getElementById(previewImgId);
            const placeholder = document.getElementById(placeholderId);
            if (preview) {
                preview.src = e.target.result;
                preview.classList.remove('d-none');
            }
            if (placeholder) {
                placeholder.classList.add('d-none');
            }
        };
        reader.readAsDataURL(file);
    }
}

// 2. Mở Modal ở chế độ Thêm mới
function moModalThemMoiTrungTam() {
    const form = document.getElementById('formTrungTam');
    if (form) form.reset();

    document.getElementById('modalIdTrungTam').value = '0';
    document.getElementById('modalTrungTamTitle').textContent = 'Thêm mới Trung tâm';
    document.getElementById('chkTrangThai').checked = true;

    const preview = document.getElementById('imgPreview');
    const placeholder = document.getElementById('imgPlaceholder');
    if (preview) {
        preview.src = '';
        preview.classList.add('d-none');
    }
    if (placeholder) placeholder.classList.remove('d-none');

    if (modalTrungTamInstance) modalTrungTamInstance.show();
}

// 3. Mở Modal ở chế độ Cập nhật (Nạp dữ liệu chi tiết từ API)
function moModalCapNhatTrungTam(id) {
    Swal.fire({
        title: 'Đang tải dữ liệu...',
        allowOutsideClick: false,
        didOpen: () => Swal.showLoading()
    });

    fetch(`${API_BASE_TRUNGTAM}/chitiettrungtam/${id}`)
        .then(res => {
            if (!res.ok) throw new Error('Không thể tải thông tin trung tâm');
            return res.json();
        })
        .then(data => {
            Swal.close();

            document.getElementById('modalIdTrungTam').value = data.idTrungTam || data.IdTrungTam;
            document.getElementById('txtTenTrungTam').value = data.tenTrungTam || data.TenTrungTam || '';
            document.getElementById('txtMaTrungTam').value = data.maTrungTam || data.MaTrungTam || '';
            document.getElementById('txtSubdomain').value = data.subdomain || data.Subdomain || '';
            document.getElementById('txtSoDienThoai').value = data.soDienThoai || data.SoDienThoai || '';
            document.getElementById('txtEmail').value = data.email || data.Email || '';
            document.getElementById('txtDiaChi').value = data.diaChi || data.DiaChi || '';

            const hetHan = data.ngayHetHan || data.NgayHetHan;
            if (hetHan) {
                document.getElementById('txtNgayHetHan').value = hetHan.substring(0, 10);
            } else {
                document.getElementById('txtNgayHetHan').value = '';
            }

            document.getElementById('chkTrangThai').checked = (data.trangThai ?? data.TrangThai ?? true);

            const logo = data.logo || data.Logo;
            const preview = document.getElementById('imgPreview');
            const placeholder = document.getElementById('imgPlaceholder');

            if (logo && preview) {
                preview.src = logo;
                preview.classList.remove('d-none');
                if (placeholder) placeholder.classList.add('d-none');
            } else if (preview && placeholder) {
                preview.classList.add('d-none');
                placeholder.classList.remove('d-none');
            }

            document.getElementById('modalTrungTamTitle').textContent = `Chỉnh sửa: ${data.tenTrungTam || data.TenTrungTam}`;
            if (modalTrungTamInstance) modalTrungTamInstance.show();
        })
        .catch(err => {
            Swal.fire('Lỗi', err.message || 'Không thể lấy thông tin trung tâm', 'error');
        });
}

// 4. Lưu thông tin (Thêm mới / Cập nhật)
function luuThongTinTrungTam(event) {
    event.preventDefault();

    const id = parseInt(document.getElementById('modalIdTrungTam').value) || 0;
    const btnSubmit = document.getElementById('btnSubmitModal') || event.submitter;
    if (btnSubmit) {
        btnSubmit.disabled = true;
        btnSubmit.innerHTML = '<i class="fa-solid fa-spinner fa-spin me-1"></i> Đang xử lý...';
    }

    const formData = new FormData();
    formData.append('TenTrungTam', document.getElementById('txtTenTrungTam').value.trim());
    formData.append('MaTrungTam', document.getElementById('txtMaTrungTam').value.trim());
    formData.append('Subdomain', document.getElementById('txtSubdomain')?.value.trim() || '');
    formData.append('SoDienThoai', document.getElementById('txtSoDienThoai').value.trim());
    formData.append('Email', document.getElementById('txtEmail').value.trim());
    formData.append('DiaChi', document.getElementById('txtDiaChi').value.trim());

    const ngayHetHan = document.getElementById('txtNgayHetHan')?.value;
    if (ngayHetHan) {
        formData.append('NgayHetHan', ngayHetHan);
    }

    formData.append('TrangThai', document.getElementById('chkTrangThai').checked);

    const fileInput = document.getElementById('fileLogo') || document.getElementById('fileLogoTrungTam');
    if (fileInput && fileInput.files && fileInput.files[0]) {
        formData.append('fileLogo', fileInput.files[0]);
    }

    const url = id > 0 ? `${API_BASE_TRUNGTAM}/updatetrungtam/${id}` : `${API_BASE_TRUNGTAM}/addtrungtam`;
    const method = id > 0 ? 'PUT' : 'POST';

    fetch(url, {
        method: method,
        body: formData
    })
        .then(async res => {
            const data = await res.json();
            if (!res.ok) throw new Error(data.message || 'Thao tác không thành công');
            return data;
        })
        .then(res => {
            if (modalTrungTamInstance) modalTrungTamInstance.hide();

            if (id === 0 && res.data && res.data.taiKhoan) {
                const tk = res.data.taiKhoan;
                Swal.fire({
                    icon: 'success',
                    title: 'Thêm trung tâm thành công!',
                    html: `
                    <div class="text-start p-2 bg-light rounded mt-2 border">
                        <p class="mb-1 text-success fw-bold"><i class="fa-solid fa-check-circle me-1"></i>Tài khoản quản trị đã được tạo tự động:</p>
                        <div class="small mb-1"><strong>Tên đăng nhập:</strong> <span class="badge bg-primary font-monospace">${tk.tenDangNhap}</span></div>
                        <div class="small"><strong>Mật khẩu mặc định:</strong> <span class="badge bg-secondary font-monospace">${tk.matKhauMacDinh}</span></div>
                    </div>
                `,
                    confirmButtonText: 'Đã hiểu'
                }).then(() => location.reload());
            } else {
                Swal.fire({
                    icon: 'success',
                    title: 'Thành công',
                    text: res.message || 'Dữ liệu đã được lưu thành công!',
                    timer: 1500,
                    showConfirmButton: false
                }).then(() => location.reload());
            }
        })
        .catch(err => {
            Swal.fire('Lỗi', err.message || 'Lỗi hệ thống khi lưu dữ liệu.', 'error');
        })
        .finally(() => {
            if (btnSubmit) {
                btnSubmit.disabled = false;
                btnSubmit.innerHTML = '<i class="fa-solid fa-floppy-disk me-1"></i> Lưu thông tin';
            }
        });
}

// 5. Đổi trạng thái (Hoạt động / Vô hiệu hóa)
function toggleTrangThaiTrungTamApi(id, element) {
    fetch(`${API_BASE_TRUNGTAM}/toggle-trangthai/${id}`, {
        method: 'PATCH'
    })
        .then(async res => {
            const data = await res.json();
            if (!res.ok) throw new Error(data.message || 'Không thể đổi trạng thái');
            return data;
        })
        .then(res => {
            const label = document.getElementById(`status-label-${id}`);
            if (label) {
                label.textContent = res.trangThai ? "Hoạt động" : "Vô hiệu";
                label.className = res.trangThai ? "small text-success" : "small text-muted";
            }
            Swal.mixin({
                toast: true,
                position: 'top-end',
                showConfirmButton: false,
                timer: 2000
            }).fire({
                icon: 'success',
                title: res.message || 'Cập nhật trạng thái thành công!'
            });
        })
        .catch(err => {
            element.checked = !element.checked;
            Swal.fire('Lỗi', err.message || 'Không thể kết nối đến API.', 'error');
        });
}

// 6. Xóa trung tâm
function xoaTrungTamApi(id, name) {
    Swal.fire({
        title: 'Xác nhận xóa?',
        text: `Bạn có chắc chắn muốn xóa trung tâm "${name}"? Hành động này sẽ bị hủy nếu trung tâm đã có lớp học trực thuộc!`,
        icon: 'warning',
        showCancelButton: true,
        confirmButtonColor: '#d33',
        cancelButtonColor: '#6c757d',
        confirmButtonText: 'Đồng ý xóa',
        cancelButtonText: 'Hủy'
    }).then((result) => {
        if (result.isConfirmed) {
            fetch(`${API_BASE_TRUNGTAM}/deletetrungtam/${id}`, {
                method: 'DELETE'
            })
                .then(async res => {
                    const data = await res.json();
                    if (!res.ok) throw new Error(data.message || 'Không thể xóa');
                    return data;
                })
                .then(res => {
                    Swal.fire('Thành công', res.message || 'Đã xóa trung tâm!', 'success').then(() => {
                        const row = document.getElementById(`row-${id}`);
                        if (row) row.remove();
                        else location.reload();
                    });
                })
                .catch(err => {
                    Swal.fire('Thất bại', err.message || 'Không thể xóa trung tâm này.', 'error');
                });
        }
    });
}