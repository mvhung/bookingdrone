# EventMap & Crew Scheduler

> Hệ thống điều phối thợ sự kiện trên bản đồ tương tác thời gian thực dành cho team Flycam, FPV Drone, Butterfly Drone, Quay Phim & Nhiếp Ảnh.

![Tech Stack](https://img.shields.io/badge/.NET_8-Web_API-purple?style=flat-square)
![Vue 3](https://img.shields.io/badge/Vue_3-Composition_API-emerald?style=flat-square)
![TailwindCSS](https://img.shields.io/badge/TailwindCSS-v4-blue?style=flat-square)
![Leaflet](https://img.shields.io/badge/Leaflet-1.9.4-green?style=flat-square)
![SQL Server](https://img.shields.io/badge/SQL_Server-EF_Core_8-red?style=flat-square)

---

## 🌟 Tính Năng Nổi Bật

- **Bản đồ toàn màn hình 100% (Map-First)**: Sử dụng Leaflet.js + Esri World Street Map, ghim pill HTML tùy biến theo giờ, dịch vụ và danh sách thợ.
- **Tên địa điểm chính là Tên Job**: Tự động giải mã toạ độ sang tên sảnh tiệc, trung tâm sự kiện qua Overpass API (bán kính 85m) và Photon Komoot.
- **Side Drawer tinh gọn**: Duy nhất 1 cửa sổ trượt bên phải dùng chung cho cả Tạo mới và Xem/Sửa sự kiện.
- **Bảo mật & Phân quyền tuyệt đối**:
  - Thành viên chỉ nhìn thấy ghim và sự kiện mà mình là **Chủ Job** hoặc được **Phân công chạy**.
  - **Che giấu tài chính**: Thợ chỉ thấy cát-xê của chính mình; chỉ Chủ Job mới thấy tổng tiền khách và lợi nhuận chênh lệch.
  - **Quyền chỉnh sửa**: Chỉ người tạo hoặc Chủ Job mới có quyền sửa đổi thông tin, cát-xê hoặc xóa sự kiện; Thợ chạy chỉ có quyền xem (Read-Only).
- **Bộ máy tính lịch rảnh tự động (Blind Availability Engine)**: Tự động phát hiện giờ hành chính Thứ 2–6, ngày báo nghỉ riêng và lịch trùng show.
- **Xuất bảng kê Excel tháng (SheetJS)**: Xuất file `.xlsx` đầy đủ ngày giờ, địa điểm, cát-xê và lãi chênh lệch chỉ bằng 1 click.
- **Đăng nhập PIN 4 số nhẹ nhàng**: Chọn khuôn mặt đại diện -> Nhập PIN (Mặc định: `1234`) -> Nhận JWT Token 30 ngày.

---

## 🚀 Khởi Chạy Dự Án

### 1. Backend (.NET 8 Web API)
```bash
cd backend
dotnet restore
dotnet run --urls "http://localhost:5220"
```
- API Swagger: `http://localhost:5220/swagger`
- Database: Tự động khởi tạo và nạp dữ liệu mẫu 8 thành viên trên SQLite (`backend/data/eventmap.db`).

### 2. Frontend (Vue 3 + Vite)
```bash
cd frontend
npm install
npm run dev
```
- Truy cập giao diện: `http://localhost:5173/`

### 3. Triển Khai Nhanh Bằng Docker (Khuyên Dùng Cho VPS)
Chỉ cần chạy 1 lệnh duy nhất tại thư mục gốc của dự án:
```bash
docker compose up -d
```
Hệ thống sẽ tự động build cả Frontend & Backend, khởi động reverse proxy Caddy tự cấp SSL/HTTPS và mount cơ sở dữ liệu SQLite vào `./data`.

---

## 📖 Tài Liệu Chi Tiết
Xem file [AGENTS.md](AGENTS.md) để nắm toàn bộ cấu trúc cơ sở dữ liệu, các quy tắc nghiệp vụ và hướng dẫn mở rộng hệ thống.
