import {test} from 'node:test';
import assert from 'node:assert/strict';
import {Gateway,routeFor,digest,handleRequest} from '../src/core.js';

const token='a'.repeat(43),key='RGAPI-test-not-real';
function memory(){let limits;const cache=new Map();return{getLimits:async()=>structuredClone(limits),setLimits:async v=>{limits=structuredClone(v);},cached:async(k,now)=>cache.get(k)?.expires>now?cache.get(k).body:null,cache:async(k,body,expires)=>{cache.set(k,{body,expires});},limits:()=>limits};}
const status=()=>routeFor('tr1','/lol/status/v4/platform-data');
const env={RIOT_API_KEY:key,CLIENT_HASHES:JSON.stringify([await digest(token)])};
function request(body,auth=token){return new Request('https://radar.example/v1/query',{method:'POST',headers:{'Content-Type':'application/json','Authorization':'Bearer '+auth},body:JSON.stringify(body)});}
test('route allowlist rejects other hosts, path escapes, query injection and unsupported endpoints',()=>{
 for(const [host,path] of [['evil.example','/lol/status/v4/platform-data'],['tr1','/riot/account/v1/accounts/by-riot-id/name/tag'],['tr1','//evil.example'],['europe','/riot/account/v1/accounts/by-riot-id/%2e%2e/tag'],['europe','/riot/account/v1/accounts/by-riot-id/a%2fb/tag'],['tr1','/lol/status/v4/platform-data?api_key=secret'],['europe','/lol/match/v5/matches/by-puuid/abc/ids?start=0&count=1000']])assert.equal(routeFor(host,path),null);
 assert.ok(routeFor('europe','/riot/account/v1/accounts/by-riot-id/%C5%9Eafak/TR1'));assert.ok(routeFor('europe','/lol/match/v5/matches/TR1_123/timeline'));assert.ok(routeFor('europe','/lol/match/v5/matches/by-puuid/abc/ids?start=20&count=20'));
});
test('unauthorized, revoked and 11-user configurations never reach Riot',async()=>{
 let calls=0;const serve=()=>{calls++;return Response.json({});};
 assert.equal((await handleRequest(request({host:'tr1',path:status().path},'b'.repeat(43)),env,serve)).status,401);
 assert.equal((await handleRequest(request({}),{...env,CLIENT_HASHES:'[]'},serve)).status,503);
 assert.equal((await handleRequest(request({}),{...env,CLIENT_HASHES:JSON.stringify(Array(11).fill('a'.repeat(64)))},serve)).status,503);assert.equal(calls,0);
});
test('status is authenticated, protocol versioned, contains no key or invitation',async()=>{
 const r=await handleRequest(new Request('https://radar.example/v1/status',{headers:{Authorization:'Bearer '+token}}),env,()=>assert.fail());const body=await r.text();assert.equal(r.status,200);assert.deepEqual(JSON.parse(body),{protocol:1,ready:true});assert.ok(!body.includes(key)&&!body.includes(token));
});
test('oversized and malformed request bodies are rejected before forwarding',async()=>{
 const r=await handleRequest(request({host:'tr1',path:'x'.repeat(4000)}),env,()=>assert.fail());assert.equal(r.status,400);
});
test('ten concurrent users share one cached Riot request and key stays upstream',async()=>{
 let calls=0;const store=memory();const gate=new Gateway(store,key,async(url,opts)=>{calls++;assert.equal(opts.headers['X-Riot-Token'],key);assert.equal(url,'https://tr1.api.riotgames.com/lol/status/v4/platform-data');return Response.json({name:'TR'});});
 const responses=await Promise.all(Array.from({length:10},(_,i)=>gate.serve('user'+i,status())));assert.equal(calls,1);assert.ok(responses.filter(r=>r.status===200).length>=8);assert.ok(!(await responses[0].text()).includes(key));
});
test('shared 90/120-second budget survives a new Gateway instance',async()=>{
 let now=1000000,calls=0;const store=memory();const transport=async()=>{calls++;return Response.json({});};let gate=new Gateway(store,key,transport,()=>now);
 for(let i=0;i<90;i++){const route={...status(),path:'/test/'+i,ttl:0};assert.equal((await gate.serve('u',route)).status,200);now+=1100;}
 gate=new Gateway(store,key,transport,()=>now);assert.equal((await gate.serve('u',{...status(),ttl:0})).status,429);assert.equal(calls,90);now+=121000;assert.equal((await gate.serve('u',status())).status,200);
});
test('Retry-After and header-defined limits are persisted and respected',async()=>{
 let calls=0;const store=memory();const gate=new Gateway(store,key,async()=>{calls++;return new Response('',{status:429,headers:{'Retry-After':'30'}});},()=>1000000);
 const first=await gate.serve('u',status());assert.equal(first.headers.get('Retry-After'),'30');assert.equal((await gate.serve('v',status())).status,429);assert.equal(calls,1);
 const other=new Gateway(memory(),key,async()=>Response.json({},{headers:{'X-App-Rate-Limit':'1:20','X-App-Rate-Limit-Count':'1:20'}}),()=>1000000);
 assert.equal((await other.serve('u',{...status(),ttl:0})).status,200);assert.equal((await other.serve('v',{...status(),ttl:0})).status,429);
});
test('Riot error body and secrets never reach a client; custom matches are excluded',async()=>{
 const gate=new Gateway(memory(),key,async()=>new Response(key,{status:403}));assert.equal((await gate.serve('u',status())).status,503);
 const custom=new Gateway(memory(),key,async()=>Response.json({info:{queueId:0,gameType:'CUSTOM_GAME'}}));assert.equal((await custom.serve('u',routeFor('europe','/lol/match/v5/matches/TR1_1'))).status,403);
});
test('malformed or oversized upstream JSON is not cached or exposed',async()=>{
 const gate=new Gateway(memory(),key,async()=>new Response('not JSON'));assert.equal((await gate.serve('u',status())).status,502);
 const huge=new Gateway(memory(),key,async()=>new Response('x'.repeat(2000001)));assert.equal((await huge.serve('u',status())).status,502);
});
test('edge-compatible manual redirects never forward the Riot key to another host',async()=>{
 let calls=0;const gate=new Gateway(memory(),key,async(url,opts)=>{calls++;assert.equal(opts.redirect,'manual');return new Response('',{status:302,headers:{Location:'https://evil.example'}});});
 assert.equal((await gate.serve('u',status())).status,502);assert.equal(calls,1);
});
