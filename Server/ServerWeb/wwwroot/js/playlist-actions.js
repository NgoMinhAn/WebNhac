// Biến dùng chung để lưu ID playlist đang được chọn từ menu
let selectedPlaylistId = null;

// 1. Hàm hiển thị Menu khi bấm vào dấu 3 chấm
function showMenu(e, id) {
    e.preventDefault();
    e.stopPropagation(); // Ngăn sự kiện click lan ra thẻ cha (thẻ Card)

    selectedPlaylistId = id;
    const menu = document.getElementById('contextMenu');

    if (menu) {
        menu.style.display = 'block';
        menu.style.left = e.pageX + 'px';
        menu.style.top = e.pageY + 'px';

        // Đóng menu khi click bất kỳ đâu bên ngoài
        const closeMenu = () => {
            menu.style.display = 'none';
            window.removeEventListener('click', closeMenu);
        };
        window.addEventListener('click', closeMenu);
    }
}

// 2. Hàm Xóa Playlist
async function actionDelete() {
    if (!selectedPlaylistId) return;

    const isConfirmed = confirm("Bạn có chắc chắn muốn xóa Playlist này không?");
    if (isConfirmed) {
        try {
            const response = await fetch(`/Playlist/Delete/${selectedPlaylistId}`, {
                method: 'POST'
            });

            if (response.ok) {
                // Nếu đang ở trang Index, xóa thẻ card tương ứng
                const card = document.getElementById(`playlist-${selectedPlaylistId}`);
                if (card) {
                    card.remove();
                } else {
                    // Nếu đang ở trang Details, xóa xong thì về trang chủ
                    window.location.href = '/Playlist/Index';
                }
            } else {
                alert("Không thể xóa playlist. Vui lòng thử lại.");
            }
        } catch (error) {
            console.error("Lỗi khi xóa:", error);
        }
    }
}

// 3. Hàm Sửa (Chuyển hướng đến trang Details để sửa)
function actionEdit() {
    if (selectedPlaylistId) {
        window.location.href = `/Playlist/Details/${selectedPlaylistId}`;
    }
}

// 4. Hàm thay đổi trạng thái riêng tư
async function actionPrivacy() {
    if (!selectedPlaylistId) return;

    try {
        const response = await fetch(`/Playlist/TogglePrivacy/${selectedPlaylistId}`, {
            method: 'POST'
        });
        if (response.ok) {
            alert("Đã cập nhật trạng thái riêng tư.");
            location.reload();
        }
    } catch (error) {
        alert("Lỗi kết nối server.");
    }
}