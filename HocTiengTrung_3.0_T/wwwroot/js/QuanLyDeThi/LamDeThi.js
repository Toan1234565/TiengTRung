// --- Config ---
const timeConfig = {
    1: @(ViewBag.TimeNghe ?? 1800),
    2: @(ViewBag.TimeDoc ?? 1800),
    3: @(ViewBag.TimeViet ?? 900)
};

let currentPart = 1;
let timeLeft = timeConfig[1];
let answers = {};
let timer;
const totalQuestions = @Model.Count;

let startTimeISO = "";


window.onload = () => {
    startTimeISO = new Date().toISOString();

    startTimer();
    updateProgress();
    initAnswerListener();
};

// --- Logic chọn đáp án & Cập nhật Sidebar ---
function initAnswerListener() {
    const container = document.querySelector('.main-container');

    // Click chọn đáp án trắc nghiệm
    container.addEventListener('click', function (e) {
        const optBox = e.target.closest('.opt-box') || e.target.closest('.opt-btn-circle');
        if (optBox) {
            const thuTu = optBox.getAttribute('data-thutu');
            const value = optBox.getAttribute('data-value');
            if (thuTu && value) {
                processSelection(thuTu, value, optBox);
            }
        }
    });

    // Nhập liệu phần Viết
    container.addEventListener('input', function (e) {
        if (e.target.tagName === 'TEXTAREA') {
            const thuTu = e.target.getAttribute('data-thutu');
            const value = e.target.value;
            if (thuTu) {
                answers[thuTu] = value;
                updateSidebarStatus(thuTu, value.trim().length > 0);
                updateProgress();
            }
        }
    });
}

function processSelection(thuTu, value, element) {
    answers[thuTu] = value;

    // Highlight trong câu hỏi
    const questionContainer = document.getElementById(`q-${thuTu}`);
    if (questionContainer) {
        questionContainer.querySelectorAll('.opt-box, .opt-btn-circle').forEach(opt => {
            opt.classList.remove('selected');
        });
        element.classList.add('selected');
    }

    // Đổi màu Sidebar
    updateSidebarStatus(thuTu, true);
    updateProgress();
}

function updateSidebarStatus(thuTu, isDone) {
    const navItem = document.getElementById(`nav-${thuTu}`);
    if (navItem) {
        if (isDone) navItem.classList.add('answered');
        else navItem.classList.remove('answered');
    }
}

function updateProgress() {
    const count = Object.keys(answers).filter(key =>
        answers[key] !== undefined && answers[key].toString().trim() !== ""
    ).length;
    document.getElementById('progressStatus').innerText = `${count}/${totalQuestions}`;
}

// --- Điều hướng & Thời gian ---
function startTimer() {
    if (timer) clearInterval(timer);
    timer = setInterval(() => {
        if (timeLeft > 0) {
            timeLeft--;
            updateClock(timeLeft);
        } else {
            handleTimeUp();
        }
    }, 1000);
}

function updateClock(seconds) {
    const m = Math.floor(seconds / 60);
    const s = seconds % 60;
    const el = document.getElementById('countdown');
    if (el) el.innerText = `${m.toString().padStart(2, '0')}:${s.toString().padStart(2, '0')}`;
}

function handleTimeUp() {
    clearInterval(timer);
    Swal.fire({
        icon: 'warning',
        title: 'Hết thời gian!',
        showConfirmButton: false,
        timer: 2000
    }).then(() => {
        if (currentPart < 3) switchPart(currentPart + 1);
        else nopBai(false);
    });
}

function switchPart(partIdx) {
    // Dừng audio ngay lập tức
    if (currentAudio) {
        currentAudio.pause();
        if (currentBtn) resetAudioBtn(currentBtn);
        currentAudio = null;
    }

    // Logic UI
    document.querySelectorAll('.section-tab').forEach((tab, i) => {
        let idx = i + 1;
        tab.className = 'section-tab ' + (idx < partIdx ? 'completed-locked' : idx === partIdx ? 'active' : 'waiting');
    });

    document.querySelectorAll('.exam-part').forEach(p => p.classList.remove('active'));
    document.getElementById(`part-${partIdx}`).classList.add('active');

    document.querySelectorAll('.num-circle').forEach(c => {
        c.classList.toggle('d-none', parseInt(c.dataset.kynang) !== partIdx);
    });

    currentPart = partIdx;
    timeLeft = timeConfig[currentPart];

    if (currentPart === 3) document.getElementById('btnNext').innerText = "NỘP BÀI";

    window.scrollTo({ top: 0, behavior: 'smooth' });
    startTimer();
}

function nextPartHandle() {
    if (currentPart < 3) {
        Swal.fire({
            title: 'Chuyển phần thi?',
            text: "Bạn không thể quay lại sửa bài phần cũ!",
            icon: 'warning',
            showCancelButton: true,
            confirmButtonText: 'Đồng ý'
        }).then((res) => { if (res.isConfirmed) switchPart(currentPart + 1); });
    } else {
        nopBai(true);
    }
}

function scrollToQ(thuTu) {
    const el = document.getElementById(`q-${thuTu}`);
    if (el) {
        window.scrollTo({ top: el.offsetTop - 120, behavior: 'smooth' });
        if (window.innerWidth < 992) toggleSidebar();
    }
}
/**
 * Trình phát Audio cải tiến
 */
function toggleAudio(btn) {
    const url = btn.getAttribute('data-audio');
    if (!url) return;

    // Nếu nhấn lại chính nút đang phát -> Dừng
    if (currentAudio && currentBtn === btn) {
        if (!currentAudio.paused) {
            currentAudio.pause();
            resetAudioBtn(btn);
            return;
        }
    }

    // Dừng âm thanh cũ nếu đang phát nút khác
    if (currentAudio) {
        currentAudio.pause();
        if (currentBtn) resetAudioBtn(currentBtn);
    }

    currentAudio = new Audio(url);
    currentBtn = btn;

    btn.classList.add('audio-playing');
    const originalHtml = btn.innerHTML;
    btn.setAttribute('data-original', originalHtml);
    btn.innerHTML = '<i class="fas fa-stop-circle me-1"></i> Đang phát...';

    currentAudio.play().catch(err => {
        console.error("Audio playback failed:", err);
        resetAudioBtn(btn);
    });

    currentAudio.onended = () => resetAudioBtn(btn);
}

function resetAudioBtn(btn) {
    btn.classList.remove('audio-playing');
    const original = btn.getAttribute('data-original');
    if (original) {
        btn.innerHTML = original;
    } else {
        // Fallback nếu không có original
        const isGuide = btn.innerText.includes("hướng dẫn");
        btn.innerHTML = isGuide ? '<i class="fas fa-volume-up me-1"></i> Nghe hướng dẫn' : '<i class="fas fa-play me-1"></i> Nghe';
    }
}

function showWarning(msg) {
    if (window.Swal) {
        Swal.fire({
            icon: 'error',
            title: 'Lỗi chọn đáp án',
            text: msg,
            toast: true,
            position: 'top-end',
            timer: 3000,
            showConfirmButton: false
        });
    } else {
        alert(msg);
    }
}
function toggleSidebar() {
    document.getElementById('sidebar').classList.toggle('open');
}

// --- Nộp Bài ---
function nopBai(confirm) {
    const submitData = () => {
        // Tắt âm thanh nếu đang phát
        if (currentAudio && !currentAudio.paused) {
            currentAudio.pause();
        }

        window.onbeforeunload = null;
        if (timer) clearInterval(timer);

        Swal.fire({
            title: 'Đang nộp bài...',
            allowOutsideClick: false,
            didOpen: () => Swal.showLoading()
        });

        // Parse final answers explicitly to ensure integer keys match backend Dictionary<int, string>
        const finalAnswers = {};
        Object.keys(answers).forEach(key => {
            finalAnswers[parseInt(key)] = answers[key].toString();
        });

        const payload = {
            examId: parseInt("@ViewBag.DeThiId"),
            userAnswers: finalAnswers,
            startTime: startTimeISO,
            hoTen: "@(User.Identity.IsAuthenticated? User.Identity.Name : "Khách ")"
            };

        fetch('/api/quanlydethi/nop-bai', { // Đảm bảo đúng Route Controller
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(payload)
        })
            .then(async res => {
                const result = await res.json();
                if (!res.ok) throw result;
                return result;
            })
            .then(result => {
                if (result.success) {
                    Swal.fire({
                        icon: 'success',
                        title: 'Thành công!',
                        html: `Điểm: <b class="text-danger">${result.diemSo}</b><br>Số câu đúng: ${result.soCauDung}/${result.tongSoCau}`
                    }).then(() => {
                        window.location.href = `/QuanLyDeThi/KetQuaBaiThi?id=${result.idKetQua}`;
                    });
                }
            })
            .catch(err => {
                console.error("Lỗi nộp bài:", err);
                Swal.fire('Lỗi', err.message || 'Không thể nộp bài', 'error');
            });
    };

    if (confirm) {
        Swal.fire({
            title: 'Nộp bài?',
            text: "Bạn có chắc chắn muốn kết thúc?",
            icon: 'warning',
            showCancelButton: true,
            confirmButtonText: 'Nộp bài'
        }).then(r => { if (r.isConfirmed) submitData(); });
    } else {
        submitData();
    }
}