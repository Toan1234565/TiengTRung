/**
 * File: edit_baitap.js
 * Chức năng: Quản lý danh sách câu hỏi, sửa, xóa, phát âm thanh
 */

let editModal;

$(document).ready(function () {
    // 1. Khởi tạo Bootstrap Modal
    editModal = new bootstrap.Modal(document.getElementById('editModal'));

    // 2. Xử lý Checkbox "Chọn tất cả"
    $('#selectAll').on('change', function () {
        $('.item-checkbox').prop('checked', this.checked);
    });

   
    // 4. Xử lý Vô hiệu hóa hàng loạt
    // Thay đoạn gắn sự kiện cũ bằng cách này
    $('#btnDeactivateSelected').off('click').on('click', function (e) {
        e.preventDefault(); // Ngăn chặn mọi hành vi mặc định (nếu có)
        console.log("Nút đã được click");
        handleBulkAction('DEACTIVATE');
    });

    $('#btnDeleteSelected').off('click').on('click', function (e) {
        e.preventDefault();
        handleBulkAction('DELETE');
    });
});

function handleBulkAction(actionType) {
    let selectedIds = [];
    $('.item-checkbox:checked').each(function () {
        selectedIds.push(parseInt($(this).val()));
    });

    if (selectedIds.length === 0) {
        Swal.fire({
            title: 'Thông báo',
            text: 'Vui lòng chọn ít nhất một câu hỏi!',
            icon: 'warning',
            confirmButtonColor: '#3085d6'
        });
        return;
    }


    if (actionType === 'DELETE') {
        confirmDeleteBulk(selectedIds);
    } else if (actionType === 'DEACTIVATE') {
        confirmDeactivateBulk(selectedIds);
    }
}

// --- 1. XỬ LÝ ÂM THANH ---
function playAudio(url, btn) {
    const icon = $(btn).find('i');
    const audio = new Audio(url);

    icon.removeClass('fa-volume-up').addClass('fa-spinner fa-spin');

    audio.play().then(() => {
        audio.onended = () => {
            icon.removeClass('fa-spinner fa-spin').addClass('fa-volume-up');
        };
    }).catch(err => {
        console.error("Lỗi phát âm thanh:", err);
        icon.removeClass('fa-spinner fa-spin').addClass('fa-volume-up');
        Swal.fire('Lỗi', 'Không thể phát file âm thanh này.', 'error');
    });
}

// --- 2. LOAD DỮ LIỆU LÊN MODAL SỬA ---
async function openEditModal(id) {
    try {
        // Hiển thị loading nhẹ
        Swal.showLoading();

        const response = await fetch(`/api/quanlybaitap/chi-tiet-cau-hoi/${id}`);
        const result = await response.json();

        if (result.success) {
            const item = result.data;

            // Đổ dữ liệu vào các field cơ bản
            $('#displayId').text(`#${item.idCauHoi}`);
            $('#editIdCauHoi').val(item.idCauHoi);
            $('#editNoiDung').val(item.noiDung);
            $('#editIdCapDo').val(item.idCapDo);
            $('#editIdLoai').val(item.idLoai);
            $('#editAmThanhPath').val(item.amThanhPath);
            $('#editGiaiThich').val(item.giaiThich);

            // Xử lý Đoạn văn
            if (item.doanVan) {
                $('#editIdDoanVan').val(item.idDoanVan);
                $('#editTieuDeDV').val(item.doanVan.tieuDe);
                $('#editNoiDungHanDV').val(item.doanVan.noiDungHan);
                $('#editNoiDungPinyinDV').val(item.doanVan.noiDungPinyin);
                $('#editNoiDungVietDV').val(item.doanVan.noiDungViet);
            } else {
                $('#editIdDoanVan').val('');
                $('#doanVanSection input, #doanVanSection textarea').val('');
            }

            // Xử lý Đáp án (Render động)
            let dapAnHtml = '';
            // Nếu chưa có đáp án, tạo mặc định 4 ô trống
            const listDapAn = item.dapAns && item.dapAns.length > 0 ? item.dapAns : [{}, {}, {}, {}];

            listDapAn.forEach((da, index) => {
                dapAnHtml += `
                    <div class="col-md-6 mb-3">
                        <div class="input-group">
                            <div class="input-group-text">
                                <input class="form-check-input mt-0 rd-dap-an" type="radio" name="rdDapAnDung" 
                                    ${da.laDapAnDung ? 'checked' : ''} value="${index}">
                            </div>
                            <input type="hidden" class="dapan-id" value="${da.idDapAn || 0}">
                            <input type="text" class="form-control dapan-text" placeholder="Đáp án ${index + 1}" 
                                value="${da.noiDungDapAn || ''}">
                        </div>
                    </div>`;
            });
            $('#dapAnContainer').html(dapAnHtml);

            Swal.close();
            editModal.show();
        } else {
            Swal.fire('Lỗi', result.message, 'error');
        }
    } catch (error) {
        console.error(error);
        Swal.fire('Lỗi', 'Không thể kết nối đến máy chủ.', 'error');
    }
}

// --- 3. LƯU THAY ĐỔI (PUT) ---
async function saveChanges() {
    const id = $('#editIdCauHoi').val();

    // Thu thập danh sách đáp án từ giao diện
    let dsDapAn = [];
    $('.dapan-text').each(function (index) {
        dsDapAn.push({
            IdDapAn: parseInt($('.dapan-id').eq(index).val()),
            NoiDungDapAn: $(this).val(),
            LaDapAnDung: $('.rd-dap-an').eq(index).is(':checked')
        });
    });

    // Validate nhanh
    if (!$('.rd-dap-an:checked').length) {
        Swal.fire('Lưu ý', 'Vui lòng chọn một đáp án đúng!', 'warning');
        return;
    }

    const model = {
        IdCauHoi: parseInt(id),
        NoiDung: $('#editNoiDung').val(),
        IdCapDo: parseInt($('#editIdCapDo').val()),
        IdLoai: parseInt($('#editIdLoai').val()),
        AmThanhPath: $('#editAmThanhPath').val(),
        GiaiThich: $('#editGiaiThich').val(),
        // Thông tin đoạn văn
        TieuDe: $('#editTieuDeDV').val(),
        NoiDungHan: $('#editNoiDungHanDV').val(),
        NoiDungPinyin: $('#editNoiDungPinyinDV').val(),
        NoiDungViet: $('#editNoiDungVietDV').val(),
        DapAns: dsDapAn
    };

    try {
        const response = await fetch(`/api/quanlybaitap/sua-cau-hoi/${id}`, {
            method: 'PUT',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(model)
        });

        const result = await response.json();
        if (result.success) {
            Swal.fire('Thành công', 'Cập nhật bài tập hoàn tất!', 'success')
                .then(() => location.reload());
        } else {
            Swal.fire('Thất bại', result.message, 'error');
        }
    } catch (error) {
        Swal.fire('Lỗi', 'Xảy ra lỗi trong quá trình gửi dữ liệu.', 'error');
    }
}

// --- 4. XÓA DỮ LIỆU (DELETE) ---
function confirmDeleteBulk(ids) {
    Swal.fire({
        title: `Xóa ${ids.length} câu hỏi?`,
        text: "Dữ liệu lịch sử làm bài liên quan cũng sẽ bị xóa và không thể khôi phục!",
        icon: 'warning',
        showCancelButton: true,
        confirmButtonColor: '#d33',
        cancelButtonColor: '#3085d6',
        confirmButtonText: 'Đúng, xóa chúng!',
        cancelButtonText: 'Hủy'
    }).then(async (result) => {
        if (result.isConfirmed) {
            Swal.fire({ title: 'Đang xử lý...', allowOutsideClick: false, didOpen: () => Swal.showLoading() });

            try {
                // Thực hiện xóa từng ID (hoặc bạn có thể viết API xóa hàng loạt)
                for (let id of ids) {
                    await fetch(`/api/quanlybaitap/xoa-cau-hoi/${id}`, { method: 'DELETE' });
                }

                Swal.fire('Đã xóa!', 'Toàn bộ mục chọn đã được loại bỏ.', 'success')
                    .then(() => location.reload());
            } catch (error) {
                Swal.fire('Lỗi', 'Quá trình xóa gặp trục trặc.', 'error');
            }
        }
    });
}

// --- 5. VÔ HIỆU HÓA CÂU HỎI (Soft Delete) ---
function confirmDeactivateBulk(ids) {
    Swal.fire({
        title: `Vô hiệu hóa ${ids.length} câu hỏi?`,
        text: "Các câu hỏi này sẽ tạm thời bị ẩn khỏi giao diện học tập.",
        icon: 'question',
        showCancelButton: true,
        confirmButtonColor: '#ffc107',
        cancelButtonColor: '#6c757d',
        confirmButtonText: 'Đồng ý',
        cancelButtonText: 'Hủy'
    }).then(async (result) => {
        if (result.isConfirmed) {
            Swal.fire({ title: 'Đang xử lý...', allowOutsideClick: false, didOpen: () => Swal.showLoading() });

            try {
                const response = await fetch('/api/quanlybaitap/vo-hieu-hoa-hang-loat', {
                    method: 'PATCH',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify(ids)
                });

                const result = await response.json();
                if (result.success) {
                    Swal.fire('Thành công', result.message, 'success')
                        .then(() => location.reload());
                } else {
                    Swal.fire('Thất bại', result.message, 'error');
                }
            } catch (error) {
                Swal.fire('Lỗi', 'Không thể kết nối đến máy chủ.', 'error');
            }
        }
    });
}