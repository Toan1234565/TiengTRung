$(document).ready(function () {
    // Ràng buộc sự kiện nhập liệu cho tất cả các ô input dạng nối câu
    $(document).on('input', '.input-match-number', function () {
        let $input = $(this);
        let val = $input.val();

        // 1. CHỈ CHO PHÉP NHẬP SỐ (Chặn nhập chữ cái, ký tự đặc biệt)
        val = val.replace(/[^0-9]/g, '');

        // 2. TÍNH SỐ LƯỢNG CẶP TỐI ĐA (Dựa trên số lượng dòng Cột Phải)
        let $container = $input.closest('#matching-quiz-container, .matching-words-container');
        let maxRightCount = $container.find('.right-item').length || 5; // Mặc định là 5 nếu không tìm thấy

        // 3. GIỚI HẠN GIÁ TRỊ PHẢI NỎ HƠN HOẶC BẰNG MAX (VD: <= 5)
        if (val !== '') {
            let num = parseInt(val, 10);
            if (num < 1) {
                val = '';
            } else if (num > maxRightCount) {
                val = maxRightCount.toString(); // Nếu nhập > 5 thì tự gán về 5

                // Hiển thị toast/thông báo nhẹ nếu muốn
                if (typeof Swal !== 'undefined') {
                    const Toast = Swal.mixin({
                        toast: true,
                        position: 'top-end',
                        showConfirmButton: false,
                        timer: 1500
                    });
                    Toast.fire({
                        icon: 'warning',
                        title: `Chỉ được nhập số từ 1 đến ${maxRightCount}!`
                    });
                }
            }
        }

        // Cập nhật lại giá trị đã lọc sạch vào ô input
        $input.val(val);

        // 4. KIỂM TRA TRÙNG LẶP VỚI CÁC Ô KHÁC
        checkDuplicateInputs($container);
    });
});

/**
 * Hàm kiểm tra và cảnh báo trùng lặp các ô nhập số trong bài nối câu
 */
function checkDuplicateInputs($container) {
    let usedValues = {};
    let $inputs = $container.find('.input-match-number');

    // Reset trạng thái báo trùng cũ
    $inputs.removeClass('border-warning text-warning fw-bold');
    $container.find('.duplicate-msg').remove();

    $inputs.each(function () {
        let $el = $(this);
        let val = $el.val().trim();

        if (val !== '') {
            if (usedValues[val]) {
                // Đánh dấu trùng cho cả ô hiện tại lẫn ô bị trùng trước đó
                $el.addClass('border-warning text-warning fw-bold');
                usedValues[val].addClass('border-warning text-warning fw-bold');

                // Thêm dòng cảnh báo trùng bên dưới
                if ($el.parent().find('.duplicate-msg').length === 0) {
                    $('<div class="duplicate-msg text-warning fs-7 mt-1 text-end"><i class="fas fa-exclamation-triangle me-1"></i>Bị trùng số ' + val + '!</div>').insertAfter($el.parent());
                }
            } else {
                usedValues[val] = $el;
            }
        }
    });
}
function checkMatchingAnswers() {
    let score = 0;
    let total = $('.left-item').length;
    let correctPairs = [];

    // 1. Duyệt qua từng ô input vế trái để chấm điểm và gom cặp đáp án chuẩn
    $('.left-item').each(function (index) {
        let parentLi = $(this);
        let inputEl = parentLi.find('.input-match-number');
        let iconEl = parentLi.find('.result-icon');

        let userVal = inputEl.val() ? inputEl.val().trim() : "";
        let correctVal = String(inputEl.data('correct')).trim();
        let label = inputEl.data('label'); // Ký tự A, B, C...

        // Khóa ô nhập liệu sau khi nộp
        inputEl.prop('disabled', true);

        // Reset các class CSS cũ
        inputEl.removeClass('border-danger border-success text-danger text-success bg-danger bg-success bg-opacity-10 is-invalid is-valid');
        parentLi.removeClass('border-danger border-success bg-danger bg-success bg-opacity-10');

        // Lưu cặp đáp án đúng vào mảng (VD: A - 2)
        if (label && correctVal) {
            correctPairs.push(`<span class="badge bg-white text-success border border-success px-3 py-2 fs-6 shadow-2xs"><b>${label}</b> - <b>${correctVal}</b></span>`);
        }

        // Kiểm tra đúng / sai
        if (userVal === correctVal && userVal !== "") {
            // Nối ĐÚNG
            score++;
            inputEl.addClass('border-success is-valid text-success bg-success bg-opacity-10 fw-bold');
            parentLi.addClass('border-success bg-success bg-opacity-10');
            iconEl.html('<i class="fas fa-check-circle text-success ms-2"></i>');
        } else {
            // Nối SAI hoặc BỎ TRỐNG
            inputEl.addClass('border-danger is-invalid text-danger bg-danger bg-opacity-10 fw-bold');
            parentLi.addClass('border-danger bg-danger bg-opacity-10');
            iconEl.html('<i class="fas fa-times-circle text-danger ms-2"></i>');

            // Chèn gợi ý đáp án đúng ngay dưới ô input bị sai
            $(`<div class="review-feedback-msg mt-2 w-100 text-end fs-7 text-danger">
                <i class="fas fa-arrow-right me-1"></i>Đúng phải là: <span class="badge bg-success text-white px-2 py-1 fs-6">${correctVal}</span>
            </div>`).insertAfter(inputEl.parent());
        }
    });

    // 2. Ẩn nút nộp bài
    $('#submit-action-area').slideUp();

    // 3. Render khung ĐÁP ÁN CHUẨN & GIẢI THÍCH
    // Khởi tạo HTML với danh sách Badge A - 4, B - 2...
    let explHtml = `<div class="matching-pairs-summary mb-4 d-flex flex-wrap gap-2">${correctPairs.join('')}</div>`;

    // Đọc dữ liệu từ thẻ ẩn để render lời giải thích chi tiết
    const labels = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
    $('#matching-raw-data .raw-pair').each(function (index) {
        let leftText = $(this).data('left');
        let rightText = $(this).data('right');
        let explText = $(this).data('explanation') || "Không có giải thích chi tiết.";
        let currentLabel = labels[index] || (index + 1);

        explHtml += `
            <div class="p-3 bg-white border-start border-4 border-success rounded-3 mb-3 shadow-sm">
                <div class="fw-bold text-dark text-chinese mb-2 fs-6">
                    <span class="badge bg-success bg-opacity-10 text-success me-2">${currentLabel}</span> 
                    ${leftText} 
                    <i class="fas fa-arrow-right text-muted mx-2 text-sm"></i> 
                    <span class="text-success">${rightText}</span>
                </div>
                <div class="small text-secondary bg-light p-2 rounded" style="white-space: pre-line;">
                    <i class="fas fa-book-open text-warning me-1"></i> ${explText}
                </div>
            </div>
        `;
    });

    // Đổ HTML vào UI và hiển thị hiệu ứng
    $('#explanations-list-content').html(explHtml);
    $('#matching-explanations-wrapper').removeClass('d-none');

    // 4. Bật Popup thông báo kết quả bằng SweetAlert2
    if (typeof Swal !== 'undefined') {
        Swal.fire({
            title: 'Khâm thử!',
            text: `Ngài đã trả lời đúng ${score} / ${total} câu.`,
            icon: score === total ? 'success' : (score >= total / 2 ? 'info' : 'warning'),
            confirmButtonText: 'Đã rõ',
            confirmButtonColor: '#0d6efd'
        });
    } else {
        alert(`Đã nộp bài! Ngài trả lời đúng ${score} / ${total} câu.`);
    }

    // Cuộn màn hình mượt mà xuống phần giải thích
    $('html, body').animate({
        scrollTop: $("#matching-explanations-wrapper").offset().top - 100
    }, 500);
}

