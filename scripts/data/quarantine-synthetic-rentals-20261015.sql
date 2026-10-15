-- Run inside a transaction AFTER a full rental/media/source backup.
-- Preserve records, conversations, media and user-entered facts for recovery.
SET LOCAL lock_timeout = '5s';
SET LOCAL statement_timeout = '30s';

-- Stock photography is not evidence of the advertised room. The three legacy
-- fixture IDs also have placeholder storage paths, not uploaded photographs.
UPDATE homeji.rental_posts AS post
SET is_synthetic = true,
    updated_at = CURRENT_TIMESTAMP
WHERE NOT post.is_synthetic
  AND (
    EXISTS (SELECT 1 FROM homeji.rental_post_media media
            WHERE media.rental_post_id = post.id
              AND media.path ~* '^https://(images[.]unsplash[.]com|images[.]pexels[.]com|picsum[.]photos)/')
    OR (post.id IN ('e1111111-1111-1111-1111-111111111111'::uuid,
                    'e2222222-2222-2222-2222-222222222222'::uuid,
                    'e3333333-3333-3333-3333-333333333333'::uuid)
        AND EXISTS (SELECT 1 FROM homeji.rental_post_media media
                    WHERE media.rental_post_id = post.id
                      AND media.path LIKE 'rental-posts/e%/%.jpg'))
  );

UPDATE homeji.rental_posts
SET status = 5,
    moderation_reason = 'Ẩn khỏi danh sách công khai: dữ liệu giả lập hoặc ảnh minh họa, chưa xác minh với phòng thực tế.',
    updated_at = CURRENT_TIMESTAMP
WHERE is_synthetic AND status IN (2, 3);

DO $verify$
BEGIN
    IF EXISTS (SELECT 1 FROM homeji.rental_posts WHERE is_synthetic AND status=3) THEN
        RAISE EXCEPTION 'Synthetic rentals must not remain active';
    END IF;
END $verify$;
