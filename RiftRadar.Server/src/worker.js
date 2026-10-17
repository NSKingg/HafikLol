import {DurableObject} from 'cloudflare:workers';
import {Gateway,handleRequest} from './core.js';

export class RiotGate extends DurableObject {
  constructor(ctx,env) {
    super(ctx,env);
    const sql=ctx.storage.sql;
    sql.exec('CREATE TABLE IF NOT EXISTS cache (key TEXT PRIMARY KEY, body TEXT NOT NULL, expires INTEGER NOT NULL)');
    this.gateway=new Gateway({
      getLimits:()=>ctx.storage.get('limits'),
      setLimits:value=>ctx.storage.put('limits',value),
      cached:async(key,now)=>[...sql.exec('SELECT body FROM cache WHERE key = ? AND expires > ?',key,now)][0]?.body,
      cache:async(key,body,expires,now)=>{
        sql.exec('DELETE FROM cache WHERE expires <= ?',now);
        sql.exec('INSERT OR REPLACE INTO cache VALUES (?, ?, ?)',key,body,expires);
        // Bound shared cache to 2,000 entries and 32 MB independently of platform quotas.
        for(let i=0;i<100;i++){
          const stats=[...sql.exec('SELECT count(*) AS n, coalesce(sum(length(body)),0) AS bytes FROM cache')][0];
          if(stats.n<=2000&&stats.bytes<=32000000)break;
          sql.exec('DELETE FROM cache WHERE key IN (SELECT key FROM cache ORDER BY expires LIMIT 20)');
        }
      }
    },env.RIOT_API_KEY);
  }
  async query(user,route){return this.gateway.serve(user,route);}
}
export default {
  async fetch(request,env) {
    try{return await handleRequest(request,env,(user,route)=>env.RIOT_GATE.getByName('private-group-v1').query(user,route));}
    catch{return Response.json({error:'Sunucu geçici olarak kullanılamıyor.'},{status:503,headers:{'Cache-Control':'no-store'}});}
  }
};
