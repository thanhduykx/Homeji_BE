# Nội dung tiếng Việt — 23/10/2026

Sửa thông báo nghiệp vụ ví, đơn hàng và nhập chi phí; bỏ tên trạng thái kỹ thuật khỏi lỗi chuyển trạng thái. Quyền lợi gói từ API dùng “nhãn Premium”, “ưu tiên hiển thị”, “lời mời ở ghép”.

Không thay đổi giá, hạn mức, chuyển trạng thái, xác thực, schema hay giao dịch. Giữ cấu trúc ProblemDetails/validation hiện có, dịch tại nguồn để số tiền cấu hình được đi cùng thông báo; không dựa vào regex phía frontend. Client so khớp nguyên văn câu lỗi tiếng Anh sẽ cần cập nhật; Homeji không dùng các câu này làm mã trạng thái.

Kiểm tra: 50 test liên quan Wallet/Marketplace/Subscription/RentalCost đạt; API build Release không lỗi/cảnh báo. Chưa thực hiện giao dịch tiền thật. Bằng chứng deployment được ghi sau khi kiểm tra endpoint công khai.
