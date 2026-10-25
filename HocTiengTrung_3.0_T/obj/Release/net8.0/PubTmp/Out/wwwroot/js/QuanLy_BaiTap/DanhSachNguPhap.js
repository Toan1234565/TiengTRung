/** 
 * QUẢN LÝ NGỮ PHÁP - FULL JS (Final Version)
 */

// Khởi tạo các instance Modal của Bootstrap
const nguPhapModalElement = document.getElementById('nguPhapModal');
const deleteModalElement = document.getElementById('deleteModal');

// Kiểm tra sự tồn tại của Element trước khi khởi tạo để tránh lỗi "is not defined"
const modal = nguPhapModalElement ? new bootstrap.Modal(nguPhapModalElement) : null;
const delModal = deleteModalElement ? new bootstrap.Modal(deleteModalElement) : null;

let deleteId = null;
let qIdx = 0;

/**
 * 1. THÊM DÒNG CÂU HỎI (BÀI TẬP)
 * @param {Object} data - Dữ liệu câu hỏi nếu là trường hợp Sửa
 */
function addQuestionRow(data = null) {
    $('#noQuestionLabel').hide();
    qIdx++;

    // Sử dụng template để tạo cấu trúc câu hỏi
    const temp = document.getElementById('questionTemplate').content.cloneNode(true);
    const colId = "colQ_" + qIdx;

    const btn = temp.querySelector('.accordion-button');
    const collapse = temp.querySelector('.accordion-collapse');
    const optBox = temp.querySelector('.options-container');

    btn.innerText = "Câu hỏi #" + qIdx;
    btn.setAttribute('data-bs-target', "#" + colId);
    collapse.setAttribute('id', colId);

    // Gán sự kiện cho nút thêm đáp án
    temp.querySelector('.add-option-btn').onclick = () => addOption(optBox, qIdx);

    // Đổ dữ liệu nếu có (Trường hợp Edit)
    if (data) {
        // Lưu ý: data.idCauHoi khớp với DTO CauHoiModels của bạn
        const hiddenId = document.createElement('input');
        hiddenId.type = 'hidden';
        hiddenId.className = 'q-id';
        hiddenId.value = data.idCauHoi || 0;
        temp.querySelector('.accordion-body').prepend(hiddenId);

        temp.querySelector('.q-noiDung').value = data.noiDung || '';
        temp.querySelector('.q-loai').value = data.idLoai || 1;

        if (data.dapAns && data.dapAns.length > 0) {
            data.dapAns.forEach(d => addOption(optBox, qIdx, d));
        } else {
            addOption(optBox, qIdx); addOption(optBox, qIdx);
        }
    } else {
        // Thêm mới mặc định 2 ô đáp án cho người dùng nhập
        addOption(optBox, qIdx);
        addOption(optBox, qIdx);
    }

    document.getElementById('questionContainer').appendChild(temp);
}

/**
 * 2. THÊM DÒNG ĐÁP ÁN (DÙNG CHO TRẮC NGHIỆM/ĐIỀN TỪ)
 */
function addOption(box, ref, data = null) {
    const d = document.createElement('div');
    d.className = "input-group input-group-sm mb-1 option-item";
    d.innerHTML = `
        <div class="input-group-text">
            <input type="radio" name="correct_${ref}" class="opt-is-correct" ${data?.laDapAnDung ? 'checked' : ''}>
        </div>
        <input type="text" class="form-control opt-text" value="${data?.noiDungDapAn || ''}" placeholder="Nhập đáp án...">
        <button type="button" class="btn btn-outline-danger" onclick="this.parentElement.remove()">×</button>
    `;
    box.appendChild(d);
}

/**
 * 3. XÓA CÂU HỎI KHỎI GIAO DIỆN
 */
function removeQuestion(btn) {
    $(btn).closest('.accordion-item').remove();
    if ($('#questionContainer .accordion-item').length === 0) {
        $('#noQuestionLabel').show();
    }
}

/**
 * 4. MỞ MODAL THÊM MỚI
 */
function openAddModal() {
    $('#modalTitle').text('Thêm ngữ pháp mới');
    $('#IdNguPhap').val('');
    $('#nguPhapForm')[0].reset();
    $('#questionContainer').empty().append('<div class="text-center py-4 text-muted border border-dashed rounded-3" id="noQuestionLabel">Chưa có câu hỏi.</div>');
    qIdx = 0;
    modal.show();
}

/**
 * 5. MỞ MODAL SỬA (FETCH DATA TỪ API)
 */
async function openEditModal(id) {
    try {
        Swal.fire({ title: 'Đang tải...', didOpen: () => Swal.showLoading(), allowOutsideClick: false });

        const response = await fetch(`/api/quanlynguphap/chi-tiet/${id}`);
        if (!response.ok) throw new Error("Lỗi kết nối server");
        const result = await response.json();

        Swal.close();

        // Điền thông tin ngữ pháp
        $('#modalTitle').text('Chỉnh sửa ngữ pháp');
        $('#IdNguPhap').val(result.idNguPhap);
        $('#TenNguPhap').val(result.tenNguPhap);
        $('#CauTruc').val(result.cauTruc);
        $('#GiaiThich').val(result.giaiThich);
        $('#ViDu').val(result.viDu);
        $('#IdCapDo').val(result.idCapDo);

        // Reset và hiển thị danh sách câu hỏi (bài tập)
        $('#questionContainer').empty();
        qIdx = 0;

        // API của bạn trả về danh sách câu hỏi trong thuộc tính 'selectedCauHoiIds'
        const questions = result.selectedCauHoiIds || [];
        if (questions.length > 0) {
            questions.forEach(ch => addQuestionRow(ch));
        } else {
            $('#questionContainer').append('<div class="text-center py-4 text-muted border border-dashed rounded-3" id="noQuestionLabel">Chưa có câu hỏi.</div>');
        }

        modal.show();
    } catch (error) {
        Swal.fire('Lỗi', 'Không thể lấy thông tin chi tiết.', 'error');
    }
}

/**
 * 6. LƯU DỮ LIỆU (THEM-MOI HOẶC CAP-NHAT)
 */
async function saveNguPhap() {
    const id = $('#IdNguPhap').val();

    // Map chuẩn payload theo DTO NguPhapUpdata của C#
    const payload = {
        TenNguPhap: $('#TenNguPhap').val().trim(),
        CauTruc: $('#CauTruc').val().trim(),
        GiaiThich: $('#GiaiThich').val().trim(),
        ViDu: $('#ViDu').val().trim(),
        IdCapDo: parseInt($('#IdCapDo').val()),
        SelectedCauHoiIds: [] // Đồng bộ với thuộc tính trong Controller
    };

    if (!payload.TenNguPhap) {
        Swal.fire('Chú ý', 'Vui lòng nhập tên ngữ pháp', 'warning');
        return;
    }

    // Thu thập danh sách câu hỏi bài tập
    $('#questionContainer .accordion-item').each(function () {
        const qNoiDung = $(this).find('.q-noiDung').val().trim();
        if (qNoiDung) {
            const question = {
                IdCauHoi: parseInt($(this).find('.q-id').val()) || 0,
                NoiDung: qNoiDung,
                IdLoai: parseInt($(this).find('.q-loai').val()),
                DapAns: []
            };

            // Thu thập đáp án của câu hỏi
            $(this).find('.option-item').each(function () {
                const optText = $(this).find('.opt-text').val().trim();
                if (optText) {
                    question.DapAns.push({
                        NoiDungDapAn: optText,
                        LaDapAnDung: $(this).find('.opt-is-correct').is(':checked')
                    });
                }
            });
            payload.SelectedCauHoiIds.push(question);
        }
    });

    try {
        Swal.fire({ title: 'Đang lưu...', didOpen: () => Swal.showLoading(), allowOutsideClick: false });

        const url = id ? `/api/quanlynguphap/cap-nhat/${id}` : `/api/quanlynguphap/them-moi`;
        const method = id ? 'PUT' : 'POST';

        const response = await fetch(url, {
            method: method,
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(payload)
        });

        const res = await response.json();

        if (res.success) {
            Swal.fire('Thành công!', res.message, 'success').then(() => {
                modal.hide();
                location.reload();
            });
        } else {
            Swal.fire('Thất bại', res.message, 'error');
        }
    } catch (error) {
        Swal.fire('Lỗi', 'Có lỗi xảy ra khi kết nối server.', 'error');
    }
}

/**
 * 7. XỬ LÝ XÓA
 */
function confirmDelete(id) {
    deleteId = id;
    if (delModal) delModal.show();
}

// Event listener cho nút xác nhận xóa trong modal
$('#confirmDeleteBtn').on('click', async function () {
    if (!deleteId) return;

    try {
        const response = await fetch(`/api/quanlynguphap/xoa/${deleteId}`, { method: 'DELETE' });
        const res = await response.json();

        if (res.success) {
            location.reload();
        } else {
            Swal.fire('Lỗi', res.message, 'error');
        }
    } catch (error) {
        Swal.fire('Lỗi', 'Không thể xóa ngữ pháp này.', 'error');
    } finally {
        deleteId = null;
    }
});

