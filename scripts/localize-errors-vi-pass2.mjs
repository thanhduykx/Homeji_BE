import fs from 'node:fs'
import path from 'node:path'
import { fileURLToPath } from 'node:url'

const ROOT = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..', 'src')

const REPLACEMENTS = [
  ['Title is required.', 'Tiêu đề là bắt buộc.'],
  ['Description is required.', 'Mô tả là bắt buộc.'],
  ['Preferred area is required.', 'Khu vực mong muốn là bắt buộc.'],
  ['Desired move-in date cannot be in the past.', 'Ngày muốn chuyển vào không được ở quá khứ.'],
  ['A pending invitation already exists.', 'Đã có lời mời đang chờ xử lý.'],
  ['Reason is required.', 'Lý do là bắt buộc.'],
  [
    'Select between 2 and 4 different rental posts to compare.',
    'Chọn từ 2 đến 4 tin đăng khác nhau để so sánh.',
  ],
  ['Message is required.', 'Tin nhắn là bắt buộc.'],
  [
    'You already have an active viewing appointment for this rental post.',
    'Bạn đã có lịch xem phòng đang hoạt động cho tin đăng này.',
  ],
  ['A valid HTTPS document URL is required.', 'Cần URL giấy tờ HTTPS hợp lệ.'],
  [
    'You already have an active purchase request for this item.',
    'Bạn đã có yêu cầu mua đang hoạt động cho sản phẩm này.',
  ],
  ['Price must be greater than zero.', 'Giá phải lớn hơn 0.'],
  ['Latitude must be between -90 and 90.', 'Vĩ độ phải nằm trong khoảng -90 đến 90.'],
  ['Longitude must be between -180 and 180.', 'Kinh độ phải nằm trong khoảng -180 đến 180.'],
  ['Price range is invalid.', 'Khoảng giá không hợp lệ.'],
  [
    'Latitude and longitude must be provided together.',
    'Vĩ độ và kinh độ phải được cung cấp cùng lúc.',
  ],
  ['Coordinates are out of range.', 'Tọa độ nằm ngoài phạm vi cho phép.'],
  ['Search text is required.', 'Nội dung tìm kiếm là bắt buộc.'],
  [
    'Search text must not exceed {MaxSearchTextLength} characters.',
    'Nội dung tìm kiếm không được vượt quá {MaxSearchTextLength} ký tự.',
  ],
  ['Renter role is required.', 'Cần vai trò người thuê.'],
  ['Landlord role is required.', 'Cần vai trò chủ trọ.'],
  [
    'Description must not contain hidden phone numbers.',
    'Mô tả không được chứa số điện thoại ẩn.',
  ],
  ['Description contains prohibited words.', 'Mô tả chứa từ ngữ bị cấm.'],
  [
    'Message body is required and must not exceed {PostMessage.MaxBodyLength} characters.',
    'Nội dung tin nhắn là bắt buộc và không quá {PostMessage.MaxBodyLength} ký tự.',
  ],
  ['$"{field} is required."', '$"{field} là bắt buộc."'],
  [
    'Payment link request was rejected (code {providerCode ?? "unknown"}): {providerMessage ?? "No details returned."}',
    'Yêu cầu tạo link thanh toán bị từ chối (mã {providerCode ?? "unknown"}): {providerMessage ?? "Không có chi tiết trả về."}',
  ],
]

REPLACEMENTS.sort((a, b) => b[0].length - a[0].length)

function walk(dir, out = []) {
  for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
    const full = path.join(dir, entry.name)
    if (entry.isDirectory()) {
      if (entry.name === 'Migrations') continue
      walk(full, out)
    } else if (entry.name.endsWith('.cs')) out.push(full)
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
