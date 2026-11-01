"""Collect attributed publisher names and listing-map coordinates; never assign Homeji owners."""
import argparse
from concurrent.futures import ThreadPoolExecutor
from datetime import datetime, timezone
import html as html_module
import json
from pathlib import Path
import re
import time
from urllib.parse import urlparse,parse_qs,unquote
from urllib.robotparser import RobotFileParser

from bs4 import BeautifulSoup
import requests
from collect_rental_sources import AGENT, fetch, linked_data

HOSTS={'mogi':'mogi.vn','phongtro123':'phongtro123.com','phongtrovn':'phongtro.vn'}

def publisher(value):
    if not isinstance(value,str):return None
    value=' '.join(value.split())
    if not 2 <= len(value) <= 100 or not any(character.isalpha() for character in value):return None
    if re.search(r'https?://|www\.|@|\d{8,}',value,re.I):return None
    return value

def extract_details(text,source,url):
    soup=BeautifulSoup(text,'html.parser')
    schemas=list(linked_data(soup))
    name=None
    if source=='mogi':
        candidates=[item for item in schemas if item.get('@type')=='Person' and item.get('makesOffer')]
        if len(candidates)==1:name=publisher(candidates[0].get('name'))
    elif source=='phongtro123':
        for item in schemas:
            if item.get('@type')!='WebPage' or item.get('url','').rstrip('/')!=url.rstrip('/'):continue
            author=item.get('author')
            if isinstance(author,dict):name=publisher(author.get('name'))
    else:
        for item in schemas:
            if item.get('@type')=='RealEstateListing' and item.get('url')==url:
                author=item.get('author') or item.get('seller')
                if isinstance(author,dict):name=publisher(author.get('name'))
    points=set()
    # Read only listing embed q=latitude,longitude. Organization.geo is an office, not the rental.
    for raw in re.findall(r'''https://(?:www\.)?google\.com/maps/embed/v1/place\?[^\s<>"']+''',text):
        query=parse_qs(urlparse(html_module.unescape(raw)).query).get('q',[''])[0]
        match=re.fullmatch(r'\s*(10\.\d+)\s*,\s*(106\.\d+)\s*',unquote(query))
        if match:
            lat,lng=map(float,match.groups())
            if 10.70<=lat<=10.97 and 106.70<=lng<=106.91:points.add((lat,lng))
    coordinate=next(iter(points)) if len(points)==1 else None
    return {'publisher_name':name,'latitude':coordinate[0] if coordinate else None,'longitude':coordinate[1] if coordinate else None,
        'coordinate_evidence':'listing_google_embed_query' if coordinate else None}

def collect_source(source,rows,output):
    results=[]; errors=[]
    with requests.Session() as session:
        session.headers['User-Agent']=AGENT
        host=HOSTS[source]
        robots=RobotFileParser();robots.parse(fetch(session,'https://'+host+'/robots.txt',host).splitlines())
        for item in rows:
            url=item['sourceUrl']
            if urlparse(url).hostname!=host or not robots.can_fetch(AGENT,url):
                errors.append({'id':item['id'],'reason':'host_or_robots'});continue
            try:
                time.sleep(1)
                text=fetch(session,url,host)
                details=extract_details(text,source,url)
                results.append({'id':item['id'],'source':source,'source_id':item['sourceId'],'source_url':url,
                    'checked_at':datetime.now(timezone.utc).isoformat(),**details})
                output.write_text(json.dumps({'records':results,'errors':errors},ensure_ascii=False,indent=2),encoding='utf-8')
            except (ValueError,requests.RequestException) as error:errors.append({'id':item['id'],'reason':type(error).__name__})
    print(json.dumps({'source':source,'records':len(results),'publishers':sum(bool(item['publisher_name']) for item in results),'coordinates':sum(item['latitude'] is not None for item in results),'errors':len(errors)}),flush=True)
    return results,errors

def main():
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--output',type=Path,required=True);args=parser.parse_args()
    args.output.mkdir(parents=True,exist_ok=True)
    rows=[]
    for page in range(1,21):
        response=requests.get('https://homeji-be.onrender.com/api/rental-source-listings',params={'page':page,'pageSize':50},timeout=20);response.raise_for_status()
        chunk=response.json();rows+=chunk
        if len(chunk)<50:break
    if len(rows)>1000:raise ValueError('Collection exceeds bounded maintenance scope')
    groups={source:[item for item in rows if item['source']==source] for source in HOSTS}
    with ThreadPoolExecutor(max_workers=3) as pool:
        futures=[pool.submit(collect_source,source,items,args.output/(source+'.json')) for source,items in groups.items() if items]
        results=[future.result() for future in futures]
    records=[row for group,_ in results for row in group];errors=[row for _,group in results for row in group]
    (args.output/'details.json').write_text(json.dumps({'records':records,'errors':errors},ensure_ascii=False,indent=2),encoding='utf-8')
    print(json.dumps({'records':len(records),'publishers':sum(bool(item['publisher_name']) for item in records),'coordinates':sum(item['latitude'] is not None for item in records),'errors':len(errors)}))

if __name__=='__main__':main()
