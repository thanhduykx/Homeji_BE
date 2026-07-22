import fs from 'node:fs'
import path from 'node:path'
import { fileURLToPath } from 'node:url'

const ROOT = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..', 'src')

const REPLACEMENTS = [
  [
    'The matching transaction is not a MoMo payment.',
    'Giao dịch tương ứng không phải thanh toán MoMo.',
  ],
  [
    'MoMo callback does not match the payment request.',
    'Callback MoMo không khớp với yêu cầu thanh toán.',
  ],
  [
    'MoMo callback amount does not match the payment transaction.',
    'Số tiền callback MoMo không khớp giao dịch.',
  ],
  [
    'Successful response contained invalid payment link data.',
    'Phản hồi thành công nhưng dữ liệu link thanh toán không hợp lệ.',
  ],
  ['PayOS webhook data is required.', 'Dữ liệu webhook PayOS là bắt buộc.'],
  [
    'The matching transaction is not a PayOS payment.',
    'Giao dịch tương ứng không phải thanh toán PayOS.',
  ],
  [
    'PayOS webhook amount does not match the payment transaction.',
    'Số tiền webhook PayOS không khớp giao dịch.',
  ],
  ['PayOS webhook currency must be VND.', 'Tiền tệ webhook PayOS phải là VND.'],
  [
    'Amount must be a positive whole VND value.',
    'Số tiền phải là số nguyên VND dương.',
  ],
  ['Premium package code is required.', 'Mã gói Premium là bắt buộc.'],
  ['Premium package was not found.', 'Không tìm thấy gói Premium.'],
  ['Payment signature is invalid.', 'Chữ ký thanh toán không hợp lệ.'],
  [
    "Content type '{contentType}' is not allowed. Accepted types: {string.Join(\", \", AllowedContentTypes)}.",
    "Loại tệp '{contentType}' không được phép. Các loại chấp nhận: {string.Join(\", \", AllowedContentTypes)}.",
  ],
  [
    'File size exceeds the maximum allowed size of {_options.MaxFileSizeBytes / (1024 * 1024)} MB.',
    'Kích thước tệp vượt quá giới hạn {_options.MaxFileSizeBytes / (1024 * 1024)} MB.',
  ],
  [
    'Cloudinary upload failed ({(int)response.StatusCode}): {errorBody}',
    'Tải ảnh lên Cloudinary thất bại ({(int)response.StatusCode}): {errorBody}',
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
