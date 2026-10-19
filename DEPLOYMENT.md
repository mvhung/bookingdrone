# Hướng Dẫn Triển Khai (Deployment Guide) - EventMap & Crew Scheduler

> Tài liệu hướng dẫn chi tiết triển khai ứng dụng lên Cloud VPS giá rẻ, tự động cấp chứng chỉ SSL (HTTPS), dữ liệu lưu trữ SQLite bền vững.

---

## 1. Kiến Trúc Triển Khai (Production Architecture)

- **Mô hình**: Single-Host SPA + REST API đóng gói trong Docker Container.
  - **Frontend**: Vue 3 + TypeScript + Vite + TailwindCSS v4 được biên dịch thành file tĩnh và nạp trực tiếp vào thư mục `wwwroot` của .NET.
  - **Backend**: ASP.NET Core 8 Web API vừa phục vụ API (`/api/*`), vừa phục vụ giao diện tĩnh và định tuyến SPA (`app.MapFallbackToFile("index.html")`).
  - **Cơ sở dữ liệu**: SQLite (`Microsoft.EntityFrameworkCore.Sqlite`) lưu trữ toàn bộ dữ liệu vào 1 file duy nhất (`eventmap.db`), mount bền vững ra ngoài thư mục `./data` của VPS. App chỉ tiêu thụ ~80MB RAM.
  - **Reverse Proxy**: Caddy Server thế hệ mới, tự động nhận diện tên miền và cấp chứng chỉ HTTPS (Let's Encrypt) miễn phí trọn đời, tự động gia hạn.

---

## 2. Thông Số VPS Cần Mua

- **Nhà cung cấp gợi ý**:
  - Tại Việt Nam: Vietnix (gói VPS Giá Rẻ), TinoHost (gói Cloud VPS Lite), AZDIGI (gói KVM VPS) (~50.000đ – 70.000đ/tháng, thanh toán MoMo/VietQR).
  - Quốc tế: Hetzner Cloud (gói CX22 ~€3.79/tháng), OVH.
- **Cấu hình**:
  - Hệ điều hành: **Ubuntu 22.04 LTS** hoặc **Ubuntu 24.04 LTS** (64-bit).
  - CPU: 1 Core.
  - RAM: 1GB hoặc 2GB (1GB đã chạy rất mượt).
  - Ổ cứng: 20GB – 30GB SSD.

---

## 3. Các Bước Triển Khai Trên VPS (Khi Mua Xong)

### Bước 1: Đăng nhập vào VPS
Mở Terminal / PowerShell trên máy tính:
```bash
ssh root@<IP_VPS_CỦA_BẠN>
```
*(Nhập mật khẩu được gửi qua email của nhà cung cấp).*

### Bước 2: Cài đặt Docker
Chạy 1 dòng lệnh script chính thức của Docker:
```bash
curl -fsSL https://get.docker.com | sh
```

### Bước 3: Tải mã nguồn về VPS
```bash
git clone https://github.com/mvhung/bookingdrone.git
cd bookingdrone
```

### Bước 4: Khởi chạy ứng dụng
```bash
docker compose up -d
```
Docker sẽ tự động:
1. Build giao diện Vue 3 (`npm run build`).
2. Biên dịch Backend .NET 8 và nạp giao diện vào `wwwroot`.
3. Tải Caddy, liên kết mạng nội bộ, khởi tạo file cơ sở dữ liệu `eventmap.db` và seed sẵn 8 thành viên.
👉 Bây giờ bạn có thể mở trình duyệt truy cập: `http://<IP_VPS_CỦA_BẠN>` để kiểm tra.

---

## 4. Cấu Hình Tên Miền & Tự Động Cấp HTTPS (Ổ Khóa Xanh)

### Bước 1: Trỏ tên miền về VPS
Vào trang quản lý tên miền (Namecheap, Tenten, Nhanhoa, Cloudflare...), thêm 1 bản ghi:
- **Loại (Type)**: `A`
- **Host**: `@` (hoặc `booking`)
- **Giá trị (Value)**: `<IP_VPS_CỦA_BẠN>`

### Bước 2: Cập nhật file `Caddyfile`
Mở file `Caddyfile` trên VPS:
```bash
nano Caddyfile
```
Đóng comment phần `:80` và điền tên miền của bạn:
```caddyfile
booking.yourdomain.com {
    reverse_proxy web:8080
}
```
Nhấn `Ctrl + O` -> `Enter` để lưu, `Ctrl + X` để thoát.

### Bước 3: Khởi động lại Caddy
```bash
docker compose restart caddy
```
👉 Caddy sẽ tự động kết nối Let's Encrypt và kích hoạt HTTPS trong vài giây. Truy cập: `https://booking.yourdomain.com`.

---

## 5. Cẩm Nang Vận Hành & Bảo Trì

### Khi có cập nhật code mới (Deploy bản mới):
```bash
git pull
docker compose up -d --build
```

### Sao lưu (Backup) cơ sở dữ liệu:
Toàn bộ database nằm tại file:
```
./data/eventmap.db
```
Dùng WinSCP hoặc lệnh `scp` tải file này về máy tính cá nhân định kỳ để sao lưu.

### Xem Log ứng dụng:
```bash
# Xem log Web API & Frontend:
docker compose logs -f web

# Xem log Caddy & cấp SSL:
docker compose logs -f caddy
```

### Tắt / Bật hệ thống:
```bash
# Khởi động lại toàn bộ:
docker compose restart

# Tạm dừng:
docker compose stop

# Bật lại:
docker compose start

# Tắt và dọn container (Dữ liệu trong ./data không bị mất):
docker compose down
```
