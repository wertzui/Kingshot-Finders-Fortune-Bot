(async()=>{
const b=document.querySelector('div.finders-fortune-board,#finders-fortune-board');
if(!b){alert('Board not found. Open the map first.');return;}
const R=b.getBoundingClientRect();
const buttons=[...b.querySelectorAll('button[data-item-id]')];
const o=document.createElement('div');
o.style.cssText='position:fixed;inset:0;z-index:2147483647;background:rgba(0,0,0,.5);display:flex;flex-direction:column;align-items:center;justify-content:center;gap:12px;color:#fff;font:16px sans-serif';
const sp=document.createElement('div');
sp.style.cssText='width:48px;height:48px;border:5px solid rgba(255,255,255,.3);border-top-color:#fff;border-radius:50%';
sp.animate([{transform:'rotate(0deg)'},{transform:'rotate(360deg)'}],{duration:800,iterations:Infinity});
const tx=document.createElement('div');
o.append(sp,tx);
document.body.appendChild(o);
let done=0,failed=0;
const progress=()=>{tx.textContent='Analyzing items '+done+' / '+buttons.length+' ...';};
progress();
const cache=new Map();
const load=src=>{
if(!cache.has(src))cache.set(src,fetch(src).then(r=>r.blob()).then(createImageBitmap));
return cache.get(src);
};
await Promise.all(buttons.map(async btn=>{
try{
const img=btn.querySelector('img');
const bm=await load(img.src);
const w=bm.width,h=bm.height;
const c=document.createElement('canvas');c.width=w;c.height=h;
const g=c.getContext('2d',{willReadFrequently:true});
g.drawImage(bm,0,0);
const a=g.getImageData(0,0,w,h).data;
let m=0;
for(let i=3;i<a.length;i+=4)if(a[i]>m)m=a[i];
const t=Math.max(1,Math.min(128,m/2));
const W=w+2,H=h+2,d=new Float32Array(W*H);
let sx=0,sy=0,n=0;
for(let y=0;y<H;y++)for(let i=0;i<W;i++){
const inside=y>0&&i>0&&y<=h&&i<=w&&a[((y-1)*w+i-1)*4+3]>=t;
d[y*W+i]=inside?1e9:0;
if(inside){sx+=i;sy+=y;n++;}
}
if(!n)throw 0;
const q=1.414;
for(let y=1;y<H-1;y++)for(let i=1;i<W-1;i++){const k=y*W+i;d[k]=Math.min(d[k],d[k-1]+1,d[k-W]+1,d[k-W-1]+q,d[k-W+1]+q);}
for(let y=H-2;y>0;y--)for(let i=W-2;i>0;i--){const k=y*W+i;d[k]=Math.min(d[k],d[k+1]+1,d[k+W]+1,d[k+W+1]+q,d[k+W-1]+q);}
let mx=0;
for(let k=0;k<d.length;k++)if(d[k]>mx)mx=d[k];
const cx=sx/n,cy=sy/n;
let best=-1,bd=1e18;
for(let y=1;y<H-1;y++)for(let i=1;i<W-1;i++){const k=y*W+i;if(d[k]>=mx*0.6){const e=(i-cx)**2+(y-cy)**2;if(e<bd){bd=e;best=k;}}}
const px=best%W-0.5,py=Math.floor(best/W)-0.5;
const r=btn.getBoundingClientRect();
const s=Math.min(r.width/w,r.height/h);
const fx=(r.left-R.left+(r.width-w*s)/2+px*s)/R.width;
const fy=(r.top-R.top+(r.height-h*s)/2+py*s)/R.height;
btn.setAttribute('data-click-x',(fx*100).toFixed(2));
btn.setAttribute('data-click-y',(fy*100).toFixed(2));
}catch(e){failed++;}
done++;
progress();
}));
o.remove();
const html=b.outerHTML;
let shown=false;
const ok=()=>{
if(shown)return;
shown=true;
alert('Copied'+(failed?' ('+failed+' items without a shape, their centre is used)':'')+'. Go back to the bot and click Next.');
};
const manual=()=>{
const d=document.createElement('div');
d.style.cssText='position:fixed;z-index:2147483647;top:10vh;left:10vw;width:80vw;background:#fff;color:#000;padding:12px;border:2px solid #000;box-shadow:0 0 0 9999px rgba(0,0,0,.5)';
const t=document.createElement('textarea');
t.value=html;
t.style.cssText='display:block;width:100%;box-sizing:border-box;height:150px;margin:8px 0';
const bt=document.createElement('button');
bt.textContent='Copy and close';
bt.style.cssText='padding:6px 12px;background:#eee;color:#000';
const finish=()=>{d.remove();ok();};
bt.onclick=()=>{t.focus();t.select();let r=false;try{r=document.execCommand('copy');}catch(x){}if(r)finish();};
t.addEventListener('copy',()=>setTimeout(finish));
d.append('Automatic copy was blocked. Click the button, or press Ctrl+C in the text box.',t,bt);
document.body.appendChild(d);
t.focus();t.select();
};
try{navigator.clipboard.writeText(html).then(ok,manual);}catch(x){manual();}
})()
