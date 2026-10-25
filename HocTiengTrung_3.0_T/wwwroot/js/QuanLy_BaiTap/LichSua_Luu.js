// ==========================================
// BIẾN TOÀN CỤC (Global State)
// ==========================================
let totalSteps = 0;
let correctCount = 0;
let chiTiets = []; // { IdCauHoi, CauTraLoiNguoiDung, LaDung, AiEvaluation }
let idCapDo = 0;
let idLoai = 0;
let isFinished = false;

$(document).ready(function () {
    const container = $('#quiz-container-data');
    if (container.length > 0) {
        totalSteps = parseInt(container.data('total')) || 0;
        idCapDo = parseInt(container.data('capdo')) || 0;
        idLoai = parseInt(container.data('loai')) || 0;
    }

    // Khởi tạo hiển thị tiến độ ban đầu
    updateProgressBar();

    // Lắng nghe sự kiện để cập nhật tiến độ liên tục
    $(document).on('input change click', 'input, textarea, .btn-answer', function () {
        if (!isFinished) updateProgressBar();
    });

    // Xử lý riêng cho loại bài Điền từ (Word Pool)
    $(document).on('input', '.fill-blank-input', function () {
        this.value = this.value.toUpperCase();
        updateUsedWords();
    });

    $(document).on('change', '.fill-blank-input', function () {
        checkDuplicateInput(this);
    });
    $(document).ready(function () {
        // Căn đều 2 bên cho tất cả các đoạn văn tiếng Trung
        $('.text-chinese').css({
            'text-align': 'justify',
            'text-justify': 'inter-ideograph', /* Tối ưu riêng cho chữ tượng hình (Tiếng Trung/Nhật) */
            'word-break': 'break-word',       /* Giúp các dấu gạch dưới (_____) không làm lỗi dòng */
            'display': 'block'                 /* Đảm bảo thẻ h5 nhận thuộc tính căn lề hoàn hảo */
        });
    });
});

// ==========================================
// 1. XỬ LÝ LOGIC TƯƠNG TÁC GIAO DIỆN
// ==========================================

// Trắc nghiệm & Đọc hiểu
function selectAnswer(btn, indexCauHoi) {
    if (isFinished) return;
    const container = $(btn).closest('.answers-list');

    // Reset các nút khác và active nút được chọn
    container.find('.btn-answer').removeClass('selected btn-primary text-white shadow-sm active').addClass('btn-outline-dark');
    $(btn).addClass('selected btn-primary text-white shadow-sm active').removeClass('btn-outline-dark');

    updateProgressBar(); // Cập nhật lại thanh tiến độ
}

// ==========================================
// 2. TIỆN ÍCH HỖ TRỢ (Helpers)
// ==========================================

// Hàm chuẩn hóa chuỗi để so sánh đáp án (bỏ dấu câu, khoảng trắng, viết thường)
function normalizeString(str) {
    if (!str) return "";
    return str.toString()
        // Giữ lại dấu gạch chéo (/) tách các đáp án
        .replace(/[.,#!$%\^&\*;:{}=\-_`~()_+=@?？！，。、；：`“”‘’"']/g, "")
        // Loại bỏ khoảng trắng
        .replace(/\s+/g, "")
        .toLowerCase();
}

function updateProgressBar() {
    let answered = 0;

    // Đếm số câu đã trả lời dựa trên từng loại câu hỏi
    $('.question-container').each(function () {
        let type = $(this).data('type');
        if (type == 2 || type == 1006) { // Điền từ
            if ($(this).find('input').val().trim() !== '') answered++;
        } else if (type == 1002 || type == 1014 || type == 3 || type == 1007 || type == 1012 || type == 1013 || type == 1017 || type == 1020 || type == 1021 ) { // Nghe chép, Sắp xếp, Dịch thuật
            if ($(this).find('textarea').val().trim() !== '') answered++;
        }// Bổ sung thêm type 1024, 1025, 1026, 1027
        else if (type == 1024 || type == 1025 || type == 1026 || type == 1027) {
            if ($(this).find('.speaking-result-input').val().trim() !== '') answered++;
        }
        else if (type == 1015 || type == 1016) {
            const inputEl = $(this).find('.reorder-input');
            if (inputEl.length > 0 && inputEl.val().trim() !== '') {
                answered++;
            }
        } else { // Trắc nghiệm, Đọc hiểu
            if ($(this).find('.btn-answer.selected').length > 0) answered++;
        }
    });

    let percent = totalSteps > 0 ? (answered / totalSteps) * 100 : 0;
    $('#quiz-progress').css('width', percent + '%');
    $('#current-idx').text(answered); // Hiển thị số câu đã làm
}

// Map ký tự A, B, C sang từ vựng tiếng Trung tương ứng (Cho bài điền từ)
function mapLetterToWord(input) {
    let userChar = input.trim().toUpperCase();
    if (userChar.length !== 1) return input.trim();
    let foundWord = userChar;
    $('.pool-item-card').each(function () {
        if ($(this).attr('data-letter') === userChar) {
            foundWord = $(this).find('.text-chinese').text().trim();
            return false; // break loop
        }
    });
    return foundWord;
}

function updateUsedWords() {
    let usedLetters = [];
    $('.fill-blank-input').each(function () {
        let val = this.value.trim().toUpperCase();
        if (val) usedLetters.push(val);
    });

    $('.pool-item-card').each(function () {
        const letter = $(this).attr('data-letter');
        const isUsed = usedLetters.includes(letter);
        $(this).css({
            'opacity': isUsed ? '0.4' : '1',
            'filter': isUsed ? 'grayscale(1)' : 'none',
            'transition': 'all 0.3s ease'
        });
    });
}

function checkDuplicateInput(currentInput) {
    const val = currentInput.value.trim().toUpperCase();
    if (!val) return;
    let duplicate = false;
    $('.fill-blank-input').not(currentInput).each(function () {
        if ($(this).val().trim().toUpperCase() === val) duplicate = true;
    });
    if (duplicate) {
        Swal.fire({ icon: 'warning', title: 'Bẩm Hoàng đế!', text: `Mã [${val}] này Ngài đã sử dụng rồi!` });
        currentInput.value = "";
        updateUsedWords();
    }
}

// ==========================================
// 3. ĐÁNH GIÁ & NỘP BÀI (Evaluation & Submit)
// ==========================================

function submitFullQuiz() {
    chiTiets = [];
    correctCount = 0;
    let answeredCount = 0;

    $('.question-container').each(function () {
        const idCauHoi = parseInt($(this).data('id-cauhoi'));
        const type = $(this).data('type');
        let userAnswer = "";
        let correctAnswer = "";
        let isCorrect = false;

        if (type == 2 || type == 1006) { // Điền từ
            const input = $(this).find('input');
            const rawVal = input.val().trim();
            if (rawVal) answeredCount++;
            userAnswer = rawVal ? mapLetterToWord(rawVal) : "";
            correctAnswer = input.data('correct') || "";

            let normalizedUser = normalizeString(userAnswer);
            let normalizedCorrectField = normalizeString(correctAnswer);
            let validAnswers = normalizedCorrectField.split('/');
            isCorrect = validAnswers.includes(normalizedUser);

        } else if (type == 1002 || type == 1014 || type == 3 || type == 1007 || type == 1012 || type == 1013 || type == 1017 || type == 1020 || type == 1021) { // Nghe chép, Sắp xếp & Dịch thuật tự luận (1014, 1015)
            const textarea = $(this).find('textarea');
            userAnswer = textarea.val().trim();
            if (userAnswer) answeredCount++;

            // Nếu là bài dịch thuật AI (1012, 1013 ) thì không đối chiếu đáp án cứng tại client
            if (type == 1012 || type == 1013) {
                isCorrect = false; // Sẽ được tính toán lại từ kết quả chấm điểm của Gemini AI ở hàm executeFinalSubmit
            } else {
                correctAnswer = textarea.data('correct') || "";
                let normalizedUser = normalizeString(userAnswer);
                let normalizedCorrectField = normalizeString(correctAnswer);
                let validAnswers = normalizedCorrectField.split('/');
                isCorrect = validAnswers.includes(normalizedUser);
            }

        }
        else if (type == 1015 || type == 1016) {
            const inputEl = $(this).find('.reorder-input');
            userAnswer = inputEl.val().trim();
            let correctAnswer = inputEl.data('correct') || "";

            if (userAnswer) {
                // Tiền xử lý: Xóa toàn bộ dấu cách, dấu phẩy, đưa về CHỮ HOA để so sánh chuẩn tuyệt đối
                // Tránh việc user nhập "B C A" hoặc "b,c,a" bị tính sai
                let normalizedUser = userAnswer.replace(/[^a-zA-Z]/g, '').toUpperCase();
                let normalizedCorrect = correctAnswer.replace(/[^a-zA-Z]/g, '').toUpperCase();

                isCorrect = (normalizedUser === normalizedCorrect);
            }
        }
        else if (type == 1024 || type == 1025 || type == 1026 || type == 1027) {
            const inputSpeaking = $(this).find('.speaking-result-input');
            userAnswer = inputSpeaking.val().trim();
            if (userAnswer) answeredCount++;

            // So sánh chuỗi giọng nói AI nghe được với câu gốc
            let targetSentence = inputSpeaking.data('target-text') || inputSpeaking.data('correct') || "";

            let normUser = normalizeString(userAnswer);
            let normTarget = normalizeString(targetSentence);

            // Đúng nếu khớp hoàn toàn hoặc người dùng phát âm đúng câu mục tiêu
            isCorrect = (normUser !== "" && normUser === normTarget);
        }
        else { // Trắc nghiệm (1004, 1...)
            const selectedBtn = $(this).find('.btn-answer.selected');
            if (selectedBtn.length > 0) {
                answeredCount++;
                userAnswer = selectedBtn.find('.answer-text').text().trim();
                isCorrect = selectedBtn.data('is-correct') === true;
            }
        }

        if (isCorrect) correctCount++;

        chiTiets.push({
            IdCauHoi: idCauHoi,
            CauTraLoiNguoiDung: userAnswer,
            LaDung: isCorrect,
            AiEvaluation: null // Khởi tạo vùng chứa dữ liệu đánh giá từ AI
        });
    });

    // Xác nhận nộp bài
    Swal.fire({
        title: 'Nộp bài sao?',
        text: answeredCount < totalSteps
            ? `Hoàng đế mới hoàn thành ${answeredCount}/${totalSteps} câu. Ngài có chắc chắn muốn nộp bài?`
            : "Hoàng đế đã trả lời toàn bộ câu hỏi!",
        icon: 'question',
        showCancelButton: true,
        confirmButtonColor: '#3085d6',
        cancelButtonColor: '#d33',
        confirmButtonText: 'Nộp bài ngay',
        cancelButtonText: 'Khoan đã'
    }).then((result) => {
        if (result.isConfirmed) {
            // Nếu là bài đọc hiểu có dịch nghĩa, show đáp án một chút rồi mới submit
            if (idLoai === 1004 || idLoai === 1011) {
                $('#reading-explanation').slideDown();
            }
            executeFinalSubmit();
        }
    });
}

async function executeFinalSubmit() {
    isFinished = true;

    // 1. Hiển thị Modal thông báo đang đánh giá kết quả
    $('#evaluationModal').modal('show');
    $('#evaluationModal-text').html('<div class="spinner-border spinner-border-sm text-primary me-1"></div> Hệ thống đang xử lý bài làm của bạn...');

    // Ẩn giao diện làm bài
    $('#quiz-content, .word-pool-sticky, #btn-submit-all').slideUp();

    // --- KIỂM TRA LOẠI BÀI TẬP: NẾU LÀ DỊCH THUẬT TỰ LUẬN (1012 HOẶC 1013) ---
    if (idLoai === 1012 || idLoai === 1013) {
        $('#evaluationModal-text').html('<div class="spinner-border spinner-border-sm text-warning me-1"></div> Đang kết nối AI xử lý đa luồng để thẩm định bài dịch...');

        correctCount = 0; // Reset để tính toán dựa theo điểm số thực tế AI chấm

        // --- BƯỚC 1: GOM DỮ LIỆU ---
        // Tạo mảng Payload gửi lên API trong 1 lần duy nhất thay vì gọi lặp lại
        const aiPayload = chiTiets.map(item => ({
            IdCauHoi: item.IdCauHoi,
            UserAnswer: item.CauTraLoiNguoiDung ? item.CauTraLoiNguoiDung : ""
        }));

        try {
            // --- BƯỚC 2: GỌI API ĐA LUỒNG ---
            // Gọi endpoint mới nhận List (thay vì endpoint cũ nhận 1 câu)
            const aiResponse = await fetch('/api/quanlybaitap/nop-bai-nhieu-cau', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify(aiPayload)
            });

            if (aiResponse.ok) {
                // Nhận JSON trả về từ API (chứa { message, data: [...] })
                const result = await aiResponse.json();

                // Lấy mảng dữ liệu trả về từ object result.data
                const evaluatedList = result.data;

                // --- BƯỚC 3: MAP KẾT QUẢ VÀO UI ---
                // Duyệt qua từng câu hỏi gốc trên UI để gắn điểm
                for (let item of chiTiets) {
                    // Tìm kết quả của câu hỏi này trong mảng AI vừa trả về
                    // (Lưu ý: API C# thường tự động parse key thành camelCase, nên IdCauHoi thành idCauHoi)
                    const evaluatedItem = evaluatedList.find(x => x.idCauHoi === item.IdCauHoi);

                    if (evaluatedItem && evaluatedItem.evaluation) {
                        item.AiEvaluation = evaluatedItem.evaluation; // Lưu vết object EvaluationResult

                        // Quy ước: Hệ điểm 10, nếu AI chấm >= 5.0 điểm thì được ghi nhận là ĐÚNG
                        if (item.AiEvaluation.score >= 5.0) {
                            item.LaDung = true;
                            correctCount++;
                        } else {
                            item.LaDung = false;
                        }
                    } else {
                        // Trường hợp AI quá tải không chấm được câu này (trả về null)
                        item.LaDung = false;
                        item.AiEvaluation = { score: 0, comment: "Câu này chưa thể chấm do AI quá tải, vui lòng thử lại sau.", bestAlternative: "N/A", isLevelAppropriate: false, errorDetails: [] };
                    }
                }
            } else {
                // Lỗi HTTP (ví dụ: 500, 502)
                for (let item of chiTiets) {
                    item.LaDung = false;
                    item.AiEvaluation = { score: 0, comment: "Lỗi hệ thống: Máy chủ API không phản hồi kết quả.", bestAlternative: "N/A", isLevelAppropriate: false, errorDetails: [] };
                }
            }
        } catch (err) {
            // Lỗi sập mạng của client
            for (let item of chiTiets) {
                item.LaDung = false;
                item.AiEvaluation = { score: 0, comment: "Mất kết nối mạng trong quá trình gọi AI chấm bài.", bestAlternative: "N/A", isLevelAppropriate: false, errorDetails: [] };
            }
        }
    }

    // Cập nhật điểm hiển thị sau khi AI (hoặc hệ thống) đã hoàn tất chấm điểm
    $('#score-text').html(`<i class="fas fa-check-circle me-1"></i> Đúng: ${correctCount}`);
    $('#final-score').text(correctCount);

    // Cấu hình payload gửi lên server để lưu lịch sử
    const payload = {
        IdCapDo: parseInt(idCapDo),
        IdLoai: parseInt(idLoai),
        TenDe: `Luyện tập HSK ${idCapDo} - ${new Date().toLocaleDateString('vi-VN')}`,

        SoCauDung: parseInt(correctCount),
        TongSoCau: parseInt(totalSteps),
        ChiTiets: chiTiets.map(x => ({
            IdCauHoi: x.IdCauHoi,
            CauTraLoiNguoiDung: x.CauTraLoiNguoiDung,
            LaDung: x.LaDung
        }))
    };

    $('#evaluationModal-text').html('<div class="spinner-border spinner-border-sm text-info me-1"></div> Đang hoàn tất đánh giá...');

    if (idLoai === 1012 || idLoai === 1013) {
        // Không gọi API lưu database, chỉ hiện thông báo nhẹ nhàng cho user biết
        $('#save-status').html('<i class="fas fa-info-circle text-info"></i> Bài tự luận AI đã chấm xong (không lưu thành tích).');
    }
    else {
        // CHẶN GỌI API SỚM: Nếu client đã xác nhận chưa đăng nhập, kích hoạt thông báo và thoát luôn
        if (typeof window.isAuthenticated !== 'undefined' && !window.isAuthenticated) {
            handleUnauthenticated();
            return; // Thoát hoàn toàn, KHÔNG gọi API /luu-ket-qua nữa
        }
        // Các bài tập khác thì gọi API lưu bình thường
        try {
            const response = await fetch('/api/quanlybaitap/luu-ket-qua', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify(payload)
            });

            if (response.status === 401) {
                $('#evaluationModal').modal('hide');
                handleUnauthenticated();
                return;
            }

            const result = await response.json();

            if (!result.success && (result.message === "Unauthorized" || result.requiresLogin)) {
                $('#evaluationModal').modal('hide');
                handleUnauthenticated();
                return;
            } else if (result.success) {
                $('#save-status').html('<i class="fas fa-check-circle text-success"></i> Thành tích của bạn đã được lưu lại thành công!');
            } else {
                $('#save-status').html('<i class="fas fa-exclamation-triangle text-danger"></i> Có lỗi xảy ra khi lưu trữ thông tin.');
            }

        } catch (e) {
            $('#save-status').html('<i class="fas fa-wifi text-warning"></i> Máy chủ tạm thời mất kết nối khi lưu trữ.');
        }
    }

    // ĐOẠN NÀY ĐƯỢC ĐẨY RA NGOÀI IF/ELSE ĐỂ LUÔN LUÔN ĐƯỢC CHẠY
    // 2. Ẩn modal và hiển thị giao diện kết quả sau khi mọi thứ hoàn tất
    setTimeout(() => {
        $('#evaluationModal').modal('hide');
        $('#result-screen').fadeIn();
        window.scrollTo({ top: 0, behavior: 'smooth' });
    }, 800);
}

// Hàm xử lý khi chưa đăng nhập
// Hàm xử lý khi chưa đăng nhập
function handleUnauthenticated() {
    $('#save-status').html('<i class="fas fa-user-slash text-warning"></i> Hoàng đế chưa đăng nhập.');

    Swal.fire({
        title: 'Chưa đăng nhập!',
        text: "Thành tích của Ngài chưa được lưu. Ngài có muốn đăng nhập để lưu kết quả không?",
        icon: 'warning',
        showCancelButton: true,
        confirmButtonColor: '#3085d6',
        cancelButtonColor: '#aaa',
        confirmButtonText: 'Đi tới trang Đăng nhập',
        cancelButtonText: 'Bỏ qua lần này (Xem điểm luôn)'
    }).then((result) => {
        if (result.isConfirmed) {
            // Chuyển hướng tới trang đăng nhập có kèm ReturnUrl quy chuẩn
            window.location.href = '/QuanLyTaiKhoan/DangNhap?ReturnUrl=' + encodeURIComponent(window.location.pathname);
        } else {
            // Bấm "Bỏ qua lần này" -> Ẩn modal đánh giá và hiện luôn màn hình kết quả tại chỗ
            $('#evaluationModal').modal('hide');
            $('#result-screen').fadeIn();
            window.scrollTo({ top: 0, behavior: 'smooth' });
        }
    });
}

// ==========================================
// 4. XEM LẠI BÀI LÀM (Review Mode)
// ==========================================
function reviewMyQuiz() {
    $('#result-screen').hide();
    $('#quiz-content').fadeIn();
    $('.explanation-box').slideDown(); // Hiện tất cả phần giải thích dịch nghĩa

    // Duyệt qua từng câu hỏi để hiển thị kết quả đúng/sai trực quan
    $('.question-container').each(function () {
        const idCauHoi = $(this).data('id-cauhoi');
        const type = $(this).data('type');
        const item = chiTiets.find(x => x.IdCauHoi == idCauHoi);

        // Xóa thông báo cũ tránh bị lặp append khi bấm nút review nhiều lần
        $(this).find('.review-feedback-msg, .ai-evaluation-panel').remove();

        if (item) {
            // Lấy giá trị câu trả lời của người dùng (từ object hoặc DOM)
            const userAns = item.CauTraLoiNguoiDung ? item.CauTraLoiNguoiDung.trim() : "";

            // 1. DỊCH THUẬT AI (1012, 1013)
            if (type == 1012 || type == 1013) {
                const textarea = $(this).find('textarea');
                const rawVal = textarea.val() ? textarea.val().trim() : userAns;

                if (!rawVal) {
                    textarea.addClass('border-warning is-invalid bg-warning bg-opacity-10');
                    $(`<div class="review-feedback-msg alert alert-warning py-2 px-3 mt-2 rounded-3 d-flex align-items-center fw-bold">
                        <i class="fas fa-exclamation-triangle me-2 fs-5"></i> Bạn chưa nhập bản dịch cho câu này!
                       </div>`).insertAfter(textarea);
                } else {
                    textarea.removeClass('border-danger border-success border-2')
                        .addClass(item.LaDung ? 'border-success is-valid text-success' : 'border-danger is-invalid text-danger');
                }

                if (item.AiEvaluation) {
                    const eval = item.AiEvaluation;

                    // Render danh sách lỗi sai chi tiết nếu có
                    let errorDetailsHtml = "";
                    if (eval.errorDetails && eval.errorDetails.length > 0) {
                        errorDetailsHtml = `
                            <div class="mt-2 text-danger small">
                                <strong><i class="fas fa-bug me-1"></i> Các lỗi nhỏ phát hiện:</strong>
                                <ul class="mb-1 ps-3">
                                    ${eval.errorDetails.map(err => `<li>${err}</li>`).join('')}
                                </ul>
                            </div>`;
                    }

                    // Khối UI hiển thị đánh giá của AI
                    const aiPanelHtml = `
                        <div class="ai-evaluation-panel mt-3 p-3 border rounded shadow-sm bg-light" style="border-left: 4px solid #17a2b8 !important;">
                            <div class="d-flex justify-content-between align-items-center border-bottom pb-2 mb-2">
                                <span class="fw-bold text-info"><i class="fas fa-robot me-1"></i> ĐÁNH GIÁ TỪ GEMINI AI</span>
                                <div>
                                    <span class="badge bg-primary text-white me-1">Điểm số: ${eval.score}/10</span>
                                    <span class="badge ${eval.isLevelAppropriate ? 'bg-success' : 'bg-warning text-dark'}">
                                        ${eval.isLevelAppropriate ? 'Đạt chuẩn HSK' : 'Chưa chuẩn cấp độ'}
                                    </span>
                                </div>
                            </div>
                            <div class="small text-dark mb-2">
                                <strong><i class="fas fa-comment-dots me-1"></i> Nhận xét luận điểm:</strong> ${eval.comment}
                            </div>
                            ${errorDetailsHtml}
                            <div class="mt-2 p-2 bg-white rounded border-start border-success border-3">
                                <strong class="text-success small"><i class="fas fa-magic me-1"></i> Phương án dịch đề xuất tối ưu nhất:</strong>
                                <div class="text-chinese fw-bold fs-5 mt-1 text-dark">${eval.bestAlternative}</div>
                            </div>
                        </div>`;

                    $(aiPanelHtml).insertAfter(textarea);
                } else {
                    $(`<div class="text-muted mt-2 small review-feedback-msg"><i class="fas fa-info-circle me-1"></i> Không có dữ liệu chấm điểm AI cho câu này.</div>`).insertAfter(textarea);
                }
            }
            // 2. ĐIỀN TỪ (type == 2 || type == 1006)
            else if (type == 2 || type == 1006) {
                const input = $(this).find('input');
                const rawVal = input.val() ? input.val().trim() : userAns;

                if (!rawVal) {
                    input.addClass('border-warning is-invalid bg-warning bg-opacity-10');
                    $(`<div class="review-feedback-msg alert alert-warning py-2 px-3 mt-2 rounded-3 d-flex align-items-center fw-bold">
                        <i class="fas fa-exclamation-triangle me-2 fs-5"></i> Bạn chưa nhập câu trả lời! Đáp án đúng: <b class="text-success ms-1">${input.data('correct')}</b>
                       </div>`).insertAfter(input);
                } else {
                    input.removeClass('border-danger border-success border-2')
                        .addClass(item.LaDung ? 'border-success is-valid text-success' : 'border-danger is-invalid text-danger');
                    if (!item.LaDung) {
                        const correctAns = input.data('correct');
                        $(`<div class="text-danger mt-2 small review-feedback-msg"><i class="fas fa-times me-1"></i> Đáp án đúng: <b>${correctAns}</b></div>`).insertAfter(input);
                    }
                }
            }
            // 3. NGHE CHÉP, TỰ LUẬN, DỊCH THUẬT THƯỜNG (type == 1002, 1014, 3, 1007, 1017, 1020, 1021)
            else if (type == 1002 || type == 1014 || type == 3 || type == 1007 || type == 1017 || type == 1020 || type == 1021) {
                const textarea = $(this).find('textarea');
                const rawVal = textarea.val() ? textarea.val().trim() : userAns;

                textarea.css('font-size', '16px');

                if (!rawVal) {
                    textarea.addClass('border-warning is-invalid bg-warning bg-opacity-10');
                    $(`<div class="review-feedback-msg alert alert-warning py-2 px-3 mt-2 rounded-3 d-flex align-items-center fw-bold">
                        <i class="fas fa-exclamation-triangle me-2 fs-5"></i> Bạn chưa nhập nội dung! Đáp án đúng: <b class="text-success ms-1">${textarea.data('correct')}</b>
                       </div>`).insertAfter(textarea);
                } else {
                    textarea.removeClass('border-danger border-success border-2')
                        .addClass(item.LaDung ? 'border-success is-valid text-success' : 'border-danger is-invalid text-danger');
                    if (!item.LaDung) {
                        const correctAns = textarea.data('correct');
                        $(`<div class="text-danger mt-2 small review-feedback-msg"><i class="fas fa-times me-1"></i> Đáp án đúng: <b>${correctAns}</b></div>`).insertAfter(textarea);
                    }
                }
            }
            // 4. SẮP XẾP CÂU (type == 1015 || type == 1016)
            else if (type == 1015 || type == 1016) {
                const currentQuestionContainer = $(this);
                const inputEl = currentQuestionContainer.find('.reorder-input');
                const rawVal = inputEl.val() ? inputEl.val().trim() : userAns;

                inputEl.prop('disabled', true);
                inputEl.removeClass('border-danger border-success border-2 text-primary');

                if (!rawVal) {
                    inputEl.addClass('border-warning is-invalid bg-warning bg-opacity-10');
                    currentQuestionContainer.find('.input-area')
                        .removeClass('bg-primary bg-opacity-10 border-primary border-opacity-25')
                        .addClass('bg-warning bg-opacity-10 border-warning border-opacity-25');

                    const correctAns = inputEl.data('correct');
                    $(`<div class="review-feedback-msg alert alert-warning py-2 px-3 mt-3 rounded-3 d-flex align-items-center justify-content-center fw-bold w-100">
                        <i class="fas fa-exclamation-triangle me-2 fs-5"></i> Bạn chưa sắp xếp câu này! Đáp án đúng: <span class="badge bg-success px-2 py-1 fs-6 ms-2">${correctAns}</span>
                       </div>`).insertAfter(inputEl);
                } else if (item.LaDung) {
                    inputEl.addClass('border-success is-valid text-success bg-success bg-opacity-10');
                    currentQuestionContainer.find('.input-area')
                        .removeClass('bg-primary bg-opacity-10 border-primary border-opacity-25')
                        .addClass('bg-success bg-opacity-10 border-success border-opacity-25');
                } else {
                    inputEl.addClass('border-danger is-invalid text-danger bg-danger bg-opacity-10');
                    currentQuestionContainer.find('.input-area')
                        .removeClass('bg-primary bg-opacity-10 border-primary border-opacity-25')
                        .addClass('bg-danger bg-opacity-10 border-danger border-opacity-25');

                    const correctAns = inputEl.data('correct');
                    $(`<div class="text-danger mt-3 small fw-bold review-feedback-msg w-100 text-center">
                        <i class="fas fa-times me-1"></i> Đáp án đúng phải là: <span class="badge bg-danger px-2 py-1 fs-6">${correctAns}</span>
                       </div>`).insertAfter(inputEl);
                }
            }
            // 5. NỐI CẶP (type == 1023 || type == 2023)
            else if (type == 1023 || type == 2023) {
                const container = $(this);
                let correctPairs = [];

                container.find('.input-match-number').each(function () {
                    const inputEl = $(this);
                    const userVal = inputEl.val() ? inputEl.val().trim() : "";
                    const correctVal = String(inputEl.data('correct')).trim();
                    const label = inputEl.data('label');
                    const parentLi = inputEl.closest('.matching-item-left, .left-item');

                    if (label && correctVal) {
                        correctPairs.push(`<b>${label}</b> - <b>${correctVal}</b>`);
                    }

                    inputEl.removeClass('border-danger border-success border-warning text-danger text-success text-warning is-invalid is-valid bg-warning bg-opacity-10');

                    if (userVal === "") {
                        // CẢNH BÁO CHƯA NỐI
                        inputEl.addClass('border-warning is-invalid text-warning bg-warning bg-opacity-10 fw-bold');
                        parentLi.removeClass('border-success border-danger').addClass('border-warning bg-warning bg-opacity-10');
                        inputEl.next('.result-icon').html('<i class="fas fa-exclamation-circle text-warning ms-2"></i>');

                        $(`<div class="review-feedback-msg mt-1 w-100 text-end fs-7 text-warning fw-bold">
                            <i class="fas fa-exclamation-triangle me-1"></i>Chưa chọn. Đáp án: <span class="badge bg-success text-white px-2 py-1 fs-6">${correctVal}</span>
                        </div>`).insertAfter(inputEl.parent());
                    } else if (userVal === correctVal) {
                        // ĐÚNG
                        inputEl.addClass('border-success is-valid text-success bg-success bg-opacity-10 fw-bold');
                        parentLi.removeClass('border-danger border-warning').addClass('border-success bg-success bg-opacity-10');
                        inputEl.next('.result-icon').html('<i class="fas fa-check-circle text-success ms-2"></i>');
                    } else {
                        // SAI
                        inputEl.addClass('border-danger is-invalid text-danger bg-danger bg-opacity-10 fw-bold');
                        parentLi.removeClass('border-success border-warning').addClass('border-danger bg-danger bg-opacity-10');
                        inputEl.next('.result-icon').html('<i class="fas fa-times-circle text-danger ms-2"></i>');

                        $(`<div class="review-feedback-msg mt-1 w-100 text-end fs-7 text-danger">
                            <i class="fas fa-arrow-right me-1"></i>Đúng phải là: <span class="badge bg-success text-white px-2 py-1 fs-6">${correctVal}</span>
                        </div>`).insertAfter(inputEl.parent());
                    }
                });

                const expBox = container.find('.matching-explanation-box');
                if (expBox.length > 0) {
                    const summaryHtml = correctPairs.map(pair => `<span class="badge bg-white text-success border border-success px-3 py-2 fs-6 shadow-2xs">${pair}</span>`).join(' ');
                    expBox.find('.matching-pairs-summary').html(summaryHtml);

                    const giaiThichText = item.GiaiThich || item.giaiThich || item.Explanation || item.explanation || "Không có giải thích chi tiết cho câu hỏi này.";
                    expBox.find('.matching-explanation-content').html(giaiThichText);

                    expBox.slideDown();
                }
            }
            // 7. LUYỆN NÓI ĐỌC CÂU & NGHE LẶP LẠI (1024, 1025, 1026, 1027)
            else if (type == 1024 || type == 1025 || type == 1026 || type == 1027) {
                const box = $(this).find('.speaking-box');
                const inputSpeaking = $(this).find('.speaking-result-input');
                const correctText = inputSpeaking.data('target-text') || inputSpeaking.data('correct');

                $(this).find('.btn-mic').prop('disabled', true); // Khóa nút mic khi xem lại

                if (!userAns) {
                    $(`<div class="review-feedback-msg alert alert-warning py-2 px-3 mt-3 rounded-3 d-flex align-items-center justify-content-center fw-bold">
                    <i class="fas fa-exclamation-triangle me-2 fs-5"></i> Bạn chưa thu âm nói câu này! Câu chuẩn: <b class="text-success ms-1">${correctText}</b>
                   </div>`).appendTo(box);
                        } else if (item.LaDung) {
                            $(`<div class="review-feedback-msg alert alert-success py-2 px-3 mt-3 rounded-3 fw-bold">
                    <i class="fas fa-check-circle me-1 text-success fs-5"></i> Phát âm chuẩn xác! (${userAns})
                   </div>`).appendTo(box);
                        } else {
                            $(`<div class="review-feedback-msg alert alert-danger py-2 px-3 mt-3 rounded-3">
                    <div class="fw-bold"><i class="fas fa-times-circle me-1 text-danger"></i> Giọng nói ghi nhận: <span class="text-decoration-line-through">${userAns}</span></div>
                    <div class="small text-dark mt-1">Câu chuẩn cần đọc: <b class="text-success">${correctText}</b></div>
                   </div>`).appendTo(box);
                }
            }
            // 6. CÂU HỎI TRẮC NGHIỆM
            else {
                const block = $(this).find('.answers-list');

                // Trường hợp người dùng CHƯA CHỌN ĐÁP ÁN (Bỏ trống)
                if (!userAns) {
                    $(`<div class="review-feedback-msg alert alert-warning py-2 px-3 mb-3 rounded-3 d-flex align-items-center fw-bold" style="background-color: #fff3cd; color: #664d03; border: 1px solid #ffe69c;">
                        <i class="fas fa-exclamation-triangle me-2 fs-5"></i> Bạn chưa chọn đáp án cho câu này!
                       </div>`).insertBefore(block);

                    // Hiển thị đáp án đúng duy nhất
                    block.find('.btn-answer[data-is-correct="true"]').addClass('correct');
                }
                // Trường hợp người dùng ĐÃ CHỌN ĐÁP ÁN
                else {
                    block.find('.btn-answer[data-is-correct="true"]').addClass('correct');

                    if (!item.LaDung) {
                        block.find('.btn-answer').each(function () {
                            if ($(this).find('.answer-text').text().trim() === userAns) {
                                $(this).addClass('wrong');
                            }
                        });
                    }
                }
            }
        }

        // Khóa toàn bộ form nhập liệu
        $(this).find('input, textarea, button').prop('disabled', true);
    });

    // Vô hiệu hóa vùng Word Pool nếu hiển thị
    $('.word-pool-sticky').show().css({ 'pointer-events': 'none', 'opacity': '0.7' });

    window.scrollTo({ top: 0, behavior: 'smooth' });
}

// 1. Lưu lại hàm reviewMyQuiz gốc của hệ thống
const originalReviewMyQuiz = reviewMyQuiz;

// 2. Định nghĩa lại hàm reviewMyQuiz để mở rộng tính năng
reviewMyQuiz = function () {
    // Gọi lại toàn bộ logic cũ chạy trước
    originalReviewMyQuiz();

    // Thực hiện thêm logic mới sau khi hàm cũ chạy xong
    $('#btn-submit-all').hide();       // Ẩn nút nộp bài
    $('#review-actions').fadeIn();     // Hiển thị cụm nút điều hướng Review (Quay lại + Làm tiếp)
};

// 3. Hàm xử lý khi bấm nút "Quay lại bảng điểm"
function backToResult() {
    $('#quiz-content').hide();
    $('#review-actions').hide();
    $('#result-screen').fadeIn();
    window.scrollTo({ top: 0, behavior: 'smooth' });
}

// 4. Hàm xử lý khi bấm nút "Làm tiếp bài khác"
function continueNextQuiz() {
    window.location.href = `/QuanLyDanhSachBaiTap/BaiTiepTheo?capDo=${idCapDo}&loai=${idLoai}`;
}

// Hàm điều hướng về danh sách với tham số lọc
function backToDanhSach() {
    const url = `/QuanLyDanhSachBaiTap/LamBaiTap?idCapDo=${idCapDo}&idLoai=${idLoai}`;
    window.location.href = url;
}

// ==========================================
// 5. THOÁT KHỎI BÀI KIỂM TRA
// ==========================================
function confirmBack() {
    if (isFinished) { window.history.back(); return; }
    Swal.fire({
        title: 'Dừng lại sao?',
        text: 'Tiến trình làm bài của Hoàng đế sẽ bị mất!',
        icon: 'warning',
        showCancelButton: true,
        confirmButtonColor: '#3085d6',
        cancelButtonColor: '#d33',
        confirmButtonText: 'Hồi cung',
        cancelButtonText: 'Ở lại làm tiếp'
    }).then((result) => {
        if (result.isConfirmed) window.history.back();
    });
}
// ==========================================
// BỔ SUNG: XỬ LÝ NHẬN DIỆN GIỌNG NÓI TIẾNG TRUNG (ZH-CN)
// ==========================================
let recognitionInstances = {};
let currentActiveMicIndex = -1;

function getSpeechRecognizer(index) {
    const SpeechRecognition = window.SpeechRecognition || window.webkitSpeechRecognition;
    if (!SpeechRecognition) {
        Swal.fire({
            icon: 'error',
            title: 'Trình duyệt không hỗ trợ',
            text: 'Trình duyệt của bạn không hỗ trợ thu âm nhận diện giọng nói. Hãy dùng Google Chrome hoặc Microsoft Edge!'
        });
        return null;
    }

    if (!recognitionInstances[index]) {
        const recognizer = new SpeechRecognition();
        recognizer.lang = 'zh-CN'; // Nhận diện tiếng Trung chuẩn
        recognizer.continuous = false;
        recognizer.interimResults = false;

        recognizer.onstart = function () {
            currentActiveMicIndex = index;
            $(`#btn-mic-${index}`).removeClass('btn-outline-danger').addClass('btn-danger shadow-lg animate__animated animate__pulse animate__infinite');
            $(`#mic-status-${index}`).html('<span class="text-danger fw-bold"><i class="fas fa-circle me-1 small"></i> Đang lắng nghe... Hãy nói tiếng Trung!</span>');
        };

        recognizer.onresult = function (event) {
            const transcript = event.results[0][0].transcript;
            $(`#input-${index}`).val(transcript);
            $(`#transcript-text-${index}`).text(transcript);
            $(`#transcript-box-${index}`).removeClass('d-none');

            updateProgressBar(); // Cập nhật tiến độ hoàn thành câu
        };

        recognizer.onerror = function (event) {
            $(`#mic-status-${index}`).text('Lỗi nhận diện hoặc chưa cấp quyền Micro: ' + event.error);
        };

        recognizer.onend = function () {
            $(`#btn-mic-${index}`).removeClass('btn-danger animate__animated animate__pulse animate__infinite').addClass('btn-outline-danger');
            $(`#mic-status-${index}`).text('Đã thu âm xong. Bấm lại nếu muốn đọc lại.');
            currentActiveMicIndex = -1;
        };

        recognitionInstances[index] = recognizer;
    }
    return recognitionInstances[index];
}

function toggleRecordSpeech(index) {
    if (isFinished) return;
    const recognizer = getSpeechRecognizer(index);
    if (!recognizer) return;

    if (currentActiveMicIndex === index) {
        recognizer.stop();
    } else {
        try {
            recognizer.start();
        } catch (e) {
            recognizer.stop();
        }
    }
}