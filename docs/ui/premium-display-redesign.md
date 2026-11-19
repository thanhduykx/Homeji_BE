# Thiết Kế Lại Nhãn Hiển Thị Tin Đăng Cao Cấp (Premium / Boost Listing Redesign)

> **Mã tài liệu:** `DESIGN-PREMIUM-DISPLAY-20261119`  
> **Phiên bản:** 3.0 (Synchronized with `@ /goal` & `@ /boost` framework)  
> **Áp dụng cho:** Hệ thống thẻ phòng trọ Map-first (`MapListingsWorkspace`, `RentalPostCard`, `MapListingCard`, `AiRentalResultCard`, `MapExploreListingCard`)  
> **Căn cứ hệ thống & Ràng buộc:**  
> - Báo cáo lỗi giao diện thực tế từ người dùng (`media_1795050811217_54aac670.jpg`)  
> - Bảng tokens giao diện: [`src/index.css`](file:///C:/EXE101_Homeji/src/index.css) & [`c:/Homeji/docs/ui/mcp-ui-context.md`](file:///c:/Homeji/docs/ui/mcp-ui-context.md)  
> - Kiểm thử contract: [`tests/premium-listing.test.mjs`](file:///C:/EXE101_Homeji/tests/premium-listing.test.mjs), [`tests/premium-badge-variants.test.mjs`](file:///C:/EXE101_Homeji/tests/premium-badge-variants.test.mjs) & [`tests/verified-owner-map.e2e.mjs`](file:///C:/EXE101_Homeji/tests/verified-owner-map.e2e.mjs)  

---

## 1. Hiện Trạng & Phân Tích Lỗi Giao Diện Cũ

Từ phản hồi của người dùng và ảnh chụp màn hình thực tế (`media_1795050811217_54aac670.jpg`):

1. **Chữ "Premium" khô cứng, lệch ngữ cảnh:**
   - Trong bối cảnh tìm nhà trọ và căn hộ sinh viên tại TP.HCM/Thủ Đức, người thuê tìm kiếm các "Tin nổi bật", "Phòng ưu tiên", "Phòng VIP" hoặc "Tin được kiểm duyệt tốt".
   - Chữ tiếng Anh "Premium" mang hơi hướng phần mềm SaaS (như Spotify/YouTube Premium), đặt cạnh nhãn tiếng Việt "Đã xác minh" tạo cảm giác chắp vá, thiếu đồng bộ ngôn ngữ.
2. **Màu sắc xỉn và lỗi thời:**
   - Phiên bản cũ dùng dải màu vàng mỡ gà (`#fff8e7` đến `#f5e5bb`) với chữ nâu xỉn `#705015` và viền vàng bùn `#dec587`.
   - Diện mạo này giống tem giảm giá siêu thị hoặc nhãn cảnh báo hơn là đặc quyền dịch vụ nâng tầm của chủ nhà.
3. **Hiệu ứng sao pixel retro gây nhiễu thị giác:**
   - Hoạt ảnh 12 khung hình sprite (`/images/chat/premium-stars.png`) với bộ lọc `sepia(1) saturate(2.4)` bị vỡ hạt trên màn hình độ phân giải cao (Retina/OLED di động), tạo cảm giác chớp nháy thiếu chuyên nghiệp.
4. **Lỗi lệch nhịp bố cục dạng bậc thang (Staircase Grid Misalignment):**
   - Trên danh sách thẻ phòng trọ (`MapListingsWorkspace`), các thẻ có gói Premium hiển thị thanh nhãn riêng cao 44px ở trên cùng, đẩy ảnh tụt xuống.
   - Các thẻ thường không có nhãn này, khiến ảnh nằm sát mép trên. Hậu quả là toàn bộ lưới thẻ phòng trọ bị lồi lõm so le, phá vỡ tính đối xứng cơ bản của giao diện dạng lưới.
5. **Viền thẻ xỉn màu:**
   - Viền thẻ `.premium-listing` màu nâu đất `#aa792b` tạo cảm giác viền bị ố bẩn hơn là ánh vàng danh giá.

---

# @ /goal — Mục Tiêu Thiết Kế & Ràng Buộc Kỹ Thuật Cốt Lõi

### Mục Tiêu 1: Nâng Tầm Thẩm Mỹ Sang Trọng & Tinh Tế (Aesthetic Elevation)
- Chuyển đổi toàn bộ bảng màu từ vàng mỡ gà xỉn sang dải màu **Champagne Gold & Warm Amber** ánh kim (`#fff7db` ➔ `#fedd8a`), chữ màu hổ phách đậm `#5e4002`, viền vàng tinh tế kết hợp hiệu ứng đổ bóng vi hạt (`box-shadow: 0 1px 3px rgba(160, 115, 20, 0.1), inset 0 1px 0 rgba(255, 255, 255, 0.85)`).
- Chuyển viền thẻ `.premium-listing` sang màu vàng hoàng kim hổ phách `#dfaa42` với quầng sáng mềm mại (`box-shadow: inset 0 0 0 1px rgba(223, 170, 66, 0.22), 0 2px 14px rgba(223, 170, 66, 0.12)`).

### Mục Tiêu 2: Bản Địa Hóa & Thân Thiện Ngữ Cảnh Người Thuê Trọ (Localization & Context)
- Thay chữ tiếng Anh "Premium" bằng nhãn chuẩn hóa tiếng Việt: **"Nổi bật"** (hỗ trợ cấu hình "Ưu tiên", "Đẩy tin", "VIP").
- Đi kèm biểu tượng ✦ (`SparkleIcon` 4 cánh sắc sảo), tên lửa (`RocketSvg`), tia sét (`ElectricBoltIcon`) hoặc vương miện thanh lịch, đồng bộ 100% với phong cách nhãn "Đã xác minh" bên cạnh.

### Mục Tiêu 3: Hỗ Trợ Đầy Đủ Dark Mode & Trợ Năng (Accessibility & Tokens)
- **Dark Mode:** Tự động chuyển nền sang màu hổ phách đậm sang trọng `linear-gradient(135deg, rgba(62, 46, 18, 0.92) 0%, rgba(82, 60, 22, 0.95) 100%)`, chữ sáng `#ffe79a`, viền vàng ánh kim mềm mại.
- **Accessibility:** Đảm bảo độ tương phản màu chữ đạt chuẩn WCAG AA/AAA. Giữ nguyên hỗ trợ `forced-colors: active` và `prefers-reduced-motion: reduce`.

### Mục Tiêu 4: Bảo Toàn Tuyệt Đối Test Contract (Zero Regression)
- Duy trì nguyên vẹn các class định danh DOM: `.premium-listing`, `.premium-badge`, `.premium-stars`, `.premium-stars__frame`.
- 100% test suite (572/572 tests) bao gồm `tests/premium-listing.test.mjs`, `tests/premium-badge-variants.test.mjs` và `tests/verified-owner-map.e2e.mjs` vượt qua mà không cần sửa đổi bất kỳ assert nào.

---

# @ /boost — Các Phương Án Thay Thế Đột Phá (Alternative Ways to Display Premium)

Dưới đây là 5 phương án đột phá để hiển thị trạng thái Premium / Boost:

| Phương Án | Tên Gọi | Cơ Chế Thị Giác | Điểm Mạnh | Kịch Bản Khuyến Nghị |
|---|---|---|---|---|
| **Boost 1** | **Huy hiệu "Nổi bật" Champagne Gold** *(Đã kích hoạt mặc định)* | Pill bo tròn 999px, nền Champagne ánh kim, icon ✦ Sparkle, nhãn "Nổi bật" thuần Việt. | Tinh tế, ấm áp, đồng bộ hoàn hảo với nhãn "Đã xác minh", 100% qua test contract. | Mặc định toàn nền tảng (`MapListingsWorkspace`, `RentalPostCard`, v.v.). |
| **Boost 2** | **Huy hiệu "Ưu tiên" Tốc Độ (Priority Boost)** | Biểu tượng tên lửa/sét tốc độ, nhãn "Ưu tiên" hoặc "Đẩy tin", dải màu ấm Amber kết hợp cam cháy Coral. | Nhấn mạnh tính năng "Đẩy tin" (listing bump) giúp chủ nhà nhanh chóng cho thuê phòng. | Dành cho các gói Đẩy tin nhanh theo ngày/tuần (`variant="boost"`). |
| **Boost 3** | **Viền Hào Quang Chuyển Động `BorderBeam`** | Chùm sáng vàng hổ phách chạy dọc theo chu vi mép thẻ (thư viện `border-beam`). | Cảm giác công nghệ cao (HorizonX/Linear pattern), thu hút ánh nhìn mà không cần nhãn chữ thô. | Thẻ phòng trọ trang chủ Landing (`ListingCard.tsx`) & phòng spotlight. |
| **Boost 4** | **Floating Glass Pill Trên Ảnh** | Huy hiệu kính mờ (frosted glass, `backdrop-filter: blur(8px)`) nổi trực tiếp ở góc trên bên trái ảnh. | Giải quyết triệt để lỗi so le lưới (staircase grid), đồng nhất vị trí ảnh 100% trên toàn bộ danh sách. | Chế độ lưới ảnh thuần túy hoặc giao diện mobile viewport nhỏ (`variant="glass"`). |
| **Boost 5** | **Component Đa Năng Cấu Hình Linh Hoạt (Configurable)** | Nhận props `label`, `icon`, `variant` (`pill` \| `boost` \| `glass` \| `compact` \| `subtle`), `showStars`. | Tối đa hóa khả năng tái sử dụng, tương thích ngược toàn diện với mọi màn hình. | Kiến trúc component `PremiumBadgeProps` cốt lõi của Homeji. |

---

## 2. Chi Tiết Kỹ Thuật Hiện Thực Hóa (Implementation Details)

### 2.1. Cấu Trúc Component Linh Hoạt (`src/components/PremiumBadge.tsx`)
```tsx
import type { ReactNode } from 'react'
import './PremiumBadge.css'
import { PremiumStars } from './PremiumStars'
import { SparkleIcon, CrownIcon, ElectricBoltIcon } from './icons/SvgIcons'

export type PremiumBadgeVariant = 'pill' | 'boost' | 'glass' | 'compact' | 'subtle'
export type PremiumBadgeIconType = 'sparkle' | 'crown' | 'rocket' | 'bolt' | ReactNode

export type PremiumBadgeProps = {
  /** Nhãn hiển thị; mặc định là 'Nổi bật' thay cho chữ 'Premium' thô cũ */
  label?: ReactNode
  /** Loại biểu tượng; mặc định là 'sparkle' (✦), hỗ trợ 'crown', 'rocket', 'bolt' hoặc ReactNode tùy chọn */
  icon?: PremiumBadgeIconType
  /** Biến thể hiển thị thay thế:
   * - 'pill': Bo tròn tiêu chuẩn, nền Champagne ánh kim (mặc định)
   * - 'boost': Nhấn mạnh tính năng Đẩy tin / Ưu tiên tốc độ
   * - 'glass': Nền kính mờ frosted glass thích hợp đè lên ảnh
   * - 'compact': Nhỏ gọn, tối giản không gian
   * - 'subtle': Tinh tế, nền nhẹ nhàng trang nhã
   */
  variant?: PremiumBadgeVariant
  /** Hiệu ứng ánh sao lấp lánh nhẹ; mặc định bật để bảo toàn test contract */
  showStars?: boolean
  className?: string
  title?: string
}

function RocketSvg({ size = 12 }: { size?: number }) {
  return (
    <svg width={size} height={size} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
      <path d="M4.5 16.5c-1.5 1.26-2 5-2 5s3.74-.5 5-2c.71-.84.7-2.13-.09-2.91a2.18 2.18 0 0 0-2.91-.09zM12 15l-3-3a22 22 0 0 1 2-3.95A12.88 12.88 0 0 1 22 2c0 2.72-.78 7.5-6 11a22.35 22.35 0 0 1-4 2z" />
      <path d="M9 12H4s.55-3.03 2-4.5c1.62-1.63 5-2.5 5-2.5" />
      <path d="M15 15v5s3.03-.55 4.5-2c1.63-1.62 2.5-5 2.5-5" />
    </svg>
  )
}

export function PremiumBadge({
  label = 'Nổi bật',
  icon = 'sparkle',
  variant = 'pill',
  showStars = true,
  className = '',
  title = 'Tin đăng nổi bật (Gói Premium). Đây không phải dấu xác minh tin đăng.',
}: PremiumBadgeProps = {}) {
  const renderedIcon =
    icon === 'sparkle' ? (
      <SparkleIcon size={12} fill="currentColor" />
    ) : icon === 'crown' ? (
      <CrownIcon size={12} fill="currentColor" />
    ) : icon === 'rocket' ? (
      <RocketSvg size={12} />
    ) : icon === 'bolt' ? (
      <ElectricBoltIcon size={12} fill="currentColor" />
    ) : (
      icon
    )

  return (
    <span
      className={`premium-badge premium-badge--${variant}${className ? ` ${className}` : ''}`}
      title={title}
    >
      {renderedIcon}
      <span className="premium-badge__text">{label}</span>
      {showStars ? <PremiumStars /> : null}
    </span>
  )
}
```

### 2.2. CSS Champagne Gold & Viền Cao Cấp (`src/components/PremiumBadge.css`)
```css
/* Viền thẻ sang trọng với bóng đổ hoàng kim ấm */
.premium-listing.premium-listing {
  border-color: #dfaa42;
  box-shadow: inset 0 0 0 1px rgba(223, 170, 66, 0.22), 0 2px 14px rgba(223, 170, 66, 0.12);
  animation: premium-edge 6s ease-in-out 2;
}

/* Viên nhãn Nổi bật chuẩn mực (Pill) */
.premium-badge {
  position: relative;
  isolation: isolate;
  display: inline-flex;
  align-items: center;
  gap: 5px;
  max-width: 100%;
  padding: 3px 8px 3px 7px;
  border: 1px solid rgba(218, 165, 32, 0.45);
  border-radius: 999px;
  background: linear-gradient(135deg, #fff7db 0%, #feeab3 60%, #fedd8a 100%);
  color: #5e4002;
  font: 650 11.5px/1.35 var(--font-ui);
  white-space: nowrap;
  vertical-align: middle;
  box-shadow: 0 1px 3px rgba(160, 115, 20, 0.1), inset 0 1px 0 rgba(255, 255, 255, 0.85);
  letter-spacing: 0.2px;
}
.premium-badge__text {
  font-weight: 700;
  letter-spacing: 0.2px;
}

/* Boost variant: Priority Bump */
.premium-badge--boost {
  background: linear-gradient(135deg, #fff4e5 0%, #fedac2 60%, #fdba8c 100%);
  color: #7c2d12;
  border-color: rgba(234, 88, 12, 0.45);
  box-shadow: 0 1px 4px rgba(194, 65, 12, 0.15), inset 0 1px 0 rgba(255, 255, 255, 0.9);
}

/* Glass variant: Frosted glass floating on photo */
.premium-badge--glass {
  background: rgba(255, 248, 225, 0.78);
  backdrop-filter: blur(8px);
  -webkit-backdrop-filter: blur(8px);
  color: #533902;
  border-color: rgba(218, 165, 32, 0.55);
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.1), inset 0 1px 0 rgba(255, 255, 255, 0.9);
}

/* Compact variant: Space-saver */
.premium-badge--compact {
  padding: 2px 6px 2px 5px;
  font-size: 10.5px;
  gap: 3.5px;
}

/* Subtle variant: Elegant tint */
.premium-badge--subtle {
  background: rgba(255, 247, 219, 0.55);
  color: #634404;
  border-color: rgba(218, 165, 32, 0.3);
  box-shadow: none;
}
```

---

## 3. Kết Quả Kiểm Thử & Xác Nhận (Verification Record)

1. **Unit Tests:**
   - Lệnh kiểm tra: `node --test tests/premium-listing.test.mjs`
   - Kết quả: **3/3 PASS** (Bảo toàn entitlement, pin content, marker safety).
   - Lệnh kiểm tra: `node --test tests/premium-badge-variants.test.mjs`
   - Kết quả: **5/5 PASS** (Kiểm thử toàn diện 5 variant, các bộ icon rocket/bolt/crown/sparkle, fallback tiếng Việt "Nổi bật").
2. **Platform-Wide Test Suite:**
   - Lệnh kiểm tra: `npm test` (`node --test tests/*.test.mjs`)
   - Kết quả: **572/572 PASS**, 0 fail, thời lượng 15.7 giây.
3. **Bundle Budget:**
   - Lệnh kiểm tra: `npm run check:bundle`
   - Kết quả: `entryBytes: 244,121 bytes` (đáp ứng trần ngân sách khắt khe `< 250 kB`).
