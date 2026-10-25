/**
 * HỆ THỐNG TRẮC NGHIỆM TIẾNG TRUNG - HOÀNG ĐẾ VERSION
 * Đã được tối ưu hóa và sửa lỗi
 */

// ==========================================
// 1. KHỞI TẠO BIẾN CƠ BẢN
// ==========================================
let currentIdx = 0;
let totalQuestions = 0;
let score = 0;
let userResults = [];

// ==========================================
// 2. KHỞI CHẠY KHI TẢI TRANG
// ==========================================
document.addEventListener('DOMContentLoaded', function () {
    // Xáo trộn câu hỏi chung
    shuffleQuestions();

    // Cập nhật tổng số câu thực tế từ DOM
    totalQuestions = document.querySelectorAll('.question-step').length;

    // Cập nhật thanh tiến trình ban đầu
    if (totalQuestions > 0) updateProgressBar(0);

    // Khởi tạo Word Bank (nếu đang ở trang dạng Bài tập điền khuyết có word-bank)
    if (document.getElementById('word-bank')) {
        initWordBank();
    }
});

// ==========================================
// 3. CÁC HÀM XÁO TRỘN DỮ LIỆU
// ==========================================
/** Thuật toán xáo trộn Fisher-Yates cho câu hỏi */
function shuffleQuestions() {
    const container = document.getElementById('quiz-content');
    if (!container) return;

    const questions = Array.from(container.querySelectorAll('.question-step'));

    for (let i = questions.length - 1; i > 0; i--) {
        const j = Math.floor(Math.random() * (i + 1));
        [questions[i], questions[j]] = [questions[j], questions[i]];
    }

    container.innerHTML = '';
    questions.forEach((q, index) => {
        q.id = `step-${index}`;
        if (index === 0) {
            q.classList.add('active');
        } else {
            q.classList.remove('active');
        }
        container.appendChild(q);
    });
}

/** Xáo trộn danh sách từ vựng cho bài Điền khuyết */
function initWordBank() {
    const questionItems = document.querySelectorAll('.question-item');
    let words = [];

    questionItems.forEach(item => {
        let answer = item.getAttribute('data-correct-answer');
        if (answer) {
            let parts = answer.split('/');
            parts.forEach(p => words.push(p.trim()));
        }
    });

    words = words.filter(w => w !== "");

    for (let i = words.length - 1; i > 0; i--) {
        const j = Math.floor(Math.random() * (i + 1));
        [words[i], words[j]] = [words[j], words[i]];
    }

    const wordBankContainer = document.getElementById('word-bank');
    if (wordBankContainer) {
        wordBankContainer.innerHTML = '';
        words.forEach(word => {
            const span = document.createElement('span');
            span.className = 'word-badge-fill shadow-sm animate__animated animate__zoomIn';
            span.innerText = word;
            wordBankContainer.appendChild(span);
        });
    }
}

// ==========================================
// 4. DẠNG 1: SẮP XẾP CÂU
// ==========================================
function addWord(idx, el) {
    const expl = document.getElementById('expl-' + idx);
    if (expl && expl.style.display === 'block') return; // Đã trả lời thì không cho sửa

    const container = document.getElementById('input-container-' + idx);
    if (!container) return;

    const placeholder = container.querySelector('.placeholder-text');
    if (placeholder) placeholder.remove();

    const wordTag = document.createElement('span');
    wordTag.className = 'word-hint shadow-sm animate__animated animate__fadeInUp';
    wordTag.style.cssText = "background:#fff; border-color:#dc3545; color:#dc3545; cursor:pointer;";
    wordTag.innerText = el.innerText.trim();

    wordTag.onclick = function () {
        if (expl && expl.style.display === 'block') return;
        this.remove();
        el.classList.remove('used');
        if (container.querySelectorAll('.word-hint').length === 0) {
            container.innerHTML = '<span class="text-muted small placeholder-text">Chạm vào từ ở trên để sắp xếp...</span>';
        }
        syncHiddenInput(idx);
    };

    container.appendChild(wordTag);
    el.classList.add('used');
    syncHiddenInput(idx);
}

function syncHiddenInput(idx) {
    const container = document.getElementById('input-container-' + idx);
    const inputHidden = document.getElementById('input-' + idx);
    if (!container || !inputHidden) return;

    const tags = container.querySelectorAll('.word-hint');
    let result = Array.from(tags).map(t => t.innerText.trim()).join("");

    if (result.length > 0 && !result.endsWith('。')) {
        result += '。';
    }
    inputHidden.value = result;
}

function clearInput(idx) {
    const expl = document.getElementById('expl-' + idx);
    if (expl && expl.style.display === 'block') return;

    const container = document.getElementById('input-container-' + idx);
    const inputHidden = document.getElementById('input-' + idx);

    if (container) {
        container.innerHTML = '<span class="text-muted small placeholder-text">Chạm vào từ ở trên để sắp xếp...</span>';
    }
    if (inputHidden) inputHidden.value = "";

    document.querySelectorAll(`#step-${idx} .word-pool .word-hint`).forEach(h => {
        h.classList.remove('used');
    });
}

function checkArrangeAnswer(idx, correct) {
    const inputHidden = document.getElementById('input-' + idx);
    let userValue = inputHidden ? inputHidden.value.trim() : "";

    if (!userValue || userValue === "。") {
        Swal.fire('Chú ý', 'Vui lòng chọn từ để hoàn thành câu!', 'info');
        return;
    }

    const cleanUser = userValue.replace(/\s+|。|\./g, '');
    const correctStr = String(correct);
    let isCorrect = false;

    if (correctStr.includes('/')) {
        const correctOptions = correctStr.split('/');
        isCorrect = correctOptions.some(option => option.replace(/\s+|。|\./g, '') === cleanUser);
    } else {
        isCorrect = (cleanUser === correctStr.replace(/\s+|。|\./g, ''));
    }

    const container = document.getElementById('input-container-' + idx);
    if (container) {
        if (isCorrect) {
            container.style.cssText += "border-color:#198754; background-color:#f1fcf7;";
            score++;
            Swal.fire({ icon: 'success', title: 'Chính xác!', timer: 800, showConfirmButton: false });
        } else {
            container.style.cssText += "border-color:#dc3545; background-color:#fff5f5;";
            Swal.fire({ icon: 'error', title: 'Chưa đúng', timer: 800, showConfirmButton: false });
        }
    }

    pushResult(idx, userValue, isCorrect);

    const btnCheck = document.getElementById('btn-check-' + idx);
    const btnClear = document.getElementById('btn-clear-' + idx);
    if (btnCheck) btnCheck.style.display = 'none';
    if (btnClear) btnClear.style.display = 'none';

    showResult(idx);
}

// ==========================================
// 5. DẠNG 2: TRẮC NGHIỆM CHỌN ĐÁP ÁN
// ==========================================
function checkNormalAnswer(btn, isCorrect, idx) {
    const step = document.getElementById('step-' + idx);
    if (!step) return;

    step.querySelectorAll('.btn-answer').forEach(b => b.disabled = true);
    const userAnswer = btn.innerText.trim();

    if (isCorrect) {
        btn.classList.replace('btn-outline-dark', 'btn-success');
        btn.innerHTML += ' <i class="fas fa-check-circle ms-2"></i>';
        score++;
    } else {
        btn.classList.replace('btn-outline-dark', 'btn-danger');
        btn.innerHTML += ' <i class="fas fa-times-circle ms-2"></i>';
    }

    pushResult(idx, userAnswer, isCorrect);
    showResult(idx);
}

// ==========================================
// 6. DẠNG 3: NHẬP VĂN BẢN (TYPING / ĐIỀN CHỮ)
// ==========================================
function checkTypingAnswer(index, correctAnswer) {
    const inputElement = document.getElementById(`input-${index}`);
    const inputGroup = document.getElementById(`input-group-${index}`);
    const statusIcon = document.getElementById(`status-icon-${index}`);
    const btnCheck = document.getElementById(`btn-check-${index}`);
    const explanationBox = document.getElementById(`expl-${index}`);
    const btnNext = document.getElementById('btn-next');

    if (!inputElement.value.trim()) {
        Swal.fire('Chú ý', 'Vui lòng nhập đáp án của bạn!', 'info');
        return;
    }

    const cleanUserAnswer = inputElement.value.replace(/\s+/g, '').toLowerCase();
    const possibleAnswers = correctAnswer.split('/');
    let isCorrect = false;

    for (let i = 0; i < possibleAnswers.length; i++) {
        const cleanCorrectAnswer = possibleAnswers[i].replace(/\s+/g, '').toLowerCase();
        if (cleanUserAnswer === cleanCorrectAnswer) {
            isCorrect = true;
            break;
        }
    }

    inputElement.disabled = true;
    if (btnCheck) btnCheck.disabled = true;
    if (statusIcon) statusIcon.style.display = 'flex';

    if (isCorrect) {
        if (inputGroup) {
            inputGroup.classList.remove('border');
            inputGroup.style.border = '2px solid #198754';
        }
        inputElement.classList.remove('text-primary', 'text-danger');
        inputElement.classList.add('text-success');
        if (statusIcon) statusIcon.innerHTML = '<i class="fas fa-check-circle text-success fs-4 animate__animated animate__bounceIn"></i>';
        score++;
    } else {
        if (inputGroup) {
            inputGroup.classList.remove('border');
            inputGroup.style.border = '2px solid #dc3545';
        }
        inputElement.classList.remove('text-primary', 'text-success');
        inputElement.classList.add('text-danger');
        if (statusIcon) statusIcon.innerHTML = '<i class="fas fa-times-circle text-danger fs-4 animate__animated animate__headShake"></i>';
    }

    pushResult(index, inputElement.value, isCorrect);

    if (explanationBox) explanationBox.style.display = 'block';
    if (btnNext) btnNext.style.display = 'inline-block';
}

// ==========================================
// 7. DẠNG 4: ĐIỀN KHUYẾT NHIỀU CÂU CÙNG LÚC
// ==========================================
function checkFillBlankAnswers() {
    const questionItems = document.querySelectorAll('.question-item');
    let totalCorrect = 0;

    questionItems.forEach(item => {
        const inputElement = item.querySelector('.fill-input');
        const iconElement = item.querySelector('.result-icon');
        const feedbackElement = item.querySelector('.correct-feedback');

        const userAnswer = inputElement.value.trim().toLowerCase();
        const correctAnswerAttr = item.getAttribute('data-correct-answer').trim().toLowerCase();

        const validAnswers = correctAnswerAttr.split('/');
        const isCorrect = validAnswers.some(ans => ans.trim() === userAnswer);

        item.classList.remove('is-valid-ans', 'is-invalid-ans');
        if (iconElement) iconElement.classList.remove('d-none');

        if (isCorrect) {
            item.classList.add('is-valid-ans');
            if (iconElement) iconElement.innerHTML = '<i class="fas fa-check-circle text-success fs-5 animate__animated animate__bounceIn"></i>';
            if (feedbackElement) feedbackElement.classList.add('d-none');
            totalCorrect++;
        } else {
            item.classList.add('is-invalid-ans');
            if (iconElement) iconElement.innerHTML = '<i class="fas fa-times-circle text-danger fs-5 animate__animated animate__shakeX"></i>';
            if (feedbackElement) feedbackElement.classList.remove('d-none');
        }

        inputElement.readOnly = true;
    });

    Swal.fire({
        title: 'Hoàn thành!',
        text: `Bạn đã trả lời đúng ${totalCorrect} / ${questionItems.length} câu.`,
        icon: totalCorrect === questionItems.length ? 'success' : 'info',
        confirmButtonText: 'Đóng',
        confirmButtonColor: '#dc3545'
    });
}

// ==========================================
// 8. DẠNG 5: NỐI CÂU
// ==========================================
function checkMatchingAnswers() {
    const leftItems = document.querySelectorAll('#left-column-list .left-item');
    const rawPairs = document.querySelectorAll('#matching-raw-data .raw-pair');

    let totalMatchQuestions = leftItems.length;
    let correctCount = 0;
    let isAllFilled = true;

    leftItems.forEach(item => {
        const input = item.querySelector('.input-match-number');
        if (!input.value.trim()) isAllFilled = false;
    });

    if (!isAllFilled) {
        Swal.fire('Thiếu thông tin', 'Vui lòng nhập số tương ứng của vế phải vào tất cả các ô trống trước khi nộp bài!', 'warning');
        return;
    }

    let explanationResultsHTML = "";

    leftItems.forEach((item) => {
        const input = item.querySelector('.input-match-number');
        const userNumber = input.value.trim();
        const letter = item.getAttribute('data-letter');
        const leftText = item.getAttribute('data-content').trim();
        const iconContainer = item.querySelector('.result-icon');

        const targetRightRow = document.querySelector(`#right-column-list .right-item[data-index="${userNumber}"]`);
        let userSelectedRightText = targetRightRow ? targetRightRow.getAttribute('data-content').trim() : "";

        let genuineCorrectRightText = "";
        let genuineExplanation = "Chưa có giải thích cho câu hỏi này.";

        rawPairs.forEach(pair => {
            if (pair.getAttribute('data-left').trim() === leftText) {
                genuineCorrectRightText = pair.getAttribute('data-right').trim();
                const exp = pair.getAttribute('data-explanation');
                if (exp && exp.trim() !== "") genuineExplanation = exp;
            }
        });

        let correctNumberOnUI = "?";
        document.querySelectorAll('#right-column-list .right-item').forEach(rRow => {
            if (rRow.getAttribute('data-content').trim() === genuineCorrectRightText) {
                correctNumberOnUI = rRow.getAttribute('data-index');
            }
        });

        let isCorrect = (userSelectedRightText !== "" && userSelectedRightText === genuineCorrectRightText);

        input.disabled = true;
        if (iconContainer) iconContainer.classList.remove('d-none');

        if (isCorrect) {
            correctCount++;
            item.classList.add('bg-success', 'bg-opacity-10', 'border-success');
            if (iconContainer) iconContainer.innerHTML = '<i class="fas fa-check-circle text-success fs-5"></i>';
        } else {
            item.classList.add('bg-danger', 'bg-opacity-10', 'border-danger');
            if (iconContainer) iconContainer.innerHTML = `<i class="fas fa-times-circle text-danger fs-5"></i> <span class="badge bg-success ms-1">${correctNumberOnUI}</span>`;
        }

        const statusClass = isCorrect ? "border-success" : "border-danger";
        const statusBadge = isCorrect
            ? '<span class="badge bg-success me-2"><i class="fas fa-check me-1"></i>Chính xác</span>'
            : '<span class="badge bg-danger me-2"><i class="fas fa-times me-1"></i>Chưa chính xác</span>';

        explanationResultsHTML += `
            <div class="p-4 bg-light border-start border-5 ${statusClass} rounded-3 shadow-sm mb-3">
                <div class="d-flex align-items-center mb-2">
                    ${statusBadge}
                    <small class="fw-bold text-dark text-uppercase tracking-wider">Cặp câu ${letter} + Vế ${correctNumberOnUI}:</small>
                </div>
                <p class="fw-bold text-dark mb-2 ps-2" style="font-size: 1.1rem; border-left: 3px solid #6c757d; padding-left: 10px;">
                    ${leftText} <span class="text-danger mx-1">➔</span> ${genuineCorrectRightText}
                </p>
                <hr class="my-2 text-muted opacity-25">
                <div class="d-flex align-items-center mb-1">
                    <i class="fas fa-lightbulb text-info fs-6 me-2"></i>
                    <small class="d-block fw-bold text-info text-uppercase tracking-wider">Giải thích chi tiết:</small>
                </div>
                <p class="mb-0 text-secondary ps-4 lh-base small" style="white-space: pre-line;">${genuineExplanation}</p>
            </div>
        `;
    });

    const explanationsListContent = document.getElementById('explanations-list-content');
    const matchingExplanationsWrapper = document.getElementById('matching-explanations-wrapper');

    if (explanationsListContent && matchingExplanationsWrapper) {
        explanationsListContent.innerHTML = explanationResultsHTML;
        matchingExplanationsWrapper.classList.remove('d-none');
    }

    const summaryScore = document.getElementById('summary-score');
    const summaryTitle = document.getElementById('summary-title');
    const quizSummary = document.getElementById('quiz-summary');

    if (summaryScore && quizSummary) {
        summaryScore.innerText = `${correctCount}/${totalMatchQuestions}`;
        if (correctCount === totalMatchQuestions) {
            summaryTitle.innerText = "Hoàn thành xuất sắc!";
            summaryTitle.className = "fw-bold text-success";
        } else if (correctCount >= totalMatchQuestions / 2) {
            summaryTitle.innerText = "Làm tốt lắm, cố gắng thêm nhé!";
            summaryTitle.className = "fw-bold text-warning";
        } else {
            summaryTitle.innerText = "Bạn cần ôn tập kỹ hơn!";
            summaryTitle.className = "fw-bold text-danger";
        }
        quizSummary.style.display = 'block';
        quizSummary.scrollIntoView({ behavior: 'smooth' });
    } else {
        Swal.fire('Hoàn thành', `Kết quả: Đạt ${correctCount}/${totalMatchQuestions} câu chính xác.`, 'info').then(() => {
            if (matchingExplanationsWrapper) matchingExplanationsWrapper.scrollIntoView({ behavior: 'smooth' });
        });
    }
}

// ==========================================
// 9. CÁC HÀM TIỆN ÍCH & LUỒNG ĐIỀU HƯỚNG
// ==========================================
function pushResult(idx, answer, isCorrect) {
    const stepEl = document.getElementById('step-' + idx);
    if (!stepEl) return;
    const qId = stepEl.getAttribute('data-question-id');
    userResults.push({
        idCauHoi: parseInt(qId) || 0,
        cauTraLoiNguoiDung: answer || "",
        laDung: Boolean(isCorrect)
    });
}

function showResult(idx) {
    const expl = document.getElementById('expl-' + idx);
    if (expl) {
        let content = expl.innerHTML;
        content = content.replace(/Pinyin:/g, '<br><strong class="text-primary">Pinyin:</strong>');
        content = content.replace(/Dịch:/g, '<br><strong class="text-muted">Dịch:</strong>');
        content = content.replace(/Dịch nghĩa:/g, '<br><strong class="text-muted">Dịch:</strong>');

        expl.innerHTML = content;
        expl.style.display = 'block';
        expl.classList.add('animate__animated', 'animate__fadeIn');
    }

    const btnNext = document.getElementById('btn-next');
    if (btnNext) btnNext.style.display = 'inline-block';

    updateProgressBar(idx);
}

function updateProgressBar(idx) {
    if (totalQuestions === 0) return;
    const progressPercent = ((idx + 1) / totalQuestions * 100);
    const progressBar = document.getElementById('quiz-progress');
    if (progressBar) progressBar.style.width = progressPercent + '%';
}

function nextQuestion() {
    const currentStep = document.getElementById('step-' + currentIdx);
    if (currentStep) currentStep.classList.remove('active');

    currentIdx++;

    if (currentIdx < totalQuestions) {
        const nextStep = document.getElementById('step-' + currentIdx);
        if (nextStep) {
            nextStep.classList.add('active');
            const btnNext = document.getElementById('btn-next');
            if (btnNext) btnNext.style.display = 'none';
            window.scrollTo({ top: 0, behavior: 'smooth' });
        }
    } else {
        saveQuizResults();
    }
}

/** Hàm này được gọi khi hoàn thành tất cả câu hỏi */
function saveQuizResults() {
    console.log("Đã lưu kết quả bài làm:", userResults, "Điểm:", score);
    Swal.fire({
        title: 'Hoàn thành bài thi!',
        text: `Bạn đã đạt ${score}/${totalQuestions} điểm.`,
        icon: 'success',
        confirmButtonText: 'Tuyệt vời'
    });
    // Gọi API lưu vào Database tại đây...
}