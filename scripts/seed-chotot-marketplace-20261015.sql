-- Seed a local Homeji catalog from public, in-stock Chợ Tốt listings observed on 2026-10-15.
-- The image URLs point directly to the original Chợ Tốt CDN; Homeji listing cards/details stay on Homeji.
-- Source listing IDs are kept here for audit; seller accounts are existing Homeji demo renter accounts.
BEGIN;

-- Hide synthetic fixtures without active reservations. Reserved fixture listings remain untouched for order history;
-- search already excludes them when stock is zero.
UPDATE homeji.marketplace_posts
SET status = 3, updated_at = now()
WHERE is_synthetic = true AND status = 1 AND reserved_quantity = 0;

-- Prevent new purchases of synthetic items while keeping accepted orders and reserved stock intact.
UPDATE homeji.marketplace_posts
SET available_quantity = 0, updated_at = now()
WHERE is_synthetic = true AND status = 1 AND reserved_quantity > 0;

WITH items(id,seller_id,title,description,price,condition,category,address,latitude,longitude,image_url,source_id) AS (
    VALUES
    ('d4010000-0000-4000-8000-000000000001'::uuid,'d3000000-0000-0000-0000-000000000022'::uuid,'Ghế xoay văn phòng lưng đen, đệm đỏ','Ghế xoay văn phòng đã qua sử dụng, lưng đen, đệm đỏ, chân sắt. Có nhiều mẫu thanh lý với giá từ 150.000đ; chọn mẫu và kiểm tra trực tiếp trước khi nhận.',150000,'Đã sử dụng','Nội thất','Phường Tăng Nhơn Phú A, TP. Thủ Đức',10.845200,106.781000,'https://cdn.chotot.com/NsR6t3eGIh696wY0KINxdV4Au0eyckx_ouca4Qpe4QI/preset:view/plain/55e50b11ed7f5dd4de1e8f42e36e1a19-3006408155802633768.jpg','135168857'),
    ('d4010000-0000-4000-8000-000000000002'::uuid,'d3000000-0000-0000-0000-000000000023'::uuid,'Bàn làm việc gỗ 2,4 m','Bàn gỗ dài 2,4 m, rộng khoảng 70 cm. Người bán thanh lý do không còn nhu cầu sử dụng.',2500000,'Đã sử dụng','Nội thất','Phường Long Bình, TP. Thủ Đức',10.850000,106.833000,'https://cdn.chotot.com/XGN9z2sqdKAo9nOzH_pJjsgjRx7odyRhivTTK81-lEE/preset:view/plain/43fd938e483966c617404ed19c2ebf34-3006542736214456059.jpg','135265631'),
    ('d4010000-0000-4000-8000-000000000003'::uuid,'d3000000-0000-0000-0000-000000000024'::uuid,'Quạt đứng Senko LTS1636','Quạt đứng Senko LTS1636 màu đen, 3 tốc độ gió. Tin đăng mô tả quạt chạy êm, gió mạnh; ghi nhận bảo hành động cơ và đảo gió 1 năm.',180000,'Đã sử dụng','Điện gia dụng','Phường Tam Bình, TP. Thủ Đức',10.869000,106.628000,'https://cdn.chotot.com/SCOAgS0eUz1t7UUSi7aiP5vcRLcvBP6Owq8ZgtxHXDQ/preset:view/plain/d458b2818cd455043133ac14b39dbf41-3006552698495121811.jpg','135267777'),
    ('d4010000-0000-4000-8000-000000000004'::uuid,'d3000000-0000-0000-0000-000000000025'::uuid,'Quạt điều hòa Kangaroo KG50F79N','Quạt làm mát Kangaroo KG50F79N, cao khoảng 1,1 m, bình chứa lớn. Theo tin đăng, đã thay tấm làm mát và các chức năng đang hoạt động; người mua tự vận chuyển.',850000,'Đã sử dụng','Điện gia dụng','Phường Thủ Đức, TP. Thủ Đức',10.849000,106.761000,'https://cdn.chotot.com/r-baLBCLKQeMzuqodO1x7OKcIDT8U0iJTurD6MV7cgw/preset:view/plain/e4f20260a2ab4d7ee2d215153f4291f7-3006307719942278316.jpg','135237261'),
    ('d4010000-0000-4000-8000-000000000005'::uuid,'d3000000-0000-0000-0000-000000000021'::uuid,'Quạt hơi nước Daikio','Quạt hơi nước Daikio đã qua sử dụng. Tin đăng nêu quạt hoạt động, riêng bơm nước cần thay; vui lòng kiểm tra tình trạng trước khi mua.',350000,'Đã sử dụng','Điện gia dụng','Phường Tam Bình, TP. Thủ Đức',10.869000,106.628000,'https://cdn.chotot.com/ucaRCfO_UzmgrK8OJ2UrloWyZJBgSP4Vg9_XnAElmnc/preset:view/plain/01e0582506fa6155bbaa38e3be472c0c-3006547464266157439.jpg','135266745'),
    ('d4010000-0000-4000-8000-000000000006'::uuid,'d3000000-0000-0000-0000-000000000022'::uuid,'Quạt điều hòa Sunhouse SHD7714 30 lít','Quạt Sunhouse SHD7714 bình nước 30 lít. Theo mô tả, đã chuyển sang nút cơ và thiếu 2 cánh đảo gió; người bán ghi máy vẫn làm mát bình thường.',350000,'Đã sử dụng','Điện gia dụng','Phường Tam Bình, TP. Thủ Đức',10.869000,106.628000,'https://cdn.chotot.com/RQFHLvw3XQHd7Jswd5E9beL3Ozt5tgci7tGoOjgL44w/preset:view/plain/50d3122ab11332323aa1b2b61d170946-3006541766203851623.jpg','135265415'),
    ('d4010000-0000-4000-8000-000000000007'::uuid,'d3000000-0000-0000-0000-000000000023'::uuid,'Quạt sạc Perfect màu trắng vàng','Quạt sạc Perfect đã qua sử dụng, người bán mô tả tình trạng gần như mới (98%).',200000,'Đã sử dụng','Điện gia dụng','Phường Hiệp Bình Chánh, TP. Thủ Đức',10.834000,106.713000,'https://cdn.chotot.com/cllYehDbOCG5PTBRn-nJscRdRVHbrB_bRVu_xhcvp24/preset:view/plain/9519dcbf07f9ed32cee47cd40a67b4d3-3006412799329437224.jpg','135262872'),
    ('d4010000-0000-4000-8000-000000000008'::uuid,'d3000000-0000-0000-0000-000000000024'::uuid,'Tủ lạnh Samsung Inverter 345 lít','Tủ lạnh Samsung Inverter dung tích 345 lít, có lấy nước ngoài. Tin đăng nêu ngoại hình khoảng 90%, giá gồm vận chuyển và hỗ trợ đổi trả nếu lỗi trong 10 ngày.',4800000,'Đã sử dụng','Điện gia dụng','Phường Linh Trung, TP. Thủ Đức',10.868000,106.782000,'https://cdn.chotot.com/pwW81eOqyh1wQHnKRd0YcQ3l0OBnuGACrtTPn1X2jIQ/preset:view/plain/5c49561f25fcab01fd8228389cc91d7a-3005563128825965208.jpg','134910001'),
    ('d4010000-0000-4000-8000-000000000009'::uuid,'d3000000-0000-0000-0000-000000000025'::uuid,'Tủ lạnh Toshiba Inverter 253 lít','Tủ lạnh Toshiba Inverter 253 lít. Tin đăng mô tả máy và vỏ còn nguyên bản; người bán bao vận chuyển và cho kiểm tra khi nhận.',3500000,'Đã sử dụng','Điện gia dụng','Phường Linh Trung, TP. Thủ Đức',10.868000,106.782000,'https://cdn.chotot.com/WzV4pya5ovCEqJs7IDvG2VWb7APUd_ciOnVZ96EjDzY/preset:view/plain/574f0698d8052d0a540d6793dfeaf87b-3006212752325134208.jpg','134961181'),
    ('d4010000-0000-4000-8000-000000000010'::uuid,'d3000000-0000-0000-0000-000000000021'::uuid,'Tủ lạnh LG Inverter 225 lít','Tủ lạnh LG Inverter 225 lít, có chức năng tiết kiệm điện. Tin đăng mô tả khay kệ đầy đủ và máy đang được sử dụng; có thể kiểm tra khi nhận.',3000000,'Đã sử dụng','Điện gia dụng','Phường Linh Trung, TP. Thủ Đức',10.868000,106.782000,'https://cdn.chotot.com/M_GijxjOIMNncTrn3c1J3H-cUENIUncuHX_t5KTQLIQ/preset:view/plain/498f316ee0f6d9bd856cc40f084a1bac-3005585704854476646.jpg','135132451'),
    ('d4010000-0000-4000-8000-000000000011'::uuid,'d3000000-0000-0000-0000-000000000022'::uuid,'Tủ lạnh Sharp 180 lít không đóng tuyết','Tủ lạnh Sharp dung tích 180 lít, làm lạnh nhanh, phù hợp gia đình nhỏ. Tin đăng ghi có bảo hành 1 tháng và hỗ trợ vận chuyển, lắp đặt.',1200000,'Đã sử dụng','Điện gia dụng','Phường Linh Xuân, TP. Thủ Đức',10.880000,106.760000,'https://cdn.chotot.com/R3BXknfQBwhk-S2zzMU0uZY2CL2r7PTrudjYGpGxOEY/preset:view/plain/0c415da0ba533a3fac915c037ec2b86c-3006556645868250792.jpg','135268654'),
    ('d4010000-0000-4000-8000-000000000012'::uuid,'d3000000-0000-0000-0000-000000000023'::uuid,'Tủ bếp gỗ công nghiệp màu trắng xám','Bộ tủ bếp gỗ công nghiệp màu trắng xám, được đăng thanh lý cùng một số tủ quần áo. Giá và mặt hàng trong tin đăng cần xác nhận trực tiếp với người bán.',123000,'Đã sử dụng','Nội thất','Phường Long Trường, TP. Thủ Đức',10.795000,106.830000,'https://cdn.chotot.com/wzQZB-XEjFid9-4qTt1ZNuqrhJXaUA7pdMwtb5MX6Hs/preset:view/plain/f9eac38a106f7c28a0fe7ae2e45eaa8a-3006557543769299468.jpg','135268872'),
    ('d4010000-0000-4000-8000-000000000013'::uuid,'d3000000-0000-0000-0000-000000000024'::uuid,'Kệ lắp ráp nhựa màu hồng','Kệ lắp ráp nhựa màu hồng, tin đăng mô tả hàng mới chưa qua sử dụng.',45000,'Mới','Nội thất','Phường Hiệp Bình Chánh, TP. Thủ Đức',10.834000,106.713000,'https://cdn.chotot.com/VeSi7HAMiqR0eaduhC64rW4xhVcPKnyFy8UmTMH8oMY/preset:view/plain/da06269a0c560dd592d42384494c4928-3006554602752042895.jpg','135268274'),
    ('d4010000-0000-4000-8000-000000000014'::uuid,'d3000000-0000-0000-0000-000000000025'::uuid,'Máy khoan TUNMAX không chổi than','Máy khoan TUNMAX màu xanh lá, đen, dùng động cơ không chổi than; tin đăng mô tả máy hoạt động ổn định, phù hợp sửa chữa và lắp đặt.',450000,'Đã sử dụng','Dụng cụ','Phường Tam Bình, TP. Thủ Đức',10.869000,106.628000,'https://cdn.chotot.com/0Poo-MzhMuFM-gB-vboYUNhKz5PVKRWQGDp2Xe_ZL8k/preset:view/plain/d317772e7c124da7ca1921d592922a3a-3006554315730171381.jpg','135268136'),
    ('d4010000-0000-4000-8000-000000000015'::uuid,'d3000000-0000-0000-0000-000000000021'::uuid,'Motor máy may công nghiệp','Motor máy may công nghiệp đã qua sử dụng. Tin đăng mô tả động cơ hoạt động ổn định.',400000,'Đã sử dụng','Dụng cụ','Phường Trường Thọ, TP. Thủ Đức',10.850000,106.760000,'https://cdn.chotot.com/LLg6iRj6WFJyQH8FqQaCvOO2CTOm2aK354EKIEdTB-M/preset:view/plain/203d4a4fed38fe3ef0336b71c5fcd68c-3005955070048515233.jpg','135185820')
), inserted AS (
    INSERT INTO homeji.marketplace_posts
        (id,seller_id,status,title,description,is_synthetic,price,condition,category,address,latitude,longitude,linked_rental_post_id,listing_type,available_quantity,reserved_quantity,unit,preparation_minutes,created_at,updated_at)
    SELECT i.id,i.seller_id,1,i.title,i.description,false,i.price,i.condition,i.category,i.address,i.latitude,i.longitude,NULL,1,1,0,'món',NULL,now(),now()
    FROM items i
    WHERE NOT EXISTS (SELECT 1 FROM homeji.marketplace_posts p WHERE p.id=i.id OR p.title=i.title)
    RETURNING id
)
INSERT INTO homeji.marketplace_post_media (id,marketplace_post_id,url,sort_order)
SELECT gen_random_uuid(), i.id, i.image_url, 0
FROM items i JOIN inserted n ON n.id=i.id;

-- Source ads used for facts and media (not linked from Homeji):
-- https://www.chotot.com/mua-ban-ban-ghe-thanh-pho-thu-duc-tp-ho-chi-minh/135168857.htm
-- https://www.chotot.com/mua-ban-ban-ghe-thanh-pho-thu-duc-tp-ho-chi-minh/135265631.htm
-- https://www.chotot.com/mua-ban-quat-thanh-pho-thu-duc-tp-ho-chi-minh/135267777.htm
-- https://www.chotot.com/mua-ban-quat-thanh-pho-thu-duc-tp-ho-chi-minh/135237261.htm
-- https://www.chotot.com/mua-ban-quat-thanh-pho-thu-duc-tp-ho-chi-minh/135266745.htm
-- https://www.chotot.com/mua-ban-quat-thanh-pho-thu-duc-tp-ho-chi-minh/135265415.htm
-- https://www.chotot.com/mua-ban-quat-thanh-pho-thu-duc-tp-ho-chi-minh/135262872.htm
-- https://www.chotot.com/mua-ban-tu-lanh-thanh-pho-thu-duc-tp-ho-chi-minh/134910001.htm
-- https://www.chotot.com/mua-ban-tu-lanh-thanh-pho-thu-duc-tp-ho-chi-minh/134961181.htm
-- https://www.chotot.com/mua-ban-tu-lanh-thanh-pho-thu-duc-tp-ho-chi-minh/135132451.htm
-- https://www.chotot.com/mua-ban-tu-lanh-thanh-pho-thu-duc-tp-ho-chi-minh/135268654.htm
-- https://www.chotot.com/mua-ban-noi-that-do-gia-dung-khac-thanh-pho-thu-duc-tp-ho-chi-minh/135268872.htm
-- https://www.chotot.com/mua-ban-noi-that-do-gia-dung-khac-thanh-pho-thu-duc-tp-ho-chi-minh/135268274.htm
-- https://www.chotot.com/mua-ban-noi-that-do-gia-dung-khac-thanh-pho-thu-duc-tp-ho-chi-minh/135268136.htm
-- https://www.chotot.com/mua-ban-noi-that-do-gia-dung-khac-thanh-pho-thu-duc-tp-ho-chi-minh/135185820.htm
COMMIT;
