const API_BASE_URL = "/api/luyen-nghe-phan-xa";
let currentQuestions = [];
let timerInterval = null;
let isTesting = false; // Trạng thái người dùng đang trong bài thi
let modalThoatInstance = null;

document.addEventListener('DOMContentLoaded', function () {
    modalThoatInstance = new bootstrap.Modal(document.getElementById('modalThoat'));
});

// Bắt sự kiện người dùng cố tình Reload hoặc Đóng Tab khi đang làm bài
window.addEventListener('beforeunload', function (e) {
    if (isTesting) {
        e.preventDefault();
        e.returnValue = '';
    }
});

async function loadReflexPractice(idBaiHoc) {
    const tbody = document.getElementById('vocabTableBody');
    const correctCountSpan = document.getElementById('correctCount');
    const totalCountSpan = document.getElementById('totalCount');

    tbody.innerHTML = `
        <tr>
            <td colspan="5" class="text-center py-5">
                <div class="spinner-border text-primary mb-2" role="status"></div>
                <div class="text-muted fw-medium">Đang tải câu hỏi luyện nghe phản xạ...</div>
            </td>
        </tr>`;

    try {
        const response = await fetch(`${API_BASE_URL}/bai-hoc/${idBaiHoc}`);
        const result = await response.json();

        if (!response.ok || !result.success) {
            tbody.innerHTML = `
                <tr>
                    <td colspan="5" class="text-center py-5 text-muted">
                        <i class="fas fa-exclamation-circle fa-3x mb-3 text-warning d-block"></i>
                        ${result.message || 'Không thể tải bài luyện tập phản xạ.'}
                    </td>
                </tr>`;
            return;
        }

        currentReflexQuestions = result.data || [];

        if (totalCountSpan) totalCountSpan.innerText = result.total || currentReflexQuestions.length;
        if (correctCountSpan) correctCountSpan.innerText = '0';

        renderReflexTable(currentReflexQuestions);

    } catch (error) {
        console.error('Lỗi API:', error);
        tbody.innerHTML = `
            <tr>
                <td colspan="5" class="text-center py-5 text-danger">
                    <i class="fas fa-wifi fa-3x mb-3 d-block"></i>
                    Lỗi kết nối máy chủ. Vui lòng thử lại sau!
                </td>
            </tr>`;
    }
}

// 1. Submit Form Cài Đặt Bài Luyện
document.getElementById('formTaoBaiLuyen').addEventListener('submit', async function (e) {
    e.preventDefault();

    const soLuong = document.getElementById('SoLuong').value;
    const idCapDo = document.getElementById('IdCapDo').value;
    const thoiGianSuyNghi = document.getElementById('ThoiGianSuyNghi').value;
    const modeNghe = document.querySelector('input[name="ModeNghe"]:checked').value;

    const requestBody = {
        SoLuong: parseInt(soLuong),
        IdCapDo: idCapDo ? parseInt(idCapDo) : null,
        ThoiGianSuyNghi: parseInt(thoiGianSuyNghi),
        ModeNghe: modeNghe
    };

    const btnStart = document.getElementById('btnStart');
    const originalBtnText = btnStart.innerHTML;
    btnStart.disabled = true;
    btnStart.innerHTML = '<span class="spinner-border spinner-border-sm me-2"></span>Đang chuẩn bị âm thanh...';

    try {
        const response = await fetch(`${API_BASE_URL}/tao-bai-luyen`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(requestBody)
        });

        const result = await response.json();

        if (response.ok && result.success) {
            currentQuestions = result.data;
            renderQuestions(currentQuestions);

            // Chuyển UI
            document.getElementById('setup-section').classList.add('d-none');
            document.getElementById('test-section').classList.remove('d-none');
            isTesting = true;

            // Tự động phát âm thanh của câu đầu tiên khi vào bài thi
            if (currentQuestions.length > 0) {
                playAudio(currentQuestions[0].audioUrl);
                const firstInput = document.querySelector('.input-answer');
                if (firstInput) firstInput.focus();
            }

            // Bắt đầu đếm ngược thời gian
            const totalSeconds = result.thoiGianSuyNghiMoiCau * result.total;
            startTimer(totalSeconds);
        } else {
            alert(result.message || "Không có dữ liệu từ vựng phù hợp!");
        }
    } catch (error) {
        console.error(error);
        alert("Lỗi kết nối đến Server!");
    } finally {
        btnStart.disabled = false;
        btnStart.innerHTML = originalBtnText;
    }
});

// 2. Render Danh sách câu hỏi (Không cần nút loa)
function renderQuestions(questions) {
    const container = document.getElementById('questions-container');
    container.innerHTML = '';

    questions.forEach((q, index) => {
        const modeBadge = q.modeLuyenTap === 'ZH'
            ? '<span class="badge bg-danger ms-2">Phát âm Tiếng Trung</span>'
            : '<span class="badge bg-success ms-2">Phát âm Tiếng Việt</span>';

        const html = `
                            <div class="card question-card mb-3 p-3 shadow-sm">
                                <div class="d-flex align-items-center">
                                    <div class="me-3 text-center">
                                        <span class="badge bg-primary rounded-circle p-3 fs-6">#${index + 1}</span>
                                    </div>
                                    <div class="flex-grow-1">
                                        <label class="form-label fw-bold mb-1 d-block">
                                            ${q.goiYPrompt} ${modeBadge}
                                        </label>
                                        <input type="text" class="form-control form-control-lg input-answer"
                                               data-id="${q.idTuVung}"
                                               data-mode="${q.modeLuyenTap}"
                                               data-audio="${q.audioUrl}"
                                               placeholder="Nhập đáp án của bạn..." autocomplete="off">
                                    </div>
                                </div>
                            </div>
                        `;
        container.insertAdjacentHTML('beforeend', html);
    });

    // Gắn sự kiện: Khi người dùng focus vào ô nhập câu nào -> Tự động phát âm thanh câu đó
    document.querySelectorAll('.input-answer').forEach(input => {
        input.addEventListener('focus', function () {
            const audioUrl = this.getAttribute('data-audio');
            playAudio(audioUrl);
        });
    });
}

// 3. Hàm đếm ngược thời gian
function startTimer(seconds) {
    clearInterval(timerInterval);
    let timeRemaining = seconds;
    const timerDisplay = document.getElementById('global-timer');

    function updateDisplay() {
        const mins = Math.floor(timeRemaining / 60);
        const secs = timeRemaining % 60;
        timerDisplay.textContent = `${mins.toString().padStart(2, '0')}:${secs.toString().padStart(2, '0')}`;

        if (timeRemaining <= 10) {
            timerDisplay.classList.remove('bg-white', 'text-primary');
            timerDisplay.classList.add('bg-danger', 'text-white');
        }
    }

    updateDisplay();

    timerInterval = setInterval(() => {
        timeRemaining--;
        updateDisplay();

        if (timeRemaining <= 0) {
            clearInterval(timerInterval);
            alert("Đã hết thời gian làm bài! Hệ thống sẽ tự động nộp bài.");
            submitTest();
        }
    }, 1000);
}

// 4. Hàm phát Audio
function playAudio(url) {
    if (!url) return;
    const player = document.getElementById('global-audio-player');
    player.src = url;
    player.play().catch(err => {
        console.log("Autoplay restriction or audio play error:", err);
    });
}

// 5. Nộp bài
document.getElementById('btnSubmitTest').addEventListener('click', function () {
    submitTest();
});

async function submitTest() {
    clearInterval(timerInterval);
    isTesting = false; // Đã xong bài test

    const btnSubmit = document.getElementById('btnSubmitTest');
    btnSubmit.disabled = true;
    btnSubmit.innerHTML = '<span class="spinner-border spinner-border-sm me-2"></span>Đang chấm điểm...';

    const answerInputs = document.querySelectorAll('.input-answer');
    const listAnswers = [];

    answerInputs.forEach(input => {
        listAnswers.push({
            IdTuVung: parseInt(input.getAttribute('data-id')),
            ModeLuyenTap: input.getAttribute('data-mode'),
            DapAnNguoiDung: input.value.trim()
        });
    });

    try {
        const response = await fetch(`${API_BASE_URL}/nop-bai`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ DanhSachDapAn: listAnswers })
        });

        const result = await response.json();

        if (response.ok && result.success) {
            renderResults(result.data);

            document.getElementById('test-section').classList.add('d-none');
            document.getElementById('result-section').classList.remove('d-none');
        } else {
            alert(result.message || "Lỗi khi chấm điểm!");
        }
    } catch (error) {
        console.error(error);
        alert("Lỗi kết nối đến Server!");
    } finally {
        btnSubmit.disabled = false;
        btnSubmit.innerHTML = '<i class="fas fa-check-circle me-2"></i>NỘP BÀI & CHẤM ĐIỂM';
    }
}

// 6. Hiển thị Kết quả (Có nút phát loa để xem đáp án)
function renderResults(data) {
    document.getElementById('final-score').innerText = data.diemSo.toFixed(1);
    document.getElementById('correct-count').innerText = data.soCauDung;
    document.getElementById('total-count').innerText = data.tongSoCau;

    const tbody = document.getElementById('result-tbody');
    tbody.innerHTML = '';

    data.chiTiet.forEach((item, idx) => {
        const isCorrectHTML = item.isDung
            ? '<span class="badge bg-success"><i class="fas fa-check me-1"></i>Đúng</span>'
            : '<span class="badge bg-danger"><i class="fas fa-times me-1"></i>Sai</span>';

        const userAnsColor = item.isDung ? 'text-success fw-bold' : 'text-danger fw-bold text-decoration-line-through';

        // Tìm lại link audio tương ứng của từ vựng này
        const originQuestion = currentQuestions.find(q => q.idTuVung === item.idTuVung);
        const audioUrl = originQuestion ? originQuestion.audioUrl : '';

        const html = `
                            <tr>
                                <td class="fw-bold">${idx + 1}</td>
                                <td>
                                    ${audioUrl ? `
                                        <button type="button" class="btn btn-sm btn-outline-primary rounded-circle" onclick="playAudio('${audioUrl}')" title="Nghe lại âm thanh">
                                            <i class="fas fa-volume-up"></i>
                                        </button>
                                    ` : '<span class="text-muted">-</span>'}
                                </td>
                                <td>${isCorrectHTML}</td>
                                <td class="${userAnsColor}">${item.dapAnNguoiDung || '<em class="text-muted">Bỏ trống</em>'}</td>
                                <td class="text-primary fw-bold">${item.dapAnDungStandard}</td>
                                <td>
                                    <div class="small">
                                        <strong>Hán:</strong> ${item.chuHan} <br/>
                                        <strong>Pinyin:</strong> ${item.pinyin} <br/>
                                        <strong>Nghĩa:</strong> ${item.nghiaViet}
                                    </div>
                                </td>
                            </tr>
                        `;
        tbody.insertAdjacentHTML('beforeend', html);
    });
}

// 7. Xử lý Thoát bài làm
function yeuCauThoat() {
    if (modalThoatInstance) {
        modalThoatInstance.show();
    }
}

function xacNhanThoatBai() {
    isTesting = false;
    clearInterval(timerInterval);
    if (modalThoatInstance) {
        modalThoatInstance.hide();
    }
    resetUI();
}

// 8. Reset giao diện làm lại
function resetUI() {
    clearInterval(timerInterval);
    isTesting = false;
    document.getElementById('result-section').classList.add('d-none');
    document.getElementById('test-section').classList.add('d-none');
    document.getElementById('setup-section').classList.remove('d-none');
    currentQuestions = [];
}
   