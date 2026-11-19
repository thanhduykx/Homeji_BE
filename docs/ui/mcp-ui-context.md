# Homeji Web MCP UI Context & Design Token Bridge

> **Mã tài liệu:** `MCP-UI-CONTEXT-20261119`  
> **Phiên bản:** 2.0 (Synchronized with `src/index.css`, `src/lib/motionTokens.ts` & `src/lib/sellerOrderSound.ts`)  
> **Mục đích:** Cung cấp ngữ cảnh chuẩn mực định dạng Model Context Protocol (MCP) cho các Trợ lý AI (Claude Code, Cursor, Gemini CLI) khi thiết kế và hiện thực hóa giao diện người dùng cho Homeji (bao gồm Nền tảng Nhà trọ/Ở ghép Map-first và Chợ Ẩm Thực Sinh Viên - Food Marketplace).  
> **Ràng buộc công nghệ bất biến:**  
> - Framework: React 19 + TypeScript + Vite  
> - Styling: Plain CSS + CSS Variables (`src/index.css`)  
> - Motion: `motion/react` (Motion v14, tokens trong `src/lib/motionTokens.ts`)  
> - Audio: Web Audio API nội sinh (dựa trên mẫu an toàn `src/lib/sellerOrderSound.ts`)  
> - **TUYỆT ĐỐI KHÔNG** cài đặt Tailwind CSS, **KHÔNG** @shadcn/ui, **KHÔNG** Next.js  

---

## 1. Bản Đồ Tokens Toàn Cục (Global CSS Variable Tokens)

Các biến CSS dưới đây được đồng bộ chính xác 100% từ mã nguồn thực tế `src/index.css` và `DESIGN.md`. Mọi AI Agent sinh mã UI **bắt buộc** sử dụng `var(--...)` thay vì tự định nghĩa mã hex tùy tiện.

### 1.1. Bảng Màu Thương Hiệu & Ẩm Thực (Brand & Culinary Palette)

```css
:root {
  /* Font giao diện chính (được preload trong index.html) */
  --font-ui: 'Be Vietnam Pro', 'Segoe UI', system-ui, sans-serif;

  /* Brand Primary & Accent Tokens (src/index.css) */
  /* Lưu ý: --grab-green trên sản phẩm web dùng tone Teal #087f8c (nhận diện Homeji),
     hover #066875; legacy badge xanh lá #00b14f được giữ làm accent thứ cấp */
  --grab-green: #087f8c;
  --grab-green-hover: #066875;
  --grab-blue: #136fd8;
  --grab-blue-hover: #0e52a3;
  --hj-accent: #087f8c;
  --hj-brand-coral: #c24933;

  /* Maps Chrome & Navigation Tokens */
  --maps-blue: #1a73e8;
  --maps-blue-hover: #1557b0;
  --maps-blue-soft: #e8f0fe;
  --maps-blue-text: #1967d2;
  --maps-hover: rgba(60, 64, 67, 0.08);

  /* Bảng Màu Ẩm Thực Sinh Viên (Grainient Culinary Palette) */
  --food-warm-amber: #ff9f43;       /* Màu cam nướng giòn */
  --food-warm-terracotta: #ee5253;   /* Màu đỏ sốt đậm vị */
  --food-warm-honey: #feca57;        /* Màu vàng mật ong ấm */
  --food-warm-surface: #fff9f4;      /* Nền thẻ ấm áp dịu mắt */
  --food-warm-border: #fce7d8;       /* Viền thẻ ẩm thực tinh tế */

  /* Bậc Bề Mặt Trung Tính (Neutral Surface Ladder - src/index.css) */
  --surface: #ffffff;
  --surface-muted: #f3f7fa;
  --surface-input: #edf3f7;
  --surface-elevated: #ffffff;
  --surface-glass: rgba(255, 255, 255, 0.94);
  --border: #dce5eb;

  /* Màu Chữ & Phân Cấp Nội Dung (Typography Tokens - src/index.css) */
  --text-primary: #243742;
  --text-secondary: #5b6b76;
  --text-heading: #19323b;

  /* Phản Hồi Ngữ Nghĩa (Semantic Status) */
  --error: #ea4335;
  --error-soft: rgba(234, 67, 53, 0.1);
  --warning: #f39c12;
  --warning-soft: rgba(243, 156, 18, 0.12);
  --success: #39b54a;
  --success-soft: rgba(57, 181, 74, 0.12);

  /* Đổ Bóng Nhẹ Tinh Tế (Soft Elevation Shadows - src/index.css) */
  --shadow-sm: rgba(0, 0, 0, 0.02) 0 0 0 1px, rgba(0, 0, 0, 0.04) 0 2px 6px, rgba(28, 42, 34, 0.08) 0 4px 10px;
  --shadow-md: rgba(0, 0, 0, 0.02) 0 0 0 1px, rgba(0, 0, 0, 0.05) 0 4px 10px, rgba(28, 42, 34, 0.1) 0 8px 18px;
  --shadow-floating: 0 10px 30px rgba(28, 42, 34, 0.14);

  /* Bán Kính Bo Góc Chuẩn (Radius Ladder - src/index.css) */
  --radius-sm: 8px;
  --radius-md: 12px;
  --radius-lg: 14px;
  --radius-xl: 20px;
  --radius-pill: 999px;

  /* Đường Cong Chuyển Động CSS (CSS Easing - src/index.css) */
  --ease-out: cubic-bezier(0.22, 1, 0.36, 1);
  --ease-spring: cubic-bezier(0.34, 1.4, 0.64, 1);
  --dur-fast: 150ms;
}
```

---

## 2. Chuẩn Mực Chuyển Động Lò Xo (Spring Motion Configurations — Arc UI Pattern)

Được trích xuất và đồng bộ nguyên bản từ [`src/lib/motionTokens.ts`](file:///C:/EXE101_Homeji/src/lib/motionTokens.ts). Tuyệt đối không tùy tiện sinh các giá trị `stiffness` / `damping` lạ.

```typescript
import type { Transition, Variants } from 'motion/react';

// Cấu hình lò xo chuẩn hóa
export const SPRING = {
  /** Lò xo êm ái dùng cho thanh giỏ hàng nổi, modal, panel trượt */
  soft: { type: 'spring', stiffness: 380, damping: 32, mass: 0.8 } satisfies Transition,
  /** Lò xo nhạy dùng cho badge số lượng, nút bấm, toggle */
  snappy: { type: 'spring', stiffness: 520, damping: 34 } satisfies Transition,
} as const;

// Thời lượng chuyển động UI chuẩn
export const DURATION = {
  press: 0.12,
  fast: 0.16,
  base: 0.24,
  panel: 0.32,
  reveal: 0.38,
  exit: 0.18,
} as const;

// Đường cong nội suy
export const EASE = {
  out: [0.16, 1, 0.3, 1] as [number, number, number, number],
  inOut: [0.65, 0, 0.35, 1] as [number, number, number, number],
  in: [0.4, 0, 1, 1] as [number, number, number, number],
} as const;

// Hiệu ứng nghiêng Micro-Depth 3D (HorizonX Pattern - Không Three.js)
// CẢNH BÁO KỸ THUẬT: Chỉ áp dụng trên lá thẻ (leaf card), KHÔNG áp lên khối cha chứa sticky/fixed!
export const microTiltVariants: Variants = {
  initial: { rotateX: 0, rotateY: 0, scale: 1 },
  hover: { scale: 1.02, transition: { duration: 0.2, ease: EASE.out } },
};
```

---

## 3. Kiến Trúc Âm Thanh Xúc Giác Giao Diện (Bencho Web Audio Synthesis)

Kế thừa kiến trúc vững chắc từ [`src/lib/sellerOrderSound.ts`](file:///C:/EXE101_Homeji/src/lib/sellerOrderSound.ts):
- Phải có phương thức `arm()` kích hoạt từ cử chỉ người dùng (`click` / `touchstart`) để tuân thủ autoplay policy trên iOS Safari & Chrome.
- Luôn kiểm tra `revision` để ngăn chặn permission trễ kích hoạt lại context đã bị tắt hoặc đã giải phóng.
- **Bắt buộc ngắt kết nối node âm thanh (`oscillator.onended`)** để chống rò rỉ bộ nhớ (zero memory leak).
- Tích hợp sẵn "Chế độ yên lặng KTX" (Dorm Silent Mode).

```typescript
/**
 * Trình quản lý âm thanh xúc giác giao diện (Zero-Asset Web Audio API).
 * Không nạp bất kỳ file .mp3 / .wav nào từ bên ngoài.
 */
export class UiSoundEngine {
  private static context: AudioContext | null = null;
  private static enabled = false;
  private static revision = 0;
  private static readonly SILENT_KEY = 'homeji_dorm_silent_mode';

  /** Kích hoạt AudioContext từ cử chỉ người dùng đầu tiên */
  static async arm(): Promise<boolean> {
    if (typeof window === 'undefined') return false;
    const AudioCtx = window.AudioContext || (window as unknown as { webkitAudioContext: typeof AudioContext }).webkitAudioContext;
    if (!AudioCtx) return false;

    const currentRev = ++this.revision;
    try {
      this.context ??= new AudioCtx();
      if (this.context.state === 'suspended') {
        await this.context.resume();
      }
      if (currentRev !== this.revision) return false;
      this.enabled = this.context.state === 'running';
      return this.enabled;
    } catch {
      this.enabled = false;
      return false;
    }
  }

  static get isDormSilent(): boolean {
    if (typeof window === 'undefined') return false;
    return localStorage.getItem(this.SILENT_KEY) === 'true';
  }

  static setDormSilent(silent: boolean): void {
    if (typeof window === 'undefined') return;
    localStorage.setItem(this.SILENT_KEY, silent ? 'true' : 'false');
  }

  /** Chuông bếp 3 nốt ngân vang (Đơn món mới đến - E5, G5, B5) */
  static playOrderAlert(): boolean {
    if (this.isDormSilent || !this.enabled || !this.context || this.context.state !== 'running') {
      return false;
    }

    try {
      const ctx = this.context;
      const start = ctx.currentTime;
      const frequencies = [659, 784, 988]; // E5, G5, B5 arpeggio

      frequencies.forEach((freq, idx) => {
        const osc = ctx.createOscillator();
        const gain = ctx.createGain();

        osc.type = 'sine';
        osc.frequency.value = freq;

        const at = start + idx * 0.16;
        gain.gain.setValueAtTime(0, at);
        gain.gain.linearRampToValueAtTime(0.12, at + 0.02);
        gain.gain.exponentialRampToValueAtTime(0.001, at + 0.15);

        osc.connect(gain);
        gain.connect(ctx.destination);

        osc.onended = () => {
          osc.disconnect();
          gain.disconnect();
        };

        osc.start(at);
        osc.stop(at + 0.16);
      });

      // Rung haptic nếu hỗ trợ
      if (typeof navigator !== 'undefined' && 'vibrate' in navigator) {
        navigator.vibrate([100, 50, 100]);
      }
      return true;
    } catch {
      return false;
    }
  }

  /** Tiếng pop êm nhẹ khi bấm tăng giảm số lượng suất ăn */
  static playPop(): boolean {
    if (this.isDormSilent || !this.enabled || !this.context || this.context.state !== 'running') {
      return false;
    }

    try {
      const ctx = this.context;
      const start = ctx.currentTime;
      const osc = ctx.createOscillator();
      const gain = ctx.createGain();

      osc.type = 'sine';
      osc.frequency.setValueAtTime(320, start);
      osc.frequency.exponentialRampToValueAtTime(120, start + 0.06);

      gain.gain.setValueAtTime(0.08, start);
      gain.gain.exponentialRampToValueAtTime(0.001, start + 0.06);

      osc.connect(gain);
      gain.connect(ctx.destination);

      osc.onended = () => {
        osc.disconnect();
        gain.disconnect();
      };

      osc.start(start);
      osc.stop(start + 0.06);
      return true;
    } catch {
      return false;
    }
  }
}
```

---

## 4. Bộ Lớp Hạt Ẩm Thực SVG (Grainient Procedural Noise)

Không tải ảnh PNG/JPG nặng nề. Sử dụng SVG filter nội dòng nhúng một lần duy nhất vào đầu trang hoặc component cha:

```tsx
// Nhúng một lần trong App.tsx hoặc FoodMarketplaceView.tsx
export function ProceduralNoiseFilter() {
  return (
    <svg style={{ position: 'absolute', width: 0, height: 0, overflow: 'hidden' }} aria-hidden="true">
      <defs>
        <filter id="culinary-grain">
          <feTurbulence type="fractalNoise" baseFrequency="0.7" numOctaves="3" stitchTiles="stitch" />
          <feColorMatrix type="matrix" values="0 0 0 0 0   0 0 0 0 0   0 0 0 0 0  0 0 0 0.035 0" />
        </filter>
      </defs>
    </svg>
  );
}
```

Áp dụng trong file CSS thuần của thẻ món ăn:

```css
.food-card-surface {
  position: relative;
  background: radial-gradient(circle at 10% 10%, var(--food-warm-surface), var(--surface));
  border: 1px solid var(--food-warm-border);
  border-radius: var(--radius-md);
  box-shadow: var(--shadow-sm);
  overflow: hidden;
}

/* Lớp phủ hạt tinh tế, không chặn tương tác chuột/chạm */
.food-card-surface::after {
  content: '';
  position: absolute;
  inset: 0;
  filter: url(#culinary-grain);
  pointer-events: none;
  opacity: 0.7;
}
```

---

## 5. Quy Tắc Sinh Mã & Ràng Buộc Kỹ Thuật Cho AI Agent

1. **Tuyệt đối cấm Tailwind CSS & shadcn/ui:**
   - Mọi đề xuất class như `flex items-center gap-2 rounded-xl bg-white shadow-lg` phải được chuyển thành plain CSS class ngữ nghĩa (ví dụ: `.food-card`, `.food-cart-bar`) sử dụng tokens trong `src/index.css`.
2. **Bảo toàn Cấu trúc Stacking Context (Tránh Lỗi Trôi Phần Tử Nổi):**
   - **QUY TẮC SỐNG CÒN:** Không bao giờ đặt `transform: perspective(...)` hoặc `transform: rotateX/Y` lên bất kỳ container cha nào chứa phần tử `position: fixed` hoặc `position: sticky` (như `StickyFloatingCartBar` hoặc Map Bottom Sheet). Theo đặc tả W3C CSS Transforms, thuộc tính `transform` sẽ tạo ra một containing block mới, phá vỡ hoàn toàn hành vi bám dính của các phần tử con!
3. **Quy tắc Escrow Hold 24 giờ:**
   - Khi người mua xác nhận đã nhận món, giao diện chỉ cập nhật trạng thái đơn thành `Completed` (đóng biên nhận). Tuyệt đối không hiển thị thông báo "Tiền đã vào ví người bán", mà phải hiển thị rõ: *"Tiền được bảo vệ bởi Escrow Hold và sẽ giải ngân vào ví người bán sau 24 giờ kể từ lúc giao món."*
4. **Bảo tồn Bản đồ & Hiệu năng Thiết bị Di động:**
   - Không bọc `whileTap` scale trên các nút điều khiển bản đồ (tránh giật GPU WebGL Google Maps).
   - Không nhúng Three.js canvas hay shader nặng; chỉ dùng Motion tokens cho vi tương tác.
