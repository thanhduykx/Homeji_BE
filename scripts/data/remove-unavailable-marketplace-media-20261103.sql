-- Five image references returned HTTP 404 in a direct image audit on 2026-11-03.
-- Original media rows are backed up by RentalCatalogRepair before application.
-- Posts, users, orders and physical image files remain intact.
DO $repair$
DECLARE affected integer;
BEGIN
  WITH expected(post_id,url) AS (VALUES
('aa333333-3333-3333-3333-333333333333'::uuid,'https://res.cloudinary.com/dv4y7cdpi/image/upload/v1/homeji/marketplace/rice-cooker-sunhouse.jpg'),
('aa222222-2222-2222-2222-222222222222'::uuid,'https://res.cloudinary.com/dv4y7cdpi/image/upload/v1/homeji/marketplace/desk-fold-open.jpg'),
('aa222222-2222-2222-2222-222222222222'::uuid,'https://res.cloudinary.com/dv4y7cdpi/image/upload/v1/homeji/marketplace/desk-fold-closed.jpg'),
('aa111111-1111-1111-1111-111111111111'::uuid,'https://res.cloudinary.com/dv4y7cdpi/image/upload/v1/homeji/marketplace/fan-panasonic-front.jpg'),
('aa111111-1111-1111-1111-111111111111'::uuid,'https://res.cloudinary.com/dv4y7cdpi/image/upload/v1/homeji/marketplace/fan-panasonic-detail.jpg'))
  DELETE FROM homeji.marketplace_post_media media USING expected
    WHERE media.marketplace_post_id=expected.post_id AND media.url=expected.url;
  GET DIAGNOSTICS affected = ROW_COUNT;
  IF affected NOT IN (0,5) THEN
    RAISE EXCEPTION 'Media changed concurrently: expected 0 or 5 matching rows, got %', affected;
  END IF;
END $repair$;
