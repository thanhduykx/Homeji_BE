# Nghiên Cứu & Đánh Giá Toàn Diện 5 Nguồn Tham Khảo UI/UX (HorizonX, Grainient, Arc UI, Uberdesigner, Bencho)

> **Mã tài liệu:** `RESEARCH-UIUX-SOURCES-20261119`  
> **Phiên bản:** 2.0 (Platform-Wide & Food Marketplace Unified Evaluation)  
> **Ngày thực hiện:** 19/11/2026  
> **Đơn vị thực hiện:** Đội ngũ Kỹ thuật & Thiết kế Giao diện Homeji  
> **Căn cứ hệ thống & Ràng buộc chuẩn:**  
> - Ngôn ngữ miền & Định nghĩa nghiệp vụ: [`c:/Homeji/CONTEXT.md`](file:///c:/Homeji/CONTEXT.md)  
> - Nguyên tắc sản phẩm & Platform constraints: [`C:/EXE101_Homeji/PRODUCT.md`](file:///C:/EXE101_Homeji/PRODUCT.md)  
> - Quy chuẩn thiết kế giao diện: [`C:/EXE101_Homeji/DESIGN.md`](file:///C:/EXE101_Homeji/DESIGN.md) & CSS Tokens [`src/index.css`](file:///C:/EXE101_Homeji/src/index.css)  
> - Kế hoạch thiết kế chợ ẩm thực: [`c:/Homeji/docs/food-marketplace-redesign-plan.md`](file:///c:/Homeji/docs/food-marketplace-redesign-plan.md)  
> - Chuẩn chuyển động giao diện: [`src/lib/motionTokens.ts`](file:///C:/EXE101_Homeji/src/lib/motionTokens.ts)  
> - Chuẩn âm thanh giao diện an toàn: [`src/lib/sellerOrderSound.ts`](file:///C:/EXE101_Homeji/src/lib/sellerOrderSound.ts)  

---

## 1. Bối Cảnh & Đặt Vấn Đề

Homeji là hệ sinh thái kết nối nhà trọ, bạn cùng phòng (roommate matching), chợ đồ cũ (`MarketplaceListingType.SecondHand`) và chợ đồ ăn sinh viên (`MarketplaceListingType.Food`) tại khu vực Thủ Đức và Làng Đại học ĐHQG TP.HCM. 

Trong giai đoạn mở rộng sản phẩm năm 2026, giao diện người dùng đối mặt với 2 thách thức lớn:
1. **Thiếu cảm xúc trực quan và phản hồi đa giác quan:** Giao diện phẳng truyền thống tạo cảm giác khô khan, thiếu sự hấp dẫn vị giác của chợ ẩm thực, thiếu phản hồi xúc giác (tactile feedback) khi thao tác đặt phòng / chốt đơn trên điện thoại di động.
2. **Nguy cơ phình to tài nguyên và xung đột kiến trúc:** Việc chạy theo các trào lưu thiết kế hiện đại nếu không có sự chọn lọc khắt khe sẽ dễ dẫn đến việc nhúng thư viện 3D nặng (Three.js), nạp ảnh texture 4K/video nền tốn 4G của sinh viên, hoặc phá vỡ cấu trúc CSS variables chuẩn bằng Tailwind/shadcn.

Người dùng đã đề xuất khảo sát 5 nguồn tài nguyên UI/UX hàng đầu:
1. **`horizonx.so`** — HorizonX
2. **`grainient.spply` (`grainient.supply`)** — Grainient
3. **`uiarc.dev`** — Arc UI
4. **`uberdessigner.ai` (`uberdesigner.ai`)** — Uberdesigner
5. **`bencho.dev`** — Bencho

Tài liệu này tiến hành giải phẫu kỹ thuật chi tiết từng nguồn, đánh giá mức độ tương thích trên toàn bộ hệ sinh thái Homeji (Bản đồ tìm trọ, Trang giới thiệu Landing `landing-hx`, Chợ đồ cũ & ẩm thực, Bảng điều hành quản trị Admin), từ đó xây dựng ma trận tiếp nhận và tích hợp có chọn lọc vào `@ /goal` và `@ /boost`.

---

## 2. Giải Phẫu Kỹ Thuật Chi Tiết Từng Nguồn UI/UX

### 2.1. HorizonX (`horizonx.so`)
- **Bản chất sản phẩm:** Thư viện giao diện cao cấp kết hợp 3D web, hiệu ứng shader WebGL, scrollytelling điện ảnh, hệ thống component tương tác và server MCP (Model Context Protocol) cho các công cụ AI coding (Claude Code, Cursor, Codex).
- **Hiện trạng áp dụng tại Homeji:**
  - Homeji **đã từng kế thừa tư duy HorizonX** để xây dựng phân hệ Landing Scrollytelling [`src/features/landing-hx/`](file:///C:/EXE101_Homeji/src/features/landing-hx/) với 10 cảnh kể chuyện hành trình tìm trọ ("Sổ tìm trọ"), chuyển cảnh film roll, và hiệu ứng camera perspective.
- **Điểm mạnh thiết kế:** Phân lớp thị giác có chiều sâu vượt trội, cảm giác công nghệ cao, khả năng kể chuyện thương hiệu bằng chuyển động không gian.
- **Rào cản & Rủi ro kỹ thuật:**
  - Runtime Three.js và shader WebGL quá nặng: gây nóng máy, hao pin, tụt FPS nghiêm trọng trên các dòng điện thoại sinh viên tầm trung/giá rẻ.
  - Vi phạm giới hạn bundle size nghiêm ngặt của Homeji (`main.js < 250 kB`).
  - Xung đột WebGL: Nếu nhúng Three.js canvas vào cùng trang chứa Google Maps JS Vector/WebGL sẽ gây xung đột tài nguyên GPU (double-mount GPU crash).
- **Giải pháp chuyển hóa (Adaptation):**
  - **Kỹ thuật Micro-Depth Perspective Tilt (Không Three.js):** Thay vì nạp thư viện 3D, sử dụng CSS 3D Transforms (`perspective: 800px`, `rotateX/rotateY` giới hạn trong khoảng $\pm 5^\circ - 6^\circ$) kết hợp Motion (`motion/react`) cho thẻ món ăn đặc biệt, huy hiệu bếp sinh viên tự nấu, và thẻ phòng VIP.
  - **Bảo toàn Stacking Context:** Chỉ áp dụng `transform` trên lá thẻ (leaf component), tuyệt đối không áp lên container cha chứa các thành phần bám dính như `StickyFloatingCartBar` hay Bottom Sheet bản đồ.
  - **Cầu nối MCP UI Context:** Tiếp thu mô hình cung cấp ngữ cảnh tokens cho AI coding agent qua tài liệu chuẩn hóa [`docs/ui/mcp-ui-context.md`](file:///c:/Homeji/docs/ui/mcp-ui-context.md).

---

### 2.2. Grainient (`grainient.supply` / `grainient.spply`)
- **Bản chất sản phẩm:** Nền tảng chuyên biệt về gradient dạng hạt (grainy mesh gradients), chất liệu giấy/analog mộc mạc, và công cụ shader tạo dải màu thời gian thực.
- **Điểm mạnh thiết kế:** Mang lại cảm giác ấm áp, thủ công, gần gũi như sổ tay ghi chép sinh viên hoặc bữa cơm nhà nấu; phá vỡ sự lạnh lẽo của giao diện phẳng (flat UI).
- **Rào cản & Rủi ro kỹ thuật:**
  - Thư viện gốc cung cấp ảnh bitmap 4K/12K (PNG/WebP) hoặc video loop mp4: gây tốn dung lượng 4G/5G của sinh viên, làm chậm chỉ số LCP (Largest Contentful Paint) và CLS.
- **Giải pháp chuyển hóa (Adaptation):**
  - **Procedural SVG Grain (0 byte asset tải về):** Sử dụng bộ lọc SVG nội dòng (`<feTurbulence type="fractalNoise" baseFrequency="0.7" numOctaves="3"/>` kết hợp `<feColorMatrix>` với độ đục 3.5%) nhúng một lần duy nhất. Trình duyệt tự render hạt trực tiếp qua GPU mà không tải một byte ảnh nào từ server.
  - **Bảng Màu Ẩm Thực Ấm Áp (Warm Culinary Palette):** Bổ sung các token màu kích thích vị giác vào hệ thống tokens: Cam nướng giòn (`--food-warm-amber: #ff9f43`), Đỏ sốt cay ngọt (`--food-warm-terracotta: #ee5253`), Vàng mật ong (`--food-warm-honey: #feca57`), và bề mặt thẻ ấm (`--food-warm-surface: #fff9f4`).
  - **Áp dụng toàn diện:** Không chỉ áp cho thẻ món ăn, chất liệu hạt còn được dùng cho nền "Sổ tìm trọ" trên landing page và thẻ đánh giá phòng trọ chân thực.

---

### 2.3. Arc UI (`uiarc.dev`)
- **Bản chất sản phẩm:** Thư viện thành phần React/Next.js với triết lý "calm motion", ứng dụng sâu vật lý lò xo (spring physics), các thành phần bám dính/nổi (dock, floating bottom bars, sliding sheets) và MCP integration.
- **Điểm mạnh thiết kế:** Tương tác chuyển động đàn hồi tự nhiên (nhún nảy, co giãn quán tính); thao tác ngón tay một tay trên thiết bị di động cực kỳ mượt mà.
- **Rào cản & Rủi ro kỹ thuật:**
  - Arc UI phụ thuộc hoàn toàn vào hệ sinh thái Tailwind CSS và `@shadcn/ui`. Homeji cấm tuyệt đối Tailwind/shadcn theo quy chuẩn `PRODUCT.md` và `DESIGN.md`.
- **Giải pháp chuyển hóa (Adaptation):**
  - **Chuẩn hóa Spring Tokens qua `motion/react`:** Ánh xạ các thông số lò xo của Arc UI vào trực tiếp `src/lib/motionTokens.ts` (`SPRING.soft`: stiffness 380, damping 32, mass 0.8; `SPRING.snappy`: stiffness 520, damping 34).
  - **Thanh Giỏ Hàng Dính Đáy Đàn Hồi (`StickyFloatingCartBar`):** Xuất hiện từ mép dưới màn hình với lực lò xo tự nhiên; badge số lượng món nhún nảy khi thêm món.
  - **Tab Danh Mục Ẩm Thực Trượt Không Gián Đoạn (`layoutId` Sliding Pill):** Chuyển tab giữa các bữa ăn (Sáng, Trưa, Chiều, Khuya) với vệt sáng chạy trơn tru, không giật khung hình.
  - **Thẻ Đơn Hàng Kanban Vuốt Nhận Đơn (Swipe-to-Action):** Người nấu đang đứng bếp chỉ cần quẹt phải để nhận đơn hoặc quẹt trái để từ chối, giải phóng việc phải căn bấm các nút nhỏ.

---

### 2.4. Uberdesigner (`uberdesigner.ai` / `uberdessigner.ai`)
- **Bản chất sản phẩm:** Kho thiết kế mẫu giao diện web Vite + React chuẩn mực, giao diện chia đôi màn hình tương tác tức thì (interactive split-screen studios), và cấu trúc lưới Bento (Bento Grid) tối ưu tỷ lệ chuyển đổi.
- **Điểm mạnh thiết kế:** Phân cấp thông tin cực kỳ khoa học, vừa nhập liệu vừa thấy thành phẩm trực quan, hiển thị mật độ thông tin cao mà không gây ngợp mắt.
- **Rào cản & Rủi ro kỹ thuật:**
  - Một số mẫu mang phong cách SaaS dark-mode tím neon lạnh lùng, không phù hợp với văn hóa sinh viên Việt Nam.
- **Giải pháp chuyển hóa (Adaptation):**
  - **Seller Food Studio 2 Cột (Split-Screen):** Khung bên trái nhập thông tin món ăn và giá vốn nguyên liệu; khung bên phải là Live Food Card Preview phản hồi tức thì theo từng phím gõ.
  - **Thước Đo Biên Lãi Đổi Màu (Chroma Margin Gauge):** Thanh tiến trình hiển thị biên lợi nhuận ròng tự động đổi màu theo 3 ngưỡng an toàn tài chính (Đỏ < 15%, Cam 15-25%, Xanh Grab > 25%), giúp sinh viên không bị bán lỗ vốn.
  - **Bento Kitchen Dashboard:** Bố cục thẻ Bento cho trang quản trị bếp: tổng quan doanh thu, số đơn đang nấu, hạn mức suất ăn hôm nay.
  - **Rental Post Studio:** Ứng dụng mô hình split-screen cho form đăng tin cho thuê phòng (`/posts/new`), vừa nhập thông tin vừa thấy thẻ phòng trọ trên nền bản đồ bên phải.

---

### 2.5. Bencho (`bencho.dev`)
- **Bản chất sản phẩm:** Thư viện React atomic micro-interactions tập trung vào phản hồi đa giác quan (multisensory): công tắc xúc giác nam châm (magnetic/liquid toggles), rung haptic trên mobile, và đặc biệt là **hệ thống âm thanh giao diện tổng hợp (UI sound design)**.
- **Điểm mạnh thiết kế:** Tạo cảm xúc sống động, vui vẻ và nhịp nhàng cho người dùng; âm thanh báo hiệu giúp người bán không bỏ lỡ đơn hàng khi đang bận nấu ăn.
- **Rào cản & Rủi ro kỹ thuật:**
  - Nếu tải file âm thanh `.mp3`/`.wav` tĩnh sẽ làm tăng bundle size và độ trễ phát.
  - Trình duyệt di động (iOS Safari/Chrome) có chính sách Autoplay Policy khắt khe: AudioContext sẽ bị chặn phát nếu không được "arm" từ một cử chỉ người dùng trước đó.
  - Nếu không dọn dẹp node âm thanh (`disconnect`), bộ nhớ trình duyệt sẽ bị rò rỉ sau nhiều lần phát.
  - Âm thanh tự phát không kiểm soát sẽ gây phiền toái cho bạn cùng phòng trong ký túc xá hoặc trong lớp học.
- **Giải pháp chuyển hóa (Adaptation):**
  - **Kiến trúc Web Audio API An Toàn (Mở rộng từ `src/lib/sellerOrderSound.ts`):**
    - 100% âm thanh được tổng hợp thời gian thực bằng oscillator thuần (0 byte asset tải về).
    - Có cơ chế `arm()` từ user gesture (`click` / `touchstart`) đầu tiên.
    - Tự động ngắt kết nối (`oscillator.onended = () => { osc.disconnect(); gain.disconnect(); }`) chống rò rỉ bộ nhớ.
    - Chuỗi âm thanh đặc trưng: Chuông bếp 3 nốt ngân vang (E5, G5, B5), tiếng pop nhẹ (320Hz -> 120Hz) khi tăng giảm số lượng suất ăn.
  - **Rung Xúc Giác Điện Thoại (Mobile Haptics):** Tích hợp `navigator.vibrate([100, 50, 100])` khi có đơn mới đến hoặc khi có lời mời ở ghép thành công.
  - **Chế Độ Yên Lặng KTX (Dorm Silent Mode):** Công tắc tắt âm 1-chạm ở góc màn hình, lưu trạng thái vào `localStorage` (`homeji_dorm_silent_mode`) để sinh viên không làm ồn bạn cùng phòng.

---

## 3. Ma Trận Đánh Giá & Tiếp Nhận Toàn Diện (Platform-Wide Adoption Matrix)

| Nguồn tham khảo | Miền áp dụng chính | Mức độ phù hợp | Thành phần TIẾP NHẬN (Adopted/Adapted) | Thành phần TỪ CHỐI / CHẶN (Rejected Guardrails) |
|---|---|:---:|---|---|
| **`horizonx.so`** | • Landing Scrollytelling (`landing-hx`)<br>• Thẻ Món Ăn & Thẻ Phòng VIP<br>• MCP AI Context | **Cao (Adapted)** | • Hiệu ứng Micro-Depth perspective tilt ($\pm 5^\circ - 6^\circ$) bằng CSS 3D Transforms + Motion thuần.<br>• Định dạng ngữ cảnh MCP tokens cho AI coding. | • **CHẶN** Three.js runtime & WebGL shaders trong app shell.<br>• **CHẶN** giao diện sci-fi neon xa rời sinh viên. |
| **`grainient.spply`** | • Thẩm mỹ Chợ Ẩm Thực<br>• Chất liệu giấy "Sổ tìm trọ"<br>• Thẻ đánh giá người dùng | **Rất cao (Adopted)** | • Dải màu ấm ẩm thực (Amber, Terracotta, Honey).<br>• Lớp hạt mịn hữu cơ tạo bằng procedural SVG `<feTurbulence>` (0 byte). | • **CHẶN** file ảnh bitmap 4K/12K làm nền.<br>• **CHẶN** video nền lặp lại (mp4/webm) tốn 4G. |
| **`uiarc.dev`** | • Thanh giỏ hàng dính đáy<br>• Bottom Sheet chi tiết địa điểm<br>• Bộ lọc bữa ăn & Tab trượt | **Rất cao (Adapted)** | • Chuyển động lò xo mượt mà (`SPRING.soft`, `SPRING.snappy`).<br>• Thẻ Kanban cử chỉ vuốt nhận/từ chối đơn.<br>• Tab chuyển động liền mạch qua `layoutId`. | • **CHẶN** cài đặt Tailwind CSS.<br>• **CHẶN** phụ thuộc `@shadcn/ui` trực tiếp. |
| **`uberdessigner.ai`** | • Studio Đăng Món (`Seller Food Studio`)<br>• Studio Đăng Tin Cho Thuê (`/posts/new`)<br>• Dashboard Bếp & Admin | **Rất cao (Adopted)** | • Bố cục Studio 2 cột (nhập liệu + Live Card Preview).<br>• Thước đo biên lãi đổi màu sắc thái (Chroma Margin Gauge).<br>• Bố cục thẻ Bento Grid trực quan. | • **CHẶN** các template dark-mode SaaS màu tím neon. |
| **`bencho.dev`** | • Chuông thông báo đơn hàng bếp<br>• Tương tác tăng giảm giỏ hàng<br>• Thông báo ghép phòng (Match) | **Hoàn hảo (Adopted)** | • Âm thanh tổng hợp Web Audio API (zero asset bytes).<br>• Rung haptic điện thoại `navigator.vibrate`.<br>• Công tắc nhận đơn đàn hồi kèm "Chế độ yên lặng KTX". | • **CHẶN** tải file âm thanh tĩnh .mp3/.wav.<br>• **CHẶN** âm thanh tự phát không có nút Mute. |

---

## 4. Tích Hợp Chi Tiết Vào `@ /goal` & `@ /boost`

### 4.1. Tích Hợp Vào `@ /goal` (Mục Tiêu & Ràng Buộc Kỹ Thuật Cốt Lõi)
- **Bổ sung Mục Tiêu Cốt Lõi 5 (Sensory & Micro-Interaction Elevation):** Nâng cấp trải nghiệm người dùng từ tĩnh sang đa giác quan (thị giác ấm áp, xúc giác lò xo, thính giác tinh tế), giải quyết dứt điểm sự tẻ nhạt của giao diện cũ.
- **Thiết lập Ma Trận Ràng Buộc Kỹ Thuật (Hard Constraints Guardrails):**
  1. *HorizonX Guardrail:* Không Three.js, không WebGL shader trong app shell; chỉ dùng CSS 3D micro-tilt. Không áp `transform` lên container cha chứa `position: fixed` / `position: sticky`.
  2. *Grainient Guardrail:* Không file ảnh/video texture nặng; 100% hạt sinh bằng SVG `<feTurbulence>` nội dòng.
  3. *Arc UI Guardrail:* Tuyệt đối không cài Tailwind/shadcn; 100% vi tương tác viết bằng `motion/react` và CSS variables trong `src/index.css`.
  4. *Uberdesigner Guardrail:* Bảo tồn bảng màu nhận diện Homeji (Teal/Grab-green `#087f8c`, Maps-blue `#1a73e8`), không dùng SaaS neon.
  5. *Bencho Guardrail:* 100% âm thanh tạo từ Web Audio API có gating `arm()` an toàn và tự ngắt kết nối node; luôn có "Chế độ yên lặng KTX".

### 4.2. Tích Hợp Vào `@ /boost` (Tính Năng Đột Phá & Cải Tiến Nâng Cao)
- **Nâng cấp Boost 5:** Động Cơ Âm Thanh Xúc Giác & Rung Đẩy Thời Gian Thực (Tactile Audio & Haptic Feedback Engine) — Dựa trên cấu trúc an toàn của `SellerOrderSound`, bổ sung chuông bếp 3 nốt ngân vang và tiếng pop chỉnh số lượng.
- **Bổ sung Boost 6:** Hệ Thống Thẩm Mỹ Ẩm Thực Sinh Viên Grainient & Micro-Depth — Bộ màu dải ấm và hiệu ứng nghiêng thẻ 3D nhẹ nhàng.
- **Bổ sung Boost 7:** Studio Đăng Món Chia Đôi Màn Hình & Trực Quan Hóa Biên Lãi Đổi Màu — Giao diện split-screen nhập liệu thời gian thực và thước đo sắc thái tài chính giúp sinh viên tránh lỗ vốn.
- **Bổ sung Boost 8:** Thanh Giỏ Hàng Vật Lý Lò Xo & Thẻ Đơn Hàng Cử Chỉ Vuốt Nấu — Thanh giỏ hàng nổi đàn hồi và thẻ đơn thao tác 1 tay khi đứng bếp.
- **Bổ sung Boost 9:** Cầu Nối Ngữ Cảnh UI Sẵn Sàng Cho Trợ Lý AI Qua Giao Thức MCP — File đặc tả [`docs/ui/mcp-ui-context.md`](file:///c:/Homeji/docs/ui/mcp-ui-context.md) chuẩn hóa tokens cho AI coding agents.

---

## 5. Kết Luận

Cả 5 nguồn UI/UX đề xuất đều có những đóng góp giá trị cao cho dự án Homeji. Khi được chắt lọc qua các guardrails khắt khe, chúng không những không làm tổn hại đến hiệu năng máy sinh viên Thủ Đức mà còn nâng tầm toàn diện trải nghiệm sản phẩm từ bản đồ tìm trọ, trang landing kể chuyện cho tới chợ ẩm thực sinh viên.
