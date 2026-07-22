import fs from 'node:fs'
import path from 'node:path'
import { fileURLToPath } from 'node:url'

const ROOT = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..', 'src')

const REPLACEMENTS = [
  ['Title = "Validation failed"', 'Title = "Dữ liệu không hợp lệ"'],
  ['Title = "Dependency unavailable"', 'Title = "Dịch vụ phụ thuộc tạm thời không khả dụng"'],
  ['Title = "Resource not found"', 'Title = "Không tìm thấy tài nguyên"'],
  ['Title = "Resource conflict"', 'Title = "Xung đột dữ liệu"'],
  ['Title = "Unauthorized"', 'Title = "Chưa xác thực"'],
  ['Title = "Forbidden"', 'Title = "Không có quyền truy cập"'],
  ['Title = "External service unavailable"', 'Title = "Dịch vụ bên ngoài tạm thời không khả dụng"'],
  ['Title = "Business rule violation"', 'Title = "Vi phạm quy tắc nghiệp vụ"'],
  ['Title = "An unexpected error occurred"', 'Title = "Đã xảy ra lỗi không mong muốn"'],
  [
    '"Likely schema/migration drift. Run: dotnet ef database update"',
    '"Có thể schema/migration lệch. Chạy: dotnet ef database update"',
  ],
  [
    'base($"{resourceName} with key \'{key}\' was not found.")',
    'base($"Không tìm thấy {resourceName} với mã \'{key}\'.")',
  ],
  ['An account with this email already exists.', 'Email này đã được đăng ký tài khoản.'],
  [
    'The confirmation email could not be sent. Please try registering again.',
    'Không gửi được email xác nhận. Vui lòng đăng ký lại.',
  ],
  [
    'Registration succeeded. Check your email to confirm your account before signing in.',
    'Đăng ký thành công. Vui lòng kiểm tra email để xác nhận tài khoản trước khi đăng nhập.',
  ],
  ['Login succeeded.', 'Đăng nhập thành công.'],
  ['Password recovery email has been requested.', 'Đã gửi email khôi phục mật khẩu.'],
  ['Access token is required.', 'Thiếu access token.'],
  ['New password must contain at least 6 characters.', 'Mật khẩu mới phải có ít nhất 6 ký tự.'],
  ['Password has been updated.', 'Đã cập nhật mật khẩu.'],
  ['Supabase did not return an email confirmation link.', 'Supabase không trả về liên kết xác nhận email.'],
  ['Password must contain at least 6 characters.', 'Mật khẩu phải có ít nhất 6 ký tự.'],
  ['A valid email is required.', 'Email không hợp lệ.'],
  ['Supabase Auth request failed.', 'Yêu cầu xác thực Supabase thất bại.'],
  ['You cannot start a conversation with yourself.', 'Bạn không thể tự nhắn tin với chính mình.'],
  ['You are not a participant in this conversation.', 'Bạn không phải thành viên của cuộc trò chuyện này.'],
  ['Conversation subject type is invalid.', 'Loại chủ đề cuộc trò chuyện không hợp lệ.'],
  [
    'A direct conversation requires two different participants.',
    'Cuộc trò chuyện trực tiếp cần hai người khác nhau.',
  ],
  ['User is not a participant in this conversation.', 'Người dùng không thuộc cuộc trò chuyện này.'],
  [
    'The authenticated token does not contain a valid subject.',
    'Token đăng nhập không chứa thông tin người dùng hợp lệ.',
  ],
  [
    'Complete your profile before using this feature.',
    'Vui lòng hoàn thiện hồ sơ trước khi dùng tính năng này.',
  ],
  [
    'Complete your profile before updating lifestyle.',
    'Vui lòng hoàn thiện hồ sơ trước khi cập nhật lối sống.',
  ],
  ['Admin role is required.', 'Cần quyền quản trị viên.'],
  ['You can only modify your own resource.', 'Bạn chỉ có thể sửa tài nguyên của chính mình.'],
  ['Chatbot is disabled.', 'Chatbot hiện đang tắt.'],
  ['Cannot invite yourself.', 'Bạn không thể mời chính mình.'],
  [
    'Both users must save this rental post before creating a roommate invitation.',
    'Cả hai người phải lưu tin đăng này trước khi gửi lời mời ở ghép.',
  ],
  [
    'Save this rental post before viewing roommate candidates.',
    'Hãy lưu tin đăng này trước khi xem ứng viên ở ghép.',
  ],
  [
    'You are not a participant in this roommate conversation.',
    'Bạn không phải thành viên của cuộc trò chuyện ở ghép này.',
  ],
  ['Cannot send roommate invitation to yourself.', 'Không thể gửi lời mời ở ghép cho chính mình.'],
  [
    'Only pending roommate invitations can be updated.',
    'Chỉ lời mời ở ghép đang chờ mới có thể cập nhật.',
  ],
  [
    'Rental post owners cannot review their own rental post.',
    'Chủ tin đăng không thể tự đánh giá tin của mình.',
  ],
  [
    'Complete a viewing appointment before reviewing this rental post.',
    'Hãy hoàn tất lịch xem phòng trước khi đánh giá tin này.',
  ],
  ['You cannot report your own account.', 'Bạn không thể tự báo cáo tài khoản của mình.'],
  [
    'Rental post owners cannot request a viewing for their own post.',
    'Chủ tin đăng không thể tự đặt lịch xem phòng cho tin của mình.',
  ],
  [
    'Only appointment participants can propose another viewing time.',
    'Chỉ người tham gia lịch hẹn mới có thể đề xuất giờ xem khác.',
  ],
  ['The viewing time must be in the future.', 'Thời gian xem phòng phải ở tương lai.'],
  [
    'This viewing appointment can no longer be cancelled.',
    'Lịch xem phòng này không còn hủy được nữa.',
  ],
  [
    'This viewing appointment can no longer be rescheduled.',
    'Lịch xem phòng này không còn đổi giờ được nữa.',
  ],
  [
    'Only confirmed viewing appointments can be completed.',
    'Chỉ lịch xem phòng đã xác nhận mới có thể hoàn tất.',
  ],
  [
    'Only pending viewing appointments can be updated by the owner.',
    'Chỉ lịch xem phòng đang chờ mới được chủ tin cập nhật.',
  ],
  ['User id must not be empty.', 'Mã người dùng không được để trống.'],
  ['User role must be renter or landlord.', 'Vai trò phải là người thuê hoặc chủ trọ.'],
  ['Max budget must be greater than zero.', 'Ngân sách tối đa phải lớn hơn 0.'],
  [
    'Only landlord profiles can submit verification.',
    'Chỉ hồ sơ chủ trọ mới gửi được yêu cầu xác minh.',
  ],
  [
    'A landlord verification request is already pending.',
    'Đã có yêu cầu xác minh chủ trọ đang chờ xử lý.',
  ],
  ['This landlord profile is already verified.', 'Hồ sơ chủ trọ này đã được xác minh.'],
  [
    'Only pending landlord verification can be reviewed.',
    'Chỉ yêu cầu xác minh đang chờ mới có thể duyệt.',
  ],
  ['Display name is required.', 'Tên hiển thị là bắt buộc.'],
  [
    'Display name must not exceed {MaxDisplayNameLength} characters.',
    'Tên hiển thị không được vượt quá {MaxDisplayNameLength} ký tự.',
  ],
  ['Payment amount must be greater than zero.', 'Số tiền thanh toán phải lớn hơn 0.'],
  ['Payment purpose is invalid.', 'Mục đích thanh toán không hợp lệ.'],
  ['Order code is required.', 'Mã đơn hàng là bắt buộc.'],
  ['MoMo orderId and requestId are required.', 'Cần có orderId và requestId từ MoMo.'],
  ['Payment request was rejected.', 'Yêu cầu thanh toán bị từ chối.'],
  [
    'Successful response did not contain a valid payment URL.',
    'Phản hồi thành công nhưng không có đường dẫn thanh toán hợp lệ.',
  ],
  ['Premium subscription settings are not configured.', 'Chưa cấu hình gói Premium.'],
  ['MoMo payment settings are not configured.', 'Chưa cấu hình thanh toán MoMo.'],
  ['PayOS payment settings are not configured.', 'Chưa cấu hình thanh toán PayOS.'],
  [' returned an invalid response.', ' trả về phản hồi không hợp lệ.'],
  [' payment request timed out.', ' hết thời gian chờ yêu cầu thanh toán.'],
  ['Unable to communicate with ', 'Không thể kết nối tới '],
  [' payment request failed: ', ' thanh toán thất bại: '],
  ['Premium duration must be greater than zero.', 'Thời hạn Premium phải lớn hơn 0.'],
  ['Owner id must not be empty.', 'Mã chủ tin không được để trống.'],
  ['Rental post type is invalid.', 'Loại tin đăng không hợp lệ.'],
  ['Deposit must not be negative.', 'Tiền cọc không được âm.'],
  [
    'Available slots must be between 1 and the maximum occupants.',
    'Số chỗ trống phải từ 1 đến số người tối đa.',
  ],
  ['Media was not found on this rental post.', 'Không tìm thấy ảnh/media trên tin đăng này.'],
  [
    'Rental post details must be completed before submitting.',
    'Cần hoàn thiện thông tin tin đăng trước khi gửi duyệt.',
  ],
  [
    'Rental post requires at least {MinimumImageCountForSubmit} images.',
    'Tin đăng cần ít nhất {MinimumImageCountForSubmit} ảnh.',
  ],
  ['Only pending rental posts can be approved.', 'Chỉ tin đang chờ duyệt mới được phê duyệt.'],
  ['Only pending rental posts can be rejected.', 'Chỉ tin đang chờ duyệt mới bị từ chối.'],
  ['Only active rental posts can be marked as rented.', 'Chỉ tin đang hoạt động mới đánh dấu đã thuê.'],
  [
    'Rental post is not editable in its current status.',
    'Tin đăng không thể chỉnh sửa ở trạng thái hiện tại.',
  ],
  ['Rental post id must not be empty.', 'Mã tin đăng không được để trống.'],
  ['Reviewer id must not be empty.', 'Mã người đánh giá không được để trống.'],
  ['Rating must be between 1 and 5.', 'Điểm đánh giá phải từ 1 đến 5.'],
  [
    'Comment must not exceed {MaxCommentLength} characters.',
    'Bình luận không được vượt quá {MaxCommentLength} ký tự.',
  ],
  ['Occupant count must be greater than zero.', 'Số người ở phải lớn hơn 0.'],
  ['Only active wanted posts can be changed.', 'Chỉ tin tìm phòng đang hoạt động mới được sửa.'],
  ['Seller id must not be empty.', 'Mã người bán không được để trống.'],
  ['Marketplace price must be greater than zero.', 'Giá sản phẩm phải lớn hơn 0.'],
  [
    'Marketplace post requires between 1 and {MaxMediaCount} images.',
    'Tin chợ đồ cần từ 1 đến {MaxMediaCount} ảnh.',
  ],
  ['Only active marketplace posts can be changed.', 'Chỉ tin chợ đồ đang bán mới được sửa.'],
  ['Seller cannot buy their own marketplace item.', 'Người bán không thể mua sản phẩm của chính mình.'],
  ['Pickup time must be in the future.', 'Thời gian nhận hàng phải ở tương lai.'],
  ['Agreed price must be greater than zero.', 'Giá thỏa thuận phải lớn hơn 0.'],
  [
    'This marketplace order can no longer be cancelled.',
    'Đơn chợ đồ này không còn hủy được nữa.',
  ],
  [
    'Only {expected} marketplace orders can become {target}.',
    'Chỉ đơn chợ đồ ở trạng thái {expected} mới chuyển sang {target}.',
  ],
  [
    'Marketplace media URL must be an absolute HTTP or HTTPS URL.',
    'URL ảnh chợ đồ phải là HTTP/HTTPS tuyệt đối.',
  ],
  [
    'Marketplace media URL must not exceed {MaxUrlLength} characters.',
    'URL ảnh chợ đồ không được vượt quá {MaxUrlLength} ký tự.',
  ],
  [
    'Marketplace media sort order must not be negative.',
    'Thứ tự ảnh chợ đồ không được âm.',
  ],
  [
    'Invitation id and rental post id must not be empty.',
    'Mã lời mời và mã tin đăng không được trống.',
  ],
  ['Conversation participant ids must not be empty.', 'Mã người tham gia không được trống.'],
  [
    'A roommate conversation requires two different participants.',
    'Cuộc trò chuyện ở ghép cần hai người khác nhau.',
  ],
  [
    'User is not a participant in this roommate conversation.',
    'Người dùng không thuộc cuộc trò chuyện ở ghép này.',
  ],
  [
    'Conversation id and sender id must not be empty.',
    'Mã cuộc trò chuyện và người gửi không được trống.',
  ],
  ['Message body is required.', 'Nội dung tin nhắn là bắt buộc.'],
  [
    'Message body must not exceed {MaxBodyLength} characters.',
    'Nội dung tin nhắn không được vượt quá {MaxBodyLength} ký tự.',
  ],
  [
    'Message body is required and must not exceed {MaxBodyLength} characters.',
    'Nội dung tin nhắn là bắt buộc và không quá {MaxBodyLength} ký tự.',
  ],
  ['Conversation id must not be empty.', 'Mã cuộc trò chuyện không được trống.'],
  ['Chat message sender is invalid.', 'Người gửi tin nhắn không hợp lệ.'],
  ['Message content is required.', 'Nội dung tin nhắn là bắt buộc.'],
  [
    'Message content must not exceed {MaxContentLength} characters.',
    'Nội dung tin nhắn không được vượt quá {MaxContentLength} ký tự.',
  ],
  [
    'Chatbot AI quota is temporarily exhausted. Please try again later.',
    'Chatbot tạm hết hạn mức. Vui lòng thử lại sau.',
  ],
  [
    'Chatbot AI is temporarily unavailable. Please try again later.',
    'Chatbot tạm thời không khả dụng. Vui lòng thử lại sau.',
  ],
  ['Chatbot is temporarily unavailable.', 'Chatbot tạm thời không khả dụng.'],
  ['Chatbot returned no candidates.', 'Chatbot không trả về kết quả.'],
  ['Chatbot returned no content.', 'Chatbot không trả về nội dung.'],
  ['Chatbot returned empty content.', 'Chatbot trả về nội dung trống.'],
  ['AI parser is temporarily unavailable.', 'Bộ phân tích AI tạm thời không khả dụng.'],
  ['AI parser returned no candidates.', 'Bộ phân tích AI không trả về kết quả.'],
  ['AI parser returned no content.', 'Bộ phân tích AI không trả về nội dung.'],
  ['AI parser returned empty content.', 'Bộ phân tích AI trả về nội dung trống.'],
  ['Gemini AI settings are not configured.', 'Chưa cấu hình Gemini AI.'],
  ['File name is required.', 'Tên tệp là bắt buộc.'],
  ['comparison selection', 'lựa chọn so sánh'],
  ['No details returned.', 'Không có chi tiết trả về.'],
  ['$"{fieldName} is required."', '$"{fieldName} là bắt buộc."'],
  [
    '$"{fieldName} must not exceed {maxLength} characters."',
    '$"{fieldName} không được vượt quá {maxLength} ký tự."',
  ],
  [
    '$"{fieldName} is required and must not exceed {maxLength} characters."',
    '$"{fieldName} là bắt buộc và không quá {maxLength} ký tự."',
  ],
  ['$"{fieldName} must be greater than zero."', '$"{fieldName} phải lớn hơn 0."'],
  ['$"{fieldName} must not be negative."', '$"{fieldName} không được âm."'],
  ['$"{fieldName} is out of range."', '$"{fieldName} nằm ngoài phạm vi cho phép."'],
  ['$"{fieldName} must be between 1 and 5."', '$"{fieldName} phải từ 1 đến 5."'],
]

REPLACEMENTS.sort((a, b) => b[0].length - a[0].length)

function walk(dir, out = []) {
  for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
    const full = path.join(dir, entry.name)
    if (entry.isDirectory()) {
      if (entry.name === 'Migrations') continue
      walk(full, out)
    } else if (entry.name.endsWith('.cs')) {
      out.push(full)
    }
  }
  return out
}

let changedFiles = 0
let totalHits = 0
for (const file of walk(ROOT)) {
  let text = fs.readFileSync(file, 'utf8')
  const original = text
  let hits = 0
  for (const [oldStr, newStr] of REPLACEMENTS) {
    if (!text.includes(oldStr)) continue
    const count = text.split(oldStr).length - 1
    text = text.split(oldStr).join(newStr)
    hits += count
  }
  if (text !== original) {
    fs.writeFileSync(file, text, 'utf8')
    changedFiles++
    totalHits += hits
    console.log(`updated ${path.relative(ROOT, file)} (${hits})`)
  }
}

console.log(`\nDone: ${changedFiles} files, ${totalHits} replacements`)
