"""Copy bounded, allowlisted listing photographs to the application's configured image storage."""
import argparse
from concurrent.futures import ThreadPoolExecutor,as_completed
import hashlib,json
from pathlib import Path
from urllib.parse import urlparse
import requests

HOSTS={'cloud.mogi.vn','pt123.cdn.static123.com','file.phongtro.vn'}
def main():
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--output',type=Path,required=True);args=parser.parse_args()
    args.output.mkdir(parents=True,exist_ok=True)
    config=json.loads((Path(__file__).resolve().parents[2]/'src/Homeji.Api/appsettings.json').read_text(encoding='utf-8-sig'))['Cloudinary']
    rows=[]
    for page in range(1,21):
        result=requests.get('https://homeji-be.onrender.com/api/rental-source-listings',params={'page':page,'pageSize':50},timeout=25);result.raise_for_status();chunk=result.json();rows+=chunk
        if len(chunk)<50:break
    urls=set(url for row in rows for url in row['imageUrls'])
    if len(urls)>1500:raise ValueError('Image batch exceeds maintenance limit')
    cache_path=args.output/'image-map.json';cache=json.loads(cache_path.read_text()) if cache_path.exists() else {}
    def upload(url):
        parsed=urlparse(url)
        if parsed.scheme!='https' or parsed.hostname not in HOSTS or parsed.username or parsed.password or parsed.port:raise ValueError('Unexpected image origin')
        public_id='source_'+hashlib.sha256(url.encode()).hexdigest()
        response=requests.post('https://api.cloudinary.com/v1_1/'+config['CloudName']+'/image/upload',data={
            'file':url,'upload_preset':config['UploadPreset'],'folder':'homeji/rental-sources','public_id':public_id},timeout=50)
        response.raise_for_status();data=response.json();target=data['secure_url'];point=urlparse(target)
        if point.scheme!='https' or point.hostname!='res.cloudinary.com' or not point.path.startswith('/'+config['CloudName']+'/image/upload/'):raise ValueError('Unexpected stored image origin')
        return url,{'url':target,'bytes':data['bytes'],'width':data['width'],'height':data['height']}
    errors=[]
    with ThreadPoolExecutor(max_workers=4) as pool:
        pending={pool.submit(upload,url):url for url in sorted(urls) if url not in cache}
        for future in as_completed(pending):
            try:
                url,data=future.result();cache[url]=data
                cache_path.write_text(json.dumps(cache,indent=2),encoding='utf-8')
            except Exception as error:errors.append({'url':pending[future],'reason':type(error).__name__})
    result={'records':[{'id':row['id'],'source':row['source'],'source_id':row['sourceId'],'source_url':row['sourceUrl'],'original_images':row['imageUrls'],
        'image_urls':[cache[url]['url'] if url in cache else url for url in row['imageUrls']]} for row in rows],
        'unique_images':len(urls),'copied_images':len([url for url in urls if url in cache]),'bytes':sum(cache[url]['bytes'] for url in urls if url in cache),'errors':errors}
    (args.output/'mirrored.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
    print(json.dumps({k:v for k,v in result.items() if k not in ['records','errors']}|{'errors':len(errors)}),flush=True)
if __name__=='__main__':main()
