/**
         * HÀM TIỆN ÍCH: Chuyển đổi định dạng
         */

// 1. Biến "00:00:05" (TimeOnly) thành số giây: 5
function timeOnlyToSeconds(timeString) {
    if (!timeString) return 0;
    const parts = timeString.split(':');
    const hrs = parseInt(parts[0], 10) || 0;
    const mins = parseInt(parts[1], 10) || 0;
    const secs = parseFloat(parts[2]) || 0;
    return hrs * 3600 + mins * 60 + secs;
}

// 2. Biến số giây 10.5 thành chuỗi "00:00:10.500" để gửi về C# TimeOnly
function secondsToTimeOnly(seconds) {
    if (isNaN(seconds) || seconds < 0) return "00:00:00.000";
    const hrs = Math.floor(seconds / 3600).toString().padStart(2, '0');
    const mins = Math.floor((seconds % 3600) / 60).toString().padStart(2, '0');
    const secs = (seconds % 60).toFixed(3).padStart(6, '0');
    return `${hrs}:${mins}:${secs}`;
}

/**
 * CÁC HÀM XỬ LÝ GIAO DIỆN & DANH SÁCH (AJAX)
 */

// Load danh sách ngay khi trang tải xong
$(document).ready(function () {
    loadVideos();

    // Gắn sự kiện thay đổi bộ lọc nếu bạn có <select id="filterCapDo">
    $('#filterCapDo').on('change', function () {
        loadVideos();
    });
});

// Hàm lấy danh sách video từ API
async function loadVideos() {
    const idCapDo = $('#filterCapDo').val() || 0;
    try {
        const res = await fetch(`/api/quanlyluyennghe/get-all-videos?idCapDo=${idCapDo}`).then(r => r.json());
        if (res.success) {
            renderVideoTable(res.data);
        }
    } catch (e) {
        console.error("Lỗi khi tải danh sách:", e);
    }
}

// Hàm render dữ liệu vào bảng HTML
function renderVideoTable(videos) {
    const tbody = $('#tableVideos tbody');
    tbody.empty();
    if (videos.length === 0) {
        tbody.append('<tr><td colspan="5" class="text-center">Không có dữ liệu bài nghe.</td></tr>');
        return;
    }

    videos.forEach(v => {
        const row = `
                    <tr id="video-row-${v.idVideo}">
                        <td class="text-center">${v.idVideo}</td>
                        <td><b>${v.tieuDe}</b></td>
                        <td class="text-center"><span class="badge bg-primary">${v.tenCapDo}</span></td>
                        <td class="text-center">${new Date(v.ngayTao).toLocaleDateString('vi-VN')}</td>
                        <td class="text-center">
                            <button class="btn btn-sm btn-outline-warning" onclick="showEditModal(${v.idVideo})">
                                <i class="fas fa-edit"></i> Sửa
                            </button>
                            <button class="btn btn-sm btn-outline-danger" onclick="deleteVideo(${v.idVideo})">
                                <i class="fas fa-trash"></i> Xóa
                            </button>
                        </td>
                    </tr>`;
        tbody.append(row);
    });
}

/**
 * XỬ LÝ PHỤ ĐỀ TRONG MODAL
 */

function addRowSub(data = null) {
    const rowId = Date.now() + Math.floor(Math.random() * 100);
    const start = timeOnlyToSeconds(data?.startTime ?? data?.StartTime);
    const end = timeOnlyToSeconds(data?.endTime ?? data?.EndTime);
    const hantu = data?.hanTu ?? data?.HanTu ?? '';
    const pinyin = data?.pinyin ?? data?.Pinyin ?? '';
    const dich = data?.dichNghia ?? data?.DichNghia ?? '';

    const html = `
                <tr class="sub-row" id="row_${rowId}">
                    <td><input type="number" step="0.1" class="form-control form-control-sm text-center sub-start" value="${start}"></td>
                    <td><input type="number" step="0.1" class="form-control form-control-sm text-center sub-end" value="${end}"></td>
                    <td><input type="text" class="form-control form-control-sm sub-hantu" value="${hantu}" placeholder="你好..."></td>
                    <td><input type="text" class="form-control form-control-sm sub-pinyin" value="${pinyin}" placeholder="nǐ hǎo..."></td>
                    <td><input type="text" class="form-control form-control-sm sub-dich" value="${dich}" placeholder="Xin chào..."></td>
                    <td class="text-center">
                        <button type="button" class="btn btn-link text-danger p-0" onclick="$('#row_${rowId}').remove()"><i class="fas fa-minus-circle"></i></button>
                    </td>
                </tr>`;
    $('#tableSubs tbody').append(html);
}

/**
 * MODAL: THÊM & SỬA
 */

function showAddModal() {
    $('#formVideo')[0].reset();
    $('#IdVideo').val(0);
    $('#tableSubs tbody').empty();

    $('#modalTitle').text("Thêm Bài Luyện Nghe Mới");
    $('#modalHeader').attr('class', 'modal-header bg-success text-white px-4');

    addRowSub();
    $('#videoModal').modal('show');
}

async function showEditModal(id) {
    try {
        const res = await fetch(`/api/quanlyluyennghe/get-video-detail/${id}`).then(r => r.json());
        if (res.success) {
            const v = res.data;
            $('#IdVideo').val(v.idVideo);
            $('#TieuDe').val(v.tieuDe);
            $('#IdCapDo').val(v.idCapDo);
            $('#MoTa').val(v.moTa);
            $('#VideoPath').val(v.videoPath); // Nạp đường dẫn text vào input

            $('#tableSubs tbody').empty();
            if (v.videoSubtitles && v.videoSubtitles.length > 0) {
                v.videoSubtitles.forEach(s => addRowSub(s));
            } else {
                addRowSub();
            }

            $('#modalTitle').text("Cập Nhật Bài Luyện Nghe");
            $('#modalHeader').attr('class', 'modal-header bg-warning text-dark px-4');
            $('#videoModal').modal('show');
        }
    } catch (e) {
        alert("Không thể tải dữ liệu chi tiết!");
    }
}

async function saveData() {
    const btn = $('#btnSave');
    const id = $('#IdVideo').val() || 0;

    if (!$('#TieuDe').val().trim()) {
        alert("Vui lòng nhập tiêu đề!");
        return;
    }

    if (!$('#VideoPath').val().trim()) {
        alert("Vui lòng nhập đường dẫn/link video!");
        return;
    }

    btn.prop('disabled', true).html('<i class="fas fa-spinner fa-spin me-2"></i>Đang xử lý...');

    const formData = new FormData();
    formData.append("IdVideo", id);
    formData.append("TieuDe", $('#TieuDe').val().trim());
    formData.append("IdCapDo", $('#IdCapDo').val());
    formData.append("MoTa", $('#MoTa').val().trim());
    formData.append("VideoPath", $('#VideoPath').val().trim()); // Gửi đường dẫn dạng text

    let subs = [];
    let isValid = true;

    $('.sub-row').each(function (i) {
        const startVal = $(this).find('.sub-start').val();
        const endVal = $(this).find('.sub-end').val();

        // Chỉ validate nếu dòng có nhập giá trị hán tự hoặc pinyin (tránh bắt lỗi dòng rỗng lỡ bấm thêm thừa)
        const hasText = $(this).find('.sub-hantu').val() || $(this).find('.sub-pinyin').val() || $(this).find('.sub-dich').val();

        if (hasText && (startVal === "" || endVal === "")) {
            isValid = false;
            return false; // Break loop
        }

        if (startVal !== "" && endVal !== "") {
            subs.push({
                StartTime: secondsToTimeOnly(parseFloat(startVal)),
                EndTime: secondsToTimeOnly(parseFloat(endVal)),
                HanTu: $(this).find('.sub-hantu').val() || "",
                Pinyin: $(this).find('.sub-pinyin').val() || "",
                DichNghia: $(this).find('.sub-dich').val() || "",
                ThuTu: i + 1
            });
        }
    });

    if (!isValid) {
        alert("Vui lòng điền đủ thời gian cho các dòng phụ đề có nội dung!");
        btn.prop('disabled', false).html('<i class="fas fa-save me-2"></i>Lưu dữ liệu');
        return;
    }

    formData.append("SubtitlesJson", JSON.stringify(subs));

    const url = id == 0 ? '/api/quanlyluyennghe/add-video' : `/api/quanlyluyennghe/update-video/${id}`;
    const method = id == 0 ? 'POST' : 'PUT'; // Lưu ý backend update-video phải support PUT/POST nhé

    try {
        const response = await fetch(url, { method: method, body: formData });
        const result = await response.json();

        if (result.success) {
            alert("Lưu thành công!");
            $('#videoModal').modal('hide');
            loadVideos(); // Nếu gọi được hàm này thì list tự update, không cần location.reload() gây giật trang
        } else {
            alert("Lỗi: " + result.message);
        }
    } catch (e) {
        alert("Lỗi kết nối hệ thống!");
        console.error(e);
    } finally {
        btn.prop('disabled', false).html('<i class="fas fa-save me-2"></i>Lưu dữ liệu');
    }
}