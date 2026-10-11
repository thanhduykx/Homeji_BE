import { readFileSync, writeFileSync } from 'node:fs'
import { fileURLToPath } from 'node:url'
import assert from 'node:assert/strict'

const data = JSON.parse(readFileSync(new URL('./quan9-source-data-20261011.json', import.meta.url), 'utf8'))
const quote = value => `'${String(value).replaceAll("'", "''")}'`
const checkedAt = data.source_checked_at
assert.ok(Number.isFinite(Date.parse(checkedAt)), 'Set source_checked_at after reviewing sources')
const sql = ['-- Prepared source records; availability and image authenticity are NOT verified.',
  '-- Apply AddFoodSourcePlaces migration first. Re-running never overwrites existing records.',
  'BEGIN;', 'SET LOCAL standard_conforming_strings = on;', "SET LOCAL lock_timeout = '5s';", "SET LOCAL statement_timeout = '30s';"]
const keys = new Set()
for (const place of data.food_places) {
  assert.ok(place.name.length <= 200 && place.address.length <= 500)
  const source = new URL(place.source_url)
  assert.ok(source.protocol === 'https:' && ['www.nhatot.com', 'shopeefood.vn', 'vinwonders.com'].includes(source.hostname))
  const key = `${place.name}|${place.address}`
  assert.ok(!keys.has(key)); keys.add(key)
  const maps = 'https://www.google.com/maps/search/?api=1&query=' + encodeURIComponent(`${place.name} ${place.address}`)
  const values = [place.id, place.name, place.address, place.source_url, maps, place.verification_note].map(quote)
  sql.push(`INSERT INTO homeji.food_source_places (id,name,address,source_url,maps_search_url,verification_note,collected_at) VALUES (${values.join(',')},CURRENT_TIMESTAMP) ON CONFLICT (name,address) DO NOTHING RETURNING 'inserted_food' AS action,id,name AS title,source_url;`)
}
for (const rental of data.rentals) {
  assert.ok(/^\d+$/.test(rental.source_id))
  assert.ok(!data.excluded.some(record => record.source_id === rental.source_id))
  assert.ok(rental.price > 0 && rental.area > 0)
  assert.ok(new URL(rental.source_url).hostname === 'phongtro123.com')
  for (const image of rental.image_urls) assert.ok(new URL(image).hostname === 'pt123.cdn.static123.com')
  assert.ok(Date.parse(rental.source_expires_at) > Date.parse(checkedAt))
  sql.push(`DO $expiry$ BEGIN IF CURRENT_TIMESTAMP >= ${quote(rental.source_expires_at)}::timestamptz THEN RAISE EXCEPTION 'Source ${rental.source_id} has expired; refresh research before importing'; END IF; END $expiry$;`)
  const values = [rental.id, 'phongtro123', rental.source_id, rental.source_url, rental.title, rental.address, 'quan-9'].map(quote)
  sql.push(`INSERT INTO homeji.rental_source_listings (id,source,source_id,source_url,title,address,district,price,area,image_urls,source_updated_at,collected_at,source_expires_at,source_checked_at) VALUES (${values.join(',')},${rental.price},${rental.area},${quote(JSON.stringify(rental.image_urls))}::jsonb,${quote(rental.source_updated_at)}::timestamptz,CURRENT_TIMESTAMP,${quote(rental.source_expires_at)}::timestamptz,${quote(checkedAt)}::timestamptz) ON CONFLICT (source,source_id) DO NOTHING RETURNING 'inserted_rental' AS action,id,title,source_url;`)
}
sql.push('COMMIT;',
  `SELECT 'stored_food' AS action,id,name AS title,source_url FROM homeji.food_source_places WHERE (name,address) IN (${data.food_places.map(p => `(${quote(p.name)},${quote(p.address)})`).join(',')});`,
  `SELECT 'stored_rental' AS action,id,title,source_url FROM homeji.rental_source_listings WHERE source='phongtro123' AND source_id IN (${data.rentals.map(r => quote(r.source_id)).join(',')});`)
const output = new URL('./quan9-import-20261011.sql', import.meta.url)
writeFileSync(output, sql.join('\n') + '\n', 'utf8')
console.log(JSON.stringify({ status: 'prepared_not_executed', food: data.food_places.length, rentals: data.rentals.length, excluded: data.excluded.length, sql: fileURLToPath(output) }))
