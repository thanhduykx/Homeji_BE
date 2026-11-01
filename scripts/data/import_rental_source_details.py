"""Back up and enrich existing source rows only; no Homeji owners, primary IDs or room posts are changed."""
import argparse
from datetime import datetime,timezone
from decimal import Decimal
import json
from pathlib import Path
import re
from urllib.parse import urlparse
import uuid

import psycopg
from enrich_rental_source_details import HOSTS,publisher

def validate(records):
    seen=set()
    for row in records:
        uuid.UUID(row['id'])
        if row['id'] in seen:raise ValueError('Duplicate source row')
        seen.add(row['id'])
        url=urlparse(row['source_url'])
        if url.scheme!='https' or url.hostname!=HOSTS[row['source']] or url.username or url.password or url.port not in (None,443):raise ValueError('Unexpected source URL')
        if not re.fullmatch(r'\d{1,80}',row['source_id']):raise ValueError('Invalid source identity')
        if row['publisher_name'] is not None and publisher(row['publisher_name'])!=row['publisher_name']:raise ValueError('Invalid publisher name')
        lat,lng=row['latitude'],row['longitude']
        if (lat is None)!=(lng is None):raise ValueError('Incomplete coordinates')
        if lat is not None:
            a,b=Decimal(str(lat)),Decimal(str(lng))
            if not a.is_finite() or not b.is_finite() or not(Decimal('10.70')<=a<=Decimal('10.93') and Decimal('106.72')<=b<=Decimal('106.90')):raise ValueError('Coordinate outside reviewed service area')
            if row.get('coordinate_evidence')!='listing_google_embed_query':raise ValueError('Missing coordinate source evidence')
    if not 1<=len(records)<=1000:raise ValueError('Unexpected maintenance batch size')

def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('details',type=Path);parser.add_argument('--expected-project',required=True);parser.add_argument('--output',type=Path,required=True);parser.add_argument('--apply',action='store_true')
    args=parser.parse_args();rows=json.loads(args.details.read_text(encoding='utf-8'))['records'];validate(rows)
    root=Path(__file__).resolve().parents[2]
    raw=json.loads((root/'src/Homeji.Api/appsettings.Local.json').read_text(encoding='utf-8-sig'))['ConnectionStrings']['DefaultConnection']
    config={key.strip().lower():value for key,value in (part.split('=',1) for part in raw.split(';') if '=' in part)}
    if config['database']!='postgres' or not config['username'].endswith('.'+args.expected_project) or not config['host'].endswith('.pooler.supabase.com'):raise ValueError('Database project identity mismatch')
    args.output.mkdir(parents=True,exist_ok=True)
    with psycopg.connect(host=config['host'],port=config.get('port',5432),dbname=config['database'],user=config['username'],password=config['password'],sslmode='require',connect_timeout=15) as db:
        db.execute("SET LOCAL lock_timeout='5s'");db.execute("SET LOCAL statement_timeout='30s'")
        backup=db.execute('SELECT id,source,source_id,source_url,publisher_name,latitude,longitude FROM homeji.rental_source_listings WHERE id=ANY(%s::uuid[]) FOR UPDATE',[[row['id'] for row in rows]]).fetchall()
        indexed={str(row[0]):row for row in backup}
        for row in rows:
            saved=indexed.get(row['id'])
            if not saved or saved[1:4]!=(row['source'],row['source_id'],row['source_url']):raise ValueError('Readback identity mismatch')
        backup_path=args.output/('backup-apply.json' if args.apply else 'backup-preview.json')
        backup_path.write_text(json.dumps([{'id':str(row[0]),'source':row[1],'source_id':row[2],'source_url':row[3],'publisher_name':row[4],'latitude':str(row[5]) if row[5] is not None else None,'longitude':str(row[6]) if row[6] is not None else None} for row in backup],ensure_ascii=False,indent=2),encoding='utf-8')
        affected=0
        if args.apply:
            for row in rows:
                result=db.execute('''UPDATE homeji.rental_source_listings SET publisher_name=COALESCE(%s,publisher_name),
                    latitude=CASE WHEN latitude IS NULL AND longitude IS NULL THEN %s ELSE latitude END,
                    longitude=CASE WHEN latitude IS NULL AND longitude IS NULL THEN %s ELSE longitude END
                    WHERE id=%s AND source=%s AND source_id=%s AND source_url=%s RETURNING id''',
                    [row['publisher_name'],row['latitude'],row['longitude'],row['id'],row['source'],row['source_id'],row['source_url']]).fetchone()
                if not result:raise ValueError('An existing source row disappeared')
                affected+=1
            state=db.execute('SELECT count(*),count(publisher_name),count(latitude) FILTER (WHERE longitude IS NOT NULL) FROM homeji.rental_source_listings WHERE id=ANY(%s::uuid[])',[[row['id'] for row in rows]]).fetchone()
            db.commit()
        else:
            state=(len(rows),sum(row['publisher_name'] is not None for row in rows),sum(row['latitude'] is not None or indexed[row['id']][5] is not None for row in rows));db.rollback()
        receipt={'status':'committed' if args.apply else 'dry_run','at':datetime.now(timezone.utc).isoformat(),'project':args.expected_project,'records':state[0],'publisher_names':state[1],'located_records':state[2],'updated_rows':affected,'owners_or_primary_ids_changed':False,'backup':str(backup_path)}
        (args.output/('import-receipt.json' if args.apply else 'dry-run.json')).write_text(json.dumps(receipt,indent=2),encoding='utf-8')
        print(json.dumps(receipt))

if __name__=='__main__':main()
