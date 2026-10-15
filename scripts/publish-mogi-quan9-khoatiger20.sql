-- Publish the six imported Quận 9 rental records as regular Homeji room posts
-- owned by the existing landlord account. Idempotent by owner + title.
-- Unknown deposits are stored as zero; RoomDetailModal renders zero as
-- "Chưa cung cấp". Source images remain direct Mogi CDN URLs.
DO $$
DECLARE
    account_count integer;
BEGIN
    SELECT count(*) INTO account_count
    FROM auth.users u
    JOIN homeji.user_profiles p ON p.id = u.id
    WHERE lower(u.email) = lower('khoatiger20@gmail.com')
      AND p.role = 2;
    IF account_count <> 1 THEN
        RAISE EXCEPTION 'Expected exactly one existing landlord profile for the requested email';
    END IF;
END $$;

WITH account AS (
    SELECT u.id AS owner_id
    FROM auth.users u
    JOIN homeji.user_profiles p ON p.id = u.id
    WHERE lower(u.email) = lower('khoatiger20@gmail.com')
      AND p.role = 2
), details(source_id, title, description, deposit, latitude, longitude, max_occupants) AS (
    VALUES
      ('21850864', 'Căn hộ Sky9 Liên Phường 2PN đầy đủ nội thất', 'Căn hộ Sky9 diện tích 50 m², 2 phòng ngủ, 1 phòng vệ sinh, đầy đủ nội thất và có thể vào ở ngay. Tin đăng giới thiệu hồ bơi, siêu thị mini, cửa hàng và các dịch vụ thiết yếu trong khu vực; ưu tiên thuê lâu dài.', 13000000::numeric, 10.8037::numeric, 106.7826::numeric, 4),
      ('21210178', 'Căn hộ HausNeo Liên Phường có ban công', 'Căn hộ HausNeo khu Liên Phường, khoảng 54–55 m², gồm 1 phòng ngủ chính, 1 phòng ngủ phụ, phòng khách và ban công. Tin đăng nêu có bếp gas đôi, máy nước nóng; căn hộ mới, sạch và phù hợp thuê lâu dài.', 0::numeric, 10.7996::numeric, 106.7851::numeric, 4),
      ('21857133', 'Căn hộ Flora Fuji Kikyo 2PN đầy đủ nội thất', 'Căn hộ Flora Fuji Kikyo khoảng 67 m², 2 phòng ngủ, 2 phòng vệ sinh. Tin đăng giới thiệu căn hộ được trang bị đầy đủ nội thất.', 0::numeric, 10.8011::numeric, 106.7790::numeric, 4),
      ('21160993', 'Căn hộ Ehome 2 Đỗ Xuân Hợp 2PN', 'Căn hộ Ehome 2 trên đường Đỗ Xuân Hợp, khoảng 55 m², 2 phòng ngủ, 1 phòng vệ sinh, có ban công, phòng khách và bếp. Khu căn hộ có hồ bơi, phòng gym, yoga và các tiện ích sinh hoạt.', 0::numeric, 10.8025::numeric, 106.7802::numeric, 4),
      ('21160999', 'Căn hộ Flora Anh Đào gần hồ bơi và phòng gym', 'Căn hộ Flora Anh Đào khoảng 55 m². Tin đăng có lựa chọn 1–2 phòng ngủ, trang bị máy lạnh, bếp và tủ kệ; khu căn hộ có hồ bơi và phòng gym.', 0::numeric, 10.8013::numeric, 106.7829::numeric, 3),
      ('21155934', 'Căn hộ Đỗ Xuân Hợp có ban công và hồ bơi', 'Căn hộ khu vực Đỗ Xuân Hợp khoảng 50 m², tin đăng có lựa chọn 1–2 phòng ngủ, ban công và tiện ích hồ bơi.', 0::numeric, 10.8007::numeric, 106.7839::numeric, 3)
), candidates AS (
    SELECT gen_random_uuid() AS id, a.owner_id, d.*, s.address, s.price, s.area, s.image_urls
    FROM account a
    CROSS JOIN details d
    JOIN homeji.rental_source_listings s ON s.source = 'mogi' AND s.source_id = d.source_id
    WHERE s.source_expires_at IS NULL OR s.source_expires_at > now()
), inserted AS (
    INSERT INTO homeji.rental_posts (
        id, owner_id, type, status, title, description, is_synthetic, price, deposit, area,
        electricity_price, water_price, internet_price, max_occupants, available_slots,
        house_rules, available_from, transfer_kind, original_lease_ends_on, pass_fee,
        transfer_reason, owner_consent_confirmed, owner_consent_contact,
        owner_consent_verified_at, owner_consent_verified_by, owner_consent_verification_note,
        address, latitude, longitude, moderation_reason, view_count, save_count, created_at, updated_at
    )
    SELECT c.id, c.owner_id, 1, 3, c.title, c.description, false, c.price, c.deposit, c.area,
        0, 0, 0, c.max_occupants, c.max_occupants,
        NULL, CURRENT_DATE, NULL, NULL, 0, NULL, false, NULL, NULL, NULL, NULL,
        c.address, c.latitude, c.longitude, NULL, 0, 0, now(), now()
    FROM candidates c
    WHERE NOT EXISTS (
        SELECT 1 FROM homeji.rental_posts existing
        WHERE existing.owner_id = c.owner_id AND existing.title = c.title
    )
    RETURNING id, title
), inserted_media AS (
    INSERT INTO homeji.rental_post_media
        (id, rental_post_id, media_type, bucket, path, is_thumbnail, sort_order, created_at)
    SELECT gen_random_uuid(), i.id, 1, 'external', image.url, image.ordinality = 1,
        (image.ordinality - 1)::integer, now()
    FROM inserted i
    JOIN candidates c ON c.title = i.title
    CROSS JOIN LATERAL jsonb_array_elements_text(c.image_urls) WITH ORDINALITY AS image(url, ordinality)
    RETURNING rental_post_id
)
SELECT count(DISTINCT rental_post_id) AS posts_created FROM inserted_media;

-- Keep reruns aligned with the owner-confirmed catalog values.
UPDATE homeji.rental_posts p
SET status = 3,
    deposit = CASE
        WHEN p.title = 'Căn hộ Sky9 Liên Phường 2PN đầy đủ nội thất' THEN 13000000
        ELSE 0
    END,
    moderation_reason = NULL,
    updated_at = now()
FROM auth.users u
WHERE u.id = p.owner_id
  AND lower(u.email) = lower('khoatiger20@gmail.com')
  AND p.title IN (
      'Căn hộ Sky9 Liên Phường 2PN đầy đủ nội thất',
      'Căn hộ HausNeo Liên Phường có ban công',
      'Căn hộ Flora Fuji Kikyo 2PN đầy đủ nội thất',
      'Căn hộ Ehome 2 Đỗ Xuân Hợp 2PN',
      'Căn hộ Flora Anh Đào gần hồ bơi và phòng gym',
      'Căn hộ Đỗ Xuân Hợp có ban công và hồ bơi'
  );
