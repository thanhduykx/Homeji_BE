-- Public rental references collected from Mogi on 2026-10-15.
-- These remain source listings, not posts attributed to Homeji landlords.
-- Re-running this script refreshes the collection timestamp and 7-day cache expiry.
INSERT INTO homeji.rental_source_listings
    (id, source, source_id, source_url, title, address, district, price, area, image_urls,
     source_updated_at, collected_at, source_expires_at, source_checked_at)
VALUES
    (gen_random_uuid(), 'mogi', '21850864', 'https://mogi.vn/quan-9/thue-can-ho-chung-cu/can-ho-sky9-lien-phuong-50m2-2pn1wc-full-noi-that-day-du-6tr5-sach-id21850864', 'Căn hộ Sky9 Liên Phường, 2PN đầy đủ nội thất', 'Sky9, Liên Phường, Quận 9 (TP. Thủ Đức)', 'quan-9', 6500000, 50, '["https://cloud.mogi.vn/images/thumb-small/2022/09/29/532/3a4f7f9eb3aa452bad508eb66e7345da.jpg"]'::jsonb, NULL, now(), now() + interval '7 days', now()),
    (gen_random_uuid(), 'mogi', '21210178', 'https://mogi.vn/quan-9/thue-can-ho-chung-cu/can-ho-hausneo-lien-phuong-do-xuan-hop-2pn-1wc-1pk-1ban-cong-moi-dep-id21210178', 'Căn hộ HausNeo Liên Phường, 2PN, ban công', 'HausNeo, Đỗ Xuân Hợp, Quận 9 (TP. Thủ Đức)', 'quan-9', 6000000, 55, '["https://cloud.mogi.vn/images/2021/03/04/075/6dbc612acd014f00ae7237616c48fdbe.jpg"]'::jsonb, NULL, now(), now() + interval '7 days', now()),
    (gen_random_uuid(), 'mogi', '21857133', 'https://mogi.vn/quan-9/thue-can-ho-chung-cu/can-ho-flora-fuji-kikyo-1pn1wc-2pn2wc-full-day-du-noi-that-moi-sach-id21857133', 'Căn hộ Flora Fuji Kikyo, 2PN, đầy đủ nội thất', 'Flora Fuji Kikyo, Quận 9 (TP. Thủ Đức)', 'quan-9', 7500000, 67, '["https://cloud.mogi.vn/images/thumb-small/2022/10/03/284/fc087693f36c4ce88629ea0792f37039.jpg"]'::jsonb, NULL, now(), now() + interval '7 days', now()),
    (gen_random_uuid(), 'mogi', '21160993', 'https://mogi.vn/quan-9/thue-can-ho-chung-cu/can-ho-ehome-2-do-xuan-hop-lien-phuong-55m2-2pn-1wc-ve-q1-chi-20phut-id21160993', 'Căn hộ Ehome 2 Đỗ Xuân Hợp, 2PN', 'Ehome 2, Đỗ Xuân Hợp, Quận 9 (TP. Thủ Đức)', 'quan-9', 6000000, 55, '["https://cloud.mogi.vn/images/thumb-small/2021/01/08/037/938a59fd758b4415ae61767a6046ce73.jpg"]'::jsonb, NULL, now(), now() + interval '7 days', now()),
    (gen_random_uuid(), 'mogi', '21160999', 'https://mogi.vn/quan-9/thue-can-ho-chung-cu/can-ho-flora-anh-dao-55m2-1pn-2pn-may-lanh-bep-tu-ke-ho-boi-gym-yoga-id21160999', 'Căn hộ Flora Anh Đào, 1PN, nội thất cơ bản', 'Flora Anh Đào, Quận 9 (TP. Thủ Đức)', 'quan-9', 6000000, 55, '["https://cloud.mogi.vn/images/thumb-small/2021/01/08/272/5aa6a796c0e44bd4bf8f833300b167df.jpg"]'::jsonb, NULL, now(), now() + interval '7 days', now()),
    (gen_random_uuid(), 'mogi', '21155934', 'https://mogi.vn/quan-9/thue-can-ho-chung-cu/can-ho-do-xuan-hop-50m2-moi-2pn-1pn-ban-cong-ho-boi-20-phut-ve-q1-id21155934', 'Căn hộ Đỗ Xuân Hợp, 2PN, ban công, hồ bơi', 'Đỗ Xuân Hợp, Quận 9 (TP. Thủ Đức)', 'quan-9', 5500000, 50, '["https://cloud.mogi.vn/images/thumb-small/2021/01/06/352/fe224d6cc2044ac18263c9994f8b54be.jpg"]'::jsonb, NULL, now(), now() + interval '7 days', now())
ON CONFLICT (source, source_id) DO UPDATE SET
    source_url = EXCLUDED.source_url,
    title = EXCLUDED.title,
    address = EXCLUDED.address,
    district = EXCLUDED.district,
    price = EXCLUDED.price,
    area = EXCLUDED.area,
    image_urls = EXCLUDED.image_urls,
    source_updated_at = EXCLUDED.source_updated_at,
    collected_at = EXCLUDED.collected_at,
    source_expires_at = EXCLUDED.source_expires_at,
    source_checked_at = EXCLUDED.source_checked_at;
