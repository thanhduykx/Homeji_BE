# Kế hoạch Thiết Kế Lại Nghiệp Vụ & UI/UX Đăng Đồ Ăn và Bán Đồ Ăn (Homeji Food Marketplace)

> **Mã tài liệu:** `PLAN-FOOD-MARKETPLACE-2026`  
> **Phiên bản:** 3.0 (Master Blueprint & Domain Specification)  
> **Phạm vi hệ thống:**  
> - Backend: `Homeji` (.NET 9 Web API, Clean Architecture, Entity Framework Core, PostgreSQL)  
> - Frontend: `EXE101_Homeji` (React 19, TypeScript, Vite, plain CSS tokens, Motion `motion/react`)  
> **Căn cứ nghiệp vụ & tài chính:**  
> - Nghiên cứu tài chính Thủ Đức: [`docs/food-marketplace-financial-research.md`](file:///c:/Homeji/docs/food-marketplace-financial-research.md)  
> - Ràng buộc ngôn ngữ miền: [`c:/Homeji/CONTEXT.md`](file:///c:/Homeji/CONTEXT.md)  
> - Nguyên tắc thiết kế giao diện: [`C:/EXE101_Homeji/PRODUCT.md`](file:///C:/EXE101_Homeji/PRODUCT.md) & [`C:/EXE101_Homeji/DESIGN.md`](file:///C:/EXE101_Homeji/DESIGN.md)  
> **Cấu trúc tài liệu:** Áp dụng định dạng chuẩn `@ /goal`, `@ /plan`, `@ /boost`.

---

# @ /goal — Mục Tiêu Cốt Lõi & Bối Cảnh Nghiệp Vụ

## 1. Hiện trạng & Xung đột Nghiệp vụ (Problem Statement)

Homeji khởi đầu với Chợ đồ cũ (`MarketplaceListingType.SecondHand`) và sau đó bổ sung Chợ đồ ăn (`MarketplaceListingType.Food`). Tuy nhiên, đồ ăn và đồ cũ có bản chất vận hành hoàn toàn đối lập:

| Tiêu chí | Chợ Đồ Cũ (`SecondHand`) | Chợ Đồ Ăn (`Food & Beverage`) |
|---|---|---|
| **Vòng đời hàng tồn** | Hàng đơn chiếc (`AvailableQuantity = 1`), bán xong chuyển `Sold`. | Chu kỳ theo ngày/ca, số suất định mức (`DailyQuota`), tồn kho biến thiên liên tục. |
| **Thuộc tính sản phẩm** | Cần `Condition` (Mới 99%, Cũ, Tốt...) để đánh giá hao mòn. | **Không có `Condition`**. Cần độ tươi (`FreshnessBadge`), phong cách nấu, nguyên liệu, dị ứng (`AllergenAlerts`). |
| **Thời gian & Chế biến** | Không chế biến, hàng có sẵn. | Cần thời gian nấu (`PreparationMinutes` 10–45 phút), giờ mở bán, ca bán (Sáng/Trưa/Tối). |
| **Giao nhận (Fulfillment)** | Thường hẹn gặp tại phòng trọ hoặc tự đến lấy. | Nhạy cảm cao về thời gian & cự ly: Tự lấy tại bếp (`Pickup`) hoặc Bếp giao tận phòng KTX/trọ (`SellerDelivery`). |
| **Mô hình tài chính** | Người bán thanh lý đồ cũ, không tính giá vốn thường xuyên. | Cần tính giá vốn nguyên liệu + hộp + hoa hồng sàn 10% để kiểm soát **biên lợi nhuận ròng** (tối thiểu > 20%). |

### Những lỗ hổng nghiêm trọng trong mã nguồn hiện tại:

1. **Backend ép buộc trường đồ cũ vào thực phẩm:**
   - Trong [`src/Homeji.Domain/Entities/MarketplacePost.cs`](file:///c:/Homeji/src/Homeji.Domain/Entities/MarketplacePost.cs), hàm `SetDetails` bắt buộc `Condition` không được rỗng (`NormalizeRequired(condition, MaxConditionLength, nameof(Condition))`). Điều này khiến tầng Frontend phải "hack" bằng cách gán cưỡng ép chuỗi `"Mới làm trong ngày"` vào trường này.
   - Thiếu các thuộc tính chuyên biệt cho ẩm thực: `DailyQuota` (định mức ngày), `EstimatedCost` (giá vốn ước tính), `FreshnessBadge` (nhãn độ tươi), `AllergenAlerts` (cảnh báo dị ứng), `OperatingShifts` (ca bán), và `IsOpenNow` (công tắc nhận đơn tức thời).
2. **Frontend trộn lẫn form đăng tin:**
   - Trong [`C:/EXE101_Homeji/src/pages/MarketplacePage.tsx`](file:///C:/EXE101_Homeji/src/pages/MarketplacePage.tsx) (`tab === 'sell'`), người bán chọn "Đồ ăn" vẫn phải điền qua form đồ cũ; thiếu hoàn toàn công cụ tính lợi nhuận ròng trực quan, thiếu quản lý tồn kho theo ca và thiếu Live Food Card Preview.
3. **Quy trình đặt món & Checkout chưa hoàn thiện:**
   - Mặc dù backend [`MarketplaceOrderService.cs`](file:///c:/Homeji/src/Homeji.Application/Services/MarketplaceOrders/MarketplaceOrderService.cs) đã hỗ trợ `FulfillmentType.SellerDelivery` với thông tin `MarketplaceDelivery`, dialog checkout hiện tại (`food-checkout-confirmation`) chỉ hiển thị nhận tại bếp, không có giao diện chọn giao tận phòng KTX, không cho nhập Tòa nhà, Số phòng và SĐT người nhận.
4. **Vòng đời đơn hàng F&B thiếu trạng thái thực tế:**
   - Enum [`MarketplaceOrderStatus.cs`](file:///c:/Homeji/src/Homeji.Domain/Enums/MarketplaceOrderStatus.cs) hiện chỉ có `Requested -> Accepted -> Delivered -> Completed`. Trong ngành F&B, khách hàng và người nấu cần biết rõ: Đơn đã được bếp nhận và đang nấu (`Preparing`) hay món đã nấu xong sẵn sàng (`Ready / Delivering`).

---

## 2. Mục Tiêu Cốt Lõi (Core Goals)

1. **Thiết kế lại luồng Đăng đồ ăn (Seller Food Studio):** Tách biệt form đăng món khỏi đồ cũ; loại bỏ ép buộc trường `Condition`; tích hợp bộ tính giá vốn & biên lợi nhuận ròng (`Seller Margin Engine`); thêm nhãn độ tươi, cảnh báo dị ứng, số suất theo ca và Live Card Preview.
2. **Thiết kế lại luồng Quản lý Bếp & Đơn hàng (Kitchen Order Board):** Cung cấp công tắc 1-chạm bật/tắt nhận đơn (`IsOpenNow`), quản lý thực đơn hôm nay và bảng điều khiển đơn hàng theo thời gian thực (chuông báo ting-ting, đếm ngược 30 phút, cập nhật trạng thái nấu).
3. **Thiết kế lại luồng Mua & Đặt món (Buyer Experience):** Nâng cấp `FoodMarketplaceView` với bộ lọc bữa ăn linh hoạt, giỏ hàng dính cố định (`Sticky Floating Cart Bar`), checkout linh hoạt 2 chế độ (`Pickup` vs `SellerDelivery` tận phòng KTX), hỗ trợ voucher và nạp nhanh ví Homeji.
4. **Chuẩn hóa Cỗ máy trạng thái F&B (State Machine):** Nâng cấp cỗ máy trạng thái với 6 bước rõ ràng, đảm bảo tuyệt đối quy tắc **Escrow Hold 24 giờ** theo đúng [`CONTEXT.md`](file:///c:/Homeji/CONTEXT.md) để bảo vệ sức khỏe người mua và quyền lợi người bán.
5. **Nâng cấp Cảm quan & Trải nghiệm Tương tác UI/UX (Sensory & Micro-Interaction Elevation):** Tích hợp có chọn lọc tinh hoa từ 5 nguồn UI/UX hiện đại (`horizonx.so`, `grainient.spply` / `grainient.supply`, `uiarc.dev`, `uberdessigner.ai` / `uberdesigner.ai`, `bencho.dev`), xem nghiên cứu toàn diện tại [`docs/research/2026-11-19-uiux-sources-evaluation.md`](file:///c:/Homeji/docs/research/2026-11-19-uiux-sources-evaluation.md):
   - **Thẩm mỹ dải màu ấm & lớp hạt ẩm thực (Warm Culinary Grainient — `grainient.supply`):** Bổ sung bảng màu dải ấm hữu cơ (cam nướng `--food-warm-amber: #ff9f43`, đỏ sốt `--food-warm-terracotta: #ee5253`, mật ong `--food-warm-honey: #feca57`) kết hợp procedural SVG noise `<feTurbulence>` (0 byte asset tải về) tạo chiều sâu analog mộc mạc, tươi ngon cho món ăn sinh viên thay vì các mảng thẻ phẳng cứng nhắc.
   - **Tương tác âm thanh & phản hồi xúc giác an toàn (Tactile UI Audio & Haptics — `bencho.dev`):** Kế thừa kiến trúc âm thanh an toàn từ [`src/lib/sellerOrderSound.ts`](file:///C:/EXE101_Homeji/src/lib/sellerOrderSound.ts) với Web Audio API thuần (chuông bếp 3 nốt E5-G5-B5, tiếng pop chỉnh số lượng) có cơ chế `arm()` từ cử chỉ người dùng, tự dọn node bộ nhớ và rung haptic trên điện thoại, đồng thời có công tắc "Chế độ yên lặng KTX" (Dorm Silent Mode).
   - **Vật lý chuyển động lò xo mượt mà (Spring Physics Motion — `uiarc.dev`):** Ứng dụng mô hình chuyển động đàn hồi chuẩn hóa từ [`src/lib/motionTokens.ts`](file:///C:/EXE101_Homeji/src/lib/motionTokens.ts) (`SPRING.soft`: stiffness 380, damping 32, mass 0.8) cho thanh giỏ hàng dính đáy (Sticky Floating Cart) và bộ lọc bữa ăn chuyển động không gián đoạn (`layoutId`).
   - **Bố cục Studio chia đôi màn hình & Thẻ Bento (Split-Screen & Bento Hierarchy — `uberdesigner.ai`):** Nâng cấp Seller Food Studio thành giao diện 2 cột tương tác tức thì (vừa nhập liệu vừa thấy Live Card Preview) và bảng điều khiển Bếp theo lưới Bento trực quan.
   - **Hiệu ứng nghiêng chiều sâu siêu nhẹ (Micro-Depth Perspective Tilt — `horizonx.so`):** Kế thừa tư duy 3D của HorizonX (vốn đã áp dụng tại `landing-hx`) bằng CSS 3D Transform + `motion/react` nghiêng nhẹ $\pm 5^\circ - 6^\circ$ cho thẻ món ăn và huy hiệu bếp sạch mà không cần nạp Three.js nặng nề.

---

## 3. Các Ràng Buộc Bất Biến (Hard Constraints)

- **Quy tắc Escrow Hold 24 giờ:** Theo [`CONTEXT.md`](file:///c:/Homeji/CONTEXT.md), người mua bấm xác nhận đã nhận món chỉ giúp đóng sớm biên nhận và chuyển trạng thái đơn sang `Completed`, nhưng **tiền thực nhận của người bán (`SellerNetAmount`) CHỈ được giải ngân sau khi thời gian Escrow Hold (24 giờ kể từ `DeliveredAt`) kết thúc**. Tuyệt đối không giải ngân ngay tại trình duyệt.
- **Ranh giới tài chính nội bộ:** Số dư Homeji là sổ cái kép (double-entry ledger) nội bộ tuân thủ Nghị định 52/2024/NĐ-CP; nạp trước mua sau, không có số dư âm, không P2P, giá tối thiểu mỗi món 10.000đ, giá trị giỏ hàng tối thiểu 25.000đ, hoa hồng sàn 10% (Gói Bắt đầu), dự phòng rủi ro 0.5% GMV.
- **Kiến trúc công nghệ & Quy chuẩn mã nguồn:**
  - Backend: Clean Architecture (.NET 9), bảo đảm tương thích ngược với dữ liệu cũ, không làm vỡ 700 unit tests và 267 integration tests hiện hành.
  - Frontend: React 19 + TypeScript + Vite, plain CSS tokens trong `src/index.css` (`--grab-green: #087f8c`, `--maps-blue`, `--surface-*`), Motion (`motion/react`), tôn trọng quy tắc Map-first trong [`PRODUCT.md`](file:///C:/EXE101_Homeji/PRODUCT.md) (không áp hiệu ứng `whileTap` scale lên nút trên map shell).
- **Ma trận Ràng buộc Kỹ thuật khi Tiếp nhận 5 Nguồn UI/UX:**
  - **Không tải runtime 3D cồng kềnh (HorizonX Guardrail):** Tuyệt đối không nhúng Three.js canvas hay WebGL shader nặng vào giao diện chợ đồ ăn; chỉ cho phép CSS 3D Transform nhẹ (`perspective: 800px`, `rotateX/rotateY`) qua Motion tokens trên lá thẻ con (leaf component) để bảo toàn pin và CPU điện thoại sinh viên.
  - **Bảo toàn Stacking Context (Containing Block Guardrail):** Tuyệt đối không đặt `transform: perspective(...)` hoặc `transform: rotateX/Y` lên container cha chứa các thành phần bám dính như `StickyFloatingCartBar` hay Bottom Sheet, tránh phá vỡ vị trí dính theo chuẩn W3C.
  - **Không tải asset ảnh texture hay video loop lớn (Grainient Guardrail):** Không dùng file ảnh bitmap 4K/12K hay video mp4/webm làm nền; 100% texture hạt phải tạo bằng SVG `<feTurbulence>` nội dòng kết hợp CSS gradients thuần để bảo vệ dung lượng 4G/5G của người dùng.
  - **Không cài đặt Tailwind CSS hay @shadcn/ui (Arc UI Guardrail):** Mọi vi tương tác và chuyển động lò xo từ Arc UI phải được chuyển ngữ 100% sang `motion/react` và plain CSS variables theo [`DESIGN.md`](file:///C:/EXE101_Homeji/DESIGN.md) và [`src/lib/motionTokens.ts`](file:///C:/EXE101_Homeji/src/lib/motionTokens.ts).
  - **Bảo tồn bản sắc thương hiệu Homeji (Uberdesigner Guardrail):** Tuân thủ nghiêm ngặt bảng màu sản phẩm Homeji (`--grab-green: #087f8c`, `--maps-blue: #1a73e8`), không sử dụng theme SaaS tím neon hoặc dark-mode giả lập xa rời đời sống xóm trọ sinh viên.
  - **Không tải file audio tĩnh & Luôn có gating gesture + nút Mute (Bencho Guardrail):** 100% âm thanh giao diện phải được tổng hợp từ Web Audio API nội sinh (zero byte asset dung lượng tải) tuân thủ kiến trúc an toàn `SellerOrderSound` (`arm()` từ user gesture, ngắt kết nối node khi kết thúc); luôn cung cấp công tắc tắt âm (Dorm Silent Mode) để sinh viên không làm phiền bạn cùng phòng hay trong lớp học.

---

# @ /plan — Kiến Trúc & Kế Hoạch Thiết Kế Chi Tiết

## 1. Thiết Kế Logic Nghiệp Vụ (Business Logic Architecture)

### 1.1. Chiến Lược Mở Rộng Mô Hình Dữ Liệu (Domain Model Evolution)

Để vừa đáp ứng đầy đủ yêu cầu nghiệp vụ F&B vừa đảm bảo 100% tương thích với hệ thống `MarketplaceOrder`, `IMarketplacePostRepository`, hệ thống tìm kiếm bán kính địa lý và 967 tests hiện có, áp dụng giải pháp mở rộng thực thể sạch (Clean Aggregate Extension):

```
                        ┌─────────────────────────────────┐
                        │      UserProfile (Seller)       │
                        └────────────────┬────────────────┘
                                         │ 1
                                         ▼ 0..1
                        ┌─────────────────────────────────┐
                        │         KitchenProfile          │
                        ├─────────────────────────────────┤
                        │ • KitchenName: string (100)     │
                        │ • IsOpenNow: bool (Mở nhận đơn) │
                        │ • OperatingShifts: BitFlags     │
                        │ • MaxDeliveryDistanceKm: decimal│
                        │ • KitchenBio: string (500)      │
                        │ • HygieneCertificateUrl: string?│
                        └────────────────┬────────────────┘
                                         │ 1
                                         ▼ n
                        ┌─────────────────────────────────┐
                        │         MarketplacePost         │
                        ├─────────────────────────────────┤
                        │ • ListingType: SecondHand / Food│
                        │ • Title, Description, Price     │
                        │ • Unit: "suất", "phần", "hộp"...│
                        │ • PreparationMinutes: 5 - 120   │
                        │ • Address, Lat, Lng             │
                        │ ── [Thuộc tính F&B bổ sung] ──  │
                        │ • Condition: optional nếu Food  │
                        │ • EstimatedCost: decimal? (COGS)│
                        │ • DailyQuota: int (1 - 100)     │
                        │ • FreshnessBadge: string        │
                        │ • AllergenAlerts: string?       │
                        │ • IsSamplePhoto: bool           │
                        │ • MealShift: Enum (Sáng/Trưa...)│
                        └─────────────────────────────────┘
```

#### Quy tắc nghiệp vụ của các trường F&B mới:
1. **`Condition` đối với `Food`:** Khi `ListingType == MarketplaceListingType.Food`, trường `Condition` được đặt mặc định là chuỗi rỗng `""` hoặc tự động ánh xạ từ `FreshnessBadge`, không bắt buộc người bán phải chọn tình trạng đồ cũ.
2. **`DailyQuota` & `AvailableQuantity`:**
   - `DailyQuota`: Số lượng suất ăn tối đa người bán có thể nấu trong 1 ca/ngày (mặc định 10 - 50 suất).
   - `AvailableQuantity`: Số lượng suất còn lại có thể bán ngay trong ngày.
   - Mỗi khi có đơn đặt thành công (`Requested`), `AvailableQuantity` giảm đi `Quantity`. Nếu người bán từ chối hoặc đơn hết hạn, số lượng này được hoàn lại tức thì.
3. **`FreshnessBadge`:** Định danh chuẩn:
   - `CookToOrder`: "Nấu nóng theo đơn (Đặt mới nấu)"
   - `DailyBatch`: "Nấu theo mẻ trong ca (Còn nóng hổi)"
   - `ChilledReady`: "Đồ ăn vặt / Đồ uống ướp lạnh đóng chai"
4. **`AllergenAlerts`:** Chuỗi cờ cảnh báo: `Không cay`, `Cay nhiều`, `Có đậu phộng`, `Có hải sản`, `Món chay`.

---

### 1.2. Cỗ Máy Trạng Thái Đơn Hàng F&B (State Machine & Escrow Workflow)

Quy trình vòng đời đơn hàng ẩm thực gồm 6 bước rõ ràng:

```
[1. REQUESTED]  ── (Quá 30 phút / Người bán từ chối) ──> [REJECTED / EXPIRED]
       │                                                         │
       │ (Bếp bấm "Nhận đơn & Bắt đầu nấu")                      ▼
       ▼                                                [Hoàn tiền ví 100%]
[2. PREPARING]  ── (Bếp nấu xong, đóng gói món)
       │
       ▼
[3. READY]
       │
       ├─ [Nếu Tự lấy tại bếp (Pickup)]
       │        │
       │        ▼ (Khách đến bếp, đọc mã 4 số)
       │  [4. DELIVERED]
       │
       └─ [Nếu Bếp giao tận phòng (SellerDelivery)]
                │
                ▼ (Người bán bắt đầu di chuyển)
          [4. DELIVERING]
                │
                ▼ (Bàn giao tại sảnh KTX/cửa phòng)
          [5. DELIVERED]
                │
                ├───────────────────────────────────────────────────────┐
                ▼                                                       ▼
      [Khách bấm "Đã nhận món"]                           [Không phản hồi sau 24h]
                │                                                       │
                ▼                                                       ▼
      [Chuyển trạng thái sang COMPLETED]                 [Tự động chuyển COMPLETED]
                │                                                       │
                └───────────────────────┬───────────────────────────────┘
                                        │
                                        ▼
                      ┌───────────────────────────────────┐
                      │    ĐỒNG HỒ ESCROW HOLD (24 GIỜ)   │
                      │  (Tính từ mốc DeliveredAt của đơn)│
                      └─────────────────┬─────────────────┘
                                        │ (Hết 24h & Không có tranh chấp an toàn thực phẩm)
                                        ▼
                      ┌───────────────────────────────────┐
                      │    GIẢI NGÂN TIỀN VỀ VÍ NGƯỜI BÁN │
                      │ • Trừ 10% Platform Fee            │
                      │ • Ghi có SellerNetAmount          │
                      │ • Bút toán sổ cái kế toán kép     │
                      └───────────────────────────────────┘
```

#### Quy tắc chuyển trạng thái bất biến:
- **Requested (Chờ xác nhận):** Đơn có đồng hồ đếm ngược 30 phút. Quá thời gian này, background job tự động hủy đơn (`Expired`), mở khóa tồn kho và hoàn trả 100% tiền tạm giữ về ví người mua.
- **Preparing (Đang chuẩn bị món):** Khi người bán bấm nhận đơn, trạng thái chuyển sang `Preparing`. Lúc này người mua **không thể tự ý hủy đơn** nhằm bảo vệ người bán sinh viên khỏi rủi ro lãng phí nguyên liệu đã chế biến.
- **Delivered (Đã bàn giao):** Ghi nhận mốc thời gian `DeliveredAt = now`. Bắt đầu kích hoạt đồng hồ Escrow Hold 24 giờ.
- **Completed & Funds Release (Hoàn tất & Giải ngân):**
  - Người mua bấm "Đã nhận món": Chuyển trạng thái đơn sang `Completed` (đóng biên nhận).
  - **Quy tắc Escrow:** Tiền chỉ được giải ngân vào ví người bán khi thỏa mãn: `now >= DeliveredAt + 24 hours` thông qua tiến trình `MarketplaceOrderExpirationWorker`. Nếu xảy ra ngộ độc hoặc tranh chấp thực phẩm trong vòng 24h, quyền tạm giữ cho phép admin phong tỏa đơn và hoàn tiền người mua.

---

### 1.3. Động Cơ Tính Giá Vốn & Biên Lợi Nhuận Người Bán (Seller Margin Engine)

Công thức tài chính áp dụng theo [`food-marketplace-financial-research.md`](file:///c:/Homeji/docs/food-marketplace-financial-research.md):

$$\text{PlatformFee} = \text{Price} \times 10\%$$
$$\text{SellerNetRevenue} = \text{Price} - \text{PlatformFee} = \text{Price} \times 90\%$$
$$\text{EstimatedProfit} = \text{SellerNetRevenue} - (\text{EstimatedCostOfGoods} + \text{PackagingCost})$$
$$\text{MarginPercent} = \frac{\text{EstimatedProfit}}{\text{Price}} \times 100\%$$

#### Các mức cảnh báo thông minh:
1. **Giá sàn F&B:** Nếu $\text{Price} < 10.000\text{đ}$: Chặn gửi form và báo lỗi: *"Giá món ăn tối thiểu là 10.000đ để đảm bảo chi phí vận hành."*
2. **Cảnh báo biên lãi mỏng:** Nếu $\text{MarginPercent} < 20\%$: Hiển thị thẻ màu cam: *"Biên lãi ước tính dưới 20%. Bạn nên cân nhắc điều chỉnh lại giá hoặc khẩu phần để bù đắp công sức nấu nướng."*
3. **Cảnh báo giá kén sinh viên:** Nếu $\text{Price} > 45.000\text{đ}$ với món thông thường: Hiển thị gợi ý: *"Mức giá trên 45.000đ khá cao so với mặt bằng sinh viên Thủ Đức. Hãy đảm bảo phần ăn có khẩu phần lớn hoặc kèm nước uống."*

---

### 1.4. Xử Lý Các Trường Hợp Biên (Edge Cases & Concurrency Guardrails)

1. **Xung đột khi bếp tắt nhận đơn hoặc hết suất ngay lúc thanh toán (Stock & Open Status Race Condition):**
   - Nghiệp vụ: Khi khách bấm "Xác nhận đặt món", backend thực thi kiểm tra tính hợp lệ trong giao dịch cấp cơ sở dữ liệu (`SERIALIZABLE` hoặc optimistic concurrency control):
     - Kiểm tra `KitchenProfile.IsOpenNow == true`.
     - Kiểm tra `AvailableStock >= RequestedQuantity`.
   - Nếu vi phạm: Giao dịch rollback ngay lập tức, trả về `409 Conflict` với mã lỗi `KITCHEN_TEMPORARILY_CLOSED` hoặc `ITEM_OUT_OF_STOCK`. Frontend hiển thị toast thông báo rõ ràng và tự động cập nhật giỏ hàng mà không trừ tiền ví người mua.
2. **Quy tắc phân bổ Voucher và Hủy đơn giỏ hàng (Single-Checkout Voucher Policy):**
   - Theo [`CONTEXT.md`](file:///c:/Homeji/CONTEXT.md): Đơn hàng chợ là một phiên checkout duy nhất giữa 1 người mua và 1 người bán. Toàn bộ thay đổi trạng thái, hủy hay hoàn tiền đều áp dụng chung cho cả đơn.
   - Nếu đơn hàng bị người bán từ chối (`Rejected`) hoặc hết hạn (`Expired`): Voucher đồ ăn được mở khóa và hoàn trả lại vào ví voucher của người mua; toàn bộ số tiền thanh toán thực tế (`BuyerPaidAmount`) được hoàn trả ngay lập tức vào Số dư Homeji trong 1 bút toán duy nhất.

---

## 2. Thiết Kế Giao Diện & Trải Nghiệm Người Dùng (UI/UX Redesign)

Kế thừa các mô hình thiết kế xuất sắc từ `uberdessigner.ai` (Bố cục Split-Screen & Thẻ Bento), `grainient.spply` (Chất liệu dải ấm hữu cơ), `uiarc.dev` (Vật lý chuyển động đàn hồi) và `bencho.dev` (Phản hồi xúc giác đa giác quan):

### 2.1. Phân Hệ Đăng Đồ Ăn (Seller Food Studio — Mô hình Split-Screen từ `uberdesigner.ai`)

Xây dựng component chuyên biệt `MarketplaceSellFoodForm` trong `src/components/marketplace/food/`, áp dụng cấu trúc hai cột đáp ứng (Desktop/Tablet) và dạng trượt cuộn đồng bộ (Mobile):

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                       STUDIO ĐĂNG MÓN ĂN SINH VIÊN                         │
├──────────────────────────────────────────┬──────────────────────────────────┤
│ [Khối 1: Thông tin & Danh mục ẩm thực]   │ [Thẻ Xem Trước Thời Gian Thực]   │
│ • Tiêu đề món: [Cơm gà sốt teriyaki...]  │ ┌──────────────────────────────┐ │
│ • Danh mục: [Cơm] [Bún] [Nước]        │ │  [ẢNH MÓN ĂN - TỈ LỆ 4:3]   │ │
│ • Mô tả nguyên liệu, khẩu phần           │ │  Nấu theo đơn · 20 phút      │ │
├──────────────────────────────────────────┤ │  Cơm gà sốt teriyaki         │ │
│ [Khối 2: Định giá & Bộ tính lợi nhuận]   │ │  35.000 đ                    │ │
│ • Giá bán: [35.000 đ]                    │ │  Còn 15 suất hôm nay         │ │
│ • Chi phí vốn: [18.000 đ] (nguyên liệu) │ │  [+ Thêm vào giỏ]            │ │
│ ┌──────────────────────────────────────┐ │ └──────────────────────────────┘ │
│ │ BẢNG TÍNH LỢI NHUẬN RÒNG             │ │  • Nền Grainient dải màu ấm     │
│ │ Phí sàn (10%): -3.500 đ              │ │  • Lớp hạt SVG nhẹ hữu cơ      │
│ │ Thực nhận: 31.500 đ                  │ │  • Nghiêng 3D nhẹ (HorizonX)   │
│ │ Lãi ước tính: +13.500 đ (Biên 38.6%) │ │                                  │
│ │ [ Thước đo biên lãi chuyển màu ]     │ │ Món của bạn hiển thị đẹp mắt     │
│ └──────────────────────────────────────┘ │ và đúng chuẩn chợ ẩm thực.       │
│ • Số suất mở bán hôm nay: [- 15 +] suất │                                  │
│ • Thời gian nấu: [15 phút] [20 phút]    │                                  │
├──────────────────────────────────────────┤                                  │
│ [Khối 3: Cam kết tươi ngon & Dị ứng]    │                                  │
│ • Nhãn: (o) Nấu nóng theo đơn           │                                  │
│         ( ) Mới nấu trong ngày          │                                  │
│ • Cảnh báo: [x] Không cay  [ ] Đậu phộng │                                  │
├──────────────────────────────────────────┤                                  │
│ [Khối 4: Ảnh món ăn thật]                │                                  │
│ • Tải ảnh tự chụp (Ưu tiên)              │                                  │
│ • Hoặc chọn ảnh mẫu có đóng dấu "Mẫu"    │                                  │
└──────────────────────────────────────────┴──────────────────────────────────┘
```

- **Thước đo biên lãi đổi màu sống động (Chroma Margin Gauge):** Thanh đo lợi nhuận ròng đổi màu liên tục theo giá trị nhập liệu:
  - `< 15%`: Đỏ rực (`#ea4335`) — Báo động lỗ hoặc công nấu không đủ bù chi phí.
  - `15% - 25%`: Cam hổ phách (`#f39c12`) — Biên lãi mỏng, cảnh báo cân nhắc giá vốn.
  - `> 25%`: Xanh Grab an toàn (`#087f8c` / `#39b54a`) — Biên lãi lý tưởng, khuyên mở bán.
- **Thẻ xem trước Grainient & HorizonX Micro-Tilt:** Thẻ Live Card bên phải được phủ lớp nền gradient ấm kết hợp hạt mịn `<feTurbulence>`, hỗ trợ xoay nghiêng 3D tinh tế $\pm 5^\circ - 6^\circ$ khi rê chuột (`perspective: 800px`, `rotateX/rotateY` qua `motion/react`). *Lưu ý: Không bọc `transform` lên container cha chứa các thành phần fixed/sticky.*

---

### 2.2. Phân Hệ Quản Lý Bếp (Kitchen Order Board — Mô hình Bento Grid & Bencho Audio)

Tích hợp vào tab Quản lý đơn bán (`tab === 'sales'`):

1. **Bảng điều khiển trạng thái Bếp kiểu Bento (Bento Kitchen Header):**
   - **Thẻ 1 (Doanh thu & Số đơn ca này):** Hiển thị tổng tiền tích lũy và số đơn hoàn tất hôm nay.
   - **Thẻ 2 (Công tắc nhận đơn Bencho Tactile Switch):** Nút trượt lò xo đàn hồi `[Bếp Mở]` $\leftrightarrow$ `[Tạm Nghỉ]` kèm âm click êm dịu từ Web Audio API và công tắc phụ "Chế độ yên lặng KTX".
   - **Thẻ 3 (Hạn mức suất ăn còn lại):** Thanh tiến trình thể hiện số suất đã bán / tổng `DailyQuota`.
2. **Bảng Kanban Xử Lý Đơn 3 Cột (Live Order Kanban với Arc UI Gestures):**
   - **Cột 1: ĐƠN MỚI ĐẾN (Chuông báo 3 nốt Web Audio theo `SellerOrderSound`, đếm ngược 30 phút, rung haptic):**
     - Hiển thị danh sách món, số lượng, phương thức nhận (Tự lấy hay Giao phòng KTX), ghi chú khách.
     - Thẻ đơn hàng hỗ trợ cử chỉ vuốt ngang ngón tay (Arc UI Gesture): Vuốt sang phải để "Nhận đơn & Nấu" (vệt sáng xanh lá), vuốt sang trái để "Từ chối" (vệt sáng đỏ).
     - Nút bấm lớn dự phòng: `[NHẬN ĐƠN & NẤU]` / `[TỪ CHỐI ĐƠN]`.
   - **Cột 2: ĐANG NẤU (Preparing):**
     - Đếm ngược thời gian nấu theo `PreparationMinutes`.
     - Nút bấm: `[MÓN ĐÃ SẴN SÀNG]`.
   - **Cột 3: SẴN SÀNG / ĐANG GIAO (Ready / Delivering):**
     - Đơn tự lấy: Hiển thị mã 4 số chờ khách đến.
     - Đơn bếp tự giao: Nút `[BẮT ĐẦU ĐI GIAO]` và `[ĐÃ GIAO THÀNH CÔNG]`.

---

### 2.3. Phân Hệ Mua Đồ Ăn & Giỏ Hàng (Buyer Experience — Arc UI Motion & Bencho Haptics)

Nâng cấp các component hiện có trong `src/components/marketplace/food/`:

1. **Bộ lọc ẩm thực theo bữa ăn với Tab trượt liền mạch (`layoutId` Sliding Pill):**
   - Tabs trượt mượt mà: `[Tất cả]`, `[Trưa nay ăn gì?]`, `[Ăn vặt chiều]`, `[Món ăn khuya]`, `[Giải khát]` sử dụng `layoutId="activeMealCategoryTab"` cho vệt sáng chuyển động không ngắt quãng.
   - Bộ lọc khoảng cách: `[Gần tôi < 500m (Đi bộ)]`, `[Dưới 1.5 km]`.
2. **Thanh Giỏ Hàng Dính Đáy Đàn Hồi (Arc UI Elastic Sticky Floating Cart Bar):**
   - Hiển thị cố định ở chân trang trên thiết bị di động, xuất hiện bằng hiệu ứng trượt lò xo (`SPRING.soft`: `stiffness: 380, damping: 32, mass: 0.8`).
   - Huy hiệu số lượng món nhún nảy đàn hồi (`SPRING.snappy`: `scale: [1, 1.25, 1]`) kèm tiếng "pop" siêu nhẹ mỗi khi người mua bấm thêm món.
   - Thể hiện: Số lượng món, Tổng tiền tạm tính, Thanh tiến trình trực quan: *"Thêm X đ để đạt đơn tối thiểu 25.000đ"*.
3. **Drawer Thanh Toán Linh Hoạt (Dual Fulfillment Checkout Dialog):**
   - **Tab 1: Tự đến lấy tại bếp (`Pickup`):** Hiển thị bản đồ chỉ đường, địa chỉ bếp, thời gian dự kiến món sẵn sàng.
   - **Tab 2: Bếp giao tận phòng (`SellerDelivery`):** Form nhập chi tiết:
     - Tên người nhận & Số điện thoại.
     - Tòa nhà / KTX / Số phòng (ví dụ: *Phòng 402, Nhà B3, KTX Khu B*).
     - Phí ship nội bộ cố định (0đ hoặc 5.000đ - 10.000đ).
   - **Thanh toán Số dư Homeji & Nạp nhanh:** Nếu số dư không đủ, nút CTA tự động chuyển thành *"Nạp thêm X đ và Đặt món"* (kích hoạt QR nạp tiền tức thời; phát tiếng chime mừng thành công khi hoàn tất).

---

## 3. Thiết Kế Hợp Đồng API & DTOs (Technical API Contracts)

### 3.1. Tạo Bài Đăng Món Ăn (`POST /api/marketplace/food-posts`)

```json
{
  "title": "Cơm sườn bì chả nướng mật ong",
  "description": "Sườn ướp mật ong than hoa, kèm canh rau ngót và dưa chua tự làm.",
  "category": "Cơm nhà",
  "price": 35000,
  "estimatedCost": 18000,
  "unit": "hộp",
  "dailyQuota": 25,
  "preparationMinutes": 20,
  "freshnessBadge": "CookToOrder",
  "allergenAlerts": "Không cay",
  "mediaUrls": [
    "https://storage.homeji.vn/marketplace/com-suon.jpg"
  ],
  "isSamplePhoto": false,
  "linkedRentalPostId": null
}
```

### 3.2. Đặt Đơn Hàng Giỏ Món Ăn (`POST /api/marketplace-orders/cart-checkout`)

```json
{
  "sellerId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "items": [
    { "marketplacePostId": "a1b2c3d4-1111-2222-3333-444444444444", "quantity": 2 }
  ],
  "fulfillment": {
    "mode": 1,
    "delivery": {
      "recipientName": "Nguyễn Văn Sinh Viên",
      "recipientPhone": "0912345678",
      "address": "Phòng 314, Nhà B4, KTX Khu B ĐHQG TP.HCM",
      "latitude": 10.8805,
      "longitude": 106.7820
    }
  },
  "note": "Xin thêm ớt tươi và đũa muỗng dùng 1 lần",
  "voucherId": "vouch-food-welcome-5k"
}
```

---

## 4. Lộ Trình Triển Khai Theo Giai Đoạn (Execution Roadmap)

### Giai đoạn 1: Backend Foundation (Domain & Unit Tests)
- [ ] Cập nhật `MarketplacePost.cs`: Cho phép `Condition` là rỗng khi `ListingType == Food`, bổ sung các trường F&B (`EstimatedCost`, `DailyQuota`, `FreshnessBadge`, `AllergenAlerts`, `IsSamplePhoto`).
- [ ] Thêm validation trong `UpsertMarketplacePostDtoValidator`: Giá tối thiểu 10.000đ cho F&B, kiểm tra thời gian nấu 5–120 phút.
- [ ] Viết bộ test chuyên sâu: `FoodPostDomainTests.cs` (kiểm tra tạo món, tính biên lợi nhuận, trừ và hoàn tồn kho).

### Giai đoạn 2: Frontend Seller Studio (Đăng món & Quản lý Bếp)
- [ ] Xây dựng `MarketplaceSellFoodForm.tsx` kèm `ProfitMarginCalculator.tsx` và `LiveFoodCardPreview.tsx`.
- [ ] Cập nhật `tab === 'sales'` với giao diện `KitchenOrderBoard.tsx` (Kanban nhận đơn, đếm ngược 30 phút, chuông báo Web Audio API).

### Giai đoạn 3: Frontend Buyer & Checkout Enhancement
- [ ] Cập nhật `FoodMarketplaceView.tsx` với bộ lọc bữa ăn (`MealCategories`) và nhãn độ tươi trên thẻ món ăn `FoodCard.tsx`.
- [ ] Nâng cấp modal `food-checkout-confirmation` hỗ trợ đầy đủ 2 hình thức: `Pickup` (Tự lấy) và `SellerDelivery` (Giao tận phòng KTX có tên, SĐT, số phòng).

### Giai đoạn 4: Kiểm Thử Toàn Diện & Đảm Bảo Chất Lượng
- [ ] Chạy kiểm thử tự động toàn bộ test suite backend (`dotnet test`) và frontend (`npm test`).
- [ ] Kiểm tra khả năng truy cập (A11y): độ tương phản màu, hỗ trợ bàn phím (`Tab`, `Escape`), aria labels.

---

# @ /boost — Cải Tiến Nâng Cao & Tính Năng Đột Phá

Dành riêng cho hệ sinh thái sinh viên và ký túc xá Thủ Đức / ĐHQG TP.HCM:

## 1. Tính Năng "Gom Đơn KTX" (Dorm Pool Group Order)
- **Vấn đề thực tế:** Sinh viên ngại mất công đi bộ xuống sảnh lấy đồ ăn hoặc người bán ngại ship lẻ từng hộp lên các tầng cao.
- **Giải pháp Boost:**
  - Bếp tạo chuyến gom đơn: *"Bếp gom đơn giao miễn phí sảnh KTX Nhà B3 lúc 11h45 trưa nay (Chốt đơn lúc 11h15)"*.
  - Sinh viên cùng tòa nhà cùng tham gia đặt đơn vào khung giờ này.
  - Người bán gom giao 1 chuyến duy nhất tại sảnh cho 10–15 bạn sinh viên, tiết kiệm 100% thời gian vận chuyển.

## 2. Trợ Lý AI Bếp Sinh Viên (Gemini 2.5 Flash Assistant)
- **Gợi ý định giá thông minh:** Người bán nhập tên món *"Bún riêu cua đồng"* $\rightarrow$ AI tự động tham chiếu mức giá trung bình tại Làng Đại học Thủ Đức (30.000đ - 35.000đ) và đưa ra gợi ý biên lãi tối ưu.
- **Mô tả món ăn tự động:** Tạo đoạn mô tả món hấp dẫn, kích thích vị giác nhưng vẫn trung thực, tự động gắn cờ cảnh báo dị ứng.

## 3. Giờ Vàng Xả Món Tránh Lãng Phí (Zero Food Waste Happy Hour)
- **Vấn đề thực tế:** Thức ăn nấu trong ngày nếu còn dư sau 13h30 trưa hoặc sau 20h30 tối sẽ phải bỏ đi, gây lãng phí lớn.
- **Giải pháp Boost:**
  - Vào 30 phút cuối ca, người bán bấm nút kích hoạt "Giờ vàng giải cứu món ngon".
  - Hệ thống tự động giảm giá 20% - 30% cho các suất còn lại và đẩy lên đầu mục *"Món ngon giá hời cuối ca"*, giúp sinh viên khó khăn mua được bữa ăn chất lượng với giá rẻ và người bán thu hồi vốn nguyên liệu.

## 4. Huy Hiệu Tín Nhiệm Bếp Cùng Phòng (Roommate Kitchen Trust)
- Huy hiệu xác minh: `[Bếp Sinh Viên Tự Nấu]`, `[Đã Xác Minh CCCD]`, `[Được Chứng Nhận Bởi Cùng Tòa KTX]`.
- Đánh giá đính kèm ảnh thật của người mua sau khi ăn, giúp các bếp nấu ăn sạch sẽ, uy tín nhanh chóng xây dựng được lượng khách quen ổn định.

## 5. Động Cơ Âm Thanh Xúc Giác & Rung Đẩy Thời Gian Thực (Tactile Audio & Haptic Feedback Engine — Bencho & Arc UI Pattern)
- **Vấn đề thực tế:** Khi nấu ăn bận rộn trong phòng trọ KTX hoặc khi đang di chuyển, người bán rất dễ bỏ lỡ thông báo đơn hàng nếu chỉ dựa vào tin nhắn màn hình; ngược lại, chuông điện thoại thông thường quá ồn ào và dễ gây phiền toái cho bạn cùng phòng.
- **Giải pháp Boost (Kế thừa từ `bencho.dev` & chuẩn hóa theo `src/lib/sellerOrderSound.ts`):**
  - **Kiến trúc Web Audio API không tốn tài nguyên tải (Zero Asset Payload):**
    - Toàn bộ âm thanh được tổng hợp thời gian thực từ mã lệnh JavaScript (sử dụng `AudioContext`, `OscillatorNode` và `GainNode`), không tải bất kỳ file `.mp3` hay `.wav` tĩnh nào làm nặng ứng dụng.
    - *Gating cử chỉ an toàn:* Bắt buộc gọi `arm()` từ cú chạm/click đầu tiên của người dùng để tương thích tuyệt đối với Autoplay Policy trên iOS Safari và Chrome.
    - *Chống rò rỉ bộ nhớ:* Đăng ký sự kiện `oscillator.onended = () => { osc.disconnect(); gain.disconnect(); }` ngắt kết nối node ngay khi âm thanh kết thúc.
    - *Chuông bếp 3 nốt ngân vang (Đơn mới đến):* Hợp âm E minor arpeggio gồm E5 (659 Hz), G5 (784 Hz) và B5 (988 Hz) ngân vang thanh thoát 0.48 giây (khớp chuẩn `SellerOrderSound`).
    - *Xúc giác "pop" (Chỉnh số lượng suất):* Tần số lướt mềm mại từ 320 Hz xuống 120 Hz trong 60ms khi bấm tăng/giảm `DailyQuota` hoặc giỏ hàng.
    - *Âm thanh chúc mừng "chime" (Áp voucher & Đặt món thành công):* Chuỗi arpeggio F major (F5 - A5 - C6) ngân vang nhẹ nhàng.
  - **Rung phản hồi điện thoại (Mobile Haptic Feedback):**
    - Kích hoạt `navigator.vibrate([100, 50, 100])` trên điện thoại người bán khi đơn mới xuất hiện trên bảng Kanban.
    - Xung rung siêu ngắn `navigator.vibrate(40)` khi bấm chuyển trạng thái "Bắt đầu nấu" hoặc "Món đã sẵn sàng".
  - **Công tắc "Chế độ yên lặng KTX" (Dorm Silent Mode):**
    - Bật/tắt 1-chạm ở góc bảng điều khiển, tự động ghi nhớ vào `localStorage` (`homeji_dorm_silent_mode`) để sinh viên thoải mái bán hàng trong giờ học hoặc đêm khuya mà không làm ồn bạn cùng phòng.

## 6. Hệ Thống Thẩm Mỹ Ẩm Thực Sinh Viên Grainient & Micro-Depth (Grainient & HorizonX Pattern)
- **Vấn đề thực tế:** Giao diện thẻ phẳng (flat UI) thông thường tạo cảm giác công nghiệp, khô cứng, thiếu sự hấp dẫn vị giác của một chợ ẩm thực tự nấu.
- **Giải pháp Boost (Kế thừa từ `grainient.supply` & `horizonx.so`):**
  - **Dải màu ẩm thực ấm áp & Hạt hữu cơ (Warm Culinary Grainient):**
    - Phối hợp các dải màu kích thích cảm xúc ăn uống: Màu cam nướng giòn (`--food-warm-amber: #ff9f43`), đỏ sốt gia vị (`--food-warm-terracotta: #ee5253`), vàng tươi mật ong (`--food-warm-honey: #feca57`) kết hợp màu xanh thương hiệu Homeji (`--grab-green: #087f8c`).
    - Lớp hạt phủ mịn màng tạo từ bộ lọc SVG thuần (`<feTurbulence baseFrequency="0.7" numOctaves="3"/>`, độ đục 3.5%), mang lại cảm giác thủ công (handcrafted/analog), gần gũi như món ăn mẹ nấu hoặc bếp nhà làm.
  - **Hiệu ứng nghiêng 3D chiều sâu siêu nhẹ (Micro-Depth Perspective Tilt):**
    - Kế thừa triết lý chiều sâu từ HorizonX (vốn đã áp dụng tại `landing-hx`) nhưng **không nạp Three.js canvas nặng nề**.
    - Sử dụng `motion/react` kết hợp CSS 3D transforms (`perspective: 800px`, `rotateX`, `rotateY`) tính toán theo tọa độ chuột/chạm để thẻ món ăn đặc sắc và huy hiệu `[Bếp Sinh Viên Tự Nấu]` nghiêng nhẹ tối đa $\pm 5^\circ - 6^\circ$, tạo cảm giác trực quan nổi bật mà không tốn pin hay GPU của điện thoại sinh viên.
    - *Lưu ý kỹ thuật sống còn:* Chỉ áp dụng `transform` trên lá thẻ con (leaf component), tuyệt đối không áp lên container cha chứa `position: fixed` hay `position: sticky` để tránh phá vỡ containing block của thanh giỏ hàng.

## 7. Studio Đăng Món Chia Đôi Màn Hình & Trực Quan Hóa Biên Lãi Đổi Màu (Uberdesigner Split-Screen & Dynamic Chroma Margin)
- **Vấn đề thực tế:** Sinh viên nấu ăn thường bối rối khi định giá món ăn, không biết mình có bị lỗ vốn nguyên liệu và tiền hộp xốp hay không, và không hình dung được bài đăng của mình sẽ trông như thế nào khi lên chợ.
- **Giải pháp Boost (Kế thừa từ `uberdesigner.ai`):**
  - **Giao diện Studio 2 cột phản hồi tức thì (Live Split-Screen):**
    - Cột trái: Các trường nhập liệu thông minh (tên món, ca bán, nguyên liệu, giá vốn, giá bán).
    - Cột phải: Thẻ món ăn thời gian thực (Live Food Card Preview) nhảy chuyển động mượt mà theo từng ký tự gõ phím.
  - **Thước đo biên lãi đổi màu sắc thái (Dynamic Chroma Margin Visualizer):**
    - Hiển thị thanh tiến trình lợi nhuận ròng với màu sắc biến thiên linh hoạt:
      - *Vùng Đỏ nguy hiểm (`#ea4335`, < 15%):* Cảnh báo: *"Biên lãi quá thấp, bạn có nguy cơ lỗ công nấu và tiền hộp!"*
      - *Vùng Cam thận trọng (`#f39c12`, 15% - 25%):* Gợi ý: *"Biên lãi mỏng. Phù hợp bán số lượng lớn hoặc xả món."*
      - *Vùng Xanh Grab lý tưởng (`#087f8c` / `#39b54a`, > 25%):* Động viên: *"Biên lãi an toàn! Đảm bảo thu nhập tốt cho sinh viên."*

## 8. Thanh Giỏ Hàng Vật Lý Lò Xo & Thẻ Đơn Hàng Cử Chỉ Vuốt Nấu (Arc UI Spring Cart & Swipeable Kanban)
- **Vấn đề thực tế:** Thao tác trên thiết bị di động cần nhanh chóng, mượt mà và trực quan, giảm thiểu số lần bấm nút không cần thiết khi tay đang dính dầu mỡ hoặc đang đi bộ dưới sân KTX.
- **Giải pháp Boost (Kế thừa từ `uiarc.dev` & chuẩn hóa theo `src/lib/motionTokens.ts`):**
  - **Thanh giỏ hàng nổi đàn hồi (Spring Floating Cart Bar):**
    - Khi bấm thêm món, thanh giỏ hàng trượt nở từ đáy màn hình với lực lò xo tự nhiên (`SPRING.soft`: `stiffness: 380, damping: 32, mass: 0.8`).
    - Huy hiệu đếm số suất ăn nhún nảy đàn hồi (`SPRING.snappy`: `stiffness: 520, damping: 34`, scale: `[1, 1.25, 1]`) tạo cảm giác phấn khích khi mua sắm.
  - **Thẻ đơn hàng tương tác cử chỉ vuốt (Swipe-to-Action Kanban Tickets):**
    - Trên màn hình nhận đơn của bếp, người bán chỉ cần quẹt tay sang phải để nhận đơn nấu (hiển thị dải nền xanh Grab-green) hoặc quẹt tay sang trái để từ chối (hiển thị dải nền đỏ cam). Thao tác một tay cực kỳ tiện lợi khi người nấu đang đứng bếp.

## 9. Cầu Nối Ngữ Cảnh UI Sẵn Sàng Cho Trợ Lý AI Qua Giao Thức MCP (Agentic MCP UI Context & Design Token Bridge)
- **Vấn đề thực tế:** Các thư viện như HorizonX, Arc UI và Bencho đều hỗ trợ giao thức MCP (Model Context Protocol) để các AI Coding Agent (Cursor, Claude Code, Gemini CLI) có thể tự động truy xuất và dựng giao diện chuẩn xác. Homeji trước đây chưa có tài liệu ngữ cảnh UI chuẩn hóa, khiến các AI Agent dễ tự ý sinh mã Tailwind hoặc linh kiện không đúng quy cách.
- **Giải pháp Boost:**
  - Chuẩn hóa toàn bộ hệ thống tokens, quy tắc chuyển động Motion, biến số CSS và các thành phần giao diện mẫu thành một tài liệu ngữ cảnh chuyên biệt [`docs/ui/mcp-ui-context.md`](file:///c:/Homeji/docs/ui/mcp-ui-context.md).
  - Cho phép các trợ lý AI đọc trực tiếp file ngữ cảnh này để tự động thiết kế các màn hình chợ ẩm thực đạt chuẩn 100% về thương hiệu Homeji, bảo đảm không bao giờ sinh thừa thư viện hay vi phạm các ràng buộc hệ thống.
