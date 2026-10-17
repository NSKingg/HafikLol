import {randomBytes,createHash} from 'node:crypto';
import {readFile,writeFile} from 'node:fs/promises';
const [command,...args]=process.argv.slice(2);
if(command==='hash'&&args.length===1){
  const {invite}=JSON.parse(await readFile(args[0],'utf8'));
  if(typeof invite!=='string'||invite.length<32)throw new Error('Invalid invitation');
  console.log(createHash('sha256').update(invite).digest('hex'));
}else if(command==='create'&&args.length===2){
  const url=new URL(args[0]);if(url.protocol!=='https:'||url.pathname!=='/'||url.search||url.hash||url.username||url.password)throw new Error('HTTPS root URL required');
  const invite=randomBytes(32).toString('base64url');
  await writeFile(args[1],JSON.stringify({protocol:1,server:url.href,invite},null,2),{flag:'wx',mode:0o600});
  console.log('Invitation file created. Add this SHA-256 hash to CLIENT_HASHES:');
  console.log(createHash('sha256').update(invite).digest('hex'));
}else{console.error('Usage: node tools/invite.mjs create https://server.workers.dev/ file.rrconnection\n       node tools/invite.mjs hash file.rrconnection');process.exitCode=1;}
