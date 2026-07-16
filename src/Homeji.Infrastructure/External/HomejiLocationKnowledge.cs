namespace Homeji.Infrastructure.External;

internal static class HomejiLocationKnowledge
{
    internal const string GroundingPrompt = """
        Dữ liệu địa danh đã được Homeji kiểm chứng (cập nhật 16/07/2026):
        - Từ 01/01/2021, Quận 2, Quận 9 và quận Thủ Đức được nhập thành Thành phố Thủ Đức. Từ 01/07/2025, cấp huyện kết thúc hoạt động; khi nói hiện tại, ưu tiên phường hiện hành + TP.HCM. Các tên Quận 2 cũ, Quận 9 cũ, quận Thủ Đức cũ và TP Thủ Đức cũ vẫn là bí danh định hướng/tìm kiếm.
        - Trường Đại học FPT - Campus TP.HCM (FPTU HCM) có địa chỉ hiện hành: Lô E2a-7, Đường D1, Khu Công nghệ cao, phường Tăng Nhơn Phú, TP.HCM. Đây là khu vực Quận 9 cũ. Bí danh: Đại học FPT TP.HCM, FPT University HCMC, FPT quận 9, FPT Khu Công nghệ cao. Địa chỉ lịch sử có thể ghi phường Long Thạnh Mỹ, Quận 9.
        - Nhà Văn hóa Sinh viên TP.HCM - cơ sở tại ĐHQG-HCM có địa chỉ hiện hành: 01 Lưu Hữu Phước, Khu phố Tân Lập, phường Đông Hòa, TP.HCM. Bí danh: NVHSV, Nhà Văn hóa Sinh viên ĐHQG, Nhà Văn hóa Sinh viên Làng Đại học, Student Cultural House VNUHCM. Địa chỉ lịch sử có thể ghi phường Đông Hòa, TP Dĩ An, tỉnh Bình Dương.
        - FPTU/Khu Công nghệ cao và Nhà Văn hóa Sinh viên/Khu đô thị ĐHQG-HCM là hai mốc độc lập, không phải hai cơ sở của cùng một trường và không được mặc định là gần nhau.
        - Nếu người dùng chỉ nói "Thủ Đức", hãy hỏi họ muốn khu FPT/Khu Công nghệ cao, khu ĐHQG/Nhà Văn hóa Sinh viên hay khu Quận 2 cũ. Không coi phường Thủ Đức hiện nay là toàn bộ địa bàn cũ.
        - Không nêu số km, số phút di chuyển hoặc khẳng định một phòng "gần" mốc nếu chưa có dữ liệu tọa độ/tuyến đường. Có thể dùng hai địa điểm trên làm mốc tìm kiếm phòng trọ sinh viên.
        """;

    internal const string SearchParserRules = """
        - Chuẩn hóa bí danh địa điểm để tìm tin đăng theo cả địa chỉ mới và cũ:
          * FPTU HCM, Đại học FPT TP.HCM, FPT University HCMC, FPT quận 9, FPT Khu Công nghệ cao => location "Khu Công nghệ cao" và keyword "FPTU".
          * Nhà Văn hóa Sinh viên, NVHSV, Nhà Văn hóa Sinh viên ĐHQG, Nhà Văn hóa Sinh viên Làng Đại học, Student Cultural House VNUHCM => location "Khu đô thị ĐHQG-HCM" và keyword "Nhà Văn hóa Sinh viên".
        - Quận 9, Quận 2, quận Thủ Đức và TP Thủ Đức là cách gọi cũ/bí danh tìm kiếm. Giữ cụm người dùng đã nhập trong location nếu chưa xác định được phường hiện hành; không tự đổi thành phường Thủ Đức.
        """;
}
