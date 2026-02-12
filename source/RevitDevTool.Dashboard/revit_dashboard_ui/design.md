Dưới đây là bản thiết kế chi tiết (Specification) dành cho AI Agent. Tài liệu này coi Dashboard là một  **lớp áo giao diện (UI Layer) cao cấp hơn cho dữ liệu gốc của Revit** , tập trung hoàn toàn vào việc khai thác và tương tác với dữ liệu có sẵn trong file `.rvt` mà không cần xử lý phức tạp bên ngoài.

---

# PRODUCT SPECIFICATION: REVIT MODEL INSIGHT DASHBOARD

**Concept:** "Revit UI 2.0" - Biến dữ liệu thô khan hiếm thành thông tin điều hành.

**Tech Stack:** React + Semi Design + Recharts/G2Plot (Vyssuals Style).

**Scope:** Pure Revit Data (Elements, Parameters, Warnings, Views).

---

## I. MODULE 1: PROJECT PULSE (TỔNG QUAN DỰ ÁN)

*Màn hình Home - Nơi Manager nhìn thấy ngay quy mô và trạng thái của file hiện hành.*

### 1. The "Big Numbers" Cards (Thẻ chỉ số lõi)

* **Tính năng:** Hiển thị 4-6 chỉ số quan trọng nhất của file.
  * Total Elements (Tổng số cấu kiện).
  * Total Warnings (Tổng cảnh báo).
  * Total Families loaded.
  * Total Views/Sheets.
* **Mục đích:** Nắm bắt nhanh quy mô dự án và độ "nặng" của file.
* **UI/UX (Semi Design):**
  * Sử dụng component `Statistic` hoặc `Card`.
  * Font số (Typography) cực lớn (48px+), đậm, màu trắng trên nền tối.
  * Có Sparkline (biểu đồ đường nhỏ) bên dưới thể hiện sự thay đổi so với lần mở file trước (nếu có lưu cache session).

### 2. Category Distribution Donut (Phân bổ danh mục)

* **Tính năng:** Biểu đồ vành khuyên (Donut Chart) thể hiện tỉ trọng các Category chính (Ví dụ: 40% Wall, 20% Floor, 10% Window...).
* **Mục đích:** Biết được mô hình đang tập trung vào hạng mục nào (Kết cấu, Kiến trúc hay Nội thất).
* **Tương tác:**
  * **Hover:** Hiển thị số lượng tuyệt đối và % chiếm dụng.
  * **Click:** Tự động Filter toàn bộ Dashboard để chỉ hiển thị dữ liệu của Category đó.

---

## II. MODULE 2: VISUAL INVENTORY (KIỂM KÊ TRỰC QUAN)

*Thay thế Project Browser dạng cây thư mục nhàm chán bằng hình ảnh dữ liệu.*

### 3. Spatial Treemap (Bản đồ nhiệt không gian)

* **Tính năng:** Biểu đồ Treemap (các hình chữ nhật lồng nhau).
  * Cấp 1 (Hình lớn): Level (Tầng).
  * Cấp 2 (Hình nhỏ bên trong): Category (Loại đối tượng).
  * Kích thước hình chữ nhật: Dựa trên `Volume` (Thể tích) hoặc `Count` (Số lượng).
* **Mục đích:** Nhìn thấy ngay tầng nào đang có nhiều bê tông nhất, hoặc tầng nào đang quá tải đối tượng.
* **Tương tác Bi-directional:** Click vào ô "Level 1 - Walls" trên biểu đồ -> Revit lập tức thực hiện lệnh `Isolate Category` cho Tường tại Tầng 1 trong 3D View.

### 4. Family & Type Explorer (Trình khám phá thư viện)

* **Tính năng:** Danh sách dạng lưới (Grid) hiển thị tên các Family đang dùng.
* **Mục đích:** Kiểm soát việc sử dụng Family (tránh việc dùng sai Family hoặc Family rác).
* **UI/UX:**
  * Hiển thị tên Family + Số lượng Instance đang đặt.
  * Sắp xếp theo: Nhiều nhất -> Ít nhất.

---

## III. MODULE 3: MODEL HEALTH & HYGIENE (SỨC KHỎE MÔ HÌNH)

*Nâng cấp hộp thoại "Warnings" của Revit thành công cụ quản lý chất lượng.*

### 5. Warning Severity Matrix (Ma trận cảnh báo)

* **Tính năng:** Phân loại cảnh báo Revit thành 3 nhóm màu:
  * 🔴 **Critical:** (Trùng lặp, Room not enclosed, Axis off slightly).
  * 🟡 **Moderate:** (Tag không gắn vào đối tượng, Join lỗi).
  * 🟢 **Info:** (Thông báo thông thường).
* **Mục đích:** Giúp Manager biết đâu là lỗi cần sửa gấp, đâu là lỗi có thể bỏ qua.
* **Tương tác:** Toggle nút "Show Isolated" -> Revit tự động tạo 1 3D View mới chỉ chứa các đối tượng bị lỗi đỏ.

### 6. "Heavy" Elements Tracker (Theo dõi đối tượng nặng)

* **Tính năng:** Liệt kê Top 10 Family có nhiều đa giác nhất (Polygon count - nếu trích xuất được) hoặc có kích thước file lớn nhất.
* **Mục đích:** Tối ưu hóa hiệu năng file Revit.
* **UI/UX:** Bảng xếp hạng (Leaderboard) đơn giản.

---

## IV. MODULE 4: SMART SCHEDULE (BẢNG THỐNG KÊ 2.0)

*Phiên bản nâng cấp của Revit Schedule với trải nghiệm Excel.*

### 7. Interactive Data Grid (Lưới dữ liệu tương tác)

* **Tính năng:** Bảng dữ liệu (Table) load toàn bộ tham số (Parameter) của Category đang chọn.
* **Mục đích:** Xem chi tiết thông tin "I" (Information) của BIM.
* **UI/UX (Semi Design Table):**
  * **Sticky Header:** Luôn hiện tên cột khi cuộn.
  * **Group by:** Kéo thả tên cột để nhóm (VD: Nhóm theo `Base Constraint`).
  * **Search:** Tìm kiếm text bất kỳ trong bảng (nhanh hơn Revit Find rất nhiều).

### 8. Quick Parameter Auditor (Kiểm tra nhanh tham số)

* **Tính năng:** Cột trạng thái kiểm tra dữ liệu trống (Null/Empty Check).
* **Mục đích:** Đảm bảo các trường thông tin bắt buộc (như `Mark`, `Type Mark`, `Comments`) đã được điền.
* **Trực quan hóa:** Các ô trống sẽ được tô nền đỏ nhạt (Light Red Background) để dễ nhận diện.

---

## V. UX & INTERACTION BEHAVIORS (HÀNH VI TƯƠNG TÁC)

*Quy định cách Dashboard giao tiếp với người dùng và Revit.*

### 9. Contextual "Ghost" Mode (Chế độ bóng ma)

* **Hành vi:** Khi người dùng chọn một đối tượng **trong Revit** (Canvas):
  * Dashboard tự động chuyển sang trạng thái "Details".
  * Hiển thị Card thông tin riêng của đối tượng đó (ID, Level, Offset, Top/Bottom Constraint).
* **Mục đích:** Không cần tìm kiếm, thông tin tự động "bay" vào Dashboard theo thao tác chuột của người dùng.

### 10. Bulk Selection Sync (Đồng bộ chọn hàng loạt)

* **Hành vi:**
  1. User dùng bộ lọc trên Dashboard: "Chọn tất cả Cửa đi có chiều rộng < 900mm".
  2. Dashboard hiển thị: "Đã tìm thấy 45 đối tượng".
  3. User nhấn nút:  **"Select in Revit"** .
  4. Revit: Highlight xanh 45 cái cửa đó để user có thể thực hiện lệnh xóa/thay type hàng loạt.

### 11. Snapshot & Export (Lưu vết)

* **Tính năng:** Nút "Export Data".
* **Hành vi:** Xuất dữ liệu đang hiển thị trên Table ra file Excel (.xlsx) hoặc CSV ngay lập tức mà không cần qua hộp thoại Export Schedule rườm rà của Revit.

---

## VI. BỐ CỤC LAYOUT (WIREFRAME GUIDE)

1. **Sidebar (Trái - 50px):**
   * Icon Menu: [Home] | [Inventory] | [Health] | [Schedule] | [Settings].
   * Collapsible (Có thể thu gọn).
2. **Top Bar (Trên - 40px):**
   * Breadcrumb: `Model Name > Current View`.
   * Global Filter: `Level Select`, `Phase Select`.
   * Action Buttons: `Refresh Data`, `Sync Selection`.
3. **Main Content (Giữa):**
   * Sử dụng Grid Layout (Lưới).
   * Các Widget có thể thay đổi kích thước (Resizable).
4. **Properties Panel (Phải - Tùy chọn):**
   * Slide-over panel (Trượt từ phải sang) khi click vào chi tiết 1 đối tượng.

---

Đây là bản thiết kế chức năng thuần túy (Functional Spec), tập trung hoàn toàn vào việc hiển thị và tương tác lại với dữ liệu Revit gốc. Bạn có thể đưa nội dung này cho AI Agent để bắt đầu code.
