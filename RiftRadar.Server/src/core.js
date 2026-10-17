const platforms = new Set('tr1 euw1 eun1 na1 kr ru br1 la1 la2 oc1 jp1 sg2 tw2 vn2'.split(' '));
const clusters = new Set(['europe','americas','asia','sea']);
const id = '[A-Za-z0-9_-]{1,160}';
export function reply(status, message, retry,code) {
  return Response.json({error:message,...(code?{code}:{})},{status,headers:{'Cache-Control':'no-store','X-Content-Type-Options':'nosniff',...(retry?{'Retry-After':String(Math.max(1,Math.ceil(retry)))}:{})}});
}
export function routeFor(host,path) {
  if(typeof host!=='string'||typeof path!=='string'||path.length>800||/[\\\r\n]/.test(path))return null;
  if(clusters.has(host)) {
    const account = /^\/riot\/account\/v1\/accounts\/by-riot-id\/([^/?#]+)\/([^/?#]+)$/.exec(path);
    if(account) {
      try {if(account.slice(1).some(s=>{const d=decodeURIComponent(s);return d.length>100||!d.length||/[\x00-\x1f/\\?#]/.test(d)||d==='.'||d==='..';}))return null;}catch{return null;}
      return {host,path,ttl:21600,kind:'account'};
    }
    if(new RegExp(`^/lol/match/v5/matches/by-puuid/${id}/ids\\?start=(0|[1-9][0-9]{0,2})&count=([1-9]|[1-9][0-9]|100)$`).test(path))return {host,path,ttl:60,kind:'ids'};
    if(new RegExp(`^/lol/match/v5/matches/${id}/timeline$`).test(path))return {host,path,ttl:604800,kind:'timeline'};
    if(new RegExp(`^/lol/match/v5/matches/${id}$`).test(path))return {host,path,ttl:604800,kind:'match'};
  }
  if(platforms.has(host)) {
    if(new RegExp(`^/lol/summoner/v4/summoners/by-puuid/${id}$`).test(path))return {host,path,ttl:600,kind:'summoner'};
    if(new RegExp(`^/lol/league/v4/entries/by-puuid/${id}$`).test(path))return {host,path,ttl:300,kind:'league'};
    if(new RegExp(`^/lol/champion-mastery/v4/champion-masteries/by-puuid/${id}/top\\?count=12$`).test(path))return {host,path,ttl:3600,kind:'mastery'};
    if(new RegExp(`^/lol/spectator/v5/active-games/by-summoner/${id}$`).test(path))return {host,path,ttl:20,kind:'spectator'};
    if(path==='/lol/status/v4/platform-data')return {host,path,ttl:300,kind:'status'};
  }
  return null;
}
export async function digest(token) {
  return [...new Uint8Array(await crypto.subtle.digest('SHA-256',new TextEncoder().encode(token)))].map(x=>x.toString(16).padStart(2,'0')).join('');
}
export async function handleRequest(request,env,serve) {
  const url=new URL(request.url);
  if(!['/v1/status','/v1/query'].includes(url.pathname)||url.search)return reply(404,'Bulunamadı.');
  if(request.method!==(url.pathname==='/v1/status'?'GET':'POST'))return reply(405,'Yöntem desteklenmiyor.');
  let users;
  try {users=JSON.parse(env.CLIENT_HASHES||'[]');}catch{return reply(503,'Sunucu yapılandırılmamış.');}
  if(!Array.isArray(users)||users.length===0||users.length>10||users.some(x=>typeof x!=='string'||!/^[a-f0-9]{64}$/.test(x))||!env.RIOT_API_KEY?.startsWith('RGAPI-'))return reply(503,'Sunucu yapılandırılmamış.');
  const auth=/^Bearer ([A-Za-z0-9_-]{32,128})$/.exec(request.headers.get('Authorization')||'');
  if(!auth)return reply(401,'Davet kodu gerekli.');
  const user=await digest(auth[1]);
  if(!users.includes(user))return reply(401,'Davet kodu geçersiz.');
  if(url.pathname==='/v1/status')return Response.json({protocol:1,ready:true},{headers:{'Cache-Control':'no-store'}});
  if(!request.headers.get('Content-Type')?.startsWith('application/json'))return reply(400,'JSON gerekli.');
  let body;
  try {body=JSON.parse(await readBounded(request.body,2048));}catch{return reply(400,'İstek geçersiz veya çok büyük.');}
  const route=routeFor(body?.host,body?.path);
  if(!route)return reply(400,'Sorgu desteklenmiyor.');
  return serve(user,route);
}
export async function readBounded(stream,max) {
  if(!stream)return '';
  const reader=stream.getReader(),parts=[];let total=0;
  try {for(;;){const {done,value}=await reader.read();if(done)break;total+=value.byteLength;if(total>max){await reader.cancel();throw new Error('Too large');}parts.push(value);}}
  finally {reader.releaseLock();}
  const all=new Uint8Array(total);let offset=0;for(const p of parts){all.set(p,offset);offset+=p.length;}return new TextDecoder().decode(all);
}
// A single Durable Object coordinates every region and every group member.
export class Gateway {
  constructor(store,key,transport=(url,options)=>fetch(url,options),clock=Date.now){this.store=store;this.key=key;this.transport=transport;this.clock=clock;this.tail=Promise.resolve();this.pending=0;this.stage='START';}
  async serve(user,route) {
    if(this.pending>=8)return reply(429,'Sunucu sırası dolu.',5);
    this.pending++;
    const run=this.tail.then(()=>this.execute(user,route));
    this.tail=run.catch(()=>{});
    try{return await run;}catch{return reply(502,'Riot bağlantısı tamamlanamadı.',null,this.stage);}finally{this.pending--;}
  }
  async execute(user,route) {
    const now=this.clock(),cacheKey=route.host+route.path;
    this.stage='LIMIT_READ';const limits=await this.store.getLimits()||{calls:[],users:{},blocked:0};
    limits.users=Object.fromEntries(Object.entries(limits.users).map(([k,v])=>[k,v.filter(t=>t>now-60000)]).filter(([,v])=>v.length));
    const own=limits.users[user]||[];
    if(own.length>=180)return reply(429,'Kişisel sorgu sınırı dolu.',(own[0]+60001-now)/1000);
    own.push(now);limits.users[user]=own;
    this.stage='CACHE_READ';const cached=await this.store.cached(cacheKey,now);
    if(cached){await this.store.setLimits(limits);return new Response(cached,{headers:{'Content-Type':'application/json','Cache-Control':'no-store','X-Rift-Cache':'HIT'}});}
    limits.calls=limits.calls.filter(t=>t>now-120100);
    const recent=limits.calls.filter(t=>t>now-1050);
    let until=limits.blocked||0;
    if(limits.calls.length>=90)until=Math.max(until,limits.calls[0]+120100);
    if(recent.length>=15)until=Math.max(until,recent[0]+1050);
    if(until>now){await this.store.setLimits(limits);return reply(429,'Ortak Riot sınırı dolu.',(until-now)/1000);}
    this.stage='LIMIT_WRITE';limits.calls.push(now);await this.store.setLimits(limits);
    this.stage='RIOT_FETCH';
    const response=await this.transport(`https://${route.host}.api.riotgames.com${route.path}`,{method:'GET',redirect:'manual',headers:{'X-Riot-Token':this.key.trim()},signal:AbortSignal.timeout(15000)});
    if(response.status===429){const raw=response.headers.get('Retry-After');const seconds=/^\d+$/.test(raw||'')?+raw:Math.max(1,(Date.parse(raw)-now)/1000)||120;limits.blocked=now+Math.min(Math.max(seconds,1),86400)*1000;await this.store.setLimits(limits);return reply(429,'Riot sınırı dolu.',seconds);}
    // Header windows may be stricter than the conservative default budget.
    for(const family of ['App','Method']) {
      const parse=s=>Object.fromEntries((s||'').split(',').map(x=>x.split(':')).filter(x=>x.length===2).map(([n,w])=>[w,+n]));
      const defined=parse(response.headers.get(`X-${family}-Rate-Limit`)),counts=parse(response.headers.get(`X-${family}-Rate-Limit-Count`));
      for(const [window,max] of Object.entries(defined))if(+window>0&&counts[window]>=max)limits.blocked=Math.max(limits.blocked,now+Math.min(+window,86400)*1000+100);
    }
    await this.store.setLimits(limits);
    if(!response.ok)return reply(response.status===401||response.status===403?503:response.status===404?404:502,'Riot isteği tamamlanamadı.');
    this.stage='BODY_READ';const body=await readBounded(response.body,2_000_000);
    let parsed;try{parsed=JSON.parse(body);}catch{return reply(502,'Riot yanıtı geçersiz.');}
    if(route.kind==='match'&&(parsed.info?.queueId===0||parsed.info?.gameType==='CUSTOM_GAME'))return reply(403,'Özel maçlar paylaşılmaz.');
    this.stage='CACHE_WRITE';if(route.ttl>0&&new TextEncoder().encode(body).length<=250000)await this.store.cache(cacheKey,body,now+route.ttl*1000,now);
    return new Response(body,{headers:{'Content-Type':'application/json','Cache-Control':'no-store','X-Rift-Cache':'MISS','X-Content-Type-Options':'nosniff'}});
  }
}
