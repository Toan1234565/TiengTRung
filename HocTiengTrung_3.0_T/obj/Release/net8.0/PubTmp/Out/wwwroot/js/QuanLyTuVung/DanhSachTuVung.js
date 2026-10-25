/**
  * Hàm xử lý việc đánh dấu/bỏ đánh dấu từ vựng
  * Kết nối với API: /api/quanlytuvung/danh-dau-tu-vung
  */
async function toggleLearned(idTuVung, btn) {
    // Ngăn chặn bấm liên tục khi đang xử lý
    if (btn.classList.contains('is-loading')) return;
    btn.classList.add('is-loading');

    const icon = btn.querySelector('i');
    const originalIconClass = icon ? icon.className : 'fas fa-check-circle fa-lg';

    // Hiệu ứng loading tại nút
    if (icon) icon.className = 'fas fa-spinner fa-spin fa-lg';

    try {
        const response = await fetch('/api/quanlytuvung/danh-dau-tu-vung', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                // Đảm bảo gửi kèm request verification token nếu dùng AntiForgeryToken
            },
            body: JSON.stringify(idTuVung)
        });

        // Xử lý khi hết hạn session hoặc chưa đăng nhập
        if (response.status === 401) {
            alert("Phiên làm việc đã hết hạn. Vui lòng đăng nhập lại để lưu tiến trình!");
            window.location.href = '/QuanLyTaiKhoan/DangNhap';
            return;
        }

        const data = await response.json();

        if (response.ok && data.success) {
            // Lấy bộ lọc hiện tại từ URL hoặc Select Box
            const selectElement = document.querySelector('select[name="trangThai"]');
            const currentFilter = selectElement ? selectElement.value : 'all';

            // 1. Nếu đang ở tab lọc (Đã học/Chưa học) thì xóa thẻ đó đi vì trạng thái đã thay đổi
            if (currentFilter !== 'all') {
                const wrapper = btn.closest('.word-item-wrapper');
                if (wrapper) {
                    wrapper.classList.add('animate__animated', 'animate__fadeOutDown');
                    setTimeout(() => {
                        wrapper.remove();
                        // Kiểm tra nếu không còn từ nào thì load lại trang hoặc hiện thông báo trống
                        const remainingItems = document.querySelectorAll('.word-item-wrapper');
                        if (remainingItems.length === 0) {
                            window.location.reload();
                        }
                    }, 500);
                }
            }
            // 2. Nếu ở tab "Tất cả", chỉ cập nhật màu sắc và icon
            else {
                if (data.isLearned) {
                    btn.classList.remove('text-not-learned');
                    btn.classList.add('text-learned', 'animate__bounceIn');
                    btn.title = "Bỏ đánh dấu";
                } else {
                    btn.classList.remove('text-learned');
                    btn.classList.add('text-not-learned', 'animate__headShake');
                    btn.title = "Đánh dấu đã thuộc";
                }
            }

            // Lưu ý: Backend của bạn đã tự Clear Cache khi gọi API này,
            // nên lần F5 tới dữ liệu sẽ luôn chuẩn.
        } else {
            alert(data.message || "Không thể cập nhật trạng thái.");
        }
    } catch (error) {
        console.error("Lỗi Fetch:", error);
        alert("Lỗi kết nối máy chủ. Vui lòng thử lại sau.");
    } finally {
        // Trả lại icon cũ sau khi xử lý xong
        btn.classList.remove('is-loading');
        if (icon) {
            icon.className = originalIconClass;
        }
        // Xóa các class animation để có thể chạy lại lần sau
        setTimeout(() => {
            btn.classList.remove('animate__bounceIn', 'animate__headShake');
        }, 1000);
    }
}

/**
 * Hàm phát âm thanh từ vựng
 */
function playAudio(audioPath, btn) {
    if (!audioPath) return;

    // Nếu path là tương đối, hãy đảm bảo nó trỏ đúng thư mục root
    const audio = new Audio(audioPath);
    const icon = btn.querySelector('i');

    if (icon) {
        icon.classList.add('animate__animated', 'animate__pulse', 'animate__infinite', 'playing-audio');
    }

    audio.play().catch(error => {
        console.warn("Audio play failed:", error);
        // Thử xử lý nếu path thiếu dấu / ở đầu
        if (!audioPath.startsWith('/')) {
            new Audio('/' + audioPath).play().catch(e => alert("Không tìm thấy file âm thanh."));
        }
    });

    audio.onended = function () {
        if (icon) {
            icon.classList.remove('animate__animated', 'animate__pulse', 'animate__infinite', 'playing-audio');
        }
    };

    audio.onerror = function () {
        if (icon) {
            icon.classList.remove('animate__animated', 'animate__pulse', 'animate__infinite', 'playing-audio');
        }
    };
}

// 1. Gọi API lấy danh sách Cấp độ
$.ajax({
    url: '/api/quanlytuvung/danhsachcapdo',
    type: 'GET',
    success: function (data) {
        let html = '';
        if (Array.isArray(data)) {
            data.forEach(item => {
                const id = item.IdCapDo ?? item.idCapDo;
                const ten = item.TenCapDo ?? item.tenCapDo ?? "N/A";
                let selected = id == currentCapDo ? "selected" : "";
                // LƯU Ý: value là ID, data-ten là Tên hiển thị
                html += `<option value="${id}" data-ten="${ten}" ${selected}>${ten}</option>`;
            });
        }
        $('#aiCapDo').html(html || '<option value="">Không có dữ liệu</option>');
    }
});

// 2. Gọi API lấy danh sách Loại câu hỏi
$.ajax({
    url: '/api/quanlytuvung/loai-cau-hoi',
    type: 'GET',
    success: function (response) {
        if (response && response.success && Array.isArray(response.data)) {
            let html = '';
            response.data.forEach(item => {
                const idLoai = item.IdLoai ?? item.idLoai;
                const tenLoai = item.TenLoai ?? item.tenLoai ?? "N/A";
                // LƯU Ý: value là ID, data-ten là Tên hiển thị
                html += `<option value="${idLoai}" data-ten="${tenLoai}">${tenLoai}</option>`;
            });
            $('#aiLoaiCauHoi').html(html);
        }
    }
});

/**
 * Hàm bắt đầu tạo câu hỏi AI (Thực tế là gọi API tạo bài tập thủ công dựa trên từ đã thuộc)
 * Chức năng: Thu thập config từ Modal -> Gọi API -> Lưu dữ liệu và Chuyển trang
 */
async function batDauTaoCauHoiAI() {
    // 1. Lấy thông tin cấu hình từ Giao diện
    const capDo = document.getElementById('aiCapDo')?.value;
    const loaiMa = document.getElementById('aiLoaiCauHoi')?.value;
    const soLuong = document.getElementById('aiSoLuong')?.value || 10;

    if (!capDo || !loaiMa) {
        Swal.fire('Thông báo', 'Vui lòng chọn đầy đủ cấu hình bài tập!', 'warning');
        return;
    }

    // 2. Hiển thị Loading
    Swal.fire({
        title: 'Đang khởi tạo bài tập...',
        text: 'Hệ thống đang lựa chọn câu hỏi phù hợp với vốn từ của bạn',
        allowOutsideClick: false,
        didOpen: () => { Swal.showLoading(); }
    });

    try {
        // 3. Gọi API lấy đề thi
        const response = await fetch(`/api/quanlytuvung/tao-bai-tap-thu-cong?capDo=${capDo}&loaiMa=${loaiMa}&soLuong=${soLuong}`);

        if (!response.ok) {
            throw new Error(`Lỗi kết nối máy chủ: ${response.status}`);
        }

        const result = await response.json();

        // 4. KIỂM TRA DỮ LIỆU ĐẦU VÀO
        // API của bạn trả về một Mảng các Đề thi (List<DeThiViewModel>)
        if (Array.isArray(result) && result.length > 0) {

            // Lấy object đề thi đầu tiên (chứa TenDe, SoLuongCauHoi, CauHois)
            const deThiChinh = result[0];

            // Kiểm tra xem đề này có câu hỏi nào không
            if (!deThiChinh.cauHois || deThiChinh.cauHois.length === 0) {
                Swal.fire('Thông báo', 'Đề thi được tạo không có câu hỏi nào phù hợp.', 'info');
                return;
            }

            // 5. TẠO FORM POST ĐỂ CHUYỂN TRANG
            const form = document.createElement('form');
            form.method = 'POST';
            // Đảm bảo đường dẫn này khớp với Route trong Controller QuanLyDanhSachBaiTap
            form.action = '/QuanLyDanhSachTuVung/BatDauLamBaiHienThi';

            const input = document.createElement('input');
            input.type = 'hidden';
            input.name = 'jsonCauHoi';

            /** * QUAN TRỌNG: Gửi nguyên object deThiChinh
             * Object này bao gồm: { tenDe: "...", soLuongCauHoi: 10, cauHois: [...] }
             * Để khớp với DeThiViewModel ở Backend.
             */
            input.value = JSON.stringify(deThiChinh);

            form.appendChild(input);
            document.body.appendChild(form);

            // Đóng loading trước khi submit
            Swal.close();

            // Chuyển trang sang giao diện LamBaiTapThucTe
            form.submit();

        } else {
            Swal.fire({
                icon: 'info',
                title: 'Không tìm thấy câu hỏi',
                text: 'Kho lưu trữ chưa có dữ liệu phù hợp với yêu cầu của bạn.',
                confirmButtonText: 'Đóng'
            });
        }
    } catch (error) {
        console.error("Lỗi API AI:", error);
        Swal.fire('Lỗi hệ thống', 'Không thể kết nối đến máy chủ hoặc dữ liệu không hợp lệ.', 'error');
    }
}

function playAudio(audioPath, btn) {
    if (!audioPath) return;

    // Kiểm tra nhanh nếu đuôi file là ảnh (lỗi dữ liệu)
    if (audioPath.toLowerCase().endsWith('.png') || audioPath.toLowerCase().endsWith('.jpg')) {
        Swal.fire({
            icon: 'warning',
            title: 'Sai định dạng',
            text: 'Dữ liệu đang là file ảnh (.png), không thể phát âm thanh!',
        });
        return;
    }

    // Chuyển đổi đường dẫn từ \ sang / để trình duyệt hiểu
    let webPath = audioPath.replace(/\\/g, '/');

    // Nếu không phải link Cloudinary và không có dấu / ở đầu thì thêm vào
    if (!webPath.startsWith('http') && !webPath.startsWith('/')) {
        webPath = '/' + webPath;
    }

    const audio = new Audio(webPath);
    const icon = btn.querySelector('i');

    // Thêm hiệu ứng khi đang phát
    if (icon) icon.className = 'fas fa-spinner fa-spin text-danger';

    audio.play().catch(e => {
        console.error("Audio Error:", e);
        Swal.fire({
            icon: 'error',
            title: 'Lỗi phát âm',
            text: 'Không tìm thấy file tại: ' + webPath
        });
        if (icon) icon.className = 'fas fa-volume-up text-success';
    });

    audio.onended = () => {
        if (icon) icon.className = 'fas fa-volume-up text-success';
    };
}
$(document).ready(function () {
    // Sử dụng event delegation để đảm bảo hoạt động ngay cả khi danh sách từ vựng được load động (Ajax)
    $(document).on('show.bs.collapse', '.collapse', function () {
        let targetId = $(this).attr('id');
        let $btn = $(`button[data-bs-target="#${targetId}"]`);

        $btn.find('.btn-text').text('Thu gọn ví dụ');
        $btn.find('i').attr('class', 'fas fa-chevron-up me-1');

        // Thêm class để đổi màu nút khi đang mở (theo CSS bạn đã viết)
        $btn.addClass('active-btn');
    });

    $(document).on('hide.bs.collapse', '.collapse', function () {
        let targetId = $(this).attr('id');
        let $btn = $(`button[data-bs-target="#${targetId}"]`);

        $btn.find('.btn-text').text('Xem ví dụ');
        $btn.find('i').attr('class', 'fas fa-lightbulb me-1');

        $btn.removeClass('active-btn');
    });
});
/**
 * QUẢN LÝ TỪ VỰNG - JAVASCRIPT FULL
 */

// 1. Mở modal và lấy dữ liệu chi tiết từ vựng
async function openEditModal(id) {
    try {
        // Hiển thị loading nếu cần
        const response = await fetch(`/api/quanlytuvung/chi-tiet-tu-vung/${id}`);
        const result = await response.json();

        if (result.success) {
            const data = result.data;
            console.log("Dữ liệu nhận được:", data);

            // Đổ dữ liệu vào các input chính
            $('#editIdTuVung').val(data.idTuVung);
            $('#editChuHan').val(data.chuHan);
            $('#editPinyin').val(data.pinyin);
            $('#editAmHan').val(data.amHan);
            $('#editAmThanh').val(data.amThanhPath || ""); // Đường dẫn âm thanh từ vựng
            $('#editNghiaViet').val(data.nghiaViet);
            $('#editLoaiTu').val(data.loaiTu);
            $('#editIdCapDo').val(data.idCapDo);

            // Render danh sách câu ví dụ
            const container = $('#exampleContainer');
            container.empty();

            if (data.cauViDus && data.cauViDus.length > 0) {
                data.cauViDus.forEach(vd => addNewExampleRow(vd));
            } else {
                // Nếu chưa có ví dụ nào, có thể tự động thêm 1 dòng trống
                // addNewExampleRow(); 
            }

            // Hiển thị modal
            $('#editWordModal').modal('show');
        } else {
            Swal.fire('Thông báo', result.message, 'info');
        }
    } catch (error) {
        console.error('Error fetching detail:', error);
        Swal.fire('Lỗi!', 'Không thể lấy thông tin chi tiết từ vựng', 'error');
    }
}

// 2. Thêm dòng ví dụ mới vào UI (Dùng cho cả lúc Load và lúc bấm nút "Thêm ví dụ")
function addNewExampleRow(data = null) {
    const rowId = Date.now() + Math.floor(Math.random() * 1000);

    // SỬA TẠI ĐÂY: Sử dụng .amThanh thay vì .amThanhPath cho phần ví dụ
    const audioVal = data ? (data.amThanh || '') : '';

    const html = `
        <div class="example-row card p-3 mb-3 border-light shadow-sm position-relative" id="ex-row-${rowId}">
            <button type="button" class="btn-close position-absolute top-0 end-0 m-2" 
                    onclick="$('#ex-row-${rowId}').remove()" style="font-size: 0.7rem;"></button>
            
            <input type="hidden" class="ex-id" value="${data ? (data.idViDu || 0) : 0}">
            
            <div class="row g-2">
                <div class="col-md-4">
                    <label class="small fw-bold">Nội dung Hán</label>
                    <input type="text" class="form-control form-control-sm ex-han" value="${data ? data.noiDungHan : ''}">
                </div>
                <div class="col-md-4">
                    <label class="small fw-bold">Pinyin</label>
                    <input type="text" class="form-control form-control-sm ex-pinyin" value="${data ? data.noiDungPinyin : ''}">
                </div>
                <div class="col-md-4">
                    <label class="small fw-bold">Nghĩa Việt</label>
                    <input type="text" class="form-control form-control-sm ex-viet" value="${data ? data.noiDungViet : ''}">
                </div>
                <div class="col-md-12">
                    <label class="small fw-bold text-primary">
                        <i class="fas fa-link me-1"></i>Đường dẫn âm thanh ví dụ
                    </label>
                    <!-- Input này sẽ hiển thị giá trị từ data.amThanh -->
                    <input type="text" class="form-control form-control-sm ex-audio" 
                           placeholder="/audio/examples/sample.mp3" 
                           value="${audioVal}"> 
                </div>
            </div>
        </div>`;

    $('#exampleContainer').append(html);
}

// 3. Gọi API Lưu thay đổi (PUT)
async function saveWordChanges() {
    const id = $('#editIdTuVung').val();
    if (!id) return;

    // Thu thập dữ liệu từ các dòng ví dụ
    const cauViDus = [];
    $('.example-row').each(function () {
        const han = $(this).find('.ex-han').val().trim();
        // Chỉ lưu nếu có nội dung chữ Hán
        if (han !== "") {
            cauViDus.push({
                idViDu: parseInt($(this).find('.ex-id').val()) || 0,
                noiDungHan: han,
                noiDungPinyin: $(this).find('.ex-pinyin').val().trim(),
                noiDungViet: $(this).find('.ex-viet').val().trim(),
                amThanh: $(this).find('.ex-audio').val().trim() // Thu thập link audio ví dụ
            });
        }
    });

    const payload = {
        idTuVung: parseInt(id),
        chuHan: $('#editChuHan').val().trim(),
        pinyin: $('#editPinyin').val().trim(),
        amHan: $('#editAmHan').val().trim(),
        nghiaViet: $('#editNghiaViet').val().trim(),
        loaiTu: $('#editLoaiTu').val().trim(),
        idCapDo: parseInt($('#editIdCapDo').val()) || 0,
        amThanhPath: $('#editAmThanh').val().trim(), // Map đúng với tên field trong Model C#
        cauViDus: cauViDus
    };

    // Hiển thị loading
    Swal.fire({
        title: 'Đang xử lý...',
        didOpen: () => { Swal.showLoading(); }
    });

    try {
        const response = await fetch(`/api/quanlytuvung/sua-tu-vung/${id}`, {
            method: 'PUT',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(payload)
        });

        const result = await response.json();

        if (result.success) {
            Swal.fire({
                icon: 'success',
                title: 'Thành công!',
                text: result.message,
                timer: 1500,
                showConfirmButton: false
            }).then(() => {
                $('#editWordModal').modal('hide');
                location.reload(); // Tải lại danh sách
            });
        } else {
            Swal.fire('Thất bại', result.message, 'error');
        }
    } catch (error) {
        console.error('Error saving changes:', error);
        Swal.fire('Lỗi!', 'Có lỗi xảy ra khi kết nối máy chủ', 'error');
    }
}

// 4. Gọi API Xóa (DELETE)
function deleteWord(id) {
    Swal.fire({
        title: 'Xác nhận xóa?',
        text: "Mọi dữ liệu liên quan đến từ vựng này sẽ bị mất!",
        icon: 'warning',
        showCancelButton: true,
        confirmButtonColor: '#d33',
        cancelButtonColor: '#6c757d',
        confirmButtonText: 'Đồng ý xóa',
        cancelButtonText: 'Hủy'
    }).then(async (result) => {
        if (result.isConfirmed) {
            try {
                const response = await fetch(`/api/quanlytuvung/xoa-tu-vung/${id}`, {
                    method: 'DELETE'
                });
                const data = await response.json();

                if (data.success) {
                    Swal.fire('Đã xóa!', data.message, 'success').then(() => location.reload());
                } else {
                    Swal.fire('Lỗi', data.message, 'error');
                }
            } catch (error) {
                Swal.fire('Lỗi!', 'Không thể thực hiện lệnh xóa', 'error');
            }
        }
    });
}

