document.addEventListener("DOMContentLoaded", function () {
    // Kiểm tra cờ từ chối quyền truy cập
    const isAccessDenied = "@(TempData["AccessDenied"]?.ToString() ?? "")" === "true";
    const errorMessage = "@Html.Raw(TempData["ErrorMessage"]?.ToString() ?? "Bạn không có quyền truy cập chức năng này!")";

    if (isAccessDenied) {
        Swal.fire({
            icon: 'error',
            title: 'Truy cập bị từ chối!',
            text: errorMessage,
            timer: 2500, // Tự động đóng sau 2.5 giây
            timerProgressBar: true,
            confirmButtonText: 'Quay lại Trang chủ',
            confirmButtonColor: '#d33',
            allowOutsideClick: false,
            allowEscapeKey: false
        }).then(() => {
            // Chuyển hướng ngay về trang chủ
            window.location.href = "/";
        });

        // Tự động chuyển hướng phòng khi người dùng không bấm nút
        setTimeout(function () {
            window.location.href = "/";
        }, 2600);
    }
});