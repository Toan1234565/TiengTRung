let currentStep = 0;
let totalSteps = @displays.Count;
let correctCount = 0;

// Thêm từ vào ô nhập (Sắp xếp)
function addWord(idx, word) {
    const input = document.getElementById(`input-${idx}`);
    if (!input.disabled) {
        const space = input.value.length > 0 && !input.value.endsWith(" ") ? " " : "";
        input.value += space + word;
        input.focus();
    }
}

// Kiểm tra câu trả lời dạng nhập (Sắp xếp/Dịch)
function checkInputAnswer(idx, correctAnswer) {
    const input = document.getElementById(`input-${idx}`);
    const userAns = input.value.trim().toLowerCase().replace(/[.,!?;:\s]/g, '');
    const correctAns = correctAnswer.trim().toLowerCase().replace(/[.,!?;:\s]/g, '');

    input.disabled = true;
    document.getElementById(`btn-check-${idx}`).classList.add('d-none');

    if (userAns === correctAns) {
        input.classList.add('is-valid');
        input.style.borderColor = "var(--hsk-success)";
        input.style.backgroundColor = "#e8f5e9";
        correctCount++;
    } else {
        input.classList.add('is-invalid');
        input.style.borderColor = "var(--hsk-danger)";
        input.style.backgroundColor = "#ffebee";
    }

    updateScore();
    showFeedback(idx);
}

// Kiểm tra trắc nghiệm
function checkNormalAnswer(btn, isCorrect, idx) {
    const container = btn.closest('.answers-list');
    const buttons = container.querySelectorAll('.btn-answer');

    buttons.forEach(b => {
        b.disabled = true;
        // Highlight đáp án đúng nếu người dùng chọn sai
        if (b.getAttribute('onclick').includes('true')) {
            b.style.borderColor = "var(--hsk-success)";
            b.querySelector('.answer-prefix').style.backgroundColor = "var(--hsk-success)";
            b.querySelector('.answer-prefix').style.color = "white";
        }
    });

    if (isCorrect) {
        btn.classList.replace('btn-outline-dark', 'btn-success');
        correctCount++;
    } else {
        btn.classList.replace('btn-outline-dark', 'btn-danger');
    }

    updateScore();
    showFeedback(idx);
}

function updateScore() {
    document.getElementById(`score-text`).innerText = `Đúng: ${correctCount}`;
}

function showFeedback(idx) {
    document.getElementById(`expl-${idx}`).style.display = 'block';
    document.getElementById('btn-next').style.display = 'block';

    // Nếu là câu cuối
    if (currentStep === totalSteps - 1) {
        document.getElementById('btn-next').innerText = "Xem kết quả";
        document.getElementById('btn-next').classList.replace('btn-warning', 'btn-info');
    }
}

function nextQuestion() {
    if (currentStep < totalSteps - 1) {
        document.getElementById(`step-${currentStep}`).style.display = 'none';
        currentStep++;
        document.getElementById(`step-${currentStep}`).style.display = 'block';
        document.getElementById('btn-next').style.display = 'none';
        document.getElementById('current-idx').innerText = currentStep + 1;

        let percent = (currentStep / totalSteps) * 100;
        document.getElementById('quiz-progress').style.width = percent + '%';
        window.scrollTo({ top: 0, behavior: 'smooth' });
    } else {
        alert(`Hoàn thành! Bạn đạt: ${correctCount}/${totalSteps} câu đúng.`);
    }
}